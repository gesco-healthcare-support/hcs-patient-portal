---
plan: old-app-parity-implementation
created: 2026-05-01
scope: Registration + Login + Forgot Password + Appointment Lifecycle (request, view, package docs, ad-hoc docs, JDF, cancellation, rescheduling) + required internal-user dependencies
strict-parity: true
audit-docs-covered: 18
status: ready-to-execute
final-output-target: W:\patient-portal\replicate-old-app\docs\plans\2026-05-01-old-app-parity-implementation.md
---

# OLD-app parity implementation plan

## Executive summary

This plan executes the 18 parity audits in `W:\patient-portal\replicate-old-app\docs\parity\` to bring the NEW ABP / .NET 10 / Angular 20 stack to behavioral parity with the OLD single-tenant Patient Portal at `P:\PatientPortalOld`. The directive is strict OLD parity: entity names, field names, role names, status values, validation messages, business rules, notification triggers, and visible behavior must match OLD verbatim. The only allowed deviations are framework-driven (ABP modules replace OLD's custom RBAC / JWT / audit / i18n / email infra; OpenIddict replaces custom JWT issuance; Riok.Mapperly replaces in-house mapper; ABP `IBlobStorage` replaces local `wwwroot/Documents/...`; Angular 20 standalone replaces Angular 7 NgModules; ASP.NET Core Rate Limiting fills OLD's absence; Hangfire replaces OLD's custom scheduler; ABP `IDistributedEventBus` for in-process domain events; ABP `ISmsSender` replaces direct Twilio SDK calls). Two NEW-app extensions outside OLD spec (Doctor user role, AppointmentSendBackInfo) are removed in Phase 0 before parity work begins. After this plan completes, manual demo + stakeholder validation can drive any post-parity changes.

## Reading order (mirrors the prompt)

1. `W:\patient-portal\replicate-old-app\CLAUDE.md` -- branch CLAUDE.md (parity contract, OLD-to-NEW translation map, ABP conventions).
2. `W:\patient-portal\replicate-old-app\.claude\rules\*.md` -- branch rules (angular, dotnet, dotnet-env, hipaa-data, test-data).
3. `C:\Users\RajeevG\.claude\projects\w--patient-portal-replicate-old-app\memory\MEMORY.md` and indexed memory files -- locked decisions.
4. `C:\Users\RajeevG\.claude\rules\code-standards.md`, `commit-format.md`, `pr-format.md`, `hipaa.md`, `prompt-writing.md` -- user-level standards.
5. `W:\patient-portal\replicate-old-app\docs\parity\_old-docs-index.md` -- master index, naming overrides, things-to-port, things-not-to-port.
6. The 18 parity audit docs in `W:\patient-portal\replicate-old-app\docs\parity\` (10 external-user, 8 internal-user). Each has a gap table; rows are the implementation backlog.
7. OLD source under `P:\PatientPortalOld\` for any feature you implement (read-only). Per-feature OLD code map listed in each audit doc's "OLD code map" section.
8. NEW source under `W:\patient-portal\replicate-old-app\` to verify the audit's "NEW current state" section before coding.
9. Framework docs cited per-phase (ABP Identity / Account / Object-Extensions / Distributed-Event-Bus / Authorization / Background-Jobs / Blob-Storing / Emailing; OpenIddict configuration; ASP.NET Core Rate Limiting; Angular standalone components; Riok.Mapperly).

## Cross-cutting decisions (locked)

These are confirmed in `MEMORY.md` and the audit set. Each plan item below assumes these and does not re-litigate.

- **Strict-parity directive.** Reproduce OLD logic verbatim except for framework swaps. Source: `MEMORY.md` -> `feedback_strict_parity.md`.
- **Role model.** 4 external roles (Patient, Adjuster, Applicant Attorney, Defense Attorney) + 3 internal roles (Clinic Staff, Staff Supervisor, IT Admin). Doctor is a non-user entity managed by Staff Supervisor on its behalf. Source: `external-user-registration.md`, `_old-docs-index.md`, `staff-supervisor-doctor-management.md`.
- **Naming overrides.** OLD `Patient Attorney` -> NEW `Applicant Attorney` (role string, schema, API, UI labels, permissions, localization). OLD `Adjuster` stays unchanged. Source: `_old-docs-index.md` lines 24-25.
- **Email verification approach.** ABP `IsEmailConfirmationRequiredForLogin = true` (replaces OLD `IsVerified` flag); ABP `DataProtectionTokenProvider` issues cryptographic tokens (replaces OLD's `VerificationCode` GUID column reuse). Source: `external-user-login.md`, `external-user-forgot-password.md`.
- **Email lowercase semantics.** OLD lowercases on save AND lookup. NEW ABP uses `ILookupNormalizer` (uppercase by default); override or accept ABP semantics behind the scenes -- public-visible behavior matches. Source: `external-user-registration.md`.
- **Confirmation # format.** `A#####` (5 zero-padded digits, prefix `A`). On reschedule, NEW Appointment row reuses original confirmation #. Source: `external-user-appointment-request.md`, `external-user-appointment-rescheduling.md`.
- **Slot transitions.** Available -> Reserved (external book) -> Booked (internal/staff approve); Reserved/Booked -> Available on cancel-approve / reject / reschedule-reject / reschedule-approve(old slot). `BookingStatus` already has `Reserved = 10` in NEW code (`Domain.Shared/Enums/BookingStatus.cs`). Source: `external-user-appointment-request.md`, `clinic-staff-appointment-approval.md`, `external-user-appointment-rescheduling.md`.
- **AME requires Joint Declaration Form.** Auto-cancel near due date if missing. Source: `external-user-appointment-joint-declaration.md`.
- **Cleanup before parity.** Task #7 (remove Doctor user role + login) and Task #8 (remove `AppointmentSendBackInfo`) execute in Phase 0 so subsequent code does not depend on artifacts that will be deleted. Source: `MEMORY.md` -> tasks list.
- **`IsExternalUser` extension property** on IdentityUser differentiates external vs internal roles (replaces OLD `RoleUserTypes` mapping). Source: `external-user-registration.md`, `external-user-login.md`.
- **Branding parameterization.** Use OLD's colors / UI design as starting point; parameterize via CSS variables + config-driven `BrandingService` for Phase 2 multi-doctor extension. Per-feature audit captures branding touchpoints; aggregate in `docs/parity/_branding.md` (Task #6, executed in Phase Branding). Source: `external-user-login.md`, `_old-docs-index.md`.
- **Multi-tenancy.** Phase 1 uses ABP row-level multi-tenancy with one demo tenant = one office. Phase 2 (database-per-doctor per OLD's plan) is out of scope until parity is stakeholder-validated. Source: branch `CLAUDE.md` + `_old-docs-index.md` lines 86-100.
- **Rate limiting on forgot-password.** 5 requests / email / hour via ASP.NET Core Rate Limiting middleware. NEW addition vs OLD (OLD had none); accepted as a security fix that does not change visible behavior. Source: `external-user-forgot-password.md` Q3 resolution.
- **OLD bug, fixed for correctness:** JDF accept/reject flow in OLD only sets `RejectedById`, never `DocumentStatusId`; NEW always sets `DocumentStatusId`. Source: `external-user-appointment-joint-declaration.md`.
- **Audit-doc lifecycle (binding).** When a gap-table row in any `docs/parity/<feature>.md` is implemented, the implementer **must NOT delete or remove the row**. Instead, annotate it in place. Conventions:
  - Append a status tag to the row -- one of:
    - `[IMPLEMENTED 2026-MM-DD - pending testing]` -- code merged, automated tests added, manual verification not yet done.
    - `[TESTED 2026-MM-DD - <PR#>]` -- automated + manual verification both passed.
    - `[INTEGRATED 2026-MM-DD - <release tag>]` -- shipped to a stakeholder demo / staging cut.
  - For OLD-bug-fix exceptions, also append `[OLD-BUG-FIX]` so future passes immediately recognize the deviation.
  - For descoped or rejected rows, append `[DESCOPED 2026-MM-DD - rationale]` with one line stating why; do not delete.
  - Update the audit doc's frontmatter `audited:` date when annotating, so the doc still reflects the most recent verification pass.
  - Why this matters: future audit passes (post-Phase-1 demo, post-Phase-2 multi-tenant work, regression checks) re-read these docs as the canonical scope record. Removing rows hides whether work was done, tested, and integrated. Keeping them annotated lets a future Claude (or human) cross-check NEW behavior against the OLD-anchored row without re-deriving scope.
  - This convention applies to all 18 audit docs and to the `_old-docs-index.md` Things-To-Port / Things-Not-To-Port lists.

## Per-plan-item structure (template)

Every numbered plan item below uses this skeleton. When a section is empty for an item, it is omitted.

- **Audit reference:** `<file>` rows `<list>`.
- **Files to create:** `<paths>`.
- **Files to modify:** `<paths>` + nature of change.
- **Files to delete:** `<paths>` (cleanup phase only).
- **Code shape:** signatures, invariants, state-machine guards (no full code).
- **Tests:** xUnit + Shouldly file path + method names.
- **Acceptance criteria:** 3-7 bullets verifiable end-to-end.
- **Strict-parity note:** verbatim OLD behavior being replicated, or "OLD bug, fixed for correctness".

## Phase order rationale

1. **Phase 0 (Cleanup)** before everything because subsequent feature code references Doctor role / `AppointmentSendBackInfo`; removing first prevents cascading rework.
2. **Phase 1 (Schema/enum/seed)** because every feature reads from these entities; shipping AppServices before entities exist is impossible.
3. **Phase 2 (Identity + permissions)** because every AppService method has `[Authorize(...)]` referencing permission keys; missing keys produce runtime errors.
4. **Phase 3-N (Per-feature)** in dependency order: System Parameters -> Notification Templates / Packages / Custom Fields (lateral) -> Doctor Management -> Registration / Login / Forgot Password (parallelizable but sequence them by file overlap risk) -> Booking + Approval -> Document flows -> Change Requests -> Notifications + reminders -> Branding aggregation -> E2E integration tests.

## Phase 0 -- Cleanup (Tasks #7 and #8)

### 0.1 Remove Doctor user role + login

- **Audit reference:** `MEMORY.md` task #7; `staff-supervisor-doctor-management.md` (treats Doctor as non-user entity); `_old-docs-index.md` line 25 (Adjuster row noting role-name discipline).
- **Files to modify:**
  - `src\HealthcareSupport.CaseEvaluation.Domain\Identity\InternalUserRoleDataSeedContributor.cs` -- delete the `DoctorRoleName` constant (line 43), the `EnsureRoleAsync(DoctorRoleName, ...)` call (line 82), the `GrantAllAsync(DoctorRoleName, DoctorGrants())` call (line 86), and the entire `DoctorGrants()` method (lines 302-352). Update the per-tenant pass to seed only Staff Supervisor + Clinic Staff.
  - `src\HealthcareSupport.CaseEvaluation.Domain\Identity\InternalUsersDataSeedContributor.cs` -- delete `doctor@<tenantSlug>.test` user seed and the `LinkDoctorEntityAsync` method + its call site. Tenant pass seeds 2 users (Staff Supervisor + Clinic Staff) instead of 3.
  - `src\HealthcareSupport.CaseEvaluation.Domain\Doctors\Doctor.cs` -- remove `IdentityUserId Guid?` field (audit lookup confirmed: line 15-133). Update mapper and DTOs.
  - `src\HealthcareSupport.CaseEvaluation.Application\CaseEvaluationApplicationMappers.cs` -- adjust the Doctor mapper to drop `IdentityUserId`.
  - `src\HealthcareSupport.CaseEvaluation.Application.Contracts\Doctors\DoctorDto.cs` (and Create/Update DTOs) -- drop `IdentityUserId`.
  - `angular\src\app\doctors\` proxy + components -- regenerate proxy after backend rebuild; remove any UI element bound to `identityUserId`.
- **Files to delete:** any test fixture that depends on the Doctor user (search `test\` for `DoctorRoleName`, `doctor@`, `LinkDoctorEntityAsync`).
- **Code shape:**
  - Migration: `dotnet ef migrations add Drop_Doctor_IdentityUserId --context CaseEvaluationTenantDbContext`. EF Core drops the FK + column. Confirm no orphan FK constraint by running migration locally.
  - Reseed: re-run `DbMigrator` against a clean dev DB to verify no Doctor role / Doctor user appears.
- **Tests:** un-skip / delete any `*Doctor*Login*` integration tests; confirm `InternalUserRoleDataSeedContributorTests` no longer asserts Doctor role.
- **Acceptance criteria:**
  - [ ] After `DbMigrator` run, `AbpRoles` table contains exactly `IT Admin` (host) + `Staff Supervisor` + `Clinic Staff` per tenant; no `Doctor` row.
  - [ ] `AbpUsers` has no `doctor@<slug>.test` row.
  - [ ] `Doctors.IdentityUserId` column does not exist in DB.
  - [ ] Application starts without errors; existing tests pass.
  - [ ] Angular Doctor page builds and renders without `identityUserId` references.
- **Strict-parity note:** OLD has no Doctor as a user role -- a Doctor is an entity Staff Supervisor manages. Removing the Doctor user role + login is required to honor OLD spec.

### 0.2 Remove `AppointmentSendBackInfo` (NEW extension)

- **Audit reference:** `MEMORY.md` task #8; flagged across audits as NEW-only extension not in OLD.
- **Files to delete:**
  - `src\HealthcareSupport.CaseEvaluation.Domain\Appointments\AppointmentSendBackInfo.cs`
  - `src\HealthcareSupport.CaseEvaluation.Application.Contracts\Appointments\AppointmentSendBackInfoDto.cs` (+ Create/Update variants if present)
  - `angular\src\app\appointments\appointment\components\send-back-appointment-modal.component.{html,ts}`
- **Files to modify:**
  - `src\HealthcareSupport.CaseEvaluation.Domain\Appointments\AppointmentManager.cs` -- delete `SendBackAsync` (line 87-101) and `SaveAndResubmitAsync` (line 108-124); delete any field-level state for these flows.
  - `src\HealthcareSupport.CaseEvaluation.Application\Appointments\AppointmentsAppService.cs` -- delete `SendBackAsync` / `SaveAndResubmitAsync` endpoints; delete imports.
  - `src\HealthcareSupport.CaseEvaluation.Application.Contracts\Appointments\IAppointmentsAppService.cs` -- delete `SendBackAsync` / `SaveAndResubmitAsync` interface methods.
  - `src\HealthcareSupport.CaseEvaluation.HttpApi\Controllers\Appointments\AppointmentsController.cs` -- delete corresponding endpoint methods + routes.
  - `src\HealthcareSupport.CaseEvaluation.EntityFrameworkCore\CaseEvaluationDbContext.cs` (line 45) and `CaseEvaluationTenantDbContext.cs` (line 44) -- remove `DbSet<AppointmentSendBackInfo>`.
  - `src\HealthcareSupport.CaseEvaluation.Application\CaseEvaluationApplicationMappers.cs` (line 292-303) -- remove mapper.
  - `angular\src\app\appointments\appointment\appointment.component.ts` (and abstract) -- remove send-back action button, modal trigger, related service calls.
  - Migration `20260428003045_Added_AppointmentSendBackInfo.cs` -- add a NEW migration that drops the table; do not edit the historical migration.
- **Code shape:** EF migration `Drop_AppointmentSendBackInfo` removes the table. Application/Domain/HttpApi layers compile clean after deletes.
- **Tests:** delete `AppointmentSendBackInfoTests.cs` and any test method touching `SendBack*` / `SaveAndResubmit*`.
- **Acceptance criteria:**
  - [ ] Solution builds with no `AppointmentSendBackInfo` references.
  - [ ] DB schema has no `AppointmentSendBackInfos` table.
  - [ ] Angular app builds; the appointment detail page no longer shows a "Send Back" button.
  - [ ] No proxy file under `angular\src\app\proxy\` references the dropped types after `abp generate-proxy` regeneration.
- **Strict-parity note:** OLD has no send-back flow. OLD's appointment lifecycle is Pending -> Approved/Rejected only; corrections via cancel/reschedule.

## Phase 1 -- Schema, enums, and seed foundations

This phase ships missing entities and missing fields so subsequent feature code can compile. Each item lists the entity, file path, fields, and audit reference. EF migrations are generated AFTER all entity edits in a phase to minimize churn (one migration per logical group).

### 1.1 `SystemParameter` aggregate (singleton config)

- **Audit reference:** `it-admin-system-parameters.md` rows B/B/B/B/I/I/I/C.
- **Files to create:**
  - `src\HealthcareSupport.CaseEvaluation.Domain\SystemParameters\SystemParameter.cs`
  - `src\HealthcareSupport.CaseEvaluation.Domain\SystemParameters\ISystemParameterRepository.cs`
  - `src\HealthcareSupport.CaseEvaluation.EntityFrameworkCore\SystemParameters\EfCoreSystemParameterRepository.cs`
  - `src\HealthcareSupport.CaseEvaluation.Domain\SystemParameters\SystemParameterDataSeedContributor.cs`
- **Code shape:**
  - `public class SystemParameter : FullAuditedEntity<Guid>, IMultiTenant` (use `[Audited]` attribute via `FullAuditedEntity`).
  - Fields (12 from OLD `SystemParameterDomain.cs` + verified against booking/cancel/reschedule audits):
    - `int AppointmentLeadTime` (days). Default 3.
    - `int AppointmentMaxTimePQME`. Default 60.
    - `int AppointmentMaxTimeAME`. Default 90.
    - `int AppointmentMaxTimeOTHER`. Default 60.
    - `int AppointmentCancelTime`. Default 2.
    - `int AppointmentAutoCancelCutoff` (JDF auto-cancel cutoff days). Default 7.
    - `int ReminderCutoffDays`. Default 7.
    - `int DurationTime` (default slot duration mins). Default 60.
    - `int DueDays` (package doc due days from approval). Default 14.
    - `int JointDeclarationUploadCutoffDays`. Default 7.
    - `int OverdueNotificationDays`. Default 3.
    - `bool IsCustomField` (gates custom-fields rendering on intake form). Default false.
  - Singleton enforcement: seed contributor inserts ONE row per tenant on tenant-create; AppService never exposes Create/Delete -- only `GetAsync()` (anyone authenticated) + `UpdateAsync(SystemParameterUpdateDto)` (IT Admin only).
- **Tests:** `SystemParameterAppServiceTests` -> `GetAsync_Anonymous_ReturnsSingleton`, `UpdateAsync_NonItAdmin_Throws403`, `Seed_OnTenantCreate_InsertsSingleRow`.
- **Acceptance criteria:**
  - [ ] After tenant seed, exactly one `SystemParameters` row exists per tenant with default values.
  - [ ] Booking flow rejects within `AppointmentLeadTime` days (verified end-to-end in Phase 7).
  - [ ] IT Admin updates via UI; values persist; non-IT-Admin sees 403 on update.
- **Strict-parity note:** Defaults match OLD seed values. Field semantics are read by all subsequent feature gates.

### 1.2 `Document` master and `AppointmentPacket` / `AppointmentPacketDocument` link

- **Audit reference:** `it-admin-package-details.md` rows B/B/B/I/B/I.
- **Files to create:**
  - `src\HealthcareSupport.CaseEvaluation.Domain\Documents\Document.cs` (Name, BlobName, ContentType, IsActive).
  - `src\HealthcareSupport.CaseEvaluation.Domain\AppointmentDocuments\AppointmentPacketDocument.cs` (link entity: `AppointmentPacketId`, `DocumentId`).
  - Verify `AppointmentPacket.cs` already exists (audit verified `AppointmentPacketsAppService.cs` exists). If missing, add: `Name`, `AppointmentTypeId` FK, `IsActive`.
- **Code shape:** Many-to-many between `AppointmentPacket` and `Document` via `AppointmentPacketDocument`.
- **Tests:** `AppointmentPacketsAppServiceTests` -> `Create_PacketWithDocuments_LinksAll`, `Update_RemoveDocument_UpdatesLinkTable`.
- **Acceptance criteria:**
  - [ ] IT Admin uploads a Document template -> stored in blob storage.
  - [ ] IT Admin creates an AppointmentPacket for AppointmentType=PQME -> visible in list.
  - [ ] IT Admin links Documents to a Packet -> link rows persist.

### 1.3 `NotificationTemplate` + `NotificationTemplateType`

- **Audit reference:** `it-admin-notification-templates.md` rows B/I/I/I/B/I/I/I/C, "Template code matrix [VERIFIED 2026-05-03]".
- **Files to create:**
  - `src\HealthcareSupport.CaseEvaluation.Domain\NotificationTemplates\NotificationTemplate.cs`: `TemplateCode string` (PK in addition to Id; unique with TenantId), `TemplateTypeId Guid`, `Subject string`, `BodyEmail string` (HTML), `BodySms string?`, `IsActive bool`, `[Audited]`.
  - `src\HealthcareSupport.CaseEvaluation.Domain\NotificationTemplates\NotificationTemplateType.cs`: `Name string`.
  - `src\HealthcareSupport.CaseEvaluation.Domain\NotificationTemplates\NotificationTemplateDataSeedContributor.cs` -- seed all 59 verbatim codes from OLD's two template systems (`TemplateCode` enum 16 codes + `EmailTemplate` static class 43 codes); see Localization checklist below for the full verbatim list.
- **Strict-parity correction (2026-05-03):** OLD has two parallel template-storage mechanisms (DB-managed `TemplateCode` + on-disk HTML `EmailTemplate`). NEW unifies both into `NotificationTemplate` because storage is a framework concern, not user-visible. Use OLD identifiers verbatim, including OLD typos (`UserRegistered` -> "User-Registed.html", `RejectedJoinDeclarationDocument` (sic), `AppointmentApprovedStackholderEmails` (sic), `PatientAppointmentCancellationApprvd` (sic)) so any external reference (search, support-doc) still matches.
- **Code shape:** ABP's `ITemplateRenderer` is Razor-based; OLD uses `##Var##` placeholders. Migration: at port time, each notification handler builds a strongly-typed model and the seed body uses `@Model.Var` syntax. Variable name list comes from OLD `ApplicationConstants.cs` lines 11-23 + per-template body inspection.
- **SMTP credentials decision:** keep ABP's `appsettings.json` approach (not editable via UI). Reason: OLD's `SMTPConfiguration` table stored DB-managed credentials; NEW's framework-driven config is more secure and is a documented framework deviation, NOT a behavior change visible to end users.
- **Tests:** `NotificationTemplatesAppServiceTests` -> `GetByCode_ReturnsTemplate`, `Update_RendersWithVariables_UsesNewBody`.
- **Acceptance criteria:**
  - [ ] Seed inserts every TemplateCode from OLD's `EmailTemplate` enum (full list in section "Localization key checklist + notification template codes" below).
  - [ ] IT Admin edits a template's Subject + BodyEmail + BodySms -> next send uses new content.
  - [ ] All notification handlers in subsequent phases load template by `TemplateCode` (no hardcoded strings in handlers).
- **Strict-parity note:** Replicate every OLD `EmailTemplate` code; the renderer engine differs but the variable substitution semantics are equivalent.

### 1.4 `CustomField` (renamed `AppointmentTypeFieldConfig` per audit)

- **Audit reference:** `it-admin-custom-fields.md` rows --/I/I/I/I/I/I/I/I/I/I.
- **Files to create:** verify `AppointmentTypeFieldConfig` entity exists (mapper and DTO already exist per audit; entity TO VERIFY). If missing, add:
  - `src\HealthcareSupport.CaseEvaluation.Domain\AppointmentTypeFieldConfigs\AppointmentTypeFieldConfig.cs`
  - Fields: `FieldLabel`, `DisplayOrder int`, `FieldType` enum (Date/Text/Number), `FieldLength int?`, `MultipleValues string?`, `DefaultValue string?`, `IsMandatory bool`, `AppointmentTypeId Guid?` (null = applies to all types), `IsActive bool`.
  - `AppointmentTypeFieldConfigValue.cs` polymorphic value rows: `AppointmentId Guid`, `FieldConfigId Guid`, `Value string`.
- **Code shape:**
  - `CreateAsync` -> count check: reject if `>= 10` active configs already exist for the AppointmentTypeId.
  - Booking form respects `SystemParameter.IsCustomField` global gate.
- **Tests:** `AppointmentTypeFieldConfigsAppServiceTests` -> `Create_11thField_Throws`, `Booking_RespectsIsCustomFieldFlag`.
- **Acceptance criteria:**
  - [ ] IT Admin creates 10 fields for one AppointmentType -> all save; 11th rejected.
  - [ ] Booking form renders fields when `IsCustomField = true`; hides when false.

### 1.5 `AppointmentChangeRequest` aggregate + `AppointmentChangeRequestDocument`

- **Audit reference:** `external-user-appointment-cancellation.md` row B; `external-user-appointment-rescheduling.md` row B; `staff-supervisor-change-request-approval.md` rows B/B/B/B/B.
- **Files to create:**
  - `src\HealthcareSupport.CaseEvaluation.Domain\AppointmentChangeRequests\AppointmentChangeRequest.cs`
  - `src\HealthcareSupport.CaseEvaluation.Domain\AppointmentChangeRequests\AppointmentChangeRequestDocument.cs`
  - `src\HealthcareSupport.CaseEvaluation.Domain\AppointmentChangeRequests\AppointmentChangeRequestManager.cs` (domain service for state transitions).
- **Code shape (`AppointmentChangeRequest`):**
  - Inherits `FullAuditedEntity<Guid>, IMultiTenant`. `[Audited]`.
  - Fields (per OLD `AppointmentChangeRequestDomain.cs` 1035 lines + cancel/reschedule audits):
    - `Guid AppointmentId` (FK to current Appointment row).
    - `ChangeRequestType` enum (`Cancel` / `Reschedule`).
    - `string CancellationReason` (required when Type=Cancel).
    - `string ReScheduleReason` (required when Type=Reschedule).
    - `Guid? NewDoctorAvailabilityId` (Reschedule only; the slot the user picked).
    - `RequestStatusType` enum (`Pending = 1`, `Accepted = 2`, `Rejected = 3`).
    - `string? RejectionNotes` (set on Reject).
    - `Guid? RejectedById`.
    - `Guid? ApprovedById`.
    - `string? AdminReScheduleReason` (set when supervisor overrides slot).
    - `Guid? AdminOverrideSlotId`.
    - `bool IsBeyondLimit` (admin override; lifts max-time gate).
    - `string? CancellationOutcome` enum mapped to AppointmentStatusType: `CancelledNoBill = 5` / `CancelledLate = 6` / `RescheduledNoBill = 7` / `RescheduledLate = 8`.
    - `[ConcurrencyCheck] string ConcurrencyStamp` (ABP populates via `IHasConcurrencyStamp`).
- **Code shape (`AppointmentChangeRequestManager`):**
  - `Task<AppointmentChangeRequest> SubmitCancellationAsync(Guid appointmentId, RequestCancellationDto dto)` -- validates Approved appointment, validates `(slot.AvailableDate - DateTime.Today).TotalDays >= AppointmentCancelTime`, validates reason, creates row in Pending status, raises `CancellationRequestSubmittedEto`.
  - `Task<AppointmentChangeRequest> SubmitRescheduleAsync(Guid appointmentId, RequestRescheduleDto dto)` -- validates Approved appointment, validates new slot Available + lead/max-time gates (skip max-time if `IsBeyondLimit`), creates row, transitions appointment status to `RescheduleRequested = 12`, transitions new slot Available -> Reserved, raises `RescheduleRequestedEto`.
  - `Task<Appointment> ApproveCancellationAsync(Guid changeRequestId, ApproveCancelDto { CancellationOutcome })` -- transitions appointment status to outcome, releases slot Booked -> Available, raises `CancellationApprovedEto`.
  - `Task RejectCancellationAsync(Guid changeRequestId, RejectDto)` -- sets RequestStatus=Rejected + Notes, leaves appointment Approved, raises `CancellationRejectedEto`.
  - `Task<Appointment> ApproveRescheduleAsync(Guid changeRequestId, ApproveRescheduleDto)` -- creates NEW Appointment row via `AppointmentManager.CloneForRescheduleAsync(originalId, newSlotId, sameConfirmationNumber: true)`, sets new appointment.Status=Approved, new slot Reserved -> Booked, old slot Booked -> Available, sets old appointment.Status to `RescheduledNoBill = 7` / `RescheduledLate = 8` per outcome, sets `AppointmentChangeRequest.AdminReScheduleReason` if supervisor overrode, raises `RescheduleApprovedEto`. Validate: `OverrideSlotId != original.NewSlotId` requires `AdminReScheduleReason`.
  - `Task RejectRescheduleAsync(Guid changeRequestId, RejectDto)` -- sets RequestStatus=Rejected + Notes, transitions appointment back to Approved, transitions new slot Reserved -> Available, raises `RescheduleRejectedEto`.
- **Strict-parity note:** Replicates OLD `AppointmentChangeRequestDomain.cs` Update + outcome bucket selection. The `OriginalAppointmentId` chain on Appointment is the cascade-copy linkage (Phase 1.6).

### 1.6 Add missing fields to `Appointment` and other existing entities

- **Audit reference:** `external-user-appointment-request.md` row B (multi-field); `external-user-appointment-cancellation.md` rows B/I/I; `external-user-appointment-rescheduling.md` rows B/B; `clinic-staff-appointment-approval.md` rows B/I/B/B.
- **Files to modify:** `src\HealthcareSupport.CaseEvaluation.Domain\Appointments\Appointment.cs`. Add (current state has 13 fields per audit verification):
  - `Guid? OriginalAppointmentId` (reschedule chain; nullable).
  - `string? ReScheduleReason`.
  - `Guid? ReScheduledById`.
  - `string? CancellationReason`.
  - `Guid? CancelledById`.
  - `string? RejectionNotes`.
  - `Guid? RejectedById`.
  - `Guid? PrimaryResponsibleUserId`.
  - `bool IsAdHoc` -- reserved for future; default false. Keep on Appointment? Per audit `external-user-appointment-ad-hoc-documents.md`, `IsAdHoc` is on `AppointmentDocument`, not Appointment. **Correction:** add to `AppointmentDocument`, NOT Appointment.
  - `bool IsBeyondLimit` (reschedule beyond-limit override flag, mirrored on Appointment for reporting).
- **Files to modify:** `src\HealthcareSupport.CaseEvaluation.Domain\Appointments\AppointmentDocument.cs`:
  - `bool IsAdHoc` (default false; true for ad-hoc / general docs).
  - `bool IsJointDeclaration` (default false; true for AME JDF docs).
  - `Guid? VerificationCode` (per-document GUID for unauthenticated upload links; nullable so internal-user uploads can leave null).
- **Files to modify:** `src\HealthcareSupport.CaseEvaluation.Domain\Appointments\AppointmentManager.cs`:
  - Replace public `Status` setter on Appointment with `void SetStatus(AppointmentStatusType target, Guid? actorId = null, string? reason = null)` enforcing transitions:
    - `Pending -> Approved` (clinic-staff approve).
    - `Pending -> Rejected` (clinic-staff reject).
    - `Approved -> RescheduleRequested` (user reschedule submit).
    - `RescheduleRequested -> Approved` (supervisor reschedule reject).
    - `RescheduleRequested -> RescheduledNoBill / RescheduledLate` (supervisor reschedule approve, on OLD row).
    - `Approved -> CancelledNoBill / CancelledLate` (supervisor cancel approve).
    - `Approved -> CheckedIn -> CheckedOut -> Billed` (out-of-Phase-1 scope but enum supports).
    - All other transitions throw `BusinessException` with message `"InvalidStatusTransition"`.
  - `CloneForRescheduleAsync(Guid originalId, Guid newSlotId, bool sameConfirmationNumber)` -- cascade-copy method (full per-field copy spec in Phase 11.2).
- **Migrations:** one EF migration per logical group: `Add_Appointment_ChangeFields`, `Add_AppointmentDocument_Flags`, `Add_AppointmentChangeRequest`. Reseed reference data; do NOT regenerate seed for live tenants.
- **Acceptance criteria:**
  - [ ] DB schema has all listed columns.
  - [ ] `Appointment.SetStatus` rejects an invalid transition with `BusinessException`.
  - [ ] Existing tests pass.

### 1.7 Confirm enums + lookup seeds

- **`BookingStatus`** (`Domain.Shared/Enums/BookingStatus.cs`) -- VERIFIED: `Available = 8, Booked = 9, Reserved = 10`. No change.
- **`AppointmentStatusType`** (`Domain.Shared/Enums/AppointmentStatusType.cs`) -- VERIFIED: 14 values incl `RescheduleRequested = 12, CancellationRequested = 13`. No change. Note: per cancellation audit, `CancellationRequested = 13` is **not used** as interim in OLD; appointment stays `Approved` while change request is Pending. Strict parity: do NOT use `CancellationRequested` in Phase 1; leave the enum value in place but unused (audit rationale: future-proof for stakeholder request).
- **`DocumentStatus`** (`Domain.Shared/AppointmentDocuments/DocumentStatus.cs`) -- VERIFIED: `Uploaded = 1, Approved = 2, Rejected = 3`. The audit mentions OLD has `Pending` and `Deleted`; NEW intentionally drops them (file comment confirms). No change.
- **`AccessType`** (`Domain.Shared/Enums/AccessType.cs`) -- VERIFIED: `View = 23, Edit = 24`. No change.
- **`AppointmentType`** seed: VERIFY all 5 OLD types are seeded (PQME, AME, PQME-REVAL, AME-REVAL, OTHER). Audit verification noted "No 'OTHER' enum value found" -- OTHER must be added. Add to `AppointmentTypeDataSeedContributor.cs` if absent.
- **`RequestStatusType`** new enum: `Pending = 1, Accepted = 2, Rejected = 3` (per OLD `Models.Enums.RequestStatus`). File: `Domain.Shared/Enums/RequestStatusType.cs`.

## Phase 2 -- Identity, password policy, and permissions

### 2.1 Tighten password policy to OLD spec

- **Audit reference:** `external-user-registration.md`, `external-user-forgot-password.md`.
- **Files to modify:** `src\HealthcareSupport.CaseEvaluation.Domain\Identity\ChangeIdentityPasswordPolicySettingDefinitionProvider.cs` (current values all `false` per verification).
- **Code shape:** override settings:
  - `IdentitySettingNames.Password.RequireDigit = true`
  - `IdentitySettingNames.Password.RequireNonAlphanumeric = true`
  - `IdentitySettingNames.Password.RequireLowercase = false`
  - `IdentitySettingNames.Password.RequireUppercase = false`
  - `IdentitySettingNames.Password.RequiredLength = 8`
  - Reasoning: OLD regex `^(?=.*[0-9])(?=.*[a-zA-Z])(?=.*[-.!@#$%^&*()_=+/\\\\'])([a-zA-Z0-9-.!@#$%^&*()_=+/\\\\']+)$` requires at least one digit + at least one alpha (case-insensitive) + at least one of an explicit special set; min length 8.
  - ABP's policy framework does not support arbitrary char-set whitelists; combine `RequireDigit + RequireNonAlphanumeric + RequiredLength = 8`. Document the small policy delta: ABP allows any non-alphanumeric (broader than OLD's 21-char set), which is more permissive; visible behavior matches when OLD passwords are submitted.
