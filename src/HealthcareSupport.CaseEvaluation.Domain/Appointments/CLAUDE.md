# Appointments -- aggregate root and domain service for IME scheduling

Links a Patient to a Doctor via a time-slotted DoctorAvailability at a Location.
Used by admin staff (CRUD), bookers (full booking flow), and accessor-scoped attorneys
(filtered list via `AccessorIdentityUserId`).

## What lives here

| File | Purpose |
|---|---|
| `Appointment.cs` | Aggregate root: 5 required FKs, `AppointmentStatusType`, `IMultiTenant`, reschedule-chain link |
| `AppointmentManager.cs` | DomainService -- Create/Update + Stateless state machine (`ApplyTransitionAsync`) |
| `AppointmentWithNavigationProperties.cs` | POCO projection wrapper for eager-loaded queries |
| `IAppointmentRepository.cs` | Custom repo interface: 4 methods, includes accessor-scoped filtering |
| `AppointmentConsts.cs` (Domain.Shared) | Max lengths: PanelNumber=50, RequestConfirmationNumber=50, InternalUserComments=250 |
| `AppointmentStatusType.cs` (Domain.Shared) | 13-value lifecycle enum (Pending=1 ... CancellationRequested=13) |

## Entity shape

```
Appointment : FullAuditedAggregateRoot<Guid>, IMultiTenant
  TenantId                   : Guid?
  PanelNumber                : string? [max 50]
  AppointmentDate            : DateTime
  IsPatientAlreadyExist      : bool
  RequestConfirmationNumber  : string [max 50, required]   auto-generated "A#####"
  DueDate                    : DateTime?
  InternalUserComments       : string? [max 250]
  AppointmentApproveDate     : DateTime?
  AppointmentStatus          : AppointmentStatusType
  PatientId                  : Guid (required)  FK -> Patient
  IdentityUserId             : Guid (required)  FK -> IdentityUser (booking user)
  AppointmentTypeId          : Guid (required)  FK -> AppointmentType (host-scoped)
  LocationId                 : Guid (required)  FK -> Location (host-scoped)
  DoctorAvailabilityId       : Guid (required)  FK -> DoctorAvailability
```

All 5 required FKs are `OnDelete: NoAction`. Tenant isolation is via ABP's automatic
`IMultiTenant` data filter; no manual `WHERE TenantId = ...` in the repository.

Inbound FKs (FK lives on the other entity, all tenant-scoped, `NoAction`):
`AppointmentEmployerDetail`, `AppointmentAccessor`, `AppointmentApplicantAttorney`.

## State machine

IMPORTANT: NEVER set `Appointment.AppointmentStatus` directly. Use
`AppointmentManager.ApplyTransitionAsync` (called via `ApproveAsync`, `RejectAsync`,
`RequestRescheduleAsync`). Direct assignment bypasses the guard, skips
`AppointmentStatusChangedEto`, and leaves `AppointmentApproveDate` / `RejectionNotes` unset.
The layer CLAUDE.md has the full transition diagram.

## Business rules

1. **Confirmation number auto-generated; client value ignored.** `CreateAsync` calls
   `GenerateNextRequestConfirmationNumberAsync`: finds max `A#####` row, returns
   `"A" + next:D5`. Overflow at 99999 throws `UserFriendlyException`. Unique index
   `IX_AppEntity_Appointments_TenantId_RequestConfirmationNumber` + a 5-attempt
   `ConfirmationNumberRetryPolicy` closes the race on concurrent bookings.

2. **Five-step slot gate (CreateAsync).** `ValidateDoctorAvailabilityForBooking` checks:
   slot status must be `Available` (not `Reserved`), `LocationId` match, `AppointmentTypeId`
   match (if slot has a type set), `AvailableDate.Date` match, time in `[FromTime, ToTime)`.
   Capacity-aware: active-appointment-count >= `DoctorAvailability.Capacity` (default 3)
   blocks with `AppointmentBookingSlotFull`. The five terminal statuses (Rejected,
   CancelledNoBill, CancelledLate, RescheduledNoBill, RescheduledLate) do not count toward
   active count, so cancellation frees capacity automatically. Slot stays `Available` after
   booking; `BookingStatus.Booked` is a legacy value treated the same as `Available`.

3. **Five GUID-empty guards before FK lookups.** Both `CreateAsync` and `UpdateAsync` reject
   `Guid.Empty` for Patient, IdentityUser, AppointmentType, Location, DoctorAvailability
   with localized `UserFriendlyException` naming the field.

4. **Past-date bookings rejected at domain layer.** `AppointmentManager.EnsureAppointmentDateNotInPast`
   throws `BusinessException(AppointmentBookingDateInsideLeadTime)` on Create always and on
   Update only when the date is changing (so completed appointments with past dates can still
   be edited on other fields).

5. **Update freezes four fields.** `AppointmentManager.UpdateAsync` does not accept
   `AppointmentStatus`, `InternalUserComments`, `AppointmentApproveDate`, or
   `IsPatientAlreadyExist`. These are also absent from `AppointmentUpdateDto`. There is
   currently no code path that writes `InternalUserComments` or `IsPatientAlreadyExist`
   after creation.

6. **Lookup endpoints filter through Doctor relations.** `GetAppointmentTypeLookupAsync` and
   `GetLocationLookupAsync` return only entities assigned to a Doctor. `GetDoctorAvailabilityLookupAsync`
   returns all availabilities unfiltered.

7. **Attorney upsert is one-per-appointment.** `UpsertApplicantAttorneyForAppointmentAsync`
   creates or updates an `ApplicantAttorney` then upserts the single `AppointmentApplicantAttorney`
   link row (takes the first result of a `maxResultCount: 10` fetch).

8. **Permission gap on Create/Update at the API.** Both carry only `[Authorize]`; any
   authenticated user can call them. The Angular UI checks `Create`/`Edit` permissions.
   Tracked as `[Fact(Skip="KNOWN GAP: ...")]` in the test file.

## Gotchas

1. **`view/:id` route has no `permissionGuard`.** Only `authGuard`. The server's
   `GetWithNavigationPropertiesAsync` is also `[Authorize]` only, so any authenticated user
   in the same tenant can deep-link to any appointment by ID.

2. **Three parallel form patterns in Angular.** Modal (FormBuilder reactive), Add page
   (FormBuilder reactive, ~1,594 lines), View page (plain ngModel). The ngModel form is
   the divergence risk; reactive validation and async slot lookup do not apply to it.

3. **`console.log('Date check:', ...)` in `appointment-add.component.ts` line 1546.**
   Production-bound debug log in the date-validation path.

4. **Proxy `getList` sends 18 query params; `GetAppointmentsInput` exposes 7.** The server
   ignores the extras. The proxy was generated against a richer input shape and is out of sync.
   Do not add server-side handling for the extras without re-evaluating the input DTO first.

5. **`AppointmentsAppService` carries `[RemoteService(IsEnabled = false)]`.** HTTP surface is
   the manual `AppointmentController` only; ABP auto-API is disabled for this service.

## Angular UI surface (summary)

Five components: list page, abstract list directive, detail modal, full-page Add
(`/appointments/add`, ~1,594 lines, standalone), and View page (`/appointments/view/:id`,
~969 lines, standalone ngModel). Routes registered in `appointment-routes.ts`. Auto-generated
proxy at `angular/src/app/proxy/appointments/`.

## Related

- docs/business-domain/APPOINTMENT-LIFECYCLE.md
- src/HealthcareSupport.CaseEvaluation.Domain/CLAUDE.md (state machine, capacity model)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
