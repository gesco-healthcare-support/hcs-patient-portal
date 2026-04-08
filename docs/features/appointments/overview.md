<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md on 2026-04-08 -->

# Appointments

The core scheduling feature. Links patients to doctors via time-slotted availability at specific locations for workers' comp IME evaluations. Used by healthcare staff (admin CRUD), patients (self-service booking), and applicant attorneys (booking on behalf of clients).

## Entity Shape

```
Appointment : FullAuditedAggregateRoot<Guid>, IMultiTenant
├── TenantId          : Guid?              (tenant isolation)
├── PanelNumber       : string? [max 50]   (case panel reference)
├── AppointmentDate   : DateTime           (scheduled date)
├── RequestConfirmationNumber : string [max 50, required]  (auto-generated "A00001" format)
├── DueDate           : DateTime?          (report due date)
├── InternalUserComments : string? [max 250] (staff-only notes)
├── AppointmentApproveDate : DateTime?     (when approved)
├── IsPatientAlreadyExist  : bool          (patient pre-existed at booking time)
├── AppointmentStatus : AppointmentStatusType  (13-state lifecycle)
├── PatientId         : Guid               (FK → Patient)
├── IdentityUserId    : Guid               (FK → IdentityUser, the booking user)
├── AppointmentTypeId : Guid               (FK → AppointmentType)
├── LocationId        : Guid               (FK → Location)
└── DoctorAvailabilityId : Guid            (FK → DoctorAvailability slot)
```

## State Machine

13 states defined in `AppointmentStatusType` enum. No domain methods enforce valid transitions — any code can set any status directly.

```
Pending(1) → Approved(2) → CheckedIn(9) → CheckedOut(10) → Billed(11)
                ↘ Rejected(3)
                ↘ NoShow(4)
Pending → RescheduleRequested(12) → RescheduledNoBill(7) / RescheduledLate(8)
Pending → CancellationRequested(13) → CancelledNoBill(5) / CancelledLate(6)
```

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `PatientId` | Patient | NoAction | Required. Patient may or may not pre-exist |
| `IdentityUserId` | IdentityUser | NoAction | Required. The user who booked |
| `AppointmentTypeId` | AppointmentType | NoAction | Required. Host-scoped lookup |
| `LocationId` | Location | NoAction | Required. Host-scoped lookup |
| `DoctorAvailabilityId` | DoctorAvailability | NoAction | Required. Slot marked Booked on create |

**Related entities** (not FKs on Appointment, but linked to it):
- `AppointmentEmployerDetail` → has `AppointmentId` FK back to Appointment
- `AppointmentApplicantAttorney` → links Appointment to ApplicantAttorney
- `AppointmentAccessor` → grants View/Edit access to specific users (used for attorney-scoped filtering)

## Multi-tenancy

**IMultiTenant: Yes.** Appointment data is tenant-scoped — each tenant sees only its own appointments.

- DbContext config is **outside** `IsHostDatabase()` block — exists in both `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext`
- Reference data FKs (AppointmentType, Location) point to **host-scoped** entities
- `EfCoreAppointmentRepository` relies on ABP's automatic tenant filter — no manual `WHERE TenantId = X`
- `AccessorIdentityUserId` filter in the repository queries the `AppointmentAccessor` table (also tenant-scoped)

## Mapper Configuration

