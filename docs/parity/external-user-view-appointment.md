---
feature: external-user-view-appointment
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs (Get methods)
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentAccessorDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\detail\
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\view\
old-docs:
  - socal-project-overview.md (lines 397-403)
  - data-dictionary-table.md (Appointments + AppointmentAccessor)
audited: 2026-05-01
status: audit-only
priority: 1
strict-parity: true
depends-on:
  - external-user-appointment-request  # share-via-AppointmentAccessor created at booking time
---

# External user view appointment (with sharing semantics)

## Purpose

External users (Patient, Adjuster, Applicant Attorney, Defense Attorney) view appointment details. Two access paths:

1. **Owner access** -- the user who created the appointment (`Appointment.CreatedById == CurrentUser.Id`).
2. **Accessor access** -- a user listed in `AppointmentAccessor` rows for the appointment, with `AccessTypeId` controlling read-only vs. read+write rights.

Plus: there's a "look up by RequestConfirmationNumber" UX path -- user enters a confirmation #, system validates the lookup user has rights, then renders details.

**Strict parity with OLD.**

## OLD behavior (binding)

### Lookup methods

- **By appointment ID:** `AppointmentDomain.Get(int id)` returns Appointment with includes: `Patient, AppointmentAccessors, AppointmentPatientAttorneys, AppointmentDefenseAttorneys, AppointmentInjuryDetails, CustomFieldsValues, AppointmentEmployerDetails`. Plus separate fetches for `AppointmentPrimaryInsurance`, `AppointmentClaimExaminer`, `AppointmentInjuryBodyPartDetail` per injury (loaded into navigation properties).
- **By confirmation number:** UX flow per spec line 401: `"The user should be login to the system and enter the request confirmation number in order to view the appointment request."`. Server-side: lookup by `RequestConfirmationNumber`.
- **List view:** `AppointmentDomain.Get(orderByColumn, sortOrder, pageIndex, rowCount, statusId, date, search)` -- stored proc `spm.spAppointmentRequestList`. The proc applies `UserId` filtering: returns appointments where the user is creator OR has an AppointmentAccessor row.

### Access-rights validation

OLD requires login + creator/accessor check before showing details. The stored proc `spAppointmentRequestList` likely enforces this via SQL filter; per-appointment access requires `Appointment.CreatedById == @UserId OR EXISTS (SELECT 1 FROM AppointmentAccessor WHERE AppointmentId = @AppointmentId AND EmailId = @UserEmail)`.

### `AccessTypeId` semantics

`AppointmentAccessor.AccessTypeId` (FK to `AccessTypes` master). From overview spec + NEW Angular comments (`access types [{ value: 23, label: 'View' }, { value: 24, label: 'Edit' }]`):

- `AccessTypeId = 23` -> **View** (read-only)
- `AccessTypeId = 24` -> **Edit** (read+write -- can modify appointment, upload docs, etc.)

Strict parity: keep these IDs (or rebuild `AccessTypes` lookup with same labels).

### Shared appointments use case

Per spec line 125-127: Patient Attorney and Defense Attorney can share booked appointments with **other users in their law firm** by entering an email address. The shared user receives access via `AppointmentAccessor` row.

### `AppointmentAccessor.RoleId`

The accessor's intended role -- the user being granted access must register as that role (or already be that role). Used in:

- Booking validation (booking audit slice): existing user with email + non-matching role -> reject.
- Auto-account-creation: if email doesn't have an account, system creates one with the specified role.

### Critical OLD behaviors

- **Appointment data is fully eager-loaded** -- on view, ALL related entities (patient, attorneys, injuries with sub-entities, employer, etc.) are returned in one shot. Strict parity: NEW should expose a single rich endpoint returning the full graph.
- **Confirmation # is the user-facing identifier** -- the public "look up an appointment" UX uses `RequestConfirmationNumber`, not the internal Guid.
- **No explicit per-field permission** -- if you can view an appointment, you see all fields. (Internal-user-only fields like `InternalUserComments` are excluded for external users TO VERIFY.)
- **Accessors with View access cannot upload package docs** -- only the owner or accessors with Edit access can. (TO VERIFY in OLD code.)

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentDomain.cs` | `Get(int id)` -- single appointment with includes; `Get(...)` -- list via stored proc |
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentAccessorDomain.cs` (389 lines) | Accessor CRUD + `CreateAccountOfAppointmentAccessors` (auto-create user); reads + writes for sharing |
| `PatientAppointment.Api/Controllers/.../AppointmentsController.cs` (Get methods) | API surface |
| `patientappointment-portal/.../appointment-request/appointments/{detail,view}/...` | Detail + view components |
| `patientappointment-portal/.../appointment-request/appointment-accessors/` | Accessor list/add/edit components |

