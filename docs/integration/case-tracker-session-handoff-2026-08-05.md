# Case Tracker integration -- session handoff, 2026-08-05

Written to carry a session across a Claude account switch. Self-contained on purpose: assume the
reader has NO transcript, NO episodic memory of prior sessions, and possibly not even the
user-level rules at `C:\Users\RajeevG\.claude\`. Everything load-bearing is restated here.

Repo: `C:\src\patient-portal\main`. Project: Appointment Portal (Angular 20 / .NET 10 / ABP
Commercial 10.0.2). Adrian is the sole developer.

"Levon" is the Case Tracker team's developer -- the receiver of this integration. The Case Tracker
is a separate Gesco system (Angular 17 / Spring Boot / MySQL) that turns approved portal
appointments into cases.

---

## 0. Hard rules -- read before doing anything

1. **DO NOT DEPLOY. DO NOT MERGE THE CASCADE PR.** Adrian said verbatim: "You do not deploy
   anything. I never asked." A prior handoff pushed a deploy as the top action; acting on that was
   a mistake and was corrected. Section 8 records the deploy findings ONLY so they are not
   re-derived. Treat that section as reference, not a task list. Do not raise deploying again
   unless Adrian raises it first.

2. **Shared worktree.** `C:/src/patient-portal/main` is worked concurrently by other sessions. It
   moved branches twice during the session that produced this file:
   `docs/reschedule-consent-rounds-plan` (clean) -> `feat/reschedule-consent-rounds` (with someone
   else's uncommitted files) -> `main` (clean). Therefore:
   - Run `git branch --show-current` and `git status --short` IMMEDIATELY before any commit.
   - NEVER `git add -A` or a bare `git commit`. Commit by explicit pathspec only.
     A previous session's `git add -A` swept another session's plan file into the wrong commit.
   - Never create a git worktree (project rule), even though other worktrees already exist.
   - If `git status` shows files you did not touch, they belong to another session. Leave them.

3. **Verify before asserting.** Every factual claim below carries a `file:line` or a command.
   Re-run the check before repeating a claim to Adrian or to Levon. Two wrong answers nearly
   reached Levon in the week before this handoff, both because a fact was taken from memory or
   from a truncated grep. A truncated grep is not evidence.

4. **ASCII only** in all output, files, and commits. No em dashes, no smart quotes, no emoji.

5. **Ask via the AskUserQuestion modal**, not inline prose. Recommend one option; do not survey.

6. **Scope discipline.** Do only what was asked. Propose gaps separately rather than expanding.

---

## 1. Where the work stands

### Verified repo state (2026-08-05, re-verify -- it moves)

| Fact                              | Value                                                                                  |
| --------------------------------- | -------------------------------------------------------------------------------------- |
| `origin/main` tip                 | `2ce2ef3f` feat(patient-portal): collect reschedule consent in rounds (#428)           |
| Worktree branch at handoff        | `main`, clean                                                                          |
| Other worktrees that exist        | `C:/src/patient-portal/development`, `C:/src/patient-portal/spa-cache-headers`         |
| `staging` / `production` branches | Frozen at 2026-05-01; staging is 573 commits behind development. Effectively abandoned |
| Live box tracks                   | `development`                                                                          |

Note: an earlier handoff claimed `main` is the ONLY checkout. That is false -- `git worktree list`
shows three. The rule "do not CREATE a worktree" still stands.

### The epic (owned by other sessions -- do not touch its phases)

`docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md` is the living roadmap.

| Phase                                         | Status                                       |
| --------------------------------------------- | -------------------------------------------- |
| 1 Supervisor CT permissions                   | DONE, PR #409                                |
| 2 Cancellation reason + billing status        | DONE, PR #414                                |
| 3 Staff schedule calendar                     | DONE, PR #418                                |
| 4a Extract availability calendar              | DONE, PR #420                                |
| 4b Staff pick reschedule date                 | DONE, PR #423                                |
| 4c Consent rounds                             | **DONE, PR #428, merged to main `2ce2ef3f`** |
| 4d Reschedule creates a new appointment       | TODO, no plan written                        |
| 4e CT two-case semantics + contract amendment | TODO, no plan written                        |
| 5 No-show round trip (INBOUND from CT)        | TODO, no plan written                        |

4a-4e are strictly sequential. Phases 1, 2, 3 were mutually independent -- that precedent matters
for the recommendation in section 6.

---

## 2. What this session was asked, and what it produced

Three questions from Adrian, in order:

1. (Implicit, from a prior handoff) -- the prior handoff pushed "deploy" as the top action. Adrian
   rejected it: he never asked for a deploy. **Closed. Do not reopen.**
2. "Tell me what is left that Levon asked us to implement, other than the 5-phase epic."
   -> Answered in section 4, verified against code.
3. "We have completed 4c. Where in that epic can we add the incomplete things we promised Levon?"
   -> Answered in section 6.

The natural next step is section 6's recommendation: write a plan for a new independent phase.
That has NOT been started, and section 7's open decisions should be resolved first.

---

## 3. Verified code facts (each re-checked this session against `main`)

These are the anchors. Re-verify before relying on them.

**Payload shape** -- `src/HealthcareSupport.CaseEvaluation.Domain/Integration/CaseTracker/Payload/IntakePayload.cs`

| Fact                                                                                                                            | Anchor                                         |
| ------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------- |
| `IntakePatientSection` has NO address fields (name, email, DOB, phones, `SamePersonGroupKey` only)                              | `IntakePayload.cs:161-192`                     |
| `Address` / `City` / `ZipCode` on the payload belong to `IntakeLocationSection` -- the CLINIC, not the patient. Easy to misread | `IntakePayload.cs:141-145`                     |
| `Doctor.Id` present (PR #413 is in the code on main)                                                                            | `IntakePayload.cs:214`                         |
| `BillingStatus` present, non-nullable, defaults `NONE`                                                                          | `IntakePayload.cs:44`                          |
| `CancellationReason` present, nullable                                                                                          | `IntakePayload.cs:50`                          |
| Only timestamps sent: `ApprovedAtUtc`, `SubmittedAtUtc`, `UpdatedAt`                                                            | `IntakePayload.cs:53,56,59`                    |
| NO actor / "who cancelled" field anywhere in the payload                                                                        | verified by reading the whole 269-line file    |
| NO reschedule sequence or count field                                                                                           | same                                           |
| Attorney / claim examiner / insurance sections DO each carry a full street/city/state/zip                                       | `IntakeClaimSections.cs:86-93,114-120,139-145` |

**Data that exists internally but is not sent**

| Fact                                                                                                                                                                                           | Anchor                                                               |
| ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------- |
| `Patient` holds `Address`, `City`, `ZipCode`, `Street`, `StateId`                                                                                                                              | `Patient.cs:51,54,57,65,76`                                          |
| Patient has BOTH `Address` and `Street` as separate nullable strings (ambiguous which is line 1 vs 2), and NO `Unit` field -- the older docs saying "street, unit, city, state, zip" are wrong | same                                                                 |
| State is a `Guid? StateId` FK, so the state NAME needs a lookup join                                                                                                                           | `Patient.cs:76`                                                      |
| Pattern to mirror for resolving a state name: batched id collection then `StateNameOrNull`                                                                                                     | `PartyDetailResolver.cs:75-79` and `:112`                            |
| `AppointmentChangeRequest` is a `FullAuditedAggregateRoot<Guid>`, so `CreationTime` / `LastModificationTime` exist                                                                             | `AppointmentChangeRequest.cs:40`                                     |
| Who submitted a change request                                                                                                                                                                 | `AppointmentChangeRequest.cs:119` (`SubmittedByUserId`)              |
| Which party side submitted it                                                                                                                                                                  | `AppointmentChangeRequest.cs:113` (`RequestingSide`)                 |
| Staff actor on the decision                                                                                                                                                                    | `AppointmentChangeRequest.cs:71,73` (`RejectedById`, `ApprovedById`) |
| `ChangeRequestSide`: SideA = patient + applicant attorney; SideB = defense attorney + claim examiner                                                                                           | `ChangeRequestSide.cs`                                               |
| `ChangeRequestType`: `Cancel = 1`, `Reschedule = 2` -- so ONE `RequestingSide` field covers both cancel and reschedule attribution                                                             | `ChangeRequestType.cs`                                               |

**The AME auto-cancel** -- `JointDeclarationAutoCancelJob.cs`

| Fact                                                                                                                     | Anchor |
| ------------------------------------------------------------------------------------------------------------------------ | ------ |
| `AutoCancelReason = "JDF-not-uploaded"` is an internal event ROUTING DISCRIMINATOR. It is never sent to the Case Tracker | `:54`  |
| `AutoCancelReasonText` = "The Joint Declaration Form was not uploaded before the required deadline."                     | `:62`  |
| That PROSE SENTENCE is what gets written to `Appointment.CancellationReason` and forwarded to the Case Tracker           | `:180` |

**Appointment type change**

| Fact                                                                                                                               | Anchor                                           |
| ---------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------ |
| `AppointmentManager.UpdateAsync` changes `AppointmentTypeId` IN PLACE on the same row. No cancel, no rebook, no second appointment | `AppointmentManager.cs:205-234`, write at `:226` |

---

## 4. What Levon asked for that is still outstanding (outside the epic)

Primary source: `docs/integration/case-tracker-correspondence/reply-4-sent.md` (his 31 questions,
lettered A-J, with our answers). Backlog summary: `docs/integration/case-tracker-open-items.md`.

### Promised, not built

| #   | Item                                                                                              | His question    | Verified state                                                                                                                                     |
| --- | ------------------------------------------------------------------------------------------------- | --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| N1  | Who cancelled -- party type at minimum, name where held                                           | Q4              | Genuine gap. Data exists (`RequestingSide`, `SubmittedByUserId`), payload has no actor field. ORPHANED: epic phase 2 shipped and closed without it |
| N2  | Explicit AME auto-cancel boolean                                                                  | Q4              | Genuine gap. He has since explicitly asked for the flag rather than matching a constant                                                            |
| N3  | Patient postal address on the intake payload                                                      | Q16             | Genuine gap. The ONLY address gap; blocks his proof-of-service document                                                                            |
| N4  | Requested-vs-finalized timestamps, UTC + local zone                                               | Q5              | Genuine gap. A notice period measured from the wrong end over-bills someone who gave timely notice                                                 |
| D1  | Contract note: a non-200 reconcile response carries no document info and must never drive pruning | Q24             | Doc only. He has ACCEPTED this. Do NOT weaken the deliberate 404 ambiguity -- it is anti-enumeration by design                                     |
| D2  | Contract note: every push is a full snapshot, never slim                                          | follow-up email | Doc only. True by construction today (one `BuildAsync`, two callers) but never stated                                                              |

### Requested, explicitly NOT promised

| #   | Item                                                                | Note                                                                                              |
| --- | ------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------- |
| R1  | Reschedule sequence / count for an appointment moved more than once | Q9. We said "noted as a request"                                                                  |
| R2  | Type-change link or marker                                          | Q19. We said his argument is sound and gave no timing. **The premise is broken -- see section 5** |

### Declined by Levon -- do not build

- Unconditional document re-send on the feed after any intake. He declined it: a superseding push
  already carries the complete set and his push path never prunes.

### Not code, still owed to him

- **A1** Populate `Location.FacilityId` on both production clinics. Empty today, so his staff type
  it manually on every intake. Data entry, Adrian's job.
- **I1-I7** Infrastructure, all verified NOT started as of 2026-08-03/05: publish MinIO (no `ports:`
  mapping at all), MinIO route in `docker/nginx-proxy/default.conf.template` (none exists),
  create `case-tracker-documents` bucket (only `case-evaluation-documents` exists), scoped policy
  (zero custom policies), scoped user/key (zero non-root users), joint DNS with their IT (Rod --
  this GATES THEIR DEPLOY), and issue `CaseTracker:IntegrationToken` (still EMPTY in production, so
  the reconcile GET rejects everything and Levon's entire reconcile section concerns an endpoint he
  cannot call). Strict order I3 -> I4 -> I5; I2 is pointless before I1.
  **Do NOT create buckets, policies or keys without Adrian's explicit go** -- it grants an external
  party access to production storage and mints a credential that then has to be protected.
- **X1** Delete synthetic appointment `A00005` from their live Pending Intakes queue. Agreed with
  Levon. Our reply asked HIM to do it (Q31), so confirm who owns it before chasing.
- **X2** Compare notice recipient lists (Q15). The portal already notifies on cancellation via
  `ClinicalStaffCancellationEmailHandler`, `JdfAutoCancelledEmailHandler` and
  `StatusChangeEmailHandler`, so his paperwork may duplicate our email. He suggested a call.

### Built and merged but NOT deployed (so invisible to Levon)

`data.doctor.id` and the 100/office/hour volume cap (PR #413), `cancellationReason` and
`billingStatus` (PR #414). All four are in the code on `main`. See section 8 -- and do not act on it.

---

## 5. Two corrections to the record -- carry these forward

**5.1 What we told Levon about the auto-cancel is wrong.**

`reply-4-sent.md` Q4 says the auto-cancel "writes a fixed constant into that same reason field ...
so you can separate the two on the reason value." It does not. The job writes
`AutoCancelReasonText`, an English sentence (`JointDeclarationAutoCancelJob.cs:180`). The terse
constant `"JDF-not-uploaded"` is an internal routing discriminator that never leaves the portal
(`:54`). So Levon's only detection today is string-matching a prose sentence that any copy-edit
would silently break. This makes N2 (the boolean) the correct fix rather than a nicety, and it
means the answer he currently holds is misleading. Worth telling him when N2 ships.

**5.2 R2's premise does not hold.**

`reply-4-sent.md` Q19 told Levon his argument was sound "because our own workflow performs the
cancel-and-rebook and therefore knows the two are the same exam." Verified: it does not. A type
change is an in-place edit of `AppointmentTypeId` on the same row
(`AppointmentManager.cs:226`). There is no cancel, no rebook, no second appointment, and nothing
to link. The ONLY sources for the cancel-and-rebook claim are our own reply and the
`case-tracker-open-items.md` row derived from it -- no code and no business-domain doc supports it.

Two live possibilities, not yet resolved: (a) staff perform cancel-then-rebook by hand as a
convention the system does not model, in which case R2 needs a new staff-facing "this replaces
appointment X" action -- 4d/4e-shaped machinery, not a wire field; or (b) there is a flow that was
not found. **Ask Adrian before scoping R2.**

---

## 6. The deliverable: where the outstanding items belong in the epic

Recommendation given to Adrian on 2026-08-05. Not yet acted on.

**Headline: only two items belong inside the sequential 4d/4e chain. The rest should be a NEW
INDEPENDENT PHASE running parallel to 4d.**

Reasoning: N1-N4 are all additive wire fields. The epic's own Case Tracker note (roadmap line ~479)
records that "additive wire fields need NO coordinated release -- a receiver that ignores unknown
fields stays correct. Only phase 4e's two-case reschedule is a genuine contract BREAK." None of
N1-N4 touches reschedule machinery. 4d (create a new appointment, copy documents, regenerate
packets) is the largest remaining piece, and N3 blocks Levon's proof-of-service document today.
Making it wait behind 4d couples small independent work to the epic's longest pole. The epic
already models independent phases -- 1, 2 and 3 were mutually independent.

| Item                                                                | Recommended home                                   | Why                                                                                                                                                                                                       |
| ------------------------------------------------------------------- | -------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| N3 patient address                                                  | New independent phase                              | One payload section. `PartyDetailResolver.cs:112` already has the `StateNameOrNull` + batched-id pattern to mirror. Zero coupling to reschedule                                                           |
| N1 who cancelled                                                    | New independent phase, and BEFORE 4e               | `RequestingSide` sits on `AppointmentChangeRequest`, whose type is `Cancel` OR `Reschedule`. The same field also answers "who requested this reschedule", which 4e needs. Doing it first makes 4e smaller |
| N2 auto-cancel flag                                                 | New independent phase, paired with N1              | Same payload area, same PR. See correction 5.1                                                                                                                                                            |
| N4 requested-vs-finalized timestamps                                | New independent phase, but 4c changed the question | See open decision 7.3                                                                                                                                                                                     |
| D1 non-200 never prunes                                             | Now. Contract section F / C                        | Doc only, and 4e does not rewrite those sections                                                                                                                                                          |
| D2 every push is a full snapshot                                    | Fold into 4e                                       | It belongs in contract section E2, which is exactly what 4e rewrites. Writing it now means editing a section 4e will replace                                                                              |
| D3 status list five -> seven                                        | 4e, by definition                                  | `RescheduledNoBill` / `RescheduledLate` only become reachable there                                                                                                                                       |
| D4 reschedule link is load-bearing for PATIENT FILING, not cosmetic | 4e, plus the epic roadmap doc                      | Because we send no patient identifier, following the link is the only way they can read their own patient id for a rescheduled appointment                                                                |
| R1 reschedule sequence / count                                      | 4d or 4e                                           | A sequence has no meaning until 4d creates the chain. The roadmap already notes `Appointment.OriginalAppointmentId` as the chaining link. Premature before 4d                                             |
| R2 type-change marker                                               | Blocked                                            | See correction 5.2                                                                                                                                                                                        |

**Suggested shape:** one new phase, descriptive branch name per the epic's rules (the epic forbids
phase-letter branch names), e.g. `feat/case-tracker-payload-completeness`, containing
N1 + N2 + N3 + N4 + D1. Four additive fields and one contract clarification. One plan file in
`docs/plans/`, one PR, squash-merged to `main`. Buildable and deployable while 4d is in progress
because nothing in it is a contract break.

**Contract sections touched:** N1-N4 add rows to section A's field tables and to `data.patient`.
4e rewrites section E2, section A's STATUS table, section H timing, and Coordination decisions 4
and 6. Mostly disjoint -- the one genuine collision is D2, which is why D2 is assigned to 4e.

**Not done:** the epic roadmap's Phase table has NOT had a row added for this. That file is a living
document other sessions write to; at the time of writing another session had uncommitted work in
the tree. Coordinate before editing it.

---

## 7. Open decisions -- need Adrian, block clean work

**7.1 Was `reply-4-sent.md` actually sent?** The file is named "sent" but its own first line reads
"DRAFT - NOT SENT". This matters a lot: `reply-3-NOT-SENT.md` genuinely was never sent, which is
why Levon re-asked several questions. If reply-4 also did not go out, then Levon does not know
about `billingStatus`, does not know about `data.doctor.id`, and never received the patient-address
commitment -- and the whole picture of what he is waiting on changes.

**7.2 Does a type-change cancel-and-rebook procedure exist?** See correction 5.2. Blocks scoping R2.

**7.3 For N4, what does "requested at" mean now?** Phase 4c introduced consent ROUNDS -- a
reschedule can carry several staff-proposed dates, each with its own timestamps. (The roadmap
records a live fixture: falkinstein `A00036`, round 1 superseded with Side B rejected, round 2
current with both sides approved.) So "requested at" is ambiguous between the original change
request's `CreationTime` and the current round's proposal. Levon's use is a notice period, which
argues for the original request, but this needs deciding rather than assuming. This decision did
not exist before 4c.

**7.4 Add the new phase row to the epic roadmap?** Requires coordinating with whichever session
owns 4d.

---

## 8. Deploy dossier -- REFERENCE ONLY, DO NOT ACT

Recorded so it is not re-derived. **Adrian has explicitly said not to deploy.**

- Cascade PR **#410** (`main` -> `development`) has been OPEN since 2026-07-31. It already exists;
  the auto-PR workflow (`.github/workflows/auto-pr-dev.yml`) skips creating a second one.
- `#410` is `mergeable: MERGEABLE` but `mergeStateStatus: BLOCKED`. The only blocker is
  `required_approving_review_count: 1` with zero reviews -- the unsatisfiable sole-dev gate.
