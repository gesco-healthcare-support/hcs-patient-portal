---
feature: appointment-lifecycle-parity-fixes
date: 2026-05-04
status: draft
base-branch: feat/replicate-old-app-track-domain
related-plans:
  - docs/plans/2026-05-01-old-app-parity-implementation.md
related-research:
  - docs/research/stage-0-gates.md
  - docs/research/stage-0b-build-blockers.md
  - docs/research/stage-0c-infra-hygiene.md
  - docs/research/stage-1-registration-and-booking-entry.md
  - docs/research/stage-2-3-booking-and-view.md
  - docs/research/stage-4-documents.md
  - docs/research/stage-5-approval.md
  - docs/research/stage-6-change-requests.md
  - docs/research/stage-7-jobs-and-notifications.md
  - docs/research/stage-8-internal-and-master-data.md
related-issues: []
---

# Appointment-lifecycle parity fixes

Verified consolidation of Session A + Session B audits + smoke-test
report. Every gap below was independently checked against
`P:\PatientPortalOld` code and the parity/design docs. Tasks are
ordered to restore the **appointment lifecycle as the primary thread**:
external user must register, log in, book, see notifications, view,
upload documents, reschedule, cancel; internal user must approve,
review documents, approve change requests. Cross-cutting concerns
(notifications, jobs, reports, IT-admin master data) are sequenced
after the lifecycle is whole.

**Implementation note for fresh sessions:** every task in the Tasks
section below names a research doc + section. The research docs
contain OLD path:line citations, NEW current state, concrete strip-
lists, role-matrices, and email cascades. Read the per-task research
doc FIRST — only re-read OLD code if the research doc has a marked
uncertainty.

---

## Goal

