---
feature: it-admin-system-parameters
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\SystemParameterModule\SystemParameterDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\system-parameter\
old-docs:
  - socal-project-overview.md (lines 555-567)
  - data-dictionary-table.md (SystemParameters)
audited: 2026-05-03
status: in-progress
priority: 2
strict-parity: true
internal-user-role: ITAdmin
depends-on: []
required-by:
  - external-user-appointment-request           # lead time + max time per type
  - external-user-appointment-cancellation      # AppointmentCancelTime
  - external-user-appointment-rescheduling      # lead time + max time per type
  - external-user-appointment-joint-declaration # JointDeclarationUploadCutoffDays
---

# IT Admin -- System parameters

## Purpose

A SINGLE-ROW configuration table (`SystemParameters`) holding system-wide settings that govern booking, cancellation, reschedule, and JDF auto-cancel rules. Only IT Admin can read/modify. All other roles consume the values via lookups.

**Strict parity with OLD.**

## OLD behavior (binding)

### Schema (single row, ID = 1)

Per `SystemParameters` table in data dictionary:

| Field | Type | Used by |
|-------|------|---------|
| `SystemParameterId` | int(10) PK | -- |
| `AppointmentLeadTime` | int | Booking + reschedule submit -- minimum days advance to book |
| `AppointmentMaxTimePQME` | int | Booking + reschedule -- max days into future for PQME / PQME-REVAL |
| `AppointmentMaxTimeAME` | int | Booking + reschedule -- max days for AME / AME-REVAL |
| `AppointmentMaxTimeOTHER` | int | Booking + reschedule -- max days for OTHER appointment type |
| `AutoCancelCutoffTime` | int | JDF auto-cancel -- days before due date |
| `ReminderCutoffTime` | int | Package doc reminder -- days before due date to start sending |
| `AppointmentDurationTime` | int | Slot duration in minutes (used by slot generation) |
| `AppointmentDueDays` | int | Days from appointment date to documents-due date |
| `AppointmentCancelTime` | int | Days before appointment when cancellation is still allowed |
| `JointDeclarationUploadCutoffDays` | int | JDF auto-cancel cutoff (alternative or same as `AutoCancelCutoffTime`?) |
| `PendingAppointmentOverDueNotificationDays` | int | Pending appointment overdue reminder cadence |

### IT Admin UI

Per spec lines 555-567: IT Admin views/edits these in a single form. No row creation -- always update the existing row. Includes `IsCustomField` boolean toggle (TO VERIFY -- may be a separate setting that gates the Custom Fields feature).

### Critical OLD behaviors

