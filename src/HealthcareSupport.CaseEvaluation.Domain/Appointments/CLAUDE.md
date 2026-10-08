# Appointments -- aggregate root and domain service for IME scheduling

Links a Patient to a Doctor via a time-slotted DoctorAvailability at a Location.
Used by admin staff (CRUD), bookers (full booking flow), and accessor-scoped attorneys
(filtered list via `AccessorIdentityUserId`).

## What lives here

| File | Purpose |
|---|---|
| `Appointment.cs` | Aggregate root: required FKs to Patient, AppointmentType, Location and DoctorAvailability (`IdentityUserId` is optional -- a booking can have no login), `AppointmentStatusType`, `IMultiTenant`, reschedule-chain link |
| `AppointmentManager.cs` | DomainService -- Create/Update + Stateless state machine (`ApplyTransitionAsync`) |
| `AppointmentWithNavigationProperties.cs` | POCO projection wrapper for eager-loaded queries |
| `IAppointmentRepository.cs` | Custom repo interface: navigation-property reads, accessor-scoped filtering, status counts, and the per-slot active-count queries the capacity gate uses |
| `AppointmentConsts.cs` (Domain.Shared) | Max lengths: PanelNumber=50, RequestConfirmationNumber=50, InternalUserComments=250 |
| `AppointmentStatusType.cs` (Domain.Shared) | Lifecycle enum (Pending=1 ... CancellationRequested=13, InfoRequested=14, NotSeen=15) |

## Entity shape

See `Appointment.cs` for all fields. Key structural facts:

- All 5 required FKs (`PatientId`, `IdentityUserId`, `AppointmentTypeId`, `LocationId`,
  `DoctorAvailabilityId`) are `OnDelete: NoAction`.
- `RequestConfirmationNumber` is auto-generated as `"A#####"`; client value is ignored.
- Tenant isolation is via ABP's automatic `IMultiTenant` data filter; no manual
  `WHERE TenantId = ...` in the repository.
- Inbound FKs (FK lives on the other entity, all tenant-scoped, `NoAction`):
  `AppointmentEmployerDetail`, `AppointmentAccessor`, `AppointmentApplicantAttorney`.

## State machine

Never set `Appointment.AppointmentStatus` directly; always use
`AppointmentManager.ApplyTransitionAsync`. See the Domain layer CLAUDE.md for the full
transition diagram and rule.

## Business rules

1. **Confirmation number auto-generated; client value ignored.** Allocated by
   `Application/Appointments/RequestConfirmationNumberGenerator.cs` (extracted 2026-08-05; booking
   and reschedule finalize both use it): finds the max `A#####` row with the soft-delete filter OFF,
   so a deleted appointment's number is never reused, and returns `"A" + next:D5`. Overflow at
   99999 throws `UserFriendlyException`. Unique index
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

8. **Create/Update are permission-gated at the API.** `CreateAsync` (and
   `ReSubmitAsync`/`CreateRevalAsync`) require `Appointments.Create`; `UpdateAsync`
   requires `Appointments.Edit` (internal-staff only -- external parties edit via
   change-requests, matching OLD). Enforced by ABP's authorization interceptor on the
   `[Authorize(permission)]` attributes; guarded by
   `AppointmentsAppServiceAuthorizationTests` (reflection, since the SQLite harness
   does not seed role->permission grants for behavioral denial).

## Gotchas

1. **`view/:id` route has only `authGuard` (no `permissionGuard`), but the server
   enforces party-scoping.** `GetWithNavigationPropertiesAsync` calls `EnsureCanReadAsync`
   (7-pathway access guard: internal user / creator / patient / AA / DA / CE / accessor),
   so a deep-link to a non-party appointment is rejected server-side. The thin Angular
   guard is intentional -- the API is the authority.

2. **Three parallel form patterns in Angular.** Modal (FormBuilder reactive), Add page
   (FormBuilder reactive), View page (plain ngModel). The ngModel form is the divergence
   risk; reactive validation and async slot lookup do not apply to it.

3. **Remove the `'Date check:'` debug log in `appointment-add.component.ts`** (date-validation
   path) before release.

4. **Proxy `getList` sends 18 query params; `GetAppointmentsInput` exposes 7.** The server
   ignores the extras. The proxy was generated against a richer input shape and is out of sync.
   Do not add server-side handling for the extras without re-evaluating the input DTO first.

5. **`AppointmentsAppService` carries `[RemoteService(IsEnabled = false)]`.** HTTP surface is
   the manual `AppointmentController` only; ABP auto-API is disabled for this service.

## Angular UI surface (summary)

Five components: list page, abstract list directive, detail modal, full-page Add
(`/appointments/add`, standalone), and View page (`/appointments/view/:id`, standalone
ngModel). Routes registered in `appointment-routes.ts`. Auto-generated proxy at
`angular/src/app/proxy/appointments/`.

## Other types in this folder

Access and parties:

- `AppointmentAccessRules` -- the pure view / edit access predicates behind the read-access
  guard; `AccessPathway` names which of the seven pathways granted access, and `AccessorEntry`
  is the lightweight accessor projection the rules consume.
- `ExternalCoPartyRules` -- the pure transform behind the co-party-scoped external user lookup;
  `AppointmentParties` is one appointment's four party-email columns and `CoParty` one named
  party with its role.

Lifecycle and booking:

- `AppointmentTransitionTrigger` -- the actions fed into the status state machine.
- `AppointmentLifecycleValidators` -- pure predicates for the resubmit and re-evaluation booking
  flows; `AppointmentLifecycleFlow` is the discriminator passed to them.
- `EvaluationKindPolicy` -- decides an appointment's evaluation kind from its booking flow.
- `IAppointmentChildCascadeCopier` -- copies every child row of one appointment onto another when
  a reschedule is finalized; `CopiedGroupCounts` reports rows copied per child group.

Snapshots and projections:

- `AppointmentPatientSnapshot` / `AppointmentPatientSnapshotResolver` -- the patient values as
  booked, and which values a record-side reader should report.
- `AttorneySnapshot` -- an attorney master's displayed fields copied onto the appointment at
  booking time.
- `ActiveSlotAppointment` -- a non-terminal appointment occupying a slot, projected for the staff
  schedule.

## Related

- docs/business-domain/APPOINTMENT-LIFECYCLE.md
- src/HealthcareSupport.CaseEvaluation.Domain/CLAUDE.md (state machine, capacity model)

<!-- MANUAL:START -->

## Dead states -- do not treat as live (PF-005)

`CheckedIn` (9), `CheckedOut` (10) and `Billed` (11) are **dead states**. Their
transitions, email dispatch and status-pill mapping were removed from the code;
only the enum values (stored as integers), the status labels, the localization
keys and the notification template rows remain, as deferred data migrations. No
appointment can reach them. They are OLD's front-desk day-of-exam flow, carried over but
never wired up; Case Tracker owns attendance and billing now. Do **not** build on
them, wire them up, or assume they are reachable when adding or changing lifecycle
code. Flagged, not deleted, pending a keep-vs-remove decision from Adrian.

`NoShow` (4) and `NotSeen` (15) are the opposite -- **live but inbound-only**: the
Case Tracker records them and pushes them in (`CaseTrackerAttendanceService`), so
the portal never originates them but does reach and store them. Do not flag these
as dead.

Full evidence and the file-by-file reference list: `docs/parity/_parity-flags.md`
PF-005.

<!-- MANUAL:END -->