- **Strict-parity note:** OLD's exact char-set is more restrictive; the policy delta is a framework constraint. ABP-permissive characters are not an attack vector.
- **Tests:** `PasswordPolicyTests` -> `Register_NoDigit_Throws`, `Register_NoSpecial_Throws`, `Register_LengthSeven_Throws`, `Register_Valid_Succeeds`.
- **Acceptance criteria:**
  - [ ] Registration form rejects "password" -> too short, no digit.
  - [ ] Registration form accepts `Test123!`.
  - [ ] Reset-password form enforces same policy.

### 2.2 Enable email-verification gate

- **Files to create:** `src\HealthcareSupport.CaseEvaluation.Domain\Settings\CaseEvaluationSettingDefinitionProvider.cs` (or modify existing if present).
- **Code shape:** override `IdentitySettingNames.User.IsEmailConfirmationRequiredForLogin = true`.
- **Acceptance criteria:**
  - [ ] Unverified user attempts login -> AuthServer shows "Verify your email" message.

### 2.3 Lowercase email semantics (verify or override)

- **Files to investigate:** ABP `ILookupNormalizer` default (uppercase). Confirm whether NEW uses default. If default: optional override to lowercase per OLD; ABP's normalization is internal and visible behavior (login by any-case email succeeds) matches OLD. **Decision: accept ABP defaults**; do NOT override unless an automated test surfaces a divergence.
- **Acceptance criteria:**
  - [ ] User registers `Test@Foo.com`; logs in as `test@foo.com`; both succeed.

