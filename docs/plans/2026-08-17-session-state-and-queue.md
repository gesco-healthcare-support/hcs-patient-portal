---
feature: Session state and work queue (Patient Portal, 2026-08-13 to 2026-08-17)
date: 2026-08-17
status: reference
base-branch: main
related-issues: []
---

# Session state and work queue

Written to survive a context compaction. Self-contained: assume the reader has NO transcript.
Repo `C:\src\patient-portal\main` -- Angular 20 / .NET 10 / ABP Commercial 10.0.2, database
per office (tenant = a doctor's practice; "clinic" = a location).

## Git state as of 2026-08-17

| Ref                                           | Meaning                                                          |
| --------------------------------------------- | ---------------------------------------------------------------- |
| `origin/main` @ `65827113`                    | includes #437 payload completeness, #440 JDF overdue, #442 MinIO |
| `origin/development`                          | deployed branch; box pulled `872911a3` on 2026-08-13             |
| `docs/appointment-booking-plans` @ `f9f00bae` | the five plan files below, plus this doc                         |

The worktree is SHARED with other sessions and its branch moves underneath you. Run
`git branch --show-current` and `git status --short` immediately before EVERY commit, stage by
explicit pathspec, never `git add -A`, never a bare `git commit`. Do not create a worktree.
To commit onto a branch without disturbing the checkout, use plumbing: `git hash-object -w`,
`git read-tree` into a temp `GIT_INDEX_FILE`, `git update-index --cacheinfo`, `git write-tree`,
`git commit-tree`, `git branch -f`.

## THE QUEUE -- work in this order

Bug fixes first, then the feature. Items 1-3 each close something already broken and make the
feature safer to add. Each item gets its OWN branch off `main` and its own PR. Do not stack.

**1. 60-day booking message** -- one localization string, no plan file.
`en.json` key `Appointment:ContactStaffForFurtherBookingMessage` currently promises
"...our staff will schedule it for you", which we cannot guarantee. Replace with wording that
directs to intake/support and promises nothing. Note the string hard-codes "60 days" while
internal bookers get 90 (`appointment-add.component.ts:276-277`); the notice is external-only
today so it is correct, but the number is duplicated in prose.

**2. Caller-linkage fix** -- extracted from the re-book plan's T5, ship alone.
`LoadRevalSourceAsync` (`AppointmentManager.cs:184`) and `LoadResubmitSourceAsync` (`:151`)
check source STATUS only. The accessor/creator check exists solely in
`GetByConfirmationNumberAsync` (`AppointmentsAppService.cs:287`, via
`EnsureCanReadAppointmentAsync` at `:344`) -- the read path the UI happens to call first. So
re-eval and re-submit accept ANY confirmation number the caller can guess, and numbers are
sequential (`A00005`, `A00036`, `A00065`). Live authorization hole, no feature dependency.
Keep the check in Application; do NOT move `EnsureCanReadAppointmentAsync` into Domain.

**3. Delete-guard error messages + orphan joins** -- two related defects, diagnosed 2026-08-17.

_3a. Nine `*InUse` guards report nothing useful._ Clicking delete on People or configuration
screens returns HTTP 500 "An internal error occurred during your request!". The guard is
CORRECT and must not change. The message ALSO already exists -- e.g.
`en.json:272` `"ApplicantAttorney:InUse": "This applicant attorney cannot be deleted because
they are referenced by one or more appointments."` Nothing connects them: per
`AppointmentExceptionTranslator`'s own header, _"ABP's BusinessException auto-localization via
MapCodeNamespace does not resolve in this codebase"_, and only the Appointment paths were given
a translator. WHEN VERIFYING, grep the KEY (`ApplicantAttorney:InUse`) not the C# constant name
(`ApplicantAttorneyInUse`) -- searching the constant finds nothing and reads as "message
missing", which is wrong.

Sites -- throw directly (Application): `ApplicantAttorneysAppService.cs:102`,
`DefenseAttorneysAppService.cs:97`, `DocumentsAppService.cs:152`, `PatientsAppService.cs:458`.
Throw from a Domain manager, so wrap at the Application call site:
`AppointmentDocumentTypesAppService.cs:68` and `:95`, `AppointmentLanguagesAppService.cs:59`,
`AppointmentTypesAppService.cs:59`, `StatesAppService.cs:97`, `LocationsAppService.cs:103`
(calls `EnsureCanDeleteAsync`).

Approach: add a shared `DomainErrorTranslator` in Application mapping the nine codes to their
`en.json` keys, returning a `UserFriendlyException`. Do NOT modify
`AppointmentExceptionTranslator` -- it is sealed, Appointment-namespaced and working; changing
it is an unrequested refactor. Keep localization in Application, not in Domain managers.

_3b. Deleting an appointment orphans its party joins._ `AppointmentsAppService.DeleteAsync`
(`:673`) publishes `AppointmentStatusChangedEto(toStatus: null)` so `SlotCascadeHandler` frees
the slot, then soft-deletes the appointment -- but does NOT touch
`AppAppointmentApplicantAttorneys`, which is its own `FullAuditedAggregateRoot` so ABP does not
cascade. The orphaned live join then blocks unrelated party deletions via 3a's opaque error.
Confirmed live on 2026-08-17; required a manual SQL step to clear.

**4. Patient snapshot on the appointment** -- plan
`docs/plans/2026-08-14-patient-snapshot-on-appointment.md`. The only remaining item fixing
something already broken, and the only one with a migration, so it deploys alone.

**5. Prefill shared-entity identity** -- plan
`docs/plans/2026-08-14-prefill-shared-entity-identity.md`. Small; removes the attorney-overwrite
hazard before the picker makes prefill-then-edit routine.

**6. Re-book from a prior appointment** -- plan
`docs/plans/2026-08-14-rebook-from-prior-appointment.md` (minus T5, shipped as queue item 2).

**7. Section picker + attorney question** -- plan
`docs/plans/2026-08-14-booking-section-picker.md`.

**8. Re-evaluation visibility** -- plan
`docs/plans/2026-08-14-reevaluation-visibility.md`. Independent; last because the email
templates are seeded per office and carry the most deploy friction.

## Two tasks are OUTWARD-FACING

Re-book T8 and patient-snapshot T6 both amend
`docs/integration/case-tracker-api-contract.md` and change what the Case Tracker team (Levon)
receives. Tell Adrian when each lands so it reaches them rather than arriving unannounced.
Specifically, re-book sets `OriginalAppointmentId`, which arrives as `previousAppointmentId`
alongside `evaluationKind: EVAL` -- a combination their contract does not currently describe.

## Done this session -- do NOT redo

- **MinIO for the Case Tracker.** PR #442 merged; nginx routes `minio.${BASE_DOMAIN}` to
  `http://minio:9000` (not published to the host). Bucket `case-tracker-documents`, policy
  `case-tracker-access`, user `case-tracker` all created on the box; secret at
  `secrets/case-tracker-minio.txt` (mode 600). Scope verified end to end through the public
  endpoint: own bucket read/write/delete works, our bucket listing denied, reads outside the
  clinic prefix denied. Path-style addressing, region `us-east-1`.
- **Deploy 2026-08-13.** Cascade PR #438 merged (admin bypass, Adrian authorised per-merge --
  the review gate is unsatisfiable because the auto-PR is authored by his own account and
  GitHub forbids self-approval). Box pulled `2c82c358` -> `872911a3`, shipping #437 and #442.
  All routes verified 200; tenant isolation intact.