Close every verified parity gap so the appointment lifecycle behaves
as OLD does for Patient, Adjuster (Claim Examiner), Applicant Attorney,
Defense Attorney (external) and Clinic Staff, Staff Supervisor, IT
Admin (internal). One demo office, single tenant. Reports render in
PDF (OLD's actual format — see correction below; CLAUDE.md's "DOCX ->
PDF" framing is misleading).

---

## Research index — task -> research doc map

When implementing a task, open the named research doc and section.
Re-reading OLD code is allowed only when the doc explicitly flags
"open question" or "uncertain".

| Task | Research doc | Section |
|------|--------------|---------|
| G0 (build blockers) | docs/research/stage-0b-build-blockers.md | All |
| G0a (infra hygiene)  | docs/research/stage-0c-infra-hygiene.md | All |
| G1 npm install + proxy regen | docs/research/stage-0-gates.md | "G1" + "Proxy regen command" |
| G2 merge identity branch | docs/research/stage-0-gates.md | "G2" |
| G3 CustomFieldType enum | docs/research/stage-0-gates.md | "G3" |
| G4 AuthServer Razor | docs/research/stage-0-gates.md | "G4" |
| R1 Angular registration form | docs/research/stage-1-registration-and-booking-entry.md | "R1" |
| R2 IsPatientAlreadyExist | docs/research/stage-1-registration-and-booking-entry.md | "R2" |
| B1 CustomField rendering | docs/research/stage-2-3-booking-and-view.md | "B1" |
| B2 ApproveAsync + RejectAsync permission | docs/research/stage-2-3-booking-and-view.md | "B2" |
| V1 view-detail + change-log | docs/research/stage-2-3-booking-and-view.md | "V1" |
| D1-D4 documents | docs/research/stage-4-documents.md | All |
| A1 approval + send-back | docs/research/stage-5-approval.md | All |
| C1 cancel modal | docs/research/stage-6-change-requests.md | "C1" |
| C2 reschedule modal | docs/research/stage-6-change-requests.md | "C2" |
| C3 supervisor approval UI | docs/research/stage-6-change-requests.md | "C3" |
| C4 ChangeRequestSubmitted handler | docs/research/stage-6-change-requests.md | "C4" |
| C5 AccessorInvited handler | docs/research/stage-6-change-requests.md | "C5" |
| C6 cascade-clone gap | docs/research/stage-6-change-requests.md | "C2 cascade-clone gap" |
| N1 missing recurring jobs | docs/research/stage-7-jobs-and-notifications.md | All |
| X1-M2 internal/admin/reports | docs/research/stage-8-internal-and-master-data.md | All |

---

## Verified findings

### Confirmed gaps (need fix)

| ID | Area | Gap (verified) | Source |
|----|------|----------------|--------|
| F1 | Branches | `feat/replicate-old-app-track-identity` has 3 commits the working branch lacks: 6d9ce4b (Phase 4 NotificationTemplates 59-code OLD parity + AppService), 140aae7 (Phase 6 CustomFields catalog AppService), 284fdf0 (Phase 8 ExternalSignupAppService). | `git log domain..identity` |
| F2 | CustomFieldType enum | OLD declares 7 values (Alphanumeric=12, Numeric=13, Picklist=14, Tickbox=15, Date=16, Radio=17, Time=18). NEW: 3 (Date=1, Text=2, Number=3). **Note**: OLD's HTML only renders 3 (Alphanumeric/Numeric/Date) — the other 4 are latent in OLD. Picking all 7 in NEW = parity-plus, requires `_parity-flags.md` row. | `Domain.Shared/Enums/CustomFieldType.cs` |
| F3 | IsPatientAlreadyExist | OLD writes on initial booking from dedup (`AppointmentDomain.cs:732-780` — exact 3-of-6 = LastName + SSN + Email + PhoneNumber + DateOfBirth + ClaimNumber-from-any-injury). NEW only writes on reschedule approval (`Approval.cs:96`). | OLD `AppointmentDomain.cs:210,217` |
| F4 | Permission attribute | Legacy `AppointmentsAppService.ApproveAsync` (line 1194) uses `.Edit`. **Sibling defect**: `RejectAsync` (line 1201) has the same bug. The `Approve`/`Reject` constants exist + are registered. | NEW `AppointmentsAppService.cs:1194-1201` |
| F5 | Unwired Etos | `AppointmentChangeRequestSubmittedEto` (published from Manager) and `AppointmentAccessorInvitedEto` (published from `AppointmentAccessorManager:150` only on CreateUserAndLink) have no `IDistributedEventHandler<>` implementation. | grep returns 0 handlers |
| F6 | Recurring jobs | OLD has 9; NEW has 5. Missing: ApproveRejectInternalUserReminder, DueDateDocumentApproaching, JdfReminder (separate from auto-cancel), PendingDocumentSendToResponsibleUser, PendingAppointmentDailyNotification. | `CaseEvaluationHttpApiHostModule.cs:585-627` |
| F7 | AuthServer Razor | NEW AuthServer has only Index.cshtml + view imports. Missing `Pages/Account/Login.cshtml` override + `ResendEmailConfirmation.cshtml`. OpenIddict requires Razor login UI. | `src/...AuthServer/Pages/` listing |
| F8 | Email-confirmation gate | `IsEmailConfirmationRequiredForLogin` not set in `CaseEvaluationSettingDefinitionProvider`. | NEW Settings provider |
| F9 | Localization keys | 4 OLD-verbatim login error strings: 6 of the 7 expected `Login:*` keys are present; 1 still missing. `LoginErrorMapper` already references them. | NEW `en.json:449-454` |
| F10 | Angular registration form | No `angular/src/app/register/` (or `account/`) module. OLD's lives at `components/user/users/add/`. NEW backend method is `RegisterAsync` (NOT `SignupAsync` — Session B's audit naming was wrong). | `git -C ... show identity:src/.../ExternalSignupAppService.cs` |
| F11 | Angular post-install | `node_modules/` absent. Plus stale-proxy: `LookupRequestDto.skipCount/maxResultCount` and `PatientDto.id` errors are phantoms — they resolve once `npm install` lands `@abp/ng.core` typed bases. | TS diagnostics on `app.routes.ts`, `appointment-add.component.ts` |
| F12 | Domain-branch constant set | 23-code template set is a deliberate prune from OLD's 59. Strict-parity: adopt identity branch's 59-code set. | `NotificationTemplateConsts.cs` |
| **F13** | **Build-blocker (smoke)** | Phase 0 cleanup left ~33 dangling SendBack refs in `appointment-view.component.ts` + 52 in `.html`. Build fails: `Could not resolve "./send-back-appointment-modal.component"`. | Smoke test 2026-05-04 |
| **F14** | **Seed user (smoke)** | Seeded password `1q2w3E*` is 7 chars; Phase 2 policy requires 8. `InternalUsersDataSeedContributor` fails to create `it.admin@hcs.test` and 3 other internal users. Internal side completely unreachable. | DbMigrator log |
| **F15** | **Localization race (smoke)** | `CaseEvaluationTenantDatabaseMigrationHandler` (registered as `ITransientDependency` in every host loading the Domain module) subscribes to `TenantCreatedEto`/`ApplyDatabaseMigrationsEto` and runs the seeder concurrently from AuthServer + API. Polly papered over the duplicate-key on `AbpLocalizationResources` but logs are full of stack traces. | API logs |
| **F16** | **AuthServer health probe (smoke)** | `authserver` container missing `App__HealthUiCheckUrl` env var (API has it). Probe resolves `[::]:8080` and rejects `0.0.0.0` as a target. Cosmetic dev only. | Smoke test |
| **F17** | **Schema warnings (smoke)** | (a) Query-filter mismatch: `Doctor` is `IMultiTenant + ISoftDelete` but `DoctorAppointmentType`/`DoctorLocation` joins are plain `Entity`. (b) `DocumentStatus` enum has no `0` member yet `HasDefaultValue(Uploaded)` set — sentinel trap. (c) `Location.ParkingFee` lacks `HasPrecision`; OLD shape was `decimal(18,2)`. | EF Core warnings on migrate |
| **F18** | **Phase 17 cascade-clone gap** | NEW reschedule approval clones 5 of OLD's 8 child-entity groups; missing `CustomFieldsValues` and `AppointmentDocuments` (third group disputed — see open question). | docs/research/stage-6 + OLD `AppointmentChangeRequestDomain.cs:322-549` |
| **F19** | **AppointmentStatusType drift** | `Domain.Shared/AppointmentStatusType.cs` enum file is stale (says AwaitingMoreInfo was removed) but the proxy enum + send-back DTOs/methods exist on the Angular side. | docs/research/stage-5 |