### 2.4 Extend IdentityUser via `IObjectExtensionManager`

- **Audit reference:** `external-user-registration.md` (FirmName, FirmEmail); `external-user-login.md` (IsAccessor, IsExternalUser).
- **Files to modify:** `src\HealthcareSupport.CaseEvaluation.Domain\CaseEvaluationDomainModule.cs` -- in `ConfigureServices` add `OneTimeRunner` block to register extension properties on `IdentityUser`:
  - `FirmName string?` (max 256). For attorney roles only (validation in registration AppService).
  - `FirmEmail string?` (max 256). For attorney roles only.
  - `IsExternalUser bool` (default `false`). Set on registration: external roles -> `true`; internal seed -> `false`.
  - `IsAccessor bool` (default `false`). Set when accessor account is auto-created via `AppointmentAccessorManager.CreateAccountAsync`.
- **Cite:** ABP `Object-Extensions` doc -- extension props are stored on `AbpUserExtraProperties` JSON column, available via `user.GetProperty<string>("FirmName")`.
- **Tests:** `IdentityUserExtensionsTests` -> `Register_AttorneyRole_FirmNameSaved`, `Register_PatientRole_FirmNameNull`, `IsExternalUser_DefaultsToFalse_TrueWhenExternalRegistered`.
- **Acceptance criteria:**
  - [ ] Attorney registration with FirmName saves to extra properties.
  - [ ] `/api/identity/my-profile` returns FirmName, FirmEmail, IsExternalUser, IsAccessor.

### 2.5 Permission keys

- **Audit reference:** every audit doc adds permission rows.
- **Files to modify:**
  - `src\HealthcareSupport.CaseEvaluation.Application.Contracts\Permissions\CaseEvaluationPermissions.cs` -- add the missing permission groups:
    - `Appointments.Approve`, `Appointments.Reject`, `Appointments.RequestCancellation`, `Appointments.RequestReschedule` (Custom action keys).
    - `AppointmentChangeRequests.Default`, `.Approve`, `.Reject` (entire new group).
    - `NotificationTemplates.Default`, `.Edit` (new group; IT Admin only).
    - Verify: `SystemParameters.Default`, `.Edit` (already present per audit verification).
    - Verify: `CustomFields.Default`, `.Create`, `.Edit`, `.Delete` (already present).
  - `src\HealthcareSupport.CaseEvaluation.Application.Contracts\Permissions\CaseEvaluationPermissionDefinitionProvider.cs` -- register the new permissions in the same definition tree.
  - `src\HealthcareSupport.CaseEvaluation.Domain\Identity\InternalUserRoleDataSeedContributor.cs` -- add new permission grants:
    - IT Admin: all new keys.
    - Staff Supervisor: `Appointments.Approve, Appointments.Reject` (clinic-staff also; confirm via approval audit), `AppointmentChangeRequests.Default, .Approve, .Reject`, `NotificationTemplates.Default, .Edit`.
    - Clinic Staff: `Appointments.Approve, Appointments.Reject`, `AppointmentChangeRequests.Default` (read-only).