In `src/.../Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly):

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `AppointmentToAppointmentDtoMappers` | `Appointment` → `AppointmentDto` | No |
| `AppointmentWithNavigationPropertiesToAppointmentWithNavigationPropertiesDtoMapper` | `AppointmentWithNavigationProperties` → `AppointmentWithNavigationPropertiesDto` | No |
| `AppointmentToLookupDtoGuidMapper` | `Appointment` → `LookupDto<Guid>` | Yes — sets `DisplayName = source.RequestConfirmationNumber` |

All use `[Mapper]` attribute with `MapperBase<TSource, TDest>` inheritance.

## Permissions

Defined in `CaseEvaluationPermissions.cs`:
```
CaseEvaluation.Appointments          (Default — menu visibility + GetAsync)
CaseEvaluation.Appointments.Create   (New Appointment button on list page)
CaseEvaluation.Appointments.Edit     (Edit action on list page — but NOT checked in AppService.UpdateAsync)
CaseEvaluation.Appointments.Delete   (Delete action on list page + AppService.DeleteAsync)
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` as parent + 3 children.

**Angular usage:**
- List page: `*abpPermission="'CaseEvaluation.Appointments.Create'"` on button, `.Edit` and `.Delete` on row actions
- Menu: `requiredPolicy: 'CaseEvaluation.Appointments'` in base routes
- `appointment-add` and `appointment-view` pages: **no client-side permission checks** — relies on route-level `authGuard` only

**Gap:** The `Appointments.Edit` permission is checked on the Angular list UI but **never checked in the AppService** — `UpdateAsync` only requires `[Authorize]` (any authenticated user). Same for `CreateAsync`.

## Business Rules

1. **Slot validation on create** — AppService validates DoctorAvailability is `BookingStatus.Available`, location matches, type matches, date matches, time is within slot range
2. **Auto-generated confirmation number** — format "A" + 5-digit sequential (A00001), caps at A99999 with no overflow handling
3. **Slot booking is one-way** — creation marks slot as Booked, but deletion does NOT release it back to Available
4. **3-day minimum booking rule** — enforced in Angular `appointment-add.component` datepicker, not server-side
5. **Lookup filtering through Doctor** — appointment type and location lookups return only values assigned to doctors (via join tables), not all reference data
6. **Update omits key fields** — `AppointmentManager.UpdateAsync` does not update status, comments, approve date, or IsPatientAlreadyExist

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `AppointmentEmployerDetail.AppointmentId` | NoAction | No | One employer detail per appointment |
| `AppointmentAccessor.AppointmentId` | NoAction | No | Access grants for this appointment |
| `AppointmentApplicantAttorney.AppointmentId` | NoAction | No | Attorney associations for this appointment |

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| AppointmentComponent | `angular/src/app/appointments/appointment/components/appointment.component.ts` | `/appointments` | List view with status badges |
| AbstractAppointmentComponent | `angular/src/app/appointments/appointment/components/appointment.abstract.component.ts` | -- | Base directive with CRUD wiring |
| AppointmentDetailModalComponent | `angular/src/app/appointments/appointment/components/appointment-detail.component.ts` | -- | Quick-edit modal from list page |
| AppointmentAddComponent | `angular/src/app/appointments/appointment-add.component.ts` | `/appointments/add` | Full booking page (~30 form fields) |
| AppointmentViewComponent | `angular/src/app/appointments/appointment/components/appointment-view.component.ts` | `/appointments/view/:id` | View/edit page for existing appointment |

**Pattern:** Mixed -- abstract/concrete for list, standalone pages for add and view. Three different form approaches (FormBuilder modal, FormBuilder full-page, ngModel view page).

**Forms:**
- Add page: patientId, doctorAvailabilityId, appointmentTypeId, locationId, appointmentDate, panelNumber, dueDate, internalUserComments + employer detail + attorney fields
- View page: status, appointmentDate, requestConfirmationNumber, all fields from add (via ngModel)

**Permission guards:**
- List route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.Appointments`)
- View route: `authGuard` only (NO `permissionGuard` -- any authenticated user can view)
- `*abpPermission="'CaseEvaluation.Appointments.Edit'"` -- edit action in list
- `*abpPermission="'CaseEvaluation.Appointments.Delete'"` -- delete action in list

**Services injected:**
- `ListService`, `AppointmentViewService`, `AppointmentDetailViewService`, `PermissionService`, `AppointmentService` (proxy)

## Known Gotchas

1. **Two parallel UI flows exist and do NOT share form logic:**
   - Admin modal (`appointment-detail.component`) uses reactive `FormBuilder` for quick list-page CRUD
   - Full-page booking (`appointment-add.component`) uses reactive `FormBuilder` with ~30 controls
   - View/edit page (`appointment-view.component`) uses plain objects + `ngModel` — **inconsistent** with the other two

2. **`view/:id` route has no `permissionGuard`** — any authenticated user can view any appointment by navigating to `/appointments/view/{id}`. The list route has `permissionGuard`; the view route only has `authGuard`.