- **Disk reclaim.** 8.6 GB free -> 33 GB (82% -> 29%), almost entirely Docker build cache.
- **mayram@socalpm.com cleanup.** Now Staff Supervisor ONLY in both databases. Attorney party
  record `B6B0C1AA` soft-deleted; appointment A00002 (Rejected, never pushed) deleted; the
  orphaned join cleared by hand. Backup `20260817-163839` predates all of it.

## Open threads

- **Levon reply** drafted at `scratchpad/minio/reply-to-levon.md`, needs two edits before
  sending: the MinIO endpoint is now LIVE (the draft says it is not yet), and that section
  should move to past tense.
- **Backup retention is not pruning.** Twelve `.bak` files, oldest 22 days against a stated
  14-day policy. PHI past its own retention rule, and it is the exact question Levon asked us
  in writing.
- **The other Mayra record.** `mayram@gesco.com` is still an applicant attorney on one live
  appointment -- a staff-domain email holding a case-party record. Same category as the one
  just cleaned up; needs the same three-step treatment because it is attached.
- **Object storage has no backup at all.** `backup-databases.sh` covers databases only; the
  `miniodata` volume is unbacked. True of our own patients' documents, not just the Case
  Tracker bucket.

## Gotchas that have cost real time

- `docker compose exec -T` READS STDIN. Inside a script piped to `ssh ... bash -s <<'EOF'` it
  swallows the rest of the script and everything after it silently never runs. Append
  `< /dev/null`, or scp the script and run it as a FILE.
- Redis keys use LOWERCASE guids; SQL Server returns them UPPERCASE. Scanning with the SQL
  casing matches nothing and reads as "nothing cached" -- a convincing false negative.
- A bogus SPA host returns 200, not 404 -- the Angular container serves the static shell for any
  name. Tenant isolation is only observable on the `*.api` host.
- `abp generate-proxy` is a DOTNET GLOBAL TOOL at `~/.dotnet/tools/abp`, never npm.
  `angular/src/app/proxy` is generated; never hand-edit it.
- Karma on Windows needs `CHROME_BIN` exported before `npx ng test`.
- Run the FULL validation loop, not just a build. Specs pinning exact column sets or email
  bodies live outside the folder being edited.
- MSYS rewrites a leading `/opt/...` path before Docker sees it. Use `MSYS_NO_PATHCONV=1` plus a
  `bash -c` wrapper for `sqlcmd` inside a container.
- A dual-context `IMultiTenant` entity needs migrations in BOTH `Migrations/` and
  `TenantMigrations/`. Verify columns in SQL in both databases; `dotnet build` cannot detect a
  missing set.

## Standing constraints

- Do NOT deploy, and do not merge the `main` -> `development` cascade, without asking Adrian.
- ASCII only in code, comments, docs and commit messages.
- Ask decisions via the AskUserQuestion modal, never as prose. Adrian runs multiple sessions at
  once, so a turn ending in a prose question is indistinguishable from a finished turn and
  silently stalls that session.
- Commit format: `~/.claude/rules/commit-format.md`. PR format: `~/.claude/rules/pr-format.md`.