- **Acceptance criteria:**
  - [ ] Reseed dev DB; permission tree shows new keys.
  - [ ] Calling `/api/case-evaluation/appointment-change-requests` as Patient -> 403; as Staff Supervisor -> 200.

## Phase 3 -- IT Admin: System Parameters

- **Audit reference:** `it-admin-system-parameters.md`.
- **Files to create:**
  - `src\HealthcareSupport.CaseEvaluation.Application.Contracts\SystemParameters\ISystemParametersAppService.cs` -- methods `Task<SystemParameterDto> GetAsync()`, `Task<SystemParameterDto> UpdateAsync(SystemParameterUpdateDto input)`.
  - `src\HealthcareSupport.CaseEvaluation.Application.Contracts\SystemParameters\SystemParameterDto.cs` and `SystemParameterUpdateDto.cs`.
  - `src\HealthcareSupport.CaseEvaluation.Application\SystemParameters\SystemParametersAppService.cs` -- extends `CaseEvaluationAppService`, `[RemoteService(IsEnabled = false)]`.
  - `src\HealthcareSupport.CaseEvaluation.HttpApi\Controllers\SystemParameters\SystemParametersController.cs` -- `[Route("api/app/system-parameters")]`.
  - `src\HealthcareSupport.CaseEvaluation.Application\CaseEvaluationApplicationMappers.cs` -- partial class mapper `[Mapper(...)]` for `SystemParameter <-> SystemParameterDto` and `SystemParameterUpdateDto -> SystemParameter`.
  - Angular: `angular\src\app\system-parameters\system-parameters.component.{ts,html}` (standalone), reactive form via FormBuilder.
- **Code shape:**
  - `GetAsync` -- reads via `IRepository<SystemParameter>.FirstOrDefaultAsync(IMultiTenant)` filtered by current tenant; returns DTO. No `[Authorize]` (any authenticated user reads).
  - `UpdateAsync` -- `[Authorize(SystemParameters.Edit)]`. Updates singleton row; uses `_objectMapper.Map<SystemParameterUpdateDto, SystemParameter>(input, existing)` -- WAIT: per branch CLAUDE.md, no `ObjectMapper.Map<>`; use direct field copy via Mapperly partial method or assign by hand. Use Mapperly.
- **Tests:** `test\HealthcareSupport.CaseEvaluation.Application.Tests\SystemParameters\SystemParametersAppServiceTests.cs` -- `GetAsync_AnyAuthenticated_ReturnsSingleton`, `UpdateAsync_AsClinicStaff_Throws403`, `UpdateAsync_AsItAdmin_PersistsChanges`, `Seed_OnTenantCreate_InsertsOneRow`.
- **Acceptance criteria:**
  - [ ] IT Admin opens `/system-parameters` page, sees 12 fields, edits, saves.
  - [ ] Patient role calls `GetAsync` -> 200; calls `UpdateAsync` -> 403.
  - [ ] After update, booking flow respects new `AppointmentLeadTime`.

## Phase 4 -- IT Admin: Notification Templates

- **Audit reference:** `it-admin-notification-templates.md`.
- **Files to create:**
  - AppService + Contracts + Controller + Mapper + DTO + Angular form, all under `NotificationTemplates/`.
  - Methods: `GetListAsync(GetNotificationTemplatesInput)`, `GetByCodeAsync(string templateCode)`, `UpdateAsync(Guid id, NotificationTemplateUpdateDto)`. No Create/Delete (templates are seeded; IT Admin edits only).
  - DTO `NotificationTemplateDto`, `NotificationTemplateUpdateDto`, `NotificationTemplateWithNavigationPropertiesDto` (includes TemplateType).
- **Code shape:**
  - `[Authorize(NotificationTemplates.Default)]` on Get, `[Authorize(NotificationTemplates.Edit)]` on Update.
  - The renderer service (Phase Notifications) loads template by `TemplateCode`, applies Razor template against typed model.
- **Tests:** `NotificationTemplatesAppServiceTests` -> `GetByCode_ReturnsTemplate`, `Update_AsItAdmin_PersistsBody`.
- **Acceptance criteria:** [ ] IT Admin updates `AppointmentApproved` body; next approval uses new content.

## Phase 5 -- IT Admin: Package Details