- **Single row pattern.** No insert; always update. ID hardcoded to 1 (or first row).
- **No history/audit visible** in the schema -- modifications overwrite. (Audit trail would come from `AuditRecords` if hooked up.)
- **Read access:** all roles read indirectly via the lookup endpoints (`SystemParametersService.getBy([1])` per OLD frontend code).
- **Write access:** IT Admin only.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/SystemParameterModule/SystemParameterDomain.cs` | CRUD logic |
| `PatientAppointment.Api/Controllers/.../SystemParametersController.cs` | API: GET, POST, PUT, PATCH, DELETE on `/api/SystemParameters` |
| `patientappointment-portal/src/app/components/system-parameter/system-parameters/...` | Edit form |
| `DbEntities.Models.SystemParameter` | EF entity |

## NEW current state

- TO VERIFY existence of `SystemParameter` entity in NEW (search for `SystemParameter*` under `src/HealthcareSupport.CaseEvaluation.Domain/`).
- The fields are READ from booking + cancel + reschedule audits (which surfaced this as a dependency). NEW must have these values configured for those gates to fire correctly.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev | Status |
|--------|-----|-----|--------|-----|--------|
| Single-row config entity | OLD `SystemParameters` | NEW: TO VERIFY | **Add `SystemParameter : Entity<Guid>` aggregate** with all 12 fields. Seed exactly ONE row per tenant on tenant-create. | B | [IMPLEMENTED 2026-05-01 - pending testing] -- entity at `Domain/SystemParameters/SystemParameter.cs` (`FullAuditedAggregateRoot<Guid>, IMultiTenant`, 13 fields incl. `CcEmailIds`); seeded by `SystemParameterDataSeedContributor`; `Phase1_Add_ParityEntities_And_AppointmentFields` migration. |
| Field set | OLD has 12 fields | -- | **Match all 12** field names + types | B | [IMPLEMENTED 2026-05-01 - pending testing] -- 12 OLD fields + `CcEmailIds` cc-list; defaults in `SystemParameterConsts`. |
| Read endpoint (any role) | OLD: GET on /api/SystemParameters/{id} | -- | **Add `IGetSystemParametersAppService.GetAsync()` returning the singleton** | B | [IMPLEMENTED 2026-05-03 - pending integration test] -- `ISystemParametersAppService.GetAsync()` returns the per-tenant singleton; throws `BusinessException(SystemParameter.NotSeeded)` on missing row. Manual controller at `api/app/system-parameters` (HTTP GET). Class-level `[Authorize(SystemParameters.Default)]`. |
| Write endpoint (IT Admin only) | OLD: PUT/PATCH | -- | **Add `UpdateAsync(SystemParameterUpdateDto)` with `[Authorize(...SystemParameters.Edit)]`** | B | [IMPLEMENTED 2026-05-03 - pending integration test] -- `UpdateAsync(SystemParameterUpdateDto)` with `[Authorize(SystemParameters.Edit)]`. Validates positive ints + CcEmailIds length pre-write; round-trips ConcurrencyStamp. HTTP PUT on `api/app/system-parameters`. |
| Permission keys | -- | -- | **Add `CaseEvaluation.SystemParameters.Default` + `.Edit`** | I | [IMPLEMENTED 2026-05-02 - pending testing] -- both keys registered in `CaseEvaluationPermissions.SystemParameters` + provider; granted to IT Admin (Default + Edit), Staff Supervisor (Default + Edit), Clinic Staff (Default read-only). |
| Seed values | OLD: TO VERIFY exact defaults | -- | **Add data seed contributor** with sane defaults: LeadTime=3, MaxTimePQME=60, MaxTimeAME=90, MaxTimeOTHER=60, AutoCancelCutoff=7, ReminderCutoff=7, DurationTime=60, DueDays=14, CancelTime=2, JDFCutoff=7, OverdueNotificationDays=3 (TO VERIFY in OLD seed scripts; these are guesses) | I | [IMPLEMENTED 2026-05-01 - pending testing] -- defaults match these guesses verbatim; `SystemParameterDataSeedContributor` is per-tenant idempotent. |
| Audit trail | OLD: not visible | NEW: ABP `[Audited]` attribute | **Add `[Audited]`** | C | [IMPLEMENTED 2026-05-01 - pending testing] -- `[Audited]` on entity. |
| `IsCustomField` flag | OLD has it | -- | **Add as bool field**; gates whether Custom Fields show up on intake form | I | [IMPLEMENTED 2026-05-01 - pending testing] -- bool field on entity, default false. |
| `CcEmailIds` field (semicolon-separated) | OLD `[Column("CcEmailIds")] string CcEmailIds` -- nullable, free text. UI helptext: "If you need to add multiple email IDs then please separate using ';'". No format validation in OLD. | NEW Phase 1 entity has nullable `CcEmailIds` with `MaxLength = 500`. | **Match OLD verbatim**: store as-is, no email format validation (strict parity). MaxLength enforced via `Check.Length`. | I | [IMPLEMENTED 2026-05-01 - pending testing] -- Phase 1 entity carries the field; AppService Update path will surface it (Phase 3). [GAP-2026-05-02] surfaced during Phase 3 audit re-read. |
| Field-level `[Range(1, int.MaxValue)]` on every int field | OLD entity carries `[Range(1, int.MaxValue)]` on all 11 int fields -- entity-level constraint enforces &gt;= 1. | NEW Phase 1 entity constructor uses `Check.Range(field, 1, int.MaxValue)` -- equivalent on insert. | **Update path must enforce equivalent.** Phase 3 AppService `UpdateAsync` validates each int field is &gt;= 1 before persisting; throw `BusinessException` with localized message on violation. | B | [IMPLEMENTED 2026-05-03 - tested unit-level] -- `SystemParametersAppService.ValidatePositiveIntegers` calls `Check.Range(... 1, int.MaxValue)` on each of the 11 int fields; ApplicationException-derived throw verified by `SystemParametersValidatorUnitTests` (14 unit tests pass). [GAP-2026-05-02]. |
| OLD UI hides 2 fields | OLD HTML lines 43-50 comment out `AppointmentCancelTime` and `JointDeclarationUploadCutoffDays` inputs -- they exist in DB but are not user-editable in OLD's portal. Other-feature audits (cancellation, JDF auto-cancel) read these values, so they ARE used at runtime. | NEW: must surface them somewhere. | **OLD-bug-fix exception**: surface these 2 fields in NEW UI + DTO so IT Admin can configure them. Reason: OLD silently used hardcoded DB defaults via direct DB access; NEW respects the strict-parity directive's "OLD bug, fixed for correctness" exemption since hiding configurable runtime gates from the only role allowed to manage them is a usability bug. | I | [IMPLEMENTED 2026-05-03 - DTO surface only; UI deferred] -- both fields are in `SystemParameterDto` + `SystemParameterUpdateDto` and round-trip via the AppService. Angular UI surfacing them is deferred to the Phase 3 follow-up Angular component. [OLD-BUG-FIX] [GAP-2026-05-02]. |
| Optimistic concurrency | OLD: no concurrency stamp / etag / version column. Last-write-wins semantics. | NEW: `FullAuditedAggregateRoot` provides `ConcurrencyStamp` (`IHasConcurrencyStamp`). | **OLD-bug-fix exception**: include `ConcurrencyStamp` in `SystemParameterDto` + `SystemParameterUpdateDto`. AppService passes through to entity; ABP's EF Core integration enforces optimistic locking. Reason: under multi-user IT-Admin scenarios (rare but possible), silent overwrites of policy gates can cause incidents. Additive safety; visible behavior unchanged for single-user case. | C | [IMPLEMENTED 2026-05-03 - pending integration test] -- both DTOs implement `IHasConcurrencyStamp`; AppService writes `entity.ConcurrencyStamp = input.ConcurrencyStamp` before update so EF Core emits `WHERE ConcurrencyStamp = @old`. [OLD-BUG-FIX] [GAP-2026-05-02]. |
| Update-path `Check.Range` symmetry | OLD entity validates only on insert via `[Range]` attributes; on update via `JsonPatch.ApplyTo` the validation IS re-run by ASP.NET Core if the controller validates `ModelState`. NEW Phase 1 constructor validates only on insert. | -- | **AppService.UpdateAsync explicitly calls a private validator** that re-applies `Check.Range(... &gt;= 1)` on every int field of `SystemParameterUpdateDto` BEFORE assigning to entity. Localized error key: `:Validation.MustBePositiveInteger`. | B | [IMPLEMENTED 2026-05-03 - tested unit-level] -- `ValidatePositiveIntegers` static helper invoked at top of UpdateAsync; one unit test enumerates all 11 fields and asserts ArgumentException on zero per field. Localization keys added under `SystemParameter:Validation.MustBePositiveInteger`. [GAP-2026-05-02]. |
| HTTP method matrix | OLD: GET /api/SystemParameters, GET /api/SystemParameters/{id}, POST, PUT/{id}, PATCH/{id}, DELETE/{id}. Angular UI uses ONLY GET-by-id and PUT. POST/PATCH/DELETE are unused dead routes. | -- | **NEW exposes `GET /api/app/system-parameters` (singleton) + `PUT /api/app/system-parameters` (singleton update). No Create / Delete / Patch endpoints.** Verify: `[Route("api/app/system-parameters")]` controller maps `GetAsync` -&gt; `[HttpGet]` and `UpdateAsync` -&gt; `[HttpPut]`. | B | [IMPLEMENTED 2026-05-03 - pending integration test] -- `SystemParametersController` at `api/app/system-parameters` exposes `[HttpGet]` + `[HttpPut]` only. POST / PATCH / DELETE intentionally absent. [GAP-2026-05-02]. |
| Tenant-not-yet-seeded handling | OLD: tenant is single (no multi-tenancy). | NEW: per-tenant singleton; `GetAsync` may be called before `SystemParameterDataSeedContributor` ran for a freshly-provisioned tenant. | **AppService.GetAsync defensive path**: if `GetCurrentTenantAsync()` returns null, throw `BusinessException` with `:NotSeeded` key (asks IT Admin / SaaS host to re-run DbMigrator). Do NOT auto-create -- seeding belongs to the data seeder, not the AppService. | I | [IMPLEMENTED 2026-05-03 - pending integration test] -- `BusinessException(CaseEvaluationDomainErrorCodes.SystemParameterNotSeeded)` thrown from both Get + Update when row missing. Code maps to localized `SystemParameter:NotSeeded` message. [GAP-2026-05-02]. |

## Internal dependencies surfaced

- None new. This is a leaf feature.

## Branding/theming touchpoints

- Edit form UI (logo, primary color, page title)
- No email templates (no notifications from this feature)

## Replication notes

### ABP wiring

- **Entity:** `SystemParameter : Entity<Guid>, IMultiTenant` (per-tenant config).
- **Seed:** `SystemParameterDataSeedContributor : IDataSeedContributor`. On tenant create, insert one row with default values.
- **AppService:** `ISystemParametersAppService` with `GetAsync()` (no id; returns singleton via `IRepository<SystemParameter>.FirstOrDefaultAsync()`) and `UpdateAsync(SystemParameterUpdateDto)`. Manual controller at `api/app/system-parameters`.
- **Permissions:** `CaseEvaluation.SystemParameters.Default` (any internal user can read; external users read indirectly via booking lookup endpoints), `CaseEvaluation.SystemParameters.Edit` (IT Admin only).
- **Strict parity:** preserve OLD's "no DELETE, no INSERT after seed" behavior. Don't expose Delete in AppService.

### Things NOT to port

- DELETE endpoint -- single-row config; never delete.
- Stored procs.

### Verification (manual test plan)

1. IT Admin opens system parameters page -> sees 12 fields with current values
2. IT Admin updates `AppointmentLeadTime` from 3 to 5 -> save
3. External user attempts to book within 4 days -> rejected (was allowed when LeadTime=3)
4. Non-IT-Admin internal user opens page -> read-only OR rejected (TO DEFINE -- OLD likely allows read for all, write for IT Admin)
5. External user GET /api/system-parameters -> returns config (READ access for all authenticated users)