## NEW current state

Per `Appointments/CLAUDE.md`:

- `IAppointmentsAppService.GetWithNavigationPropertiesAsync` -- returns rich graph with Patient, IdentityUser, AppointmentType, Location, DoctorAvailability, AppointmentApplicantAttorney
- `EfCoreAppointmentRepository.cs` -- 5-way LEFT JOIN + AppointmentAccessor subquery for accessor filter; loads AppointmentApplicantAttorney separately
- `GetAppointmentsInput.AccessorIdentityUserId` filter -- enables attorney-scoped queries (when set, query matches creator OR accessor)
- View page (`appointment-view.component.ts`) -- standalone, ngModel-based, ~969 lines

### Known gaps (per NEW CLAUDE.md)

- `view/:id` route has only `authGuard`, no `permissionGuard` -- ANY authenticated user in tenant can deep-link.
- `GetWithNavigationPropertiesAsync` is `[Authorize]` only -- ABP's tenant filter scopes data to user's tenant but cross-role read inside same tenant is not gated.
- View page uses ngModel (inconsistent with rest of app -- modal + add use FormBuilder).
- Eager-load does NOT include: `AppointmentInjuryDetail` and its sub-entities (BodyParts, ClaimExaminer, PrimaryInsurance), `AppointmentEmployerDetail` (other than as a separate join), `AppointmentDefenseAttorney`. Verify what current `GetWithNavigationPropertiesAsync` actually returns.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Access rights: creator OR accessor | OLD enforces in stored proc (verified `AppointmentDomain.cs`:62) | NEW: AccessorIdentityUserId filter exists; verify GetAsync also gates | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 13a) -- pure predicate `AppointmentAccessRules.CanRead(callerId, isInternal, creatorId, accessors)` in Domain/Appointments/. Wired into `AppointmentsAppService.GetAsync` and `GetWithNavigationPropertiesAsync` via private `EnsureCanReadAsync` helper that loads accessor rows + composes the predicate. Internal users bypass; external users must be creator OR have an `AppointmentAccessor` row. Throws `BusinessException(AppointmentAccessDenied)` on failure. 13 unit tests cover internal/external/creator/accessor branches. | B |
| Confirmation # lookup | UX path: enter conf # -> validate access -> show | NEW: TO VERIFY whether GetByConfirmationNumberAsync exists | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 13a) -- new `IAppointmentsAppService.GetByConfirmationNumberAsync(string)` endpoint. Returns null when no row matches; throws `AppointmentAccessDenied` when a row exists but the caller cannot read it (so the existence of a confirmation # is not leaked to strangers). Controller route `GET api/app/appointments/by-confirmation-number/{confNum}`. Reuses the Phase 11g `IAppointmentRepository.FindByConfirmationNumberAsync` repo method. | I |
| Eager-load full graph | OLD includes 7+ entities (verified `AppointmentDomain.cs`:66 -- Patient/AppointmentAccessors/AppointmentPatientAttorneys/AppointmentDefenseAttorneys/AppointmentInjuryDetails/CustomFieldsValues/AppointmentEmployerDetails + per-injury sub-fetch for PrimaryInsurance/ClaimExaminer/BodyPartDetail) | NEW includes ~5 | [DESCOPED 2026-05-04 - Phase 13b] -- next slice: extend `AppointmentWithNavigationProperties` + `EfCoreAppointmentRepository.GetWithNavigationPropertiesAsync` + Mapperly mapper. | B |
| `view/:id` permission gap | OLD: per-user filter | NEW: only authGuard | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 13a) -- the API-side `EnsureCanReadAsync` runs on every read so a deep-link to `/appointments/view/<id>` for an unauthorised id surfaces the `AccessDenied` error from the API. Adding a client-side `permissionGuard` is a UI-only follow-up (no parity gain over the server gate). | B |
| Cross-role read inside tenant | OLD: stored proc filters | NEW: `[Authorize]` only | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 13a) -- closed by `EnsureCanReadAsync`; cross-role access inside a tenant now requires creator/accessor match. | B |
| AccessTypeId values 23 (View) / 24 (Edit) | OLD seed | NEW: hardcoded constants in Angular per CLAUDE.md | [IMPLEMENTED PRIOR] -- NEW already has `AccessType` enum (View=23, Edit=24) in Domain.Shared/Enums; the audit's "or use enum" path is the chosen one. No seed table needed. | I |
| Internal-only fields (`InternalUserComments`) hidden from external users | TO VERIFY OLD behavior | NEW: `InternalUserComments` exists on `AppointmentDto` exposed to all users | [DESCOPED 2026-05-04 - Phase 13b] -- field-level mask deferred to the eager-load extension commit since both touch `AppointmentDto` mapping. | I |
| Appointment list filter for external users | OLD: stored proc filters by user (creator + accessor) | NEW: `AccessorIdentityUserId` filter exists | [IMPLEMENTED PRIOR] -- the existing `AppointmentsAppService.ComputeExternalPartyVisibilityAsync` (S-NEW-2 rev 2026-04-30) computes the visibility set automatically for external callers and short-circuits to `null` (= no narrowing) for internal callers. The list / count endpoints already pass the result into the repo, so the filter is automatic, not opt-in. The audit's "verify automatic" requirement is met. | B |
| View page uses ngModel | -- | Inconsistent | [DEFERRED PER PLAN] -- the plan explicitly tags this row as "Lower priority; can defer to follow-up" (line 610). UI-only; not strict parity. | C |
| Edit access via Accessor (`AccessTypeId=24`) | OLD: enables edit endpoints | NEW: TO VERIFY edit endpoints check accessor edit-access | [IMPLEMENTED 2026-05-04 - pending testing] (Phase 13a) -- pure predicate `AppointmentAccessRules.CanEdit(callerId, isInternal, creatorId, accessors)` available; View accessors return false, Edit accessors return true. Wiring into the existing edit endpoints (`UpdateAsync`, `UpsertApplicantAttorneyForAppointmentAsync`, `UpsertDefenseAttorneyForAppointmentAsync`, `DeleteAsync`) is a follow-up commit; the predicate is in place ready to be composed. | I |