- **Audit reference:** `it-admin-package-details.md`.
- **Files to create / verify:**
  - `IAppointmentPacketsAppService` -- per audit verified to exist. Add Documents linking endpoints if missing: `LinkDocumentsAsync(Guid packetId, Guid[] documentIds)`, `UnlinkDocumentAsync(Guid packetId, Guid documentId)`.
  - `IDocumentsAppService` (master document templates) -- new. Methods `CreateAsync(DocumentCreateDto)` (uploads via `IBlobStorage`), `GetListAsync`, `UpdateAsync`, `DeleteAsync` (soft via `ISoftDelete`).
  - Angular: `angular\src\app\documents\` and `angular\src\app\appointment-packets\` standalone components.
- **Tests:** `AppointmentPacketsAppServiceTests` -> `LinkDocuments_PersistsAllRows`, `Approval_AutoQueue_ReadsLinkedDocuments` (integration with Phase 7).
- **Acceptance criteria:** [ ] After approving an appointment of a packet-linked AppointmentType, the system creates `AppointmentDocument` rows for each linked Document.

## Phase 6 -- IT Admin: Custom Fields

- **Audit reference:** `it-admin-custom-fields.md`.
- **Files to create / verify:**
  - `IAppointmentTypeFieldConfigsAppService` exists per verification. Add: `CreateAsync` enforces max-10 active per AppointmentTypeId; `GetActiveForAppointmentTypeAsync(Guid appointmentTypeId)` returns active configs sorted by `DisplayOrder`.
  - Booking AppService reads `SystemParameter.IsCustomField` and surfaces config to Angular booking form.
  - Angular: dynamic field rendering in `appointment-add.component.ts` reads `getActiveForAppointmentTypeAsync(typeId)` and renders Date/Text/Number inputs with mandatory validation.
- **Tests:** `AppointmentTypeFieldConfigsAppServiceTests` -> `CreateAsync_11thActive_Throws`, `GetActiveFor_ReturnsSortedByDisplayOrder`.
- **Acceptance criteria:** [ ] Booking form shows or hides custom fields based on `SystemParameter.IsCustomField`; mandatory validation fires.

## Phase 7 -- Staff Supervisor: Doctor Management

- **Audit reference:** `staff-supervisor-doctor-management.md`.
- **Files to create / modify:**
  - `IDoctorAvailabilityManagement` (new domain service) under `src\HealthcareSupport.CaseEvaluation.Domain\DoctorAvailabilities\DoctorAvailabilityManagement.cs`:
    - `Task GenerateSlotsAsync(GenerateSlotsDto input)` -- generates slots for date range + location + 8AM-5PM (`FromTime`/`ToTime` from DTO) + slot duration (DTO or `SystemParameter.DurationTime`) + AppointmentTypeId (nullable).
    - `Task BulkDeleteByDateAsync(DateOnly date, Guid locationId)` -- deletes only slots in `Available` status; rejects if any are `Booked` or `Reserved`.
  - `DoctorAvailabilityValidator.ValidateOverlap(DoctorAvailability newSlot)` -- 4 checks:
    1. Reject overlap: existing slot at same Location + Date with `FromTime > new.FromTime` AND `ToTime < new.ToTime` -> "TimeSlotExists".
    2. Reject duplicate Available: same Location + Date + same FromTime + same ToTime + Available -> "TimeSlotExists".
    3. Reject conflict with Booked/Reserved: same Location + Date + same FromTime + same ToTime + (Booked OR Reserved) -> "TimeSlotBooked".
    4. `TimeSlotValidation(timeSlot)` time-format/range checks (port from OLD `DoctorsAvailabilityDomain.cs` lines 150+).
  - Update `IDoctorAvailabilitiesAppService.GetDoctorAvailabilityLookupAsync` -- known gap: returns all unfiltered. Add filter: `LocationId? + AppointmentTypeId? + BookingStatus.Available + slot.AvailableDate >= today + lead-time + slot.AvailableDate <= today + max-time-per-type`.
  - Verify `DoctorPreferredLocation` and `DoctorAppointmentType` link entities exist; add CRUD if missing.
  - Permissions: Add `DoctorAvailabilities.ManageAvailability`, `DoctorAvailabilities.BulkGenerate`, `DoctorAvailabilities.BulkDelete`. Gate to Staff Supervisor + IT Admin.
- **Tests:** `DoctorAvailabilityManagementTests` -> `Generate_NoOverlap_CreatesAllSlots`, `Generate_OverlapsExisting_Throws`, `BulkDelete_OneBooked_ThrowsAndKeepsAll`, `Lookup_FiltersByLocationAndType`.
- **Acceptance criteria:**
  - [ ] Staff Supervisor generates slots for a week + location + 60-min duration -> N slots created.
  - [ ] Tries to overlap an existing Booked slot -> rejected.
  - [ ] Bulk delete on a date with booked slots -> rejected.
  - [ ] External user booking sees only Available slots filtered by their selection.

## Phase 8 -- External user: Registration

- **Audit reference:** `external-user-registration.md`.
- **Files to create:**
  - `src\HealthcareSupport.CaseEvaluation.Application.Contracts\ExternalRegistration\IExternalUserRegistrationAppService.cs` -- `Task RegisterAsync(ExternalUserRegisterInput input)`. Public (`[AllowAnonymous]`).
  - `src\HealthcareSupport.CaseEvaluation.Application.Contracts\ExternalRegistration\ExternalUserRegisterInput.cs`: `Email`, `Password`, `ConfirmPassword`, `FirstName`, `LastName`, `Role` enum (Patient / Adjuster / ApplicantAttorney / DefenseAttorney), `FirmName` (validated when Role in attorney set), `FirmEmail` (validated same).
  - `src\HealthcareSupport.CaseEvaluation.Application\ExternalRegistration\ExternalUserRegistrationAppService.cs` -- wraps ABP `IAccountAppService.RegisterAsync` + post-create assigns role + sets extension props (`FirmName`, `FirmEmail`, `IsExternalUser = true`).
  - HttpApi controller + Angular registration form (standalone, FormBuilder, role picker, attorney-conditional fields).
- **Code shape:**
  - Validation: `ConfirmPassword == Password`, `FirmName.IsNotNullOrEmpty()` for attorney roles, password regex (handled by Phase 2.1 policy).
  - On success: ABP's `IAccountAppService.RegisterAsync` triggers email confirmation token issuance + email send via `IEmailSender`.
  - Email template: `UserRegistered`. Subject: `"You have registered successfully - {ClinicName}"` (typo `Your -> You` correction; OLD bug fixed).
  - Soft-delete re-registration: ABP's `ISoftDelete` filter excludes deleted users from the unique-email check. Verify; add a unit test.
- **Tests:** `ExternalUserRegistrationAppServiceTests` -> `RegisterAsync_WithValidData_CreatesUserAndAssignsRole`, `RegisterAsync_WithDuplicateEmail_ThrowsBusinessException`, `RegisterAsync_WithWeakPassword_ThrowsBusinessException`, `RegisterAsync_AttorneyWithoutFirmName_ThrowsBusinessException`, `RegisterAsync_SendsConfirmationEmail`.
- **Acceptance criteria:**
  - [ ] Registration form shows role picker; selecting Applicant Attorney / Defense Attorney shows FirmName + FirmEmail fields.
  - [ ] Submit with valid Patient -> account created; verification email arrives.
  - [ ] Click verification link -> account verified; login succeeds.
  - [ ] Try login before verification -> "verify your email" message; "Resend" link visible.
  - [ ] Duplicate email rejected.
  - [ ] Weak password rejected per policy.

## Phase 9 -- External user: Login + email verification

- **Audit reference:** `external-user-login.md`.
- **Files to modify:**
  - AuthServer Razor Page: `src\HealthcareSupport.CaseEvaluation.AuthServer\Pages\Account\Login.cshtml` (LeptonX customization). Add "Resend confirmation email" link below the password field for unverified users.
  - `src\HealthcareSupport.CaseEvaluation.AuthServer\Pages\Account\ResendEmailConfirmation.cshtml` (+ model) -- POST endpoint that re-issues the confirmation email via ABP's account service.
  - Localization: add keys for OLD's verbatim error messages: `User not exist`, `Invalid username or password`, `Your account is not activated`, `We have sent a verification link to your registered email id, please verify your email address to login`.
  - Angular: `angular\src\app\route.provider.ts` -- guard reading `IsExternalUser` extension prop after login: external -> `/home`, internal -> `/dashboard`.
- **Code shape:**
  - `IsExternalUser` lookup via `/api/identity/my-profile` (ABP's profile endpoint surfaces extra props via Object-Extensions).
  - `callBackUrl` honored via OAuth2 `redirect_uri`; no custom code.
  - Drop `document.cookie = "requestContext=abc"` legacy marker in NEW (verify nothing reads it; if NEW already lacks it, no-op).
- **Tests:**
  - Setting test: `CaseEvaluationSettingDefinitionProviderTests.IsEmailConfirmationRequiredForLogin_True`.
  - `ExternalUserLoginTests` (E2E via WebApplicationFactory) -> `Login_UnverifiedUser_ShowsResendLink`, `Login_Verified_RedirectsToHome`.
- **Acceptance criteria:**
  - [ ] Click "Login" on SPA -> AuthServer login page.
  - [ ] Unverified user -> error + Resend link.
  - [ ] Click Resend -> email arrives.
  - [ ] Verified Patient login -> `/home`.
  - [ ] Verified Clinic Staff login -> `/dashboard`.
  - [ ] Wrong password -> "Invalid username or password".
  - [ ] Inactive user -> "Your account is not activated".
  - [ ] Soft-deleted user -> "User not exist".

## Phase 10 -- External user: Forgot password + reset

- **Audit reference:** `external-user-forgot-password.md`.
- **Files to modify:**
  - AuthServer Razor: customize `Pages\Account\ForgotPassword.cshtml` to show OLD's verbatim error messages.
  - `src\HealthcareSupport.CaseEvaluation.Application\Identity\Overrides\CustomAccountAppService.cs` (new) -- override `IAccountAppService.SendPasswordResetCodeAsync`:
    - If `user.EmailConfirmed == false` -> throw `BusinessException("UnverifiedEmailReset")` with localized message.
    - If user `IsActive == false` -> throw `BusinessException("InactiveUserReset")`.
  - Subscribe to `UserPasswordChangedEto` (ABP distributed event) in a new `PasswordChangedEmailHandler` to send the `PasswordChange` template after successful reset.
- **Rate limiting:**
  - `src\HealthcareSupport.CaseEvaluation.HttpApi.Host\Program.cs` -- add `AddRateLimiter(...)` middleware with `FixedWindowRateLimiter` keyed on email body field, 5 requests / 1 hour.
  - Cite: https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit
- **Code shape (Node analogue for Adrian):** rate limiter is conceptually the same as `express-rate-limit`. ABP wires it via `app.UseRateLimiter()` between auth and routing middleware.
- **Tests:** `ForgotPasswordTests` -> `SendResetCode_UnverifiedUser_Throws`, `ResetPassword_ValidToken_UpdatesPasswordHash`, `ResetPassword_ConsumedToken_Fails`, `ResetPassword_SendsConfirmationEmail`, `ResetPassword_WeakPassword_Throws`, `RateLimit_BlocksSixthRequest`.
- **Acceptance criteria:**
  - [ ] Click Forgot Password -> AuthServer page.
  - [ ] Unverified user -> error.
  - [ ] Inactive user -> error.
  - [ ] Verified active user -> success message + email arrives.
  - [ ] Click reset link -> reset page loads with token.
  - [ ] Weak password rejected.
  - [ ] Mismatched confirmation rejected.
  - [ ] Valid new password -> success + login redirect.
  - [ ] Confirmation email arrives with subject `"Your password has been successfully changed - {ClinicName}"`.
  - [ ] Old password rejected on next login; new accepted.
  - [ ] Token consumed (re-use rejected).
  - [ ] 6th request within 1 hour -> 429.

## Phase 11 -- External user: Appointment booking + accessor sharing

This is the largest single phase; it lands the booking aggregate behavior end-to-end.

### 11.1 Booking AppService + AppointmentManager

- **Audit reference:** `external-user-appointment-request.md`.
- **Files to modify:**
  - `src\HealthcareSupport.CaseEvaluation.Domain\Appointments\AppointmentManager.cs` -- replace `CreateAsync` with full OLD-parity implementation:
    1. Validate slot exists + `BookingStatus == Available`. Else throw `BusinessException("AppointmentBookingDateNotAvailable")`.
    2. Validate `slot.AvailableDate >= DateTime.Today.AddDays(SystemParameter.AppointmentLeadTime)`. Else implicit reject (OLD message N/A; localize).
    3. Validate per-type max-time:
       - PQME, PQME-REVAL: `slot.AvailableDate <= DateTime.Today.AddDays(SystemParameter.AppointmentMaxTimePQME)`.
       - AME, AME-REVAL: ditto with `AppointmentMaxTimeAME`.
       - OTHER: ditto with `AppointmentMaxTimeOTHER`.
    4. Patient dedup via `IPatientRepository.FindMatchByDeduplicationRule(intake)` -- 3-of-6 rule (LastName, DOB, Phone, Email, SSN, ClaimNumber). Match -> reuse `PatientId`, set `IsPatientAlreadyExist = true`. No match -> create new `Patient`.
    5. Generate `RequestConfirmationNumber` via `GenerateConfirmationNumber()` (port `ApplicationUtility.GenerateConfirmationNumber` -- `A` + 5-digit zero-padded sequence). Use a dedicated tenant-scoped counter table or `IGuidGenerator` + collision retry. Add unique constraint `(TenantId, RequestConfirmationNumber)` on Appointment.
    6. Create Appointment with status:
       - External user: `Pending`. Slot `Available -> Reserved`.
       - Internal user: `Approved`. Slot `Available -> Booked`. Auto-queue package docs (Phase 12).
    7. Attach sub-entities: `AppointmentInjuryDetail` rows (with `AppointmentBodyPart`, `AppointmentClaimExaminer`, `AppointmentPrimaryInsurance`); `AppointmentEmployerDetail` rows (1:N -- audit notes NEW current is 1:1; FIX); `AppointmentApplicantAttorney`, `AppointmentDefenseAttorney`; `AppointmentDocument` rows for any uploaded ad-hoc docs; `AppointmentTypeFieldConfigValue` rows for custom fields.
    8. Accessors: for each `AccessorEmail` in DTO, call `IAppointmentAccessorManager.CreateOrLinkAsync(email, role, accessTypeId)`:
       - If user does not exist: create AbpUser with random temp password + `IsAccessor = true` + role; send invitation email via `AccessorInvitedEto`.
       - If user exists with same role: link via `AppointmentAccessor` row.
       - If user exists with different role: throw `BusinessException("AccessorRoleMismatch", "Your added accessor '<email>' is already registered in our system with different user type")`.
    9. Adjuster auto-fill: when creator is in Adjuster role, prefill `AppointmentClaimExaminer.Email = currentUser.Email` (readonly in UI).
    10. Persist; raise `AppointmentRequestedEto` event for stakeholder notifications.
  - `ReSubmitAppointmentAsync(string originalConfirmationNumber, ...)` -- Re-Request flow: validates original is `Rejected`, creates new appointment reusing the same confirmation #.
  - `CreateRevalAppointmentAsync(string originalConfirmationNumber, ...)` -- REVAL flow: validates original is `Approved` (or current user is IT Admin override), prefills DTO from original.
- **Files to modify:** `src\HealthcareSupport.CaseEvaluation.Application\Appointments\AppointmentsAppService.cs` -- add wrapper endpoints for the new manager methods; ensure `[Authorize(Appointments.Create)]`.
- **Files to modify:** `angular\src\app\appointments\appointment\components\appointment-add.component.ts`:
  - Remove `console.log('Date check', ...)` at line 1955 (audit pointed at 1546; verified at 1955 via search).
  - Switch lead-time/max-time gate to be informational only (server enforces; UI shows the disabled dates from `getDoctorAvailabilityLookupAsync`).
  - Wire Adjuster prefill of Claim Examiner.
  - Verify multi-injury support (add/remove injury rows; each row supports its own ClaimExaminer / PrimaryInsurance / BodyParts).
  - Verify multi-employer support (1:N change to entity).
- **Tests (book):**
  - `AppointmentManagerTests.CreateAsync_ExternalUser_StatusPending_SlotReserved`.
  - `AppointmentManagerTests.CreateAsync_InternalUser_StatusApproved_SlotBooked_PackageDocsQueued`.
  - `AppointmentManagerTests.CreateAsync_PatientDedup_3of6Fields_LinksExistingPatient`.
  - `AppointmentManagerTests.CreateAsync_LeadTimeViolation_Throws`.
  - `AppointmentManagerTests.CreateAsync_MaxTimePQME_AtBoundary_Succeeds`.
  - `AppointmentManagerTests.CreateAsync_MaxTimeAME_OneDayPast_Throws`.
  - `AppointmentManagerTests.RevalFlow_OriginalApproved_Prefills`.
  - `AppointmentManagerTests.RevalFlow_OriginalPending_Throws`.
  - `AppointmentManagerTests.ReRequestFlow_OriginalRejected_Succeeds`.
  - `AppointmentAccessorTests.CreateAsync_NewEmail_CreatesUserAndSendsInvite`.
  - `AppointmentAccessorTests.CreateAsync_ExistingEmailDifferentRole_Throws`.
  - `Appointment.SetStatus_InvalidTransition_Throws`.
- **Acceptance criteria:** [ ] Each row in audit's verification-test-plan section passes (14 scenarios).
- **Strict-parity note:** The 3-of-6 dedup rule, reval/re-request gates, accessor account creation, and confirmation # format are verbatim from OLD `AppointmentDomain.cs` Add + AddValidation.

### 11.2 `CloneForRescheduleAsync` cascade-copy

- **Audit reference:** `external-user-appointment-rescheduling.md`.
- **Code shape:** `Task<Appointment> CloneForRescheduleAsync(Guid originalAppointmentId, Guid newSlotId, bool sameConfirmationNumber)`:
  - Loads original with `GetWithNavigationPropertiesAsync` (full graph).
  - Creates new `Appointment` with:
    - Same `RequestConfirmationNumber` (when `sameConfirmationNumber = true`).
    - `OriginalAppointmentId = original.Id`.
    - `DoctorAvailabilityId = newSlotId`.
    - `Status = Approved` (set by caller after manager returns).
    - All scalar fields copied except: `AppointmentApproveDate` (recompute), `AppointmentDate` (from new slot), `IsBeyondLimit` (per supervisor decision).
  - Cascade-copies child entities (deep clone, new Ids):
    - `AppointmentInjuryDetail` (with sub: `AppointmentBodyPart`, `AppointmentClaimExaminer`, `AppointmentPrimaryInsurance`).
    - `AppointmentEmployerDetail` (1:N).
    - `AppointmentApplicantAttorney`, `AppointmentDefenseAttorney`.
    - `AppointmentAccessor` rows.
    - `AppointmentTypeFieldConfigValue` rows.
    - `AppointmentDocument` rows (preserve IsAdHoc + IsJointDeclaration; reset Status to Uploaded for re-review? Decide: keep status Approved for already-Approved package docs to avoid redundant review). Ground-truth: OLD `AppointmentChangeRequestDomain.cs` lines 800+ show document copy preserves `DocumentStatusId`; replicate.
- **Tests:** `AppointmentManagerTests.CloneForRescheduleAsync_CopiesAllChildEntities`, `..._SameConfirmationNumber`, `..._OriginalAppointmentIdSet`, `..._DocumentsPreserveStatus`.
- **Acceptance criteria:**
  - [ ] After supervisor approves reschedule, new appointment exists with same confirmation #, all sub-entities present, original linked via `OriginalAppointmentId`.

## Phase 12 -- Clinic Staff: Appointment approval

- **Audit reference:** `clinic-staff-appointment-approval.md`.
- **Files to create / modify:**
  - `IAppointmentsAppService.ApproveAppointmentAsync(Guid id, ApproveAppointmentDto)` -- DTO: `ResponsibleUserId Guid`, `PatientMatchOverride bool`.
    - Permission `[Authorize(Appointments.Approve)]`.
    - Logic: verify status `Pending`; set `Status = Approved` via `SetStatus`; set `AppointmentApproveDate = DateTime.UtcNow`; set `PrimaryResponsibleUserId`; transition slot `Reserved -> Booked`; if `PatientMatchOverride == false` and `IsPatientAlreadyExist == true`, link to matched patient; else create new patient (already done at booking).
    - Idempotency: if status already Approved, throw `BusinessException("AppointmentAlreadyApproved")`.
    - Raise `AppointmentApprovedEto` event.
  - `RejectAppointmentAsync(Guid id, RejectAppointmentDto)` -- DTO: `RejectionNotes string`.
    - Permission `[Authorize(Appointments.Reject)]`.
    - Logic: verify status `Pending`; set `Status = Rejected`; set `RejectionNotes`, `RejectedById = CurrentUser.Id`; transition slot `Reserved -> Available`.
    - Raise `AppointmentRejectedEto` event.
  - Patient match preview: `GetWithNavigationPropertiesAsync` includes `IsPatientAlreadyExist` and matched `PatientId` (verify present; add if missing).
  - Subscribers: `PackageDocumentQueueHandler : IDistributedEventHandler<AppointmentApprovedEto>` -- reads `AppointmentPacket` for the approved appointment's `AppointmentTypeId`, inserts `AppointmentDocument` rows with `IsAdHoc = false`, `Status = Pending` (or `Uploaded` -- audit confirms `Status = Uploaded` is the OLD "ready for upload" sentinel; we use `Uploaded` only after upload, so the package row starts with status `Uploaded` only conceptually -- actually the OLD docs use "Pending" / "Uploaded" / "Accepted" / "Rejected" / "Deleted"; NEW dropped Pending. **Strict-parity correction:** the queue handler creates rows with `Status = Uploaded` once the file is uploaded; before upload, no row exists. **Decision:** create rows with no file + `Status = null/Uploaded` mark-pending-upload. Cleaner: use `IsUploaded = false` flag. But that's a NEW deviation; alternative is to add `Pending = 0` enum value. **Recommendation:** add `Pending = 0` to `DocumentStatus` enum; queue creates rows with `Status = Pending`; upload transitions to `Uploaded`. This preserves OLD semantics. Update Phase 1.7 enum check accordingly.
  - Branding-token email handlers: `ApprovalEmailHandler` (subscribes to `AppointmentApprovedEto`) sends 3 emails: patient (template `AppointmentApproved` patient-version), responsible user (`ResponsibleUserAssigned`), all stakeholders (`AppointmentApprovedStakeholder`). `RejectionEmailHandler` sends to creator with notes (`AppointmentRejected`).
  - SMS handlers: parallel handlers using `ISmsSender`.
- **Tests:** `AppointmentApprovalTests` -> `Approve_PendingAppointment_StatusApproved`, `Approve_AlreadyApproved_Throws`, `Approve_WithoutResponsibleUser_Throws`, `Approve_AsPatient_Returns403`, `Reject_PendingAppointment_StatusRejected_SlotReleased`, `Approve_QueuesPackageDocs`.
- **Acceptance criteria:**
  - [ ] Pending -> Approve -> status=Approved, slot=Booked, package docs queued, 3 emails + 3 SMS sent.
  - [ ] Pending -> Reject -> status=Rejected, slot=Available, creator email + SMS.
  - [ ] Try approve already-Approved -> rejected.
  - [ ] Approve without ResponsibleUserId -> rejected.
  - [ ] Approve as Patient -> 403.

## Phase 13 -- External user: Appointment view + accessor read

- **Audit reference:** `external-user-view-appointment.md`.
- **Files to modify:**
  - `IAppointmentsAppService.GetWithNavigationPropertiesAsync(Guid)` -- extend eager-load to include all sub-entities listed in audit (InjuryDetails with subs, EmployerDetail, DefenseAttorney, ClaimExaminer, PrimaryInsurance, BodyParts).
  - Add `GetByConfirmationNumberAsync(string)` -- same access check.
  - Authorization policy: `AppointmentAccessPolicyHandler` -- allows access if (a) `Appointment.CreatorId == CurrentUser.Id`, OR (b) an `AppointmentAccessor` row exists for `(AppointmentId, IdentityUserId)` with `AccessTypeId in (View=23, Edit=24)`. Inject into `[Authorize(Policy="CanViewAppointment")]`.
  - Field-level filter on DTO: `InternalUserComments` excluded for external users (use a separate `AppointmentExternalDto` or null the field in mapper based on `IsExternalUser`).
  - List endpoint `GetListAsync` -- automatically filter for external users to show only `(CreatorId == me) OR (AppointmentAccessor.IdentityUserId == me)`. Verify automatic, not opt-in.
  - Angular: `appointment-view.component.ts` -- refactor from ngModel to FormBuilder for consistency (out-of-strict-parity but consistent with branch standards). Lower priority; can defer to follow-up.
- **Tests:** `AppointmentAccessTests` -> `Owner_CanRead`, `AccessorView_CanRead_NoEdit`, `AccessorEdit_CanRead_CanEdit`, `Stranger_403`, `LookupByConfirmationNumber_RespectsAccess`.
- **Acceptance criteria:**
  - [ ] Owner sees own appointment; non-creator non-accessor blocked.
  - [ ] View accessor sees read-only view.
  - [ ] Edit accessor sees writable view.
  - [ ] Confirmation # lookup respects access policy.
  - [ ] External list shows only own + shared.
  - [ ] Internal list shows all in tenant.

## Phase 14 -- Document review: package + ad-hoc + JDF

This phase consolidates all 4 document audits since they share the upload + accept/reject + notification machinery.

### 14.1 Unified `AppointmentDocument` upload paths

- **Audit reference:** `external-user-appointment-package-documents.md`, `external-user-appointment-ad-hoc-documents.md`, `external-user-appointment-joint-declaration.md`, `clinic-staff-document-review.md`.
- **Files to modify / create:**
  - `IAppointmentDocumentsAppService.UploadAsync(UploadDocumentInput)` -- DTO: `Guid AppointmentId`, `Guid DocumentId?` (null for ad-hoc), `bool IsAdHoc`, `bool IsJointDeclaration`, file bytes.
    - Validation gates per flag combo:
      - Package (`IsAdHoc = false`, `IsJointDeclaration = false`): appointment status must be `Approved` or `RescheduleRequested`; current date <= `Appointment.DueDate`.
      - Ad-hoc (`IsAdHoc = true`, `IsJointDeclaration = false`): no status gate (any time), no due-date gate.
      - JDF (`IsAdHoc = false`, `IsJointDeclaration = true`): `AppointmentType in (AME, AME-REVAL)`; status `Approved`; <= `DueDate`; uploader role in attorney roles AND `Appointment.CreatorId == CurrentUser.Id`.
    - Internal-user fast-path: if `CurrentUser.IsExternalUser == false`, set `Status = Approved` immediately. Else `Status = Uploaded`.
    - File storage: `await _blobContainer.SaveAsync(blobName, fileStream)`. Filename convention: `{Guid.NewGuid()}_{Appointment.RequestConfirmationNumber}{DateTime.UtcNow:yyyyMMddHHmmss}.{ext}`. Cite: ABP `Blob-Storing` doc.
    - Re-upload logic: if document exists with `Status = Rejected`, clear `RejectionReason`, set `Status = Uploaded`, replace blob.
    - Accepted documents immutable for external users: if `Status = Approved` and `CurrentUser.IsExternalUser`, throw `BusinessException("DocumentImmutable")`.
    - Raise `DocumentUploadedEto`.
  - `UploadByVerificationCodeAsync(Guid verificationCode, Stream file)` -- public (`[AllowAnonymous]`) endpoint for unauthenticated package-doc upload via email link.
    - Lookup `AppointmentDocument` where `VerificationCode == code AND Status != Approved`.
    - Same gates as authenticated upload, but uploader is implicitly the appointment patient/creator.
    - Rate-limited: 5 / hour / verification code (ASP.NET Core Rate Limiting middleware).
  - `AcceptDocumentAsync(Guid documentId)` -- `[Authorize(AppointmentDocuments.Approve)]`. Sets `Status = Approved`. Raises `DocumentAcceptedEto`.
  - `RejectDocumentAsync(Guid documentId, RejectDocumentInput { Notes })` -- `[Authorize(AppointmentDocuments.Approve)]`. Sets `Status = Rejected`, `RejectionReason = Notes`, `RejectedByUserId = CurrentUser.Id`. Raises `DocumentRejectedEto`. Strict parity: always set `Status` (fix OLD JDF bug where only `RejectedById` was set).
  - JDF auto-cancel: `JointDeclarationAutoCancelJob : IRecurringJob` (Hangfire). Runs daily.
    - Query: `Appointment.Status = Approved AND AppointmentType in (AME, AME-REVAL) AND NOT EXISTS(AppointmentDocument WHERE IsJointDeclaration AND Status in (Uploaded, Approved)) AND (DueDate - DateTime.Today).Days <= JointDeclarationUploadCutoffDays`.
    - For each: `appointment.SetStatus(CancelledNoBill)` (or whichever outcome OLD picks; verify by reading OLD `AppointmentJointDeclarationDomain.cs`; default `CancelledNoBill`); release slot; raise `AppointmentAutoCancelledEto` -> notify all stakeholders.
- **Code shape (Node analogue for Adrian):** the `IDistributedEventBus.PublishAsync(eto)` is conceptually identical to Node's EventEmitter; subscribers register via `IDistributedEventHandler<TEto>`. Within the same process, ABP delivers synchronously or via an in-memory queue depending on config.
- **Tests:**
  - `DocumentUploadTests.UploadByCode_ValidCode_AcceptsFile`.
  - `DocumentUploadTests.UploadByCode_InvalidCode_Throws`.
  - `DocumentUploadTests.PackageDoc_AfterDueDate_Throws`.
  - `DocumentUploadTests.AdHoc_NoStatusGate_Succeeds`.
  - `DocumentUploadTests.AdHoc_AfterDueDate_Succeeds` (package would reject).
  - `DocumentUploadTests.JDF_NonAttorney_Throws`.
  - `DocumentUploadTests.JDF_NonCreator_Throws`.
  - `DocumentUploadTests.JDF_NotAme_Throws`.
  - `DocumentUploadTests.InternalUser_AutoApproved`.
  - `DocumentReviewTests.Accept_PendingDoc_StatusApproved`.
  - `DocumentReviewTests.Reject_WithNotes_StatusRejected`.
  - `DocumentReviewTests.ReUpload_AfterReject_ClearsNotes`.
  - `DocumentReviewTests.JDF_AcceptOrReject_AlwaysSetsStatus` (regression test for OLD bug).
  - `JDFAutoCancelJobTests.AmeMissingJDFNearDueDate_AutoCancels`.
  - `RateLimitTests.UploadByCode_5InHour_6thBlocked`.
- **Acceptance criteria (consolidated across 4 audit docs):**
  - [ ] Approve appointment -> package docs auto-queued -> patient receives email with verification-code links.
  - [ ] Click email link (no login) -> upload page -> upload PDF -> success.
  - [ ] Internal user uploads ad-hoc on Pending -> auto-Approved.
  - [ ] Staff rejects with notes -> email with notes -> user re-uploads -> notes cleared.
  - [ ] AME with no JDF + due date approaching -> auto-cancelled + stakeholders notified.
  - [ ] PQME upload of JDF -> rejected.
  - [ ] JDF accept always sets `DocumentStatus.Approved` (not just `RejectedById = null`).
  - [ ] 6th verification-code request within hour -> 429.

## Phase 15 -- External user: Cancellation request

- **Audit reference:** `external-user-appointment-cancellation.md`.
- **Files to create / modify:**
  - `IAppointmentChangeRequestsAppService.RequestCancellationAsync(Guid appointmentId, RequestCancellationDto)` -- DTO: `string CancellationReason` (required); supporting docs upload optional.
    - Authorization: creator OR accessor with `AccessTypeId = 24`.
    - Validates Approved status; validates `(slot.AvailableDate - DateTime.Today).TotalDays >= AppointmentCancelTime`. Else throws `BusinessException("CannotCancelOrRescheduleAppointment")`.
    - Calls `AppointmentChangeRequestManager.SubmitCancellationAsync`.
    - Strict parity: leave appointment status at `Approved` while change request is `Pending` (OLD does NOT use the `CancellationRequested = 13` interim status for cancellation -- only for reschedule).
- **Tests:** `CancellationRequestTests` -> `Submit_Approved_WithReason_Succeeds`, `Submit_TooClose_ToAppointment_Throws`, `Submit_NoReason_Throws`, `Submit_OnPending_Throws`, `Submit_AsViewAccessor_Throws`, `Submit_AsEditAccessor_Succeeds`.
- **Acceptance criteria:** [ ] All audit verification scenarios.

## Phase 16 -- External user: Reschedule request

- **Audit reference:** `external-user-appointment-rescheduling.md`.
- **Files to create / modify:**
  - `IAppointmentChangeRequestsAppService.RequestRescheduleAsync(Guid appointmentId, RequestRescheduleDto)` -- DTO: `Guid NewDoctorAvailabilityId`, `string ReScheduleReason`, `bool IsBeyondLimit`, `Guid[] SupportingDocumentIds`.
    - Authorization: creator OR accessor with `AccessTypeId = 24`.
    - Validates Approved status; validates new slot Available + lead-time + per-type max-time (skip max-time if `IsBeyondLimit && CurrentUser is admin`).
    - Calls `AppointmentChangeRequestManager.SubmitRescheduleAsync` -> appointment `Approved -> RescheduleRequested`; new slot `Available -> Reserved`.
- **Tests:** `RescheduleRequestTests` -> `Submit_Approved_NewSlot_StatusRescheduleRequested`, `Submit_NoReason_Throws`, `Submit_UnavailableSlot_Throws`, `Submit_BeyondMaxTime_NotAdmin_Throws`, `Submit_BeyondMaxTime_AdminWithIsBeyondLimit_Succeeds`.
- **Acceptance criteria:** [ ] All audit verification scenarios.

## Phase 17 -- Staff Supervisor: Change request approval

- **Audit reference:** `staff-supervisor-change-request-approval.md`.
- **Files to create:**
  - `IAppointmentChangeRequestsAppService.ApproveCancellationAsync(Guid changeRequestId, ApproveCancelDto { CancellationOutcome })` -- delegates to manager. Outcome enum: `CancelledNoBill = 5` / `CancelledLate = 6`. Concurrency: ABP populates `ConcurrencyStamp` on read; second update sees `DbUpdateConcurrencyException` -> rethrows as `BusinessException("ChangeRequestAlreadyHandled")`.
  - `RejectCancellationAsync(Guid changeRequestId, RejectDto { Reason })`.
  - `ApproveRescheduleAsync(Guid changeRequestId, ApproveRescheduleDto { Outcome, OverrideSlotId?, AdminReScheduleReason? })`. Validation: if `OverrideSlotId.HasValue && OverrideSlotId != original.NewDoctorAvailabilityId`, then `AdminReScheduleReason` is required. Outcome: `RescheduledNoBill = 7` / `RescheduledLate = 8`.
  - `RejectRescheduleAsync(Guid changeRequestId, RejectDto)`.
  - List endpoint `GetPendingChangeRequestsAsync(GetChangeRequestsInput)` with filters: `RequestStatusType.Pending`, `ChangeRequestType?`, date range, AppointmentTypeId?.
- **Code shape:** all delegate to `AppointmentChangeRequestManager` methods (Phase 1.5). Manager raises events; subscribers notify.
- **Tests (12 scenarios from audit):**
  - `ChangeRequestApprovalTests.ApproveCancel_NoBill_StatusCancelledNoBill_SlotAvailable`.
  - `ChangeRequestApprovalTests.ApproveCancel_Late_StatusCancelledLate`.
  - `ChangeRequestApprovalTests.RejectCancel_AppointmentBackToApproved`.
  - `ChangeRequestApprovalTests.ApproveReschedule_NoOverride_NewAppointment_SameConfirmation_NewSlotBooked_OldAvailable`.
  - `ChangeRequestApprovalTests.ApproveReschedule_WithOverride_RequiresAdminReason`.
  - `ChangeRequestApprovalTests.ApproveReschedule_WithOverride_NoReason_Throws`.
  - `ChangeRequestApprovalTests.RejectReschedule_AppointmentBackToApproved_NewSlotAvailable`.
  - `ChangeRequestApprovalTests.Concurrency_TwoSupervisorsApprove_SecondThrows`.
  - `ChangeRequestApprovalTests.CascadeCopy_AllChildEntitiesPresentInNewAppointment`.
  - `ChangeRequestApprovalTests.MultipleReschedules_OriginalIdChainsCorrectly`.
  - `ChangeRequestApprovalTests.ApproveCancel_AsClinicStaff_Throws403` (Staff Supervisor + IT Admin only).
  - `ChangeRequestApprovalTests.RejectionRevertsAppointmentStatus`.
- **Acceptance criteria:** [ ] All 10 verification scenarios from audit.

## Phase 18 -- Notifications + reminder jobs

This phase wires every event handler and Hangfire job referenced in Phases 8-17.

### 18.1 Event handlers

- **Files to create:** `src\HealthcareSupport.CaseEvaluation.Application\Notifications\Handlers\` (one file per handler).
  - `RegisteredEmailHandler : IDistributedEventHandler<UserRegisteredEto>` -- ABP raises this; send `UserRegistered` template.
  - `PasswordChangedEmailHandler : IDistributedEventHandler<UserPasswordChangedEto>` -- send `PasswordChange` template.
  - `AppointmentRequestedEmailHandler / SmsHandler` -- subscribes to `AppointmentRequestedEto`, sends to patient + attorneys + ClaimExaminer + employer.
  - `ApprovalEmailHandler / SmsHandler` -- subscribes to `AppointmentApprovedEto`, sends 3 emails (patient, responsible user, stakeholders).
  - `RejectionEmailHandler / SmsHandler` -- subscribes to `AppointmentRejectedEto`.
  - `PackageDocumentQueueHandler` -- subscribes to `AppointmentApprovedEto`, creates `AppointmentDocument` rows from linked `AppointmentPacket.Documents` for the appointment's AppointmentTypeId.
  - `DocumentUploadedEmailHandler` -- subscribes to `DocumentUploadedEto`, emails uploader + responsible user.
  - `DocumentAcceptedEmailHandler` -- subscribes to `DocumentAcceptedEto`, emails uploader.
  - `DocumentRejectedEmailHandler` -- subscribes to `DocumentRejectedEto`, emails uploader with notes.
  - `CancellationRequestSubmittedHandler` -- emails supervisors.
  - `CancellationApprovedHandler` -- emails all stakeholders + SMS.
  - `CancellationRejectedHandler` -- emails requester + SMS.
  - `RescheduleRequestedHandler / Approved / Rejected` -- ditto.
  - `AccessorInvitedEmailHandler` -- subscribes to `AccessorInvitedEto`, sends invitation with reset-password link.
  - `AppointmentAutoCancelledHandler` -- emails all stakeholders + SMS.
- **Code shape (Node analogue for Adrian):** ABP handlers are equivalent to subscribing to a Node EventEmitter or BullMQ queue handler. The `IDistributedEventBus` is the in-process bus by default; can be swapped to RabbitMQ etc. via config without code changes.
- **Subject builder helper:** `EmailSubjectBuilder.Build(Appointment, TemplateCode)` -- common helper that includes `Patient.FirstName Patient.LastName + AppointmentInjuryDetail.ClaimNumber + WcabAdj` per OLD pattern. Used in handlers across feature areas.
- **Acceptance criteria:** [ ] Each event triggers correct emails + SMS to correct recipients with localized subjects + bodies. Verified by snapshot tests against rendered templates.

### 18.2 Hangfire recurring jobs

- **Files to create:** `src\HealthcareSupport.CaseEvaluation.Domain\Notifications\Jobs\`.
  - `PackageDocumentReminderJob : IRecurringJob` -- daily; queries `AppointmentDocument` rows where `Status in (Pending, Uploaded but rejected)` AND `(Appointment.DueDate - DateTime.Today).Days == OverdueNotificationDays`. Sends `PackageDocumentsReminder` email to patient/creator.
  - `JointDeclarationAutoCancelJob` -- daily (Phase 14.1).
  - `JointDeclarationReminderJob` -- daily; mirrors package reminder, but for AME JDF docs.
  - `DueDateApproachingReminderJob` -- daily; sends generic "appointment in N days" reminder.
  - `CancellationRescheduleReminderJob` -- VERIFIED present per audit; verify schedule + recipients.
- **Registration:** in `CaseEvaluationApplicationModule.ConfigureServices`, register jobs via Hangfire's `AddHangfireServer` + `RecurringJob.AddOrUpdate(...)` in module init.
- **Cite:** ABP `Background-Jobs` + Hangfire docs.
- **Tests:** `JobTests` per job -> `Job_Runs_FiltersCorrectAppointments`, `Job_SendsExpectedEmails`.
- **Acceptance criteria:** [ ] Each job runs at the scheduled cadence in dev; sends emails to correct recipients.

### 18.3 SMS via `ISmsSender` (ABP Twilio module)

- **Configure:** Twilio creds in `appsettings.secrets.json`. Cite ABP `ISmsSender` doc.
- **Code shape:** every `*EmailHandler` has a sibling `*SmsHandler` that loads same `TemplateCode`, renders `BodySms`, sends via `_smsSender.SendAsync(SmsMessage)`.
- **Acceptance criteria:** [ ] Every event that triggered an email also triggers an SMS to the same primary recipient (where mobile number is on file).

## Phase 19 -- Branding aggregation (Task #6)

- **Audit reference:** `_old-docs-index.md` line 84-85 (Angular template colors / logo / footer copy).
- **Files to create:**
  - `W:\patient-portal\replicate-old-app\docs\parity\_branding.md` -- aggregates branding touchpoints called out in every audit doc.
  - `angular\src\styles\_brand.scss` -- CSS custom properties: `--brand-primary`, `--brand-secondary`, `--brand-logo-url`, `--brand-clinic-name`, `--brand-support-email`, `--brand-support-phone`.
  - `angular\src\app\shared\branding\branding.service.ts` -- standalone service reading from a tenant-scoped config endpoint (new): `IBrandingAppService.GetForCurrentTenantAsync()`. Service exposes `BehaviorSubject<BrandingConfig>` consumed by app shell.
  - `src\HealthcareSupport.CaseEvaluation.Application\Branding\BrandingAppService.cs` -- returns clinic name, primary color, logo URL, support contact. Reads from `SystemParameter` extended fields OR from a separate `Branding` entity (Phase 2 decision; for Phase 1 use `SystemParameter` extension fields: `BrandClinicName`, `BrandPrimaryColor`, `BrandLogoBlobName`, `BrandSupportEmail`, `BrandSupportPhone`).
- **AuthServer branding:** override LeptonX theme variables in `src\HealthcareSupport.CaseEvaluation.AuthServer\wwwroot\css\brand.css`.
- **Email templates:** every notification template includes `{ClinicName}`, `{LogoUrl}`, `{SupportEmail}`, `{SupportPhone}` placeholders, rendered from BrandingAppService.
- **Acceptance criteria:**
  - [ ] Changing `BrandPrimaryColor` in SystemParameter -> Angular UI re-themes on next page load.
  - [ ] Login page (AuthServer) reflects clinic colors.
  - [ ] All emails include clinic name + logo + support contact.

## Phase 20 -- Cross-feature E2E integration tests

- **Files to create:** `test\HealthcareSupport.CaseEvaluation.Application.Tests\E2E\`.
  - `BookApproveUploadRescheduleCancelTests` -- single test class covers golden path:
    1. Patient registers, verifies email, logs in.
    2. Patient books PQME -> Pending + slot Reserved.
    3. Clinic Staff approves -> Approved + slot Booked + package docs queued + emails sent.
    4. Patient receives email with verification-code link, uploads each package doc -> Uploaded.
    5. Clinic Staff accepts each doc -> Approved.
    6. Patient submits reschedule -> RescheduleRequested.
    7. Staff Supervisor approves reschedule -> NEW Appointment row, same conf #, old slot Available, new slot Booked.
    8. Patient submits cancellation on new appointment -> change request Pending.
    9. Staff Supervisor approves cancellation with `CancelledNoBill` -> status CancelledNoBill, slot Available.
  - `AmeJdfAutoCancelTests` -- AME without JDF + cutoff approach -> job auto-cancels.
  - `AccessorInvitedAndUseTests` -- new email accessor -> account created -> invited -> sets password -> views appointment.
  - `PatientDedupTests` -- 2 bookings with same patient signature -> second reuses Patient row.
- **Acceptance criteria:** [ ] All E2E tests pass on a clean dev DB.

## Localization key checklist + notification template codes

Every key referenced in the plan must exist in `src\HealthcareSupport.CaseEvaluation.Domain.Shared\Localization\CaseEvaluation\en.json` before referencing it in code or templates. Strict parity: match OLD's exact validation messages and email subjects verbatim.

### Validation message keys (en.json)

```
"UserAlreadyExists": "User already exists",
"InvalidPassword": "Password does not meet complexity requirements",
"PasswordMismatch": "Password and confirm password do not match",
"FirmNameRequired": "Firm Name is required",
"User not exist": "User not exist",
"Invalid username or password": "Invalid username or password",
"Your account is not activated": "Your account is not activated",
"We have sent a verification link to your registered email id, please verify your email address to login": "We have sent a verification link to your registered email id, please verify your email address to login",
"We have sent a verification link to your registered email id, please verify your email address to do further process": "We have sent a verification link to your registered email id, please verify your email address to do further process",
"PasswordPatternValidation": "Password must contain at least one digit and one special character; minimum 8 characters",
"ConfirmPasswordValidation": "Password and confirm password do not match",
"AppointmentBookingDateNotAvailable": "Selected appointment slot is not available",
"AppointmentLeadTimeViolation": "Appointment date is too soon; minimum lead time is {0} days",
"AppointmentMaxTimeViolation": "Appointment date is too far out for this appointment type",
"AccessorRoleMismatch": "Your added accessor '{0}' is already registered in our system with different user type",
"NoChangeAllowedinAppointment": "No change allowed for an appointment in this status",
"CannotCancelOrRescheduleAppointment": "Cannot cancel or reschedule appointment within {0} days of the slot",
"ProvideCancelReason": "Please provide a cancellation reason",
"AppointmentAlreadyApproved": "Appointment Already Approved",
"AppointmentAlreadyRejected": "Appointment Already Rejected",
"InvalidStatusTransition": "Invalid appointment status transition",
"YouCanNotReevalThisAppointmentRequestBecauseItsNotYetApproved": "You can not Re-eval this appointment request because it's not yet approved",
"YouNotAllowedToReApplyAppointment": "You not allowed to re apply appointment",
"PleaseUploadDocumentsAfterAppointmentIsApproved": "Please upload documents after appointment is approved",
"YouCanNotUploadDocumentAfterSpecifiedDueDate": "You can not upload document after specified due date",
"AppointmentTypeIsNotValidPleaseUploadAppropriateDocument": "Appointment type is not valid. Please upload appropriate document",
"UnUnauthorizedUser": "An unauthorized user",
"DocumentImmutable": "Approved documents cannot be modified",
"ChangeRequestAlreadyHandled": "This change request has already been processed",
"UnverifiedEmailReset": "Please verify your email address before resetting your password",
"InactiveUserReset": "Your account is not activated"
```

### Notification template codes (seeded by `NotificationTemplateDataSeedContributor`) [VERIFIED 2026-05-03]

> **Correction (2026-05-03).** The earlier seed list (23 invented names like
> `UserRegistered`, `ResponsibleUserAssigned`, `JDFAutoCancelled`,
> `AccessorInvited`) does NOT exist in OLD. OLD has two storage mechanisms,
> total 59 events. Verbatim list below; full table with Phase 1 in-scope
> subset in `docs/parity/it-admin-notification-templates.md` "Template code
> matrix" section.

**Source files:**
- `P:\PatientPortalOld\PatientAppointment.Models\Enums\TemplateCode.cs` (lines 9-27) -- 16 DB-managed codes
- `P:\PatientPortalOld\PatientAppointment.DbEntities\Constants\ApplicationConstants.cs` (lines 26-71) -- 43 on-disk HTML codes
- `P:\PatientPortalOld\PatientAppointment.DbEntities\Constants\ApplicationConstants.cs` (lines 11-23) -- 13 placeholder variable names (`##confirmationnumber##`, `##documentname##`, etc.)