### Audit claims that did NOT verify

| Claim | Reality |
|-------|---------|
| "Current branch will throw at startup — seed references missing constants" | False. The 23-code seed is internally consistent. The legitimate concern is parity completeness (F12), not compilation. |
| "NEW has only 2 recurring jobs" | False. NEW has 5; gap is 4 missing OLD jobs (F6 = 5 if you count the disputed 8/9 boundary), not 7. |
| "NEW report renderer must convert DOCX to PDF" | False. OLD already renders PDF via iTextSharp + XLSX via ClosedXML (`CSVExportController.cs:1-689`). NEW reports = match OLD's PDF data + layout, library choice TBD (QuestPDF candidate). |
| "Send-back is an OLD feature" | False. **Send-back is NEW-only**; zero hits in OLD code. Originally added to NEW; cleanup commit deleted the entity but left UI shrapnel. See open question O5. |

---

## Open questions — Adrian decisions before implementation

These items affect plan scope. Default = follow strict-parity per
CLAUDE.md unless noted otherwise.

| ID | Question | Default | Affects |
|----|----------|---------|---------|
| O1 | CustomField max-10 — global (OLD) or per-AppointmentType (NEW design doc)? | OLD (global) | M2, F2 plan |
| O2 | NotificationTemplate IT Admin: free create (OLD) or edit-only (NEW design doc)? | OLD (free create) | M2 |
| O3 | All 7 CustomField types in NEW (parity-plus) or only the 3 OLD actually rendered? | All 7 + parity-flag row | G3, B1 |
| O4 | Phase 17 third disputed cascade group (some count `Notes`, others not) — clone or skip? | Verify against OLD `AppointmentChangeRequestDomain.cs:322-549` | C6 |
| O5 | ~~Send-back: NEW-only feature — keep, drop, or convert to ChangeRequest type?~~ **RESOLVED 2026-05-04: Option C (drop entirely; replicate OLD's correction story exactly).** Staff-to-booker correction in NEW = `InternalUserComments` field on approve + `RejectionNotes` field on reject. Booker can only re-file from scratch on reject (lossy by OLD design). | A1 simplified to approve+reject only; F13 strip-all (no TODO placeholders); SendBack entity/UI/migration purged. |
| O6 | N1 jobs 1+7 (ApproveRejectInternalUserReminder, PendingReminderStaffUsers) — collapse to one or distinct? | Distinct (matches OLD enum) | N1 |
| O7 | N1 PrimaryResponsibleUserId migration — confirmed present? | Verify before N1.4 | N1 |

---

## Approach

### Sequencing principle

```
F13 (build blocker) ─┐
F14 (seed user)     ─┤
F15 (loc race)      ─┼─►  G1 npm + G2 merge ─►  G3, G4 ─►  Lifecycle ─►  Cross-cutting
F11 (npm)           ─┤
F1  (branch merge)  ─┘
```

Stage 0 now has three sub-stages:
- **Stage 0a** — smoke-test defects (F13, F14, F15 — clear the runway).
- **Stage 0b** — original gates (F11/G1, F1/G2, F2/G3, F7-F9/G4).
- **Stage 0c** — non-blocking hygiene (F16, F17 — anytime, but ideally before lifecycle handoffs).

### Branch consolidation (F1, F12)

Merge `feat/replicate-old-app-track-identity` into the working branch.
Resolve the conflict on `NotificationTemplateConsts.cs` by adopting
the 59-code OLD-parity version (closes F12 simultaneously). Other
files on identity branch are net-new and conflict-free.

### Per-task `approach` flag

Per `~/.claude/rules/rpe-workflow.md`:
- Domain logic / business rules / security paths -> **tdd**
- UI components / external integration -> **test-after**
- Config, rules files, migrations, vault notes -> **code**

### Strict-parity decision rule (recap)

Per CLAUDE.md, when OLD does X and we are tempted not to: default is
replicate. Deviation requires (a) a clear-bug fix (silent), or (b) a
`// PARITY-FLAG:` comment and a row in `docs/parity/_parity-flags.md`.
**Five flags** are queued for the file across G3/B1, B2, F18 cascade,
A1 send-back, and the report-renderer choice — see research docs.

---

## Tasks

Task IDs prefix the lifecycle stage: `G0` = smoke defects, `G` = gates,
`R` = registration/login, `B` = booking, `V` = view/changelog,
`D` = documents, `A` = approval, `C` = change-request,
`N` = notifications/jobs, `M` = master data, `X` = reports.

### Stage 0a — Smoke-test defects (clear the runway, blocks everything)

- **G0a** `code` — Strip dangling SendBack refs (front-end only; back-end stays).
  - Research: `docs/research/stage-0b-build-blockers.md` section "B1" + `docs/research/sendback-equivalent.md`.
  - Frontend files: `angular/src/app/appointments/appointment/components/appointment-view.component.ts` + `.html`. Strip-list itemized in research.
  - **Full purge** per O5 = Option C — no `// TODO(stage-A1):` placeholders. Replaced `[disabled]="!canEdit('xxx')"` bindings (47 occurrences) with one `isReadOnly` getter (returns `isPatientUser`).
  - Backend SendBack residue (`AppointmentsAppService.SendBackAsync`/`SaveAndResubmitAsync`/`GetLatestUnresolvedSendBackInfoAsync`, `AppointmentManager` SendBack endpoints, `AppointmentTransitionTrigger.SendBack`+`SaveAndResubmit` enum values, `StatusChangeEmailHandler` AwaitingMoreInfo branch, `en.json` SendBack keys, settings) **left in place** per Adrian decision 2026-05-04 — smoke test confirmed backend builds and runs clean. The proxy will regenerate these client-side too (G2.1) but they will have no Angular consumer.
  - Confirm the OLD-parity correction surface is intact: `Appointment.InternalUserComments` + `Appointment.RejectionNotes` columns must exist (used by approval + reject email cascades). If absent, add a follow-up task to A1.
  - Acceptance: `grep -E "SendBack|AwaitingMoreInfo|saveAndResubmit" angular/src/app/` excluding `proxy/` returns 0 hits except the one intentional doc comment in `appointment-view.component.ts:426`. Build verification deferred to G1.
  - Closes: F13. F19 partially closed — frontend uses no AwaitingMoreInfo refs; the stale enum-file comment in `Domain.Shared/Enums/AppointmentStatusType.cs:5` is correct (the enum has 13 values, no AwaitingMoreInfo) so F19 is now resolved.

- **G0b** `code` — Bump internal-user seed password.
  - Research: `docs/research/stage-0b-build-blockers.md` section "B2".
  - Edit `InternalUsersDataSeedContributor`: change `1q2w3E*` to `1q2w3E*r` (8 chars, satisfies digit + non-alphanumeric + length policy). Same edit applies to all 4 seeded users.
  - Reset volume to re-seed: `docker compose down -v && docker compose up -d --build`.
  - Acceptance: AuthServer login with `it.admin@hcs.test` + new password succeeds.
  - Closes: F14.

- **G0c** `code` — Fix localization-resource duplicate-key race.
  - Research: `docs/research/stage-0c-infra-hygiene.md` section "H1".
  - Recommended: Option B in research — short-circuit `CaseEvaluationTenantDatabaseMigrationHandler` in non-DbMigrator hosts. Add a `IsRunningInMigratorHost` check or guard the `ITransientDependency` registration on a host-context flag.
  - Acceptance: full docker stack startup produces zero `Cannot insert duplicate key row` exceptions in any service log.
  - Closes: F15.

### Stage 0b — Foundation gates

- **G1** `code` — Restore Angular toolchain.
  - Research: `docs/research/stage-0-gates.md` section "G1".
  - `npm ci` (or `npm install` if lockfile drift). Verify `npx tsc --noEmit -p angular/tsconfig.json` clean.
  - Note: stale-proxy `skipCount`/`maxResultCount`/`PatientDto.id` errors are phantoms — they resolve once `@abp/ng.core` typed bases are restored. After G2, additionally run proxy regen (G2.1).
  - Files-touched: `angular/node_modules/` (gitignored).
  - Acceptance: TS module-resolution errors -> 0.
  - Closes: F11.

- **G2** `code` — Merge identity branch.
  - Research: `docs/research/stage-0-gates.md` section "G2".
  - Merge `feat/replicate-old-app-track-identity` (Option A from research; cherry-pick only as fallback).
  - Resolve `NotificationTemplateConsts.cs` conflict by taking identity-branch (59-code) version. Update seed contributor to match.
  - Verify 3 commits land: 6d9ce4b, 140aae7, 284fdf0.
  - Acceptance: `dotnet build` clean (14 projects, 0 errors); `dotnet test` green for Application + Domain.
  - Closes: F1, F12.

- **G2.1** `code` — Regenerate Angular proxy after G2.
  - Research: `docs/research/stage-0-gates.md` section "Proxy regen command".
  - Run the documented `abp generate-proxy` invocation. Verify `LookupRequestDto`, `PatientDto.id`, IT Admin AppService surfaces are present in `angular/src/app/proxy/`.
  - Acceptance: zero stale-proxy TS errors in `appointment-add.component.ts`; new generated files committed.

- **G3** `tdd` — CustomFieldType enum to OLD's 7 values.
  - Research: `docs/research/stage-0-gates.md` section "G3".
  - Per O3 default = parity-plus (all 7). Add `_parity-flags.md` row noting OLD only rendered 3.
  - Update `Domain.Shared/Enums/CustomFieldType.cs` to OLD ints verbatim. Migration to renumber existing dev rows. Update validator + Mapperly mapper. Tests pin all 7 ints round-trip.
  - Acceptance: enum mirrors OLD; migration applies clean; tests green.
  - Closes: F2 (with parity-flag).

- **G4** `code` — Email-verify gate + LoginErrorMapper visibility + localization keys (PARTIAL — Razor override deferred).
  - Research: `docs/research/stage-0-gates.md` section "G4".
  - **Done in G4:**
    - F8: override `IdentitySettingNames.SignIn.RequireConfirmedEmail` default from `false` to `true` in `CaseEvaluationSettingDefinitionProvider`. Applies to all hosts (AuthServer, API, DbMigrator) at module-load time. Setting key: `Abp.Identity.SignIn.RequireConfirmedEmail` (verified by reflecting on `Volo.Abp.Identity.Domain.Shared.dll`; the spelling `IdentitySettingNames.User.IsEmailConfirmationRequiredForLogin` from research was wrong).
    - F9: 6 of 6 `Login:*` keys verified present in `Domain.Shared/Localization/CaseEvaluation/en.json:449-454`. Research's "1 missing" claim was off-by-one.
    - LoginErrorMapper: promoted from `internal static` to `public static` so AuthServer assembly can import it for the deferred F7 work. `InternalsVisibleTo` line in `AssemblyInfo.cs` still serves other internal classes; left alone.
  - **Deferred to G4-followup (new task G4F):** Razor override of `Pages/Account/Login.cshtml` for OLD-verbatim error wording + a "Resend confirmation email" link. Reason: `abp get-source Volo.Abp.Account.Pro` is unsupported; only `Volo.Account` (FREE version, 10.3.0) downloads, which is structurally different from the Pro 10.0.2 obfuscated DLL we ship. Two viable paths: (a) add a localization-resource contributor that overrides ABP's built-in error keys to OLD-verbatim wording (no Razor needed; loses the explicit resend link); (b) decompile the Pro DLL with `dotnet-decompile` to extract the Login page, license-permitting. Pick (a) by default for OLD-parity wording, (b) only if the resend link is required.
  - Acceptance (partial): with this change, an unverified user logging in is correctly blocked by the gate; the on-screen error is ABP's default text rather than OLD-verbatim until G4F lands.
  - Closes: F8, F9. Partial on F7 (gate works, wording deferred).

### Stage 0c — Non-blocking hygiene (parallel with lifecycle work)

- **G5** `code` — AuthServer health-check probe.
  - Research: `docs/research/stage-0c-infra-hygiene.md` section "B3" + `0c` "H2" (cross-ref).
  - Add `App__HealthUiCheckUrl` env var to `authserver` service in docker compose (mirror API service).
  - Acceptance: AuthServer health dashboard widget green; in-process `GetHealthReport` succeeds.
  - Closes: F16.

- **G6** `code` — EF schema warnings.
  - Research: `docs/research/stage-0c-infra-hygiene.md` section "H2".
  - (a) Add matching `HasQueryFilter` on `DoctorAppointmentType` + `DoctorLocation` join entities.
  - (b) Drop `HasDefaultValue(Uploaded)` on `DocumentStatus` (entity property initializer covers it; OLD has no DB default).
  - (c) Add `HasPrecision(18,2)` to `Location.ParkingFee`.
  - One bundled migration covers (b)+(c); (a) is model-only (no migration).
  - Acceptance: `dotnet ef database update` produces zero warnings; lifecycle smoke remains green.
  - Closes: F17.

### Stage 1 — Entry into the lifecycle: external user registration + IsPatientAlreadyExist signal

- **R1** `test-after` — Angular external registration form.
  - Research: `docs/research/stage-1-registration-and-booking-entry.md` section "R1".
  - Build `angular/src/app/register/` (research recommends `account/register/` — match OLD location). 4 external roles. Conditional fields per role (research has the field-by-role table). Calls `IExternalSignupAppService.RegisterAsync` (NOT `SignupAsync` — verify name post-merge).
  - T&C is a footer link (not a checkbox), per OLD.
  - Acceptance: form renders for all 4 roles; submit creates unverified user; verification email sent; success route shown.
  - Closes: F10.

- **R2** `tdd` — IsPatientAlreadyExist on initial booking.
  - Research: `docs/research/stage-1-registration-and-booking-entry.md` section "R2".
  - Recommended Option A: extend `PatientWithNavigationPropertiesDto` with `IsExisting` bool; set in `PatientsAppService.GetOrCreatePatientForAppointmentBookingAsync` at the dedup-decision line; propagate to `AppointmentsAppService.CreateAsync` -> `appointment.IsPatientAlreadyExist`.
  - Pin tests: new patient -> false; email match -> true; 3-of-6 dedup match -> true (test all 6 fields per OLD `AppointmentDomain.cs:732-780`).
  - Acceptance: column populated correctly on booking; reschedule path untouched.
  - Closes: F3.

### Stage 2 — Booking-form fixes

- **B1** `test-after` — Render all 7 CustomField types.
  - Research: `docs/research/stage-2-3-booking-and-view.md` section "B1".
  - Renderer matrix uses ng-bootstrap (consistent with existing booking form). Picklist + Radio options come from `MultipleValues` (comma-separated string on the entity — no separate option entity).
  - Acceptance: booking form renders all 7 types from a seeded test field set; values persist into `AppCustomFieldValues`.

- **B2** `code` — Fix `ApproveAsync` AND `RejectAsync` permission attributes.
  - Research: `docs/research/stage-2-3-booking-and-view.md` section "B2".
  - Change `[Authorize(...Edit)]` -> `.Approve` on line 1194 and `.Reject` on line 1201. No tests pin the existing buggy attribute, so two new authorization-fact tests added.
  - Acceptance: a role with only `.Approve` (or only `.Reject`) can hit the legacy entry without 403.
  - Closes: F4.

### Stage 3 — View + change-log

- **V1** `test-after` — External user view-detail + change-log.
  - Research: `docs/research/stage-2-3-booking-and-view.md` section "V1".
  - Backend (Phase 13a/b) is mostly done — `AppointmentAccessRules`, `EnsureCanReadAsync`, `GetByConfirmationNumberAsync`, masked `InternalUserComments` all present.
  - Frontend gaps: 8-filter Advanced Search accordion, confirmation-# search input, 7-section view layout, per-field `canEdit()` helper, change-log sub-route.
  - Role-to-field-visibility matrix is in the research doc (4 external roles + accessor View=23/Edit=24 + non-accessor 404).
  - Acceptance: edit-access user sees edit buttons; view-only sees readonly; non-owner non-accessor 404s.

### Stage 4 — Documents

- **D1-D4** `test-after` — Document upload + review UIs.
  - Research: `docs/research/stage-4-documents.md` (full file).
  - **Backend Phase 14 + 14b is fully landed**. D2 ad-hoc + D4 staff review are essentially complete.
  - Real frontend gaps: (1) regenerate proxy for `uploadPackage`/`uploadJdf`/`uploadByVerificationCode` + `isAdHoc`/`isJointDeclaration`/`verificationCode` DTO fields (covered by G2.1); (2) Pending-row Upload button + Package/Ad-hoc/JDF badges; (3) JDF visibility gates (AME/AME-REVAL + booking-attorney role + creator match); (4) anonymous `/public/upload/:docId` page outside the auth shell.
  - Parity exceptions already captured in research: 25 MB cap (OLD's was a 1 MB bug), PDF/JPG/PNG only (OLD had `.doc/.docx/.pdf`), `Pending` enum value missing from proxy `DocumentStatus` type union.
  - Acceptance: 4 user-visible flows mirror OLD behavior end-to-end on demo seed data.

### Stage 5 — Approval (internal user lifecycle entry)

- **A1** `test-after` — Clinic staff pending list + approve / reject (+ send-back per O5).
  - Research: `docs/research/stage-5-approval.md` (full file).
  - **Important**: send-back is NEW-only (no OLD source). If O5 = keep, this task reintroduces it with a `_parity-flags.md` row. If O5 = drop, A1 ships with approve+reject only and the Stage 0a strip-list deletions stand.
  - Verify `Domain.Shared/AppointmentStatusType.cs` enum + proxy enum + send-back DTOs/methods are aligned (F19 — research doc flags drift to verify).
  - Two parallel approve/reject surfaces exist in NEW: thin `/api/app/appointments/{id}/approve` (`.Edit`-bugged, fixed in B2) and rich `/appointment-approvals/{id}/approve` (`.Approve`). The current Angular approve modal hits the THIN one and skips Responsible-User entirely — A1 must wire to the rich endpoint.
  - OLD email cascade on Approve (research-verified order): all-stakeholders -> responsible user -> patient package-docs. Reject: creator only.
  - Acceptance: staff approves (slot Reserved -> Booked + package docs auto-queued + 3-email cascade), rejects (slot released + creator email), send-back (per O5).

### Stage 6 — Change requests

- **C1** `test-after` — External cancel modal.
  - Research: `docs/research/stage-6-change-requests.md` section "C1".
  - Cancel-time gate: `SystemParameter.AppointmentCancelTime`, strict less-than (OLD `AppointmentChangeRequestDomain.cs:65-95, gate at 83-87`).
  - Backend method: `RequestCancellationAsync` at `AppointmentChangeRequestsAppService.cs:51-77`.

- **C2** `test-after` — External reschedule modal.
  - Research: `docs/research/stage-6-change-requests.md` section "C2".
  - OLD lead-time + per-AppointmentType max-time gates at `:96-193`.

- **C3** `test-after` — Supervisor change-request approval UI.
  - Research: `docs/research/stage-6-change-requests.md` section "C3".
  - Two list pages + shared 4-mode approve/reject modal. Email branch flags + matching template HTMLs documented in research.

- **C4** `tdd` — Wire `AppointmentChangeRequestSubmittedEto` handler.
  - Research: `docs/research/stage-6-change-requests.md` section "C4". Handler scaffold pattern in research, follows existing `ChangeRequestApprovedEmailHandler`.

- **C5** `tdd` — Wire `AppointmentAccessorInvitedEto` handler.
  - Research: `docs/research/stage-6-change-requests.md` section "C5".
  - Auto-create accessor user already exists in NEW. Handler must use `GeneratePasswordResetTokenAsync` (security improvement; OLD echoed plaintext password — keep the improvement, don't replicate the OLD bug).
  - Closes: F5.

- **C6** `tdd` — Phase 17 cascade-clone gap.
  - Research: `docs/research/stage-6-change-requests.md` section "C2 cascade-clone gap".
  - Add `CustomFieldsValues` + `AppointmentDocuments` to the reschedule-approval cascade. Verify O4 third-disputed group (`Notes`) against OLD source before adding.
  - Acceptance: rescheduled appointment row carries forward all OLD child groups; existing 5 unchanged.
  - Closes: F18.

### Stage 7 — Notifications + reminder jobs

- **N1** `tdd` — Implement 5 missing recurring jobs.
  - Research: `docs/research/stage-7-jobs-and-notifications.md` (full file — one section per job).
  - Each job has researched: OLD source line citations, inferred query predicate (stored-proc bodies are NOT in OLD repo, so predicates are reconstructed from the C# call sites + view-model shapes + parity doc — flagged as inferred), SystemParameter cadence knobs, recipients, NotificationTemplateCode constants, state-change scope, NEW current state, and concrete impl plan with file paths under `src/.../Notifications/Jobs/`.
  - Decision points: O6 (collapse vs distinct for jobs 1+7), O7 (verify `PrimaryResponsibleUserId` migration before N1.4).
  - Use the existing 5 jobs as templates (`PackageDocumentReminderJob`, `JointDeclarationAutoCancelJob`, `AppointmentDayReminderJob`, `CancellationRescheduleReminderJob`, `RequestSchedulingReminderJob`). Two dispatch shapes documented (direct enqueue vs Eto + dispatcher).
  - Tests: behavior tests against an in-memory time provider. **Note**: no job tests exist today — pattern starts here.
  - Acceptance: 9-of-9 OLD jobs implemented; `docs/parity/scheduler-background-jobs.md` flipped to `implemented`; logs show registration on startup with PT timezone.
  - Closes: F6.

- **N2** `code` — Audit any remaining notification handler gaps after C4/C5 + N1 land.
  - Cross-check: list every Eto + confirm each has a handler. Goal 10/10.

### Stage 8 — Internal-user dashboard, reports, master data

- **X1** `test-after` — Internal-user dashboard.
- **X2** `test-after` — Internal-user view-all-appointments (8-filter list).
- **X3** `test-after` — Internal-user reports.
  - Research: `docs/research/stage-8-internal-and-master-data.md`.
  - **Correction**: OLD already renders PDF (iTextSharp `XMLWorkerHelper.ParseXHtml` from `CSVExportController.cs:1-689`) + XLSX (ClosedXML). NEW = match OLD's PDF data + layout, library choice TBD (research recommends QuestPDF).
  - Two reports: Appointment Request Report (filtered list export PDF/Excel) + Patient Demographics PDF (per appointment, 8 sections with looped-per-injury Insurance + Claim Examiner blocks).
- **M1** `test-after` — Master-data CRUD UIs (doctors, locations, appointment types, WCAB offices).
  - OLD enforces FK delete-guard via `ApplicationUtility.CandDelete<T>(id, true)` — replicate as `DeleteValidationAsync` returning a message list.
- **M2** `test-after` — IT Admin pages.
  - Decision points: O1 (CustomField max-10 scope), O2 (NotificationTemplate creation policy).

---

## Risk / Rollback

**Blast radius:**
- G2 (branch merge) — highest-risk. Rollback: `git reset --hard` to pre-merge SHA. Verify `dotnet build` + `dotnet test` immediately after merge before any other change.
- G3 (CustomFieldType renumber) — writes to existing dev rows only (verify `SELECT COUNT(*) FROM AppCustomFields` before applying). Rollback: down-migration restores prior ints.
- G4/F8 (email-confirmation gate) — flips a behavior gate. Pre-existing demo users without `EmailConfirmed=true` will be locked out. Mitigation: re-seed demo users with `EmailConfirmed=true` (interacts with G0b — bundle if hitting same file).
- G0c (localization race fix) — host-startup change; if wrong, every host fails to start. Test with full `docker compose down -v && up -d --build` after.

**Rollback procedure:** every task lands as its own commit on `feat/replicate-old-app-track-domain`. Standard `git revert` per commit. Database changes go through EF migrations with explicit `Down()`.

---

## Verification (end-to-end test procedure)

After all tasks land, manual smoke = appointment lifecycle end-to-end:

1. **Stack starts clean**: `docker compose down -v && docker compose up -d --build`. Zero `Cannot insert duplicate key` errors. All services healthy. Angular builds.
2. **Internal login**: log in as `it.admin@hcs.test` with the new seed password.
3. **Register** a Patient via the Angular form; verify email lands; click verification link; log in.
4. **Book** an AME appointment with attorney + accessor + custom fields covering all 7 field types; observe `IsPatientAlreadyExist=false` on first book, `true` on second book with same email.
5. **Approve** as clinic staff; observe slot Reserved -> Booked, package docs auto-queued, OLD's 3-email cascade fires in order (all-stakeholders -> responsible user -> patient).
6. **Upload** package docs as patient via verification-code links; review and accept as staff.
7. **Upload** JDF as Applicant Attorney; observe due-date countdown.
8. Trigger `JointDeclarationAutoCancelJob` manually; observe cancel + stakeholder emails.
9. **Reschedule** as patient via the modal; observe change request; approve as supervisor with NoBill outcome; observe new appointment row with same confirmation #, all 7+ child groups cloned (per C6), old row -> RescheduledNoBill.
10. **Cancel** a different appointment as patient; approve as supervisor with Late outcome; observe slot release + email.
11. **View** change-log on every modified appointment; observe transitions.
12. **Reminder jobs**: trigger each of the 9 jobs manually; observe expected emails fire to expected recipients.

If every step lands the OLD-parity outcome (with documented parity-plus deviations), the lifecycle is whole.

---

## Out of scope

- Multi-tenant adaptation (Phase 2 per CLAUDE.md). Demo office only.
- CI infra changes; the worktree-local docker compose is enough.
- Refactors of NEW code that already matches OLD behavior. No pre-emptive cleanup.
- OLD's plaintext-password echo in accessor invite (security regression — explicitly NOT replicated; see C5).
- OLD's 1 MB document upload cap (bug — NEW keeps 25 MB).