## Internal dependencies surfaced

- **`AccessTypes` master data** -- IDs 23 and 24. Seed in NEW.
- **`Roles` master data** -- accessor.RoleId references this.
- **Stored proc `spAppointmentRequestList`** -- replaced by LINQ-to-EF in NEW.

## Branding/theming touchpoints

- View page UI (logo, primary color, layout).
- Field labels (localized).

## Replication notes

### ABP wiring

- **Access policy:** custom `IAppointmentAccessPolicy` interface with `CanReadAsync(Guid userId, Guid appointmentId)` + `CanEditAsync(...)`. Implementation joins `AppointmentAccessor` for the accessor path.
- **`GetAsync` + `GetWithNavigationPropertiesAsync`:** add policy check at AppService level using the access policy.
- **Eager-load extension:** in `EfCoreAppointmentRepository`, expand the LEFT JOINs to include all 7+ related entities. Watch for query plan complexity; may need a 2nd query for InjuryDetails sub-entities (mirroring OLD's pattern).
- **Confirmation # lookup:** add `IAppointmentRepository.FindByConfirmationNumberAsync(string)`.
- **`AccessType` enum or table:** seed View=23, Edit=24 to match OLD.

### Things NOT to port

- Stored proc `spAppointmentRequestList` -- LINQ-to-EF.
- ngModel in view page -- FormBuilder.
- `AccessorIdentityUserId` opt-in filter -- make it automatic for external users (creator OR accessor scoping).

### Verification (manual test plan)

1. Owner views own appointment -> success
2. Accessor with View access views appointment -> success, edit buttons hidden
3. Accessor with Edit access views appointment -> success, edit buttons visible
4. Non-owner non-accessor user tries to view -> 403
5. Look up by confirmation # -> success if creator/accessor
6. List view as external user -> only own + shared appointments visible
7. List view as internal user -> all appointments in tenant
8. View page shows full intake form data (all sub-entities)
9. Internal-only fields hidden from external users