**Seed all 59 codes (verbatim from OLD with typos fixed):**

OLD typos that NEW does NOT preserve (verified 2026-05-03; see audit doc for
evidence):
- `RejectedJoinDeclarationDocument` -> `RejectedJointDeclarationDocument`
- `AppointmentApprovedStackholderEmails` -> `AppointmentApprovedStakeholderEmails`
- `PatientAppointmentCancellationApprvd` -> `PatientAppointmentCancellationApproved`
  (also fixes the HTML-filename typo "Apporved")
- HTML filename `User-Registed.html` -> body migrated; constant `UserRegistered`
  was already correct

OLD-style abbreviations kept (consistent local patterns, not typos):
`PatientAppointmentRescheduleReq`, `PatientAppointmentRescheduleReqAdmin`,
`PatientAppointmentRescheduleReqApproved`, `PatientAppointmentRescheduleReqRejected`.

```
# DB-managed (TemplateCode enum, 16):
AppointmentBooked
AppointmentApproved
AppointmentRejected
AppointmentCancelledRequest
AppointmentCancelledRequestApproved
AppointmentCancelledRequestRejected
AppointmentRescheduleRequest
AppointmentRescheduleRequestApproved
AppointmentRescheduleRequestRejected
RejectedPackageDocument
RejectedJointDeclarationDocument               # FIXED from OLD's RejectedJoinDeclarationDocument
AppointmentDueDate
AppointmentDueDateUploadDocumentLeft
SubmitQuery
AppointmentApprovedStakeholderEmails           # FIXED from OLD's Stackholder
AppointmentCancelledByAdmin

# On-disk HTML in OLD (EmailTemplate static class, 43):
AddInternalUser
PasswordChange
ResetPassword
UserRegistered
UserQuery
AppointmentRescheduleRequestByAdmin
AppointmentChangeLogs
PatientAppointmentPending
PatientAppointmentApproveReject
PatientAppointmentApprovedInternal
PatientAppointmentApprovedExt
PatientAppointmentRejected
PatientAppointmentCheckedIn
PatientAppointmentCheckedOut
PatientAppointmentNoShow
PatientAppointmentCancelledNoBill
ClinicalStaffCancellation
AccessorAppointmentBooked
PatientDocumentAccepted
PatientDocumentRejected
PatientDocumentUploaded
PatientNewDocumentAccepted
PatientNewDocumentRejected
PatientNewDocumentUploaded
PatientDocumentAcceptedAttachment
PatientDocumentAcceptedRemainingDocs
PatientDocumentRejectedRemainingDocs
AppointmentApproveRejectInternal
UploadPendingDocuments
AppointmentDueDateReminder
AppointmentDocumentIncomplete
AppointmentCancelledDueDate
AppointmentPendingNextDay
PatientAppointmentRescheduleReqAdmin
PatientAppointmentRescheduleReqApproved
PatientAppointmentRescheduleReqRejected
PatientAppointmentCancellationApproved         # FIXED from OLD's Apprvd / Apporved
PatientAppointmentRescheduleReq
JointAgreementLetterAccepted
JointAgreementLetterUploaded
JointAgreementLetterRejected
AppointmentDocumentAddWithAttachment
PendingAppointmentDailyNotification
```