3. **`console.log` debug statement** left in `appointment-add.component.ts` (line ~1413) — date check logging in production code.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/Appointments/AppointmentConsts.cs` | Max lengths (PanelNumber=50, RequestConfirmationNumber=50, InternalUserComments=250), default sort |
| Domain.Shared | `src/.../Domain.Shared/Enums/AppointmentStatusType.cs` | 13-state lifecycle enum (Pending through Billed) |
| Domain | `src/.../Domain/Appointments/Appointment.cs` | Aggregate root entity — 5 FKs, status field, multi-tenant |
| Domain | `src/.../Domain/Appointments/AppointmentManager.cs` | DomainService — create/update with validation (does NOT set status, comments, or approve date) |
| Domain | `src/.../Domain/Appointments/AppointmentWithNavigationProperties.cs` | Projection wrapper for eager-loaded queries |
| Domain | `src/.../Domain/Appointments/IAppointmentRepository.cs` | Custom repo interface — nav-prop queries, accessor-based filtering |
| Contracts | `src/.../Application.Contracts/Appointments/AppointmentCreateDto.cs` | Creation input — client sends RequestConfirmationNumber but AppService overrides it |
| Contracts | `src/.../Application.Contracts/Appointments/AppointmentUpdateDto.cs` | Update input — no status, no comments, no approve date (those fields locked from update) |
| Contracts | `src/.../Application.Contracts/Appointments/AppointmentDto.cs` | Full output DTO with concurrency stamp |
| Contracts | `src/.../Application.Contracts/Appointments/AppointmentWithNavigationPropertiesDto.cs` | Rich output with Patient, User, Type, Location, Availability, Attorney nav props |
| Contracts | `src/.../Application.Contracts/Appointments/GetAppointmentsInput.cs` | Filter input — includes AccessorIdentityUserId for attorney-scoped queries |
| Contracts | `src/.../Application.Contracts/Appointments/ApplicantAttorneyDetailsDto.cs` | Attorney details used in booking upsert flow |
| Contracts | `src/.../Application.Contracts/Appointments/IAppointmentsAppService.cs` | Service interface — 12 methods including attorney upsert and lookup delegation |
| Application | `src/.../Application/Appointments/AppointmentsAppService.cs` | Business logic hub — slot validation, auto-generated "A#####" confirmation numbers, marks slots Booked, attorney upsert, permission-gated CRUD |
| EF Core | `src/.../EntityFrameworkCore/Appointments/EfCoreAppointmentRepository.cs` | 5-way LEFT JOIN query with AppointmentAccessor subquery for accessor filtering |
| HttpApi | `src/.../HttpApi/Controllers/Appointments/AppointmentController.cs` | Manual controller (14 endpoints) at `api/app/appointments` — delegates to AppService |
| Angular | `angular/src/app/appointments/appointment-add.component.ts` | Full-page booking form — patient demographics, employer, attorney, authorized users, slot picker with 3-day minimum |
| Angular | `angular/src/app/appointments/appointment/components/appointment.component.ts` | List page — extends AbstractAppointmentComponent, ngx-datatable grid with advanced filters |
| Angular | `angular/src/app/appointments/appointment/components/appointment.abstract.component.ts` | Shared list logic — CRUD delegation, permission checking for Edit/Delete action visibility |
| Angular | `angular/src/app/appointments/appointment/components/appointment-detail.component.ts` | Admin modal — quick create/edit from list page (separate from full-page booking form) |
| Angular | `angular/src/app/appointments/appointment/components/appointment-view.component.ts` | Single appointment view/edit — patient, employer, attorney, authorized users with ngModel forms |
| Angular | `angular/src/app/appointments/appointment/services/appointment.abstract.service.ts` | List data service — hookToQuery, delete with confirmation, filter management |
| Angular | `angular/src/app/appointments/appointment/services/appointment-detail.abstract.service.ts` | Modal form service — FormBuilder, lookup delegates, create/update submission |
| Angular | `angular/src/app/appointments/appointment/providers/appointment-base.routes.ts` | Menu config — icon, required policy `CaseEvaluation.Appointments` |
| Angular | `angular/src/app/appointments/appointment/providers/appointment-route.provider.ts` | Registers routes via ABP RoutesService at app init |
| Angular | `angular/src/app/appointments/appointment/appointment-routes.ts` | Lazy routes: `''` (list, permissionGuard) and `'view/:id'` (detail, authGuard only) |
| Proxy | `angular/src/app/proxy/appointments/appointment.service.ts` | Auto-generated REST client — 14 methods including file upload/download |
| Proxy | `angular/src/app/proxy/appointments/models.ts` | Auto-generated TypeScript interfaces mirroring backend DTOs |

## Related Features

- [Appointment Accessors](../appointment-accessors/overview.md) -- grants View/Edit access to specific users per appointment
- [Appointment Applicant Attorneys](../appointment-applicant-attorneys/overview.md) -- links appointments to attorneys
- [Appointment Employer Details](../appointment-employer-details/overview.md) -- employer info per appointment
- [Appointment Types](../appointment-types/overview.md) -- `AppointmentTypeId` FK references AppointmentType
- [Locations](../locations/overview.md) -- `LocationId` FK references Location
- [Doctor Availabilities](../doctor-availabilities/overview.md) -- `DoctorAvailabilityId` FK references slot
- [Patients](../patients/overview.md) -- `PatientId` FK references Patient
- [States](../states/overview.md) -- used indirectly via Patient and employer details

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)
- API detail: [api.md](api.md)
- UI detail: [ui.md](ui.md)
<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
