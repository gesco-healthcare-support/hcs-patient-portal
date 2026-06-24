# DoctorAvailabilities -- time slots publishing doctor availability for IME appointments

Slots are generated in bulk (date range or weekday pattern) or one-at-a-time, consumed by
appointment booking, and surfaced in Angular grouped by location + date with conflict preview.

## What lives here

| Layer | Key file | Purpose |
|---|---|---|
| Domain.Shared | `Domain.Shared/Enums/BookingStatus.cs` | `Available=8`, `Booked=9`, `Reserved=10` |
| Domain.Shared | `Domain.Shared/DoctorAvailabilities/DoctorAvailabilityConsts.cs` | Default sort `AvailableDate asc` |
| Domain | `Domain/DoctorAvailabilities/DoctorAvailability.cs` | Aggregate root -- date + time slot, `BookingStatusId`, `LocationId` (req), `AppointmentTypeId` (opt) |
| Domain | `Domain/DoctorAvailabilities/DoctorAvailabilityManager.cs` | Thin create/update over repo; no overlap or state validation |
| Domain | `Domain/DoctorAvailabilities/IDoctorAvailabilityRepository.cs` | Custom repo -- nav-prop list/get, range filters on date/time/status/location/type |
| Contracts | `Application.Contracts/DoctorAvailabilities/` | DTOs + `IDoctorAvailabilitiesAppService` (CRUD, bulk-generate preview, three delete modes) |
| Application | `Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` | CRUD + preview + three delete modes; `[RemoteService(IsEnabled=false)]` |
| EF Core | `EntityFrameworkCore/DoctorAvailabilities/EfCoreDoctorAvailabilityRepository.cs` | LEFT JOIN Location + AppointmentType; `filterText` arm is a no-op (`e => true`) |
| HttpApi | `HttpApi/Controllers/DoctorAvailabilities/DoctorAvailabilityController.cs` | Manual controller `api/app/doctor-availabilities`, 11 routes |
| Tests | `Application.Tests/DoctorAvailabilities/DoctorAvailabilitiesAppServiceTests.cs` | Active facts + 2 Skip gap-encoders (see Gotchas #2/#3) |
| Angular | `angular/src/app/doctor-availabilities/` | List + detail modal + bulk-generate + abstract/concrete view-services |

## Capacity model

`DoctorAvailability` carries `Capacity` (default 3). A slot stays `BookingStatus.Available`
after booking; fullness is `active-appointment-count >= Capacity`. Terminal statuses
(`Rejected`, `CancelledNoBill`, `CancelledLate`, `RescheduledNoBill`, `RescheduledLate`)
do not count toward active-appointment-count. `BookingStatus.Booked` is a legacy value;
the new model ignores it for capacity purposes.

## BookingStatus write paths

- `Available -> Booked`: `AppointmentsAppService` sets it when an appointment is created
  (legacy path; new model uses capacity count instead).
- `Available -> Reserved`: `AppointmentChangeRequestManager.SubmitRescheduleAsync` sets
  `newSlot.BookingStatusId = BookingStatus.Reserved` as an interim hold on the new slot
  pending supervisor approval.
- `UpdateAsync` can overwrite `BookingStatusId` to any value with no guard -- including
  flipping `Booked -> Available` while an active `Appointment` still references the slot
  (tracked as a Skip-tagged gap test; see Gotchas #2).
- No automated path releases a slot back to `Available` when an appointment is deleted.

## Bulk-preview and conflict detection

`GeneratePreviewAsync` is stateless and read-only. It generates consecutive non-overlapping
slots of `AppointmentDurationMinutes` (default 15) starting at `FromTime`, stopping when
`currentTime + duration > ToTime`. Overlap detection is scoped to `LocationId`:
- Overlap with `Booked` or `Reserved`: `IsConflict=true`, "Time slot is already booked or
  reserved at this location."
- Overlap with `Available`: `IsConflict=true`, "Time slot already exists at this location."
Both messages write to `previewList[0].SameTimeValidation` (sequential, not aggregated --
if both fire, only the second survives). The Angular UI submits non-conflicting slots one
at a time via `CreateAsync`; preview and create are not atomic.

## Bulk-delete modes

Three modes, all requiring `DoctorAvailabilities.Delete`:
1. `DeleteAsync` -- single id.
2. `DeleteBySlotAsync` -- location + date + exact `FromTime`/`ToTime`.
3. `DeleteByDateAsync` -- location + date, all slots that day.

## CreateRangeAsync transactional bulk-create

The repository exposes `CreateRangeAsync` for inserting multiple slots in a single
transaction. The AppService uses it in the bulk-generate submit path rather than calling
`CreateAsync` in a loop, avoiding partial-insert failures on network interruption.

## filterText no-op

Both `ApplyFilter` overloads in the EF repo include
`WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true)` -- the predicate is always
true, so `filterText` does nothing even though it flows through DTO / AppService / repo.

## Conventions

- `DoctorAvailabilityCreateDto.BookingStatusId` defaults to
  `Enum.GetValues<BookingStatus>()[0]` = `Available=8`. If enum order changes the default
  silently shifts -- do not reorder the enum.
- Lookups (`GetLocationLookupAsync`, `GetAppointmentTypeLookupAsync`) filter only by name;
  they do NOT scope to the doctor or to pre-existing slots.
- `GetListAsync` requires only `[Authorize]` (any authenticated user). All other read
  methods require `DoctorAvailabilities.Default`. Likely a code-gen miss.
- Angular groups API results by location + date in the browser-side abstract service; there
  is no server-side grouping endpoint.

## Gotchas

1. `filterText` is a no-op in the EF repo (see Conventions above).
2. `UpdateAsync` can flip `Booked -> Available` with no guard while an Appointment still
   references the slot. Pinned as
   `UpdateAsync_ChangeBookedStatusBackToAvailable_WhenSlotStillBooked_ShouldThrow` (Skip).
3. Preview + create is not atomic; two concurrent callers can both see no conflict and both
   persist duplicate overlapping slots. Pinned as
   `GeneratePreviewAsync_ConcurrentCalls_PreventDuplicateSlotCreation` (Skip).
4. `Reserved` has one automated writer (`AppointmentChangeRequestManager.SubmitRescheduleAsync`)
   but no automated release path; supervisor approval must manually flip it.

## Related

- docs/business-domain/DOCTOR-AVAILABILITY.md
- docs/business-domain/APPOINTMENT-LIFECYCLE.md

<!-- MANUAL:START -->
<!-- MANUAL:END -->