**Phase 1 wires 33 of these 59 to handlers** (see audit doc for the per-event
mapping table). The remaining 26 are seeded but their handlers are deferred
to post-parity feature phases (Check-In/Out, NoShow, Billing, Internal-User-
Mgmt, Audit-Log viewer, Submit-Query).

For each seeded row: Subject string + BodyEmail (HTML w/ `##Var##` placeholders
converted to Razor `@Model.Var` at seed time) + BodySms (text, only for the 16
TemplateCode entries; the 43 EmailTemplate entries are email-only in OLD --
NEW may add SMS bodies opportunistically but not as a parity requirement).

## Deferred items (not in workflow scope)

The following audit slices exist in the OLD-docs-extracted scope but are NOT part of Phase 1 parity. Track for post-parity work.

1. Internal-user (Clinic Staff / Staff Supervisor / IT Admin) management UI (CRUD on internal users).
2. Reports + Dashboards (host-side IT Admin, tenant-side Staff Supervisor).
3. Check-In / Check-Out flow (CheckedIn = 9, CheckedOut = 10 statuses).
4. Billing flow (Billed = 11 status, billing report).
5. AwaitingMoreInfo flow (status 14).
6. Audit log viewer UI (`AppointmentChangeLogs` permission key already exists; UI deferred).
7. Application timezones management.
8. Application exception logs viewer.
9. Application request logs viewer.
10. Module / Role permission management UI (ABP provides defaults; extension UI deferred).
11. Cache management.
12. Bulk import / export.

