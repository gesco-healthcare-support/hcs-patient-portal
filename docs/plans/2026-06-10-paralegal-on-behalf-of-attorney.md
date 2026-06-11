---
name: paralegal-on-behalf-of-attorney
date: 2026-06-10
status: in-progress
type: implementation-plan (Phase 1 only)
branch: feat/paralegal-on-behalf-of-attorney
base: main @ 7adb19c (includes #305 Intake-Staff rename + #306 accessor-add gate)
design: docs/design/2026-06-10-paralegal-on-behalf-of-attorney-design.md
scope: Phase 1 (booking-side paralegal). Phase 2 (opposing-side: D7/D8) deferred.
---

# Phase-1 plan: paralegal acts on behalf of an attorney

Implements **Phase 1** of the approved design (`docs/design/2026-06-10-paralegal-on-behalf-of-attorney-design.md`,
section 10). The design's decisions D1-D9 are locked; this plan does NOT re-litigate them.
Phase 2 (opposing-side self-add: the side-scoped delegate-management rule D8, the
opposing-attorney-adds flow D7, opposing-delegate consent CC) is captured at the end and is
**out of scope here**.

---

## 0. Key reconciliation (resolved -- confirm at approval)

**The design says the paralegal access pathway is "read + edit" (S7); the build constraint says
"leave `AppointmentReadAccessGuard.CanEditAsync` UNTOUCHED."** These reconcile because the only
paralegal that exists in Phase 1 is the **booking-side paralegal, who is the appointment
Creator** (the booker). The Creator already passes `CanEdit` / `CanEditAsync` and the
change-request gate via the existing creator pathway -- no edit-rule change is needed for them.

- **Phase 1:** add the paralegal pathway to `AppointmentAccessRules.CanRead` ONLY, fed by a new
  hydration block in `AppointmentReadAccessGuard.EnsureCanReadAsync`. `CanEdit`,
  `CanEditAsync`, and `CanManageAccessors` are left untouched. The booking paralegal edits /
  submits change-requests as Creator (resolves inconsistency #8 for the booking side).
- **Phase 2:** the opposing-side paralegal is NOT the creator, so they will need the pathway
  added to `CanEdit` too. That edit-side widening ships with D7/D8 in Phase 2.

This is the minimal, faithful change and keeps the constraint literally satisfied.

---

## 1. Open items from design section 14 (resolved in this plan)

| # | Open item | Resolution |
|---|---|---|
| 1 | Notification template tokens that personalize the salutation | Only `##BookerFullName##` exists, resolved in `DocumentEmailContextResolver` (`:127`) from the booker `IdentityUser` Name+Surname; used across the status / reminder / requested templates under `Domain/NotificationTemplates/EmailBodies/`. Phase 1 names the **principal attorney** when the To is promoted (T17). |
| 2 | `RecipientRole` enum change | Add `Paralegal = 8` (next after `Employer = 7`); tag the demoted paralegal CC with it (T2, T15). |
| 3 | Proxy regen required? | **Yes** -- attorney details DTOs gain paralegal fields and `ExternalUserType` gains `Paralegal=5`. Regen + revert `generate-proxy.json` + CRLF churn per the deploy memory (T22). |
| 4 | Opposing-attorney "add my paralegal" UI | **Phase 2** (D7). Deferred. |

---

## 2. Current-state map (verified 2026-06-10 against main @ 7adb19c)

Anchors the plan is built on (re-verified post-#306-merge; design's pre-merge line numbers refreshed):

- **`ExternalUserType`** -- `src/.../Domain.Shared/ExternalSignups/ExternalUserType.cs`: `Patient=1,
  ClaimExaminer=2, ApplicantAttorney=3, DefenseAttorney=4`. Gap at 5 (a stray `Adjuster=5` was
  removed 2026-06-01) -> `Paralegal=5` is safe.
- **`RecipientRole`** -- `src/.../Domain.Shared/Appointments/Notifications/RecipientRole.cs`:
  `Patient=1 .. Employer=7`. No paralegal member.
- **`AppointmentAccessRules`** -- `src/.../Domain/Appointments/AppointmentAccessRules.cs`: full
  7-pathway `CanRead` / `CanEdit` take `IEnumerable<Guid>? applicantAttorneyIdentityUserIds` +
  `defenseAttorneyIdentityUserIds` and return `(bool, AccessPathway?)`. `CanManageAccessors`
  (B, #306) and the 2-arg legacy overloads also live here -- DO NOT alter them.
- **`AppointmentReadAccessGuard`** -- `src/.../Application/Appointments/AppointmentReadAccessGuard.cs`:
  `EnsureCanReadAsync` hydrates AA/DA `IdentityUserId`s from `_applicantAttorneyLinkRepository` /
  `_defenseAttorneyLinkRepository` (filter `AppointmentId` + `HasValue`, project to `Guid`) and
  passes them to `CanRead`. `CanEditAsync` / `EnsureCanManageAccessorsAsync` exist -- untouched.
- **Attorney link entities** -- `src/.../Domain/AppointmentApplicantAttorneys/AppointmentApplicantAttorney.cs`
  and `.../AppointmentDefenseAttorneys/AppointmentDefenseAttorney.cs`: pure link rows
  (`TenantId?`, `AppointmentId`, `{Applicant|Defense}AttorneyId`, nullable `IdentityUserId`). No
  name/email columns today (attorney name/email live on the master entity + denormalized email
  on `Appointment`).
- **`Appointment`** -- `src/.../Domain/Appointments/Appointment.cs`: denormalized `PatientEmail`,
  `ApplicantAttorneyEmail`, `DefenseAttorneyEmail`, `ClaimExaminerEmail` (all `string?`). No
  denormalized attorney name.
- **EF config** -- both `CaseEvaluationDbContext` (lines ~691-727) and
  `CaseEvaluationTenantDbContext` configure the two link entities OUTSIDE `IsHostDatabase()`;
  `IdentityUserId` is `HasOne<IdentityUser>().WithMany().IsRequired(false)...OnDelete(NoAction)`.
  `Appointment.*AttorneyEmail` uses `HasMaxLength(AppointmentConsts.PartyEmailMaxLength)` (256).
- **Migrations** -- `src/.../EntityFrameworkCore/Migrations/`, naming
  `yyyyMMddHHmmss_PascalDescription.cs`; latest `20260609205915_Added_ChangeRequestConsent.cs`.
  DbMigrator: `src/.../DbMigrator/`.
- **External role seeding** -- `src/.../Domain/Identity/ExternalUserRoleDataSeedContributor.cs`:
  `EnsureRoleAsync` for the four external roles + `GrantAllAsync(BookingBaselineGrants)` per role
  inside a tenant-scoped guard.
- **Invite / registration** -- `src/.../Application/ExternalSignups/ExternalSignupAppService.cs`:
  `IsExternalRoleType` switch (`:1119`), `ToRoleName(ExternalUserType)` (`:1175`),
  `AddToRoleAsync` at registration (`:573`, role from `ToRoleName` at `:499`),
  `AutoLinkAppointmentsForUserAsync(IdentityUser, ExternalUserType)` (`:743`) dispatching to four
  per-type helpers (AA helper backfills `IdentityUserId` on master + link rows, `:770-805`).
- **Recipient resolution** -- `AppointmentRecipientResolver` (`src/.../Domain/Appointments/Notifications/`)
  builds a flat recipient list (no To/CC split). The To/CC split is in
  `BookerCcDispatcher.PartitionToBookerCc` (`src/.../Application/Notifications/BookerCcDispatcher.cs`):
  To = recipient whose email == `bookerEmail` (else synthetic Patient), CC = everyone else. Used
  by `StatusChangeEmailHandler` + `AppointmentReminderEmailHandler`. Change-request handlers
  (`ChangeRequestSubmittedEmailHandler`, `ChangeRequestConsentRequestEmailHandler`) fan out
  per-recipient with no CC. Greeting token `##BookerFullName##` -> `DocumentEmailContextResolver:127`.
- **Booking DTOs** -- `ApplicantAttorneyDetailsDto` / `DefenseAttorneyDetailsDto`
  (`src/.../Application.Contracts/Appointments/`): `IdentityUserId, FirstName, LastName, Email,
  FirmName?, ...`. Sent via the post-create upsert calls, not embedded in `AppointmentCreateDto`.
- **Upsert** -- `AppointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync` (`:1198`) /
  `UpsertDefenseAttorneyForAppointmentAsync` (`:1428`); denormalized `*AttorneyEmail` written in
  the appointment create/update paths (`:821/:1057`, `:822/:1058`) from the create/update DTO.
- **Angular booking** -- `angular/src/app/appointments/appointment-add.component.ts`:
  `isApplicantAttorney` (`:1188`), `isDefenseAttorney` (`:1194`), `isInternalBooker` (`:1225`),
  `shouldShowAuthorizedUserSection()` (`:1262`, B-edited; comment at `:1260` already says "adds
  `|| this.isParalegal` here"), `applyOwnRoleAttorneyPrefill()` (`:793-822`, gated
  `isApplicantAttorney|isDefenseAttorney && !isItAdmin`), `currentUser` getter (`:3342`).
  Shared attorney card: `angular/.../sections/appointment-add-attorney-section.component.{ts,html}`.
  Accessor section: `angular/.../sections/appointment-add-authorized-users.component`.
  Appointment view: `angular/.../appointment/components/appointment-view.component.ts`
  (`canManageAccessors()`, B). Invite dropdown: `angular/.../invite-external-user.component.ts:46`
  (hardcoded `roleOptions`). Proxy attorney DTOs: `angular/src/app/proxy/appointments/models.ts`;
  enum: `angular/src/app/proxy/external-signups/external-user-type.enum.ts`.

---

## 3. Ordered tasks

Approach flags per `~/.claude/rules/rpe-workflow.md`: `tdd` (domain logic / rules / security),
`test-after` (integration with services / persistence), `code` (enums / config / DTO shape /
Angular / docs). Build + run ONLY via Docker; make ALL edits before any `docker compose build`.

### Stage 0 -- enums (Domain.Shared)

- **T1 [code]** Add `Paralegal = 5` to `ExternalUserType` (with a doc comment noting it reuses
  the freed slot). File: `Domain.Shared/ExternalSignups/ExternalUserType.cs`.
- **T2 [code]** Add `Paralegal = 8` to `RecipientRole`. File:
  `Domain.Shared/Appointments/Notifications/RecipientRole.cs`.
- **T3 [code]** Add `Paralegal` to the `AccessPathway` enum (wherever it is declared near
  `AppointmentAccessRules`).

### Stage 1 -- Domain read pathway (pure rule, TDD)

- **T4 [tdd]** `AppointmentAccessRules.CanRead`: add `IEnumerable<Guid>? paralegalIdentityUserIds`
  (between `defenseAttorneyIdentityUserIds` and `claimExaminerEmails`) + a pathway branch
  `paralegalIdentityUserIds?.Any(id => id == callerUserId) -> (true, AccessPathway.Paralegal)`.
  Update the full-overload call sites to pass the new arg.
  **Do NOT add the param to `CanEdit` and do NOT touch `CanManageAccessors` / the legacy
  overloads** (see section 0). Tests: paralegal id matches -> allowed via `Paralegal`; no match
  -> not granted by this pathway; null/empty -> safe; deny-by-default for an unrelated caller.

### Stage 2 -- data model (additive, nullable)

- **T5 [code]** Add nullable delegate columns:
  - `AppointmentApplicantAttorney` + `AppointmentDefenseAttorney`: `ParalegalEmail` (`string?`),
    `ParalegalFirstName` (`string?`), `ParalegalLastName` (`string?`), `ParalegalIdentityUserId`
    (`Guid?`). (These denormalized name/email columns live on the link row because, per D2,
    there is no paralegal master entity -- unlike attorneys, whose name/email live on the master
    table.)
  - `Appointment`: `ApplicantParalegalEmail` (`string?`), `DefenseParalegalEmail` (`string?`)
    (mirrors the existing `*AttorneyEmail` denormalization the recipient resolver reads).
- **T6 [code]** EF config in BOTH `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext`
  (outside `IsHostDatabase()`): `HasMaxLength` on the new string columns
  (`PartyEmailMaxLength=256` for emails; reuse the existing party-name length const if present,
  else add `PartyNameMaxLength`), and `HasOne<IdentityUser>().WithMany().IsRequired(false)
  .HasForeignKey(x => x.ParalegalIdentityUserId).OnDelete(NoAction)` mirroring the attorney FK
  (a second nullable IdentityUser FK on the link table -- supported; verify the relationship is
  configured distinctly from the attorney FK).
- **T7 [code]** Add EF migration `Added_ParalegalDelegate` (additive nullable columns only, no
  backfill). Apply via the **Dockerized DbMigrator**, never the app host. Verify the generated
  `Up()` contains only `AddColumn` (no drops / type changes).

### Stage 3 -- identity (role, invite, registration, auto-link)

- **T8 [code]** `ExternalUserRoleDataSeedContributor`: `EnsureRoleAsync("Paralegal")` +
  `GrantAllAsync(BookingBaselineGrants)` block (a paralegal must book on behalf, so it needs the
  same baseline booking grants as the attorney roles).
- **T16 [code]** `ExternalSignupAppService`: extend `IsExternalRoleType` (`:1119`) and
  `ToRoleName` (`:1175`, `Paralegal -> "Paralegal"`) so the invite + registration path accepts
  the new type; registration's `AddToRoleAsync` (`:573`) then assigns ONLY `"Paralegal"` (never
  AA/DA -- D6). Add `AutoLinkParalegalAsync` to the `AutoLinkAppointmentsForUserAsync` dispatch
  (`:743`): backfill `ParalegalIdentityUserId` on `AppointmentApplicantAttorney` /
  `AppointmentDefenseAttorney` link rows where `ParalegalEmail == registering email` and
  `ParalegalIdentityUserId == null` (mirrors the AA helper).

### Stage 4 -- Application access guard + accessor-gate (D9)

- **T9 [test-after]** `AppointmentReadAccessGuard.EnsureCanReadAsync`: add a third hydration block
  that selects `ParalegalIdentityUserId` (where `HasValue`) from BOTH link repositories and
  passes the combined list as `paralegalIdentityUserIds` to `CanRead`. Leave `CanEditAsync` and
  `CanManageAccessors*` untouched. (Logic is TDD'd at T4; this is wiring -> test-after / covered
  by manual + the integration suite.)
- **T10 [code]** Append `"Paralegal"` to `BookingFlowRoles.ExternalAccessorManagerRoles` (the one
  line D9 calls for). File: `src/.../Application/Appointments/BookingFlowRoles.cs`.
- **T11 [tdd]** `BookingFlowRolesUnitTests`: add `[InlineData("Paralegal", true)]` to
  `IsExternalAccessorManager_ReturnsExpectedForSingleRole`; update
  `ExternalAccessorManagerRoles_PinnedAtAaAndDaOnly` (rename intent: Count `2 -> 3`, drop
  `ShouldNotContain("Paralegal")`, assert it now contains Paralegal).

### Stage 5 -- DTOs + mappers + upsert

- **T12 [code]** Add `ParalegalEmail`, `ParalegalFirstName`, `ParalegalLastName`,
  `ParalegalIdentityUserId?` to `ApplicantAttorneyDetailsDto` + `DefenseAttorneyDetailsDto`
  (`Application.Contracts/Appointments/`).
- **T13 [code]** Update the Riok.Mapperly mapper(s) for the link entity <-> details DTO so the
  new fields map; add `[MapperIgnoreTarget(...)]` for any target with no direct entity source
  that the AppService sets manually (deploy-memory Mapperly note).
- **T14 [test-after]** `UpsertApplicantAttorneyForAppointmentAsync` (`:1198`) +
  `UpsertDefenseAttorneyForAppointmentAsync` (`:1428`): persist `Paralegal*` onto the link row;
  resolve `ParalegalIdentityUserId` via `ResolveIdentityUserIdForBookingAsync` (for the booking
  side it resolves to the booker); denormalize `Application/DefenseParalegalEmail` onto
  `Appointment` alongside the existing `*AttorneyEmail` writes (`:821/:822`, `:1057/:1058`).

### Stage 6 -- notifications (D1 / S8)

- **T15 [tdd]** Recipient To/CC promotion. Add a pure helper (TDD) -- e.g.
  `ResolvePrincipalEmail(bookerEmail, applicantParalegalEmail, applicantAttorneyEmail,
  defenseParalegalEmail, defenseAttorneyEmail)` returning the matching side's **attorney** email
  when `bookerEmail` equals a paralegal email, else `bookerEmail` unchanged. Integrate into
  `BookerCcDispatcher.PartitionToBookerCc`: use the resolved principal for the To lookup; the
  paralegal then falls into CC, tagged `RecipientRole.Paralegal`. Tests: paralegal booker ->
  attorney To + paralegal CC; self-booking (booker is a patient/attorney, no paralegal email
  match) -> To unchanged (structural guarantee of "reduces to today"). Covers inconsistency #3;
  applies to both `BookerCcDispatcher` consumers (status + reminders).
- **T17 [test-after]** Salutation (#4): when the To is promoted to the principal attorney, resolve
  the greeting name (`##BookerFullName##` via `DocumentEmailContextResolver:127` + the two
  fallbacks in `StatusChangeEmailHandler` / `AppointmentReminderEmailHandler`) to the **attorney
  principal's** name (from the master attorney row first/last). Otherwise unchanged.
  **CONFIRM AT APPROVAL:** exact wording -- "Hello [Attorney name]," with the paralegal CC'd
  (recommended), vs. naming both. Per design ASSUMPTION (S8): only the **booking-side** attorney
  is promoted to To; the opposing attorney stays CC.

### Stage 7 -- Angular (cosmetic + booking block)

- **T18 [code]** Add an `isParalegal` getter (mirror `isApplicantAttorney`, role `"paralegal"`)
  to `appointment-add.component.ts` and `appointment-view.component.ts`. Then:
  `shouldShowAuthorizedUserSection()` += `|| this.isParalegal` (`:1262`); `canManageAccessors()`
  attorney branch += `|| this.isParalegal` (appointment-view).
- **T19 [code]** Prefill (#2): confirm `applyOwnRoleAttorneyPrefill` (`:793-822`) does NOT fire
  for a Paralegal booker. Because the paralegal holds its own role (D6) and neither
  `isApplicantAttorney` nor `isDefenseAttorney` is true, the prefill already skips -- add an
  explicit guard comment and a regression spec so a future role change cannot reintroduce the
  lock. (Resolves #2 structurally via D6.)
- **T20 [code]** Add a `Paralegal` option to the invite dropdown's hardcoded `roleOptions`
  (`invite-external-user.component.ts:46`). (The proxy enum updates via regen, T22.)
- **T21 [code]** Paralegal "(you)" sub-block in
  `appointment-add-attorney-section.component.{ts,html}`: when the booker `isParalegal`, render a
  "Paralegal (you)" block prefilled from `currentUser` and bound to the new DTO paralegal fields,
  keeping the attorney fields fully editable. The paralegal completes exactly one side's attorney
  + paralegal block (their side); the opposing attorney may be entered without a paralegal
  (opposing paralegal is Phase 2).

### Stage 8 -- proxy regen

- **T22 [code]** Regen the proxy for the DTO + enum changes:
  `cd angular && abp generate-proxy -t ng -u http://localhost:44327`, then revert
  `generate-proxy.json` drift and the CRLF-only `index.ts` churn so the committed diff is ONLY
  the real model change (deploy memory `project_proxy-and-localization-deploy`). Never hand-edit
  proxy files.

### Stage 9 -- docs

- **T23 [code]** Update `docs/security/AUTHORIZATION.md` (note the 8th read pathway + the
  accessor-gate set now `{AA, DA, Paralegal}`), `Domain/AppointmentAccessors/CLAUDE.md` (gate set
  extended), and add a short delegate note to the appointments feature CLAUDE.md / lifecycle doc.

### Stage 10 -- build + verify (Docker, STOP gate)

- **T24 [build]** Before building: run `docker compose ps`; if a build/stack is active, STOP and
  coordinate. Then make sure ALL edits are in, and build one service at a time (12 GB WSL cap):
  `docker compose build api` -> `up -d api` -> apply migration via Dockerized DbMigrator ->
  regen proxy (T22) -> `docker compose build angular` -> `up -d angular`. Run the .NET unit tests
  (TDD targets T4/T11/T15 green; full suite no regressions). Verify any new localization key via
  the runtime localization endpoint. **STOP and post a summary; wait for go-ahead before
  committing final / pushing / opening the PR.**

### Stage 11 -- live multi-persona click-test (empty DB, STOP gate)

- **T25 [manual]** The falkinstein demo tenant is EMPTY (only internal users). A live test
  (invite Paralegal + AA + Patient, gen slots, book on behalf, verify To/CC + access) needs a
  UI-driven dataset build. **STOP and ask** whether to build the dataset or defer (as B7 was
  deferred). Logic correctness is guaranteed by the unit/integration tests regardless.

---

## 4. Inconsistency resolution (design section 9) -- Phase 1 vs Phase 2

| # | Resolution | Where |
|---|---|---|
| 1 | New `Paralegal` role + `ExternalUserType=5` | T1, T8, T16 |
| 2 | Prefill only for true AA/DA self-booking; paralegal never gets the attorney-email lock | T19 (structural via D6) |
| 3 | To promoted to the represented attorney; paralegal -> CC | T15 |
| 4 | Salutation names the principal attorney | T17 (confirm wording) |
| 5 | Attorney inbox = primary To; paralegal guaranteed a CC copy | Accepted per D1 (T15) |
| 6 | Two-party consent CCs the side's paralegal | **Phase 2** (opposing-side consent CC; D7) |
| 7 | Accessor-gate includes `Paralegal` | T10, T11 (D9) |
| 8 | Paralegal can submit change-requests | Booking paralegal = Creator (Phase 1); opposing-side paralegal edit -> **Phase 2** |
| 9 | "Requested by" shows the paralegal (correct) | No change (minor UI "for [attorney]" note deferred) |
| 10 | One paralegal, many attorneys (per-appointment link) | T5 (D2) |
| 11 | Registered attorney also gets access | Existing AA/DA pathway (unchanged) |
| 12 | Invite has a paralegal type | T1, T16, T20 |

---

## 5. Phase 2 (deferred -- do NOT build here)

- Side-scoped delegate-management rule `CanManageSideDelegate(side)` (D8) + its UI.
- Opposing-attorney / opposing-paralegal "add my paralegal" flow (D7) + the
  opposing-attorney bootstrap.
- Paralegal **edit** pathway in `AppointmentAccessRules.CanEdit` + `CanEditAsync` hydration (for
  the non-creator opposing-side paralegal).
- Consent / notification CC for the opposing delegate (inconsistency #6).

---

## 6. Risks / rollback

- **Recipient regression (highest):** the To/CC partition change risks altering who is addressed
  on existing non-paralegal appointments. Mitigation: T15's promotion is keyed on a paralegal
  email match, so with no paralegal email set it is a structural no-op -> self-bookings are
  byte-identical. Both booker types covered by tests.
- **Access widening:** the new read pathway is scoped to a linked paralegal `IdentityUserId` (no
  email-only match), symmetric with AA/DA, deny-by-default tested (T4). `CanEdit` untouched.
- **Two coexisting add-rules:** generic accessor-add (D9) vs Phase-2 delegate-management (D8)
  stay distinct; only D9 ships here.
- **Migration:** additive nullable columns only; `Up()` reviewed for `AddColumn`-only. Rollback =
  `Down()` drops the columns; no data backfilled.
- **Blast radius:** appointments booking + notifications + external invite. **Rollback:** revert
  the PR; the migration's `Down()` removes the columns.

---

## 7. STOP-and-report gates (binding)

1. After this plan -> STOP for approval before any feature code. (current gate)
2. If `main` is unexpected -> STOP, ask. (verified clean: branch off `7adb19c`.)
3. Before any Docker rebuild -> `docker compose ps`; if active, STOP and coordinate.
4. Before the live multi-persona click-test (empty DB) -> STOP, ask (build dataset vs defer).
5. When build + unit tests are green -> STOP, post a summary, WAIT for go-ahead before
   committing final / pushing / opening the PR (10-section template, into `main`).