- All four REQUIRED checks pass: `Backend: Build`, `Backend: Test`, `Frontend: Build`,
  `Frontend: Lint`. SonarCloud Code Analysis FAILS on main but is NOT a required context, so it
  does not block.
- `enforce_admins: false`, and PR #407 (the previous cascade) was merged by Adrian with zero
  reviews -- so that cascade did use an admin override, which reads against the older handoff line
  "NEVER admin-bypass the cascade". Unresolved; ask before assuming either reading.
- Payload if it ever merges: 9 commits, not 4. It also carries epic UI work (#418 calendar,
  #420 refactor, #423 staff-picks-reschedule-date) and #409, #408.
- **Zero EF migrations** in that payload, so no schema change. But the Domain layer changed across
  14 files, so a db-migrator rebuild would need `--no-cache`.
- `InternalUserRoleDataSeedContributor.cs` changed (#409 grants Supervisor
  `Appointments.PushToCaseTracker` + `ViewIntegrationDeadLetters`). ABP caches permission grants in
  **Redis including negative results**, and that survives an API restart. The previously documented
  5-step deploy sequence has NO Redis clear step, so supervisors would still 403 after a deploy and
  it would look like the fix had not shipped. The roadmap also notes `GrantAllAsync` runs
  unconditionally on every seed pass, so a seeder grant reaches already-deployed roles on the next
  db-migrator run.
- Box: `/opt/hcs-patient-portal` on Linux. Backup script `scripts/hosting/backup-databases.sh`,
  cron 01:30 nightly; runbook `docs/runbooks/hosting-backup-restore.md`.
- `secrets/env.prod` is docker env-file format and is NOT shell-sourceable -- the SMTP password
  contains shell metacharacters. Read individual keys with `grep`/`cut`.

**Security note:** production server credentials were pasted into the prior session's chat
transcript. They are deliberately NOT recorded in this file. Recommend rotating that password.

---

## 9. Testing gap worth closing

`CountSentSinceAsync` -- the EF query behind the 100/office/hour volume cap -- has NO test. Unit
tests substitute the repository, so the `Status == Sent` filter, the `SentAt >= sinceUtc`
comparison and the tenant filter are all unverified. Its two failure modes are silent and
opposite: never trip (no protection at all) or never release (delivery permanently stuck). A
coverage gate flagged this and it was overridden deliberately.
`EfCoreIntegrationOutboxRepositoryTests` exists as the pattern to mirror.

---

## 10. Source documents

| Path                                                                   | What it is                                                                                                                                                                |
| ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `docs/integration/case-tracker-api-contract.md`                        | The agreed wire contract, marked FINAL. Sections A (payload), B (documents), C (retention), E2 (appointment-update channel), F (reconcile GET), H (timing), Coordination  |
| `docs/integration/case-tracker-open-items.md`                          | Backlog summary. Useful index; its address field list is WRONG (see section 3)                                                                                            |
| `docs/integration/case-tracker-verified-findings.md`                   | Code facts with citations from the prior session. Re-verify before use                                                                                                    |
| `docs/integration/case-tracker-correspondence/reply-4-sent.md`         | Levon's 31 questions A-J with our answers, plus an implementation backlog. See open decision 7.1                                                                          |
| `docs/integration/case-tracker-correspondence/reply-3-NOT-SENT.md`     | Never sent. Why he re-asked several questions                                                                                                                             |
| `docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md` | The epic roadmap and its learnings. Owned by other sessions                                                                                                               |
| `docs/plans/2026-08-05-reschedule-consent-rounds.md`                   | Phase 4c plan (shipped)                                                                                                                                                   |
| `CLAUDE.md` (repo root)                                                | Auto-loads. NOTE: it is scoped to a `feat/replicate-old-app` legacy-parity mission that is NOT this work. Do not let it redirect Case Tracker work toward the legacy port |

---

## 11. Validation loop -- run in full before any PR

```
dotnet format --verify-no-changes && dotnet build -warnaserror && dotnet test
```

If Angular is touched, additionally:

```
npx prettier --check <paths> && npx eslint <paths> && npx ng build
export CHROME_BIN=<chrome path> && npx ng test --watch=false --browsers=ChromeHeadless
```

And:

```
python .claude/scripts/verify_structure.py
```

Last known green: 1800 backend, 432 Angular (a later count of 475 frontend specs appears in the 4c
learnings -- re-establish the real baseline rather than trusting either number).

Other constraints:

- Never `ng serve` / `yarn start` / `ng build --watch` -- Vite duplicates `CORE_OPTIONS` and breaks
  ABP DI. Use `npx ng build --configuration development` then `npx serve -s dist/CaseEvaluation/browser -p 4200`.
- Never edit `angular/src/app/proxy/` -- regenerate with `abp generate-proxy` (a dotnet global
  tool, not npm).
- Set `DOTNET_ENVIRONMENT=Development` and `ASPNETCORE_ENVIRONMENT=Development` for dotnet commands.
- The PR hook greps the RAW COMMAND for `## Summary`, so the body heredoc must be in the SAME bash
  call as `gh pr create`. The repo template `.github/pull_request_template.md` wins over any default.
- Adrian admin-merges his own feature PRs (sole dev, review gate unsatisfiable), but the bypass has
  to be named per PR. That does NOT extend to the main -> development cascade.

---

## 12. Suggested first actions in the new session

1. Re-verify section 1's repo state -- branch, `git status --short`, `origin/main` tip.
2. Put open decisions 7.1, 7.2 and 7.3 to Adrian in ONE AskUserQuestion modal, with a
   recommendation on each.
3. On his answers, write the plan for the new independent phase (N1 + N2 + N3 + N4 + D1) to
   `docs/plans/YYYY-MM-DD-case-tracker-payload-completeness.md`, following the structure of
   `docs/plans/2026-08-05-reschedule-consent-rounds.md`.
4. Do NOT start 4d, 4e or 5 -- other sessions own those.
5. Do NOT deploy.