## Additional gaps detected during planning (NOT in audits)

These were surfaced by direct verification of NEW source code against audits or OLD code, and are NOT in the audits' gap tables. Each should be reviewed.

1. **`InternalUserRoleDataSeedContributor.AllEntities` and `OperationalEntities` arrays** still include `AppointmentDocuments` and `CustomFields` -- after Phase 0.1 cleanup of Doctor role + Phase 2.5 permission additions, ensure these arrays plus `DoctorGrants()` are cleanly removed; do NOT leave dead Doctor-only literals.
2. **`AppointmentChangeLogs` permission group** exists in NEW (`CaseEvaluationPermissions.cs` line 201-204) but no `AppointmentChangeLog` entity / AppService is documented in any audit. This is NEW scaffolding ahead of Phase 1; either (a) implement the audit-log entity in this plan as a follow-up under Phase Notifications, OR (b) remove the permission key for now. **Recommendation: keep the permission key, add `AppointmentChangeLog` entity in a post-parity follow-up phase since OLD has `AppointmentChangeLogs` table per `_old-docs-index.md` line 1.**
3. **`DocumentStatus.Pending` (= 0)** not in NEW enum; package-doc auto-queue (Phase 12) needs a "queued, awaiting upload" state distinct from "Uploaded". Decide between:
   - (a) Add `Pending = 0` to enum (matches OLD vocab; preferred for strict parity).
   - (b) Use `IsUploaded` separate flag on AppointmentDocument.
   - **Recommendation: (a)**. Update Phase 1.7.
4. **Comment in `DocumentStatus.cs`** ("renames Accepted -> Approved for symmetry") is a NEW-only naming choice. OLD uses `Accepted`. Decision: revert to `Accepted` for strict parity (rename `Approved -> Accepted` in enum + all references). Trade-off: NEW DocumentStatus.Approved breaks parity vocabulary; recommend rename in Phase 1.7.
5. **`AppointmentEmployerDetail`** is currently 1:1 in NEW per audit; OLD allows 1:N. Phase 11.1 includes the change but it requires schema migration + Angular form rework that the audit may have under-scoped.
6. **`DocumentStatus`** comments in NEW reference "Office Admin / Practice Admin / Supervisor" role names that don't match the locked role model (Clinic Staff / Staff Supervisor / IT Admin). Update file comment in Phase 0 cleanup.
7. **OLD JDF auto-cancel outcome** (CancelledNoBill vs CancelledLate) is not specified in the audit; decide by reading OLD `AppointmentJointDeclarationDomain.cs` directly during Phase 14 implementation. Default: `CancelledNoBill` (less punitive).
8. **`NotificationTemplate` per-tenant** vs host-wide -- audit treats as tenant-scoped. Confirm OLD's `Templates` table has TenantId. If host-wide in OLD, NEW should mirror. **Recommendation: per-tenant scoped to honor multi-tenant phase 2 plan.**
9. **`appointment-add.component.ts` line number** for `console.log` -- audit said 1546, verification found 1955. Either the file grew or the audit was inaccurate. Implementer should grep for `console.log` and remove all occurrences (file is 1594+ lines; treat as cleanup task).
10. **`AppointmentAccessor` placement** -- audit verification found `AppointmentAccessor` under `Appointments\` (NOT under `Patients\` as audit hinted). Phase 11.1 references existing path; no action needed but call out the audit's incorrect placement note.
11. **The 4 `[Fact(Skip="KNOWN GAP: ...")]` tests** mentioned in the prompt -- verification found NO Skip="KNOWN GAP" attributes in `AppointmentsAppServiceTests.cs` (first 80 lines scanned). Either the file grew, the comments were rephrased, or they were already removed. Implementer should grep `Skip=` across `test\` and un-skip each as the corresponding gap closes.
12. **`State` lookup-read entity** is in `LookupReadEntities` array in `InternalUserRoleDataSeedContributor.cs` but `States` is in `AllEntities` for IT Admin grants. Audit doesn't surface this as a gap; the array overlap means IT Admin gets `States.Edit/Create/Delete` AND lookup permissions -- this is consistent. No action needed.

## Risk register

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Cascade-copy on reschedule misses a sub-entity (e.g. `AppointmentTypeFieldConfigValue`) | Medium | High | Centralize copy in `AppointmentManager.CloneForRescheduleAsync` with explicit field-by-field copy; integration test asserts each child entity count matches original. |
| `AppointmentStatusType.CancellationRequested = 13` left unused causes future stakeholder confusion | Low | Low | Add C# XML comment on enum value: `// Reserved per OLD enum; not used in cancellation flow per strict-parity audit 2026-05-01`. |
| Hangfire job timezone drift causes JDF auto-cancel to fire late | Medium | Medium | Use `DateTime.UtcNow` + `SystemParameter.JointDeclarationUploadCutoffDays` consistently; add timezone-handling test using `IClock` mock. |
| Patient dedup 3-of-6 rule produces false positives (rare but high-impact: PHI cross-contamination) | Low | High | Tight match: require LastName + DOB + (3 of remaining 4); audit 2nd-level flag in NEW UI on next visit; document false-positive escalation procedure in user-facing copy. |
| ABP Razor template renderer differs from OLD `##Var##` substitution | High | Medium | At template seed time, convert OLD bodies to Razor syntax once; store final Razor body. Render against typed model. Snapshot tests verify output. |
| OpenIddict refresh-token TTL differs from OLD's session-token semantics | Low | Low | Accept default; OLD's session enforcement is deprecated in modern OAuth. Document in `06-Work-Context/Decision-Log.md`. |
| `IBlobStorage` provider swap (e.g., dev FileSystem -> prod Azure Blob) breaks file-naming convention | Low | Medium | Always use `BlobName = $"{guid}_{conf}_{ts}.{ext}"` and let provider control physical location. Test against both providers in CI. |
| Migration adding `OriginalAppointmentId` FK to self-referential Appointment fails on existing data | Low | Medium | Add column nullable, no FK first; reseed; then add FK constraint in a follow-up migration. |
| Removing Doctor role breaks any existing tenant DBs in dev | Medium | Low | Provide a one-time SQL script: `DELETE FROM AbpRoles WHERE Name = 'Doctor'; DELETE FROM AbpUsers WHERE Email LIKE 'doctor@%';` for dev environments only. |
| Strict-parity may require user-visible LeptonX page customizations exceeding theme override capabilities | Medium | Medium | Phase Branding budgets time for AuthServer Razor Page customization; if blocked, fall back to minimal LeptonX overrides + post-parity custom theme. |
| Ad-hoc + package + JDF unification via flags in `AppointmentDocument` causes query complexity | Medium | Low | Use scoped query helpers (`GetPackageDocs(appointmentId)` vs `GetAdHocDocs(...)` vs `GetJointDeclarations(...)`) so callers do not litter `Where(d => d.IsAdHoc && !d.IsJointDeclaration)` everywhere. |
| 3rd-party Twilio SDK in `ISmsSender` blocks builds without Twilio creds | Medium | Low | Configure Twilio creds optional; `ISmsSender` no-ops in dev when creds missing; CI uses fake. Document in dev README. |
| The `CancellationRequested = 13` decision (not used in cancel flow) means audits referencing "appointment goes to CancellationRequested while pending" are wrong | Low | Medium | Decision documented above; implementers must NOT route to this status during cancel submission. Surface a code review checklist item. |

## Final acceptance gate

Plan is "complete" only when an implementer reading this plan + the 18 audit docs can execute Phase 0 -> Phase 20 without:

- Reading any per-feature `CLAUDE.md` for direction.
- Reading any prior audit conversation history.
- Asking the user to clarify scope (only true ambiguity in OLD code requires direct OLD-code reading).

The implementer follows phase order, completes each phase's acceptance criteria, runs tests, and opens PRs against `feat/replicate-old-app` per branch CLAUDE.md.
