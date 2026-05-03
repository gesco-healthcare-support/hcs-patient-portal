---
feature: staff-supervisor-doctor-management
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\DoctorsAvailabilityDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\DoctorPreferredLocationDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\DoctorsAppointmentTypeDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\DoctorDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\LocationDomain.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\DoctorManagementModule\AppointmentTypeDomain.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\doctor-management\
old-docs:
  - socal-project-overview.md (lines 529-539)
  - data-dictionary-table.md (Doctors, DoctorsAvailabilities, DoctorPreferredLocations, DoctorsAppointmentTypes, Locations, AppointmentTypes)
audited: 2026-05-01
status: audit-only
priority: 2
strict-parity: true
internal-user-role: StaffSupervisor (also ITAdmin per permission matrix)
depends-on: []
required-by:
  - external-user-appointment-request           # slot picker, location dropdown, appointment-type dropdown all read these
  - external-user-appointment-rescheduling      # new slot picker
---

# Staff Supervisor -- Doctor management (availability + locations + appointment types)

## Purpose

Staff Supervisor (and IT Admin) configure the **single doctor** managed by this OLD app instance:

- **Availability calendar:** generate time slots (date + from-time + to-time + location).
- **Location preferences:** which clinic locations the doctor accepts appointments at.
- **Appointment-type preferences:** which appointment types (PQME / AME / PQME-REVAL / AME-REVAL / OTHER) the doctor accepts.

These three sets of records drive the booking form's slot picker + location dropdown + appointment-type dropdown.

**Strict parity with OLD.** OLD is single-doctor-per-deploy; the Doctor entity exists but the user "doctor" role does NOT (cleanup task #7).

## OLD behavior (binding)

### Slot generation (`DoctorsAvailabilityDomain.Add`)

UI input: a date range + start time + end time + slot duration + location. Backend expands via `GenerateDoctorsAvailabilityByDays(...)` into individual `DoctorsAvailability` rows -- one per slot.

Per-row fields (per data dict):

- `DoctorsAvailabilityId` (PK)
- `DoctorId` (the single doctor)
- `AppointmentTypeId` (nullable -- if set, slot is limited to that type; if null, any type accepted)
- `AvailableDate` (datetime)
- `FromTime` (time)
- `ToTime` (time)
- `LocationId` (FK to `Locations`)
- `StatusId` (Active/Delete)
- `BookingStatusId` (Available / Reserved / Booked)

Validation (`AddValidation`):

1. **No overlap:** existing slot at same location + date with `FromTime > new.FromTime` AND `ToTime < new.ToTime` (i.e., contained-in) -> reject ("TimeSlotExists").
2. **No exact duplicate:** existing slot with same location + date + same FromTime + same ToTime + Available -> reject.
3. `TimeSlotValidation(timeSlot)` -- additional time-format/range checks (TO READ in lines 150+ of OLD).
4. **No conflict with Booked/Reserved:** existing slot with same location + date + same FromTime + same ToTime + Booked or Reserved -> reject ("TimeSlotBooked").

### Slot update (`DoctorsAvailabilityDomain.Update`)

- Cannot update a slot that's not Available (Booked/Reserved are immutable -- protects existing appointments).
- Same overlap + duplicate checks as Add.

### Slot delete

- Single-slot delete: standard.
- Bulk delete by date + location (`id=0`): rejects if ANY slot at that date+location is Booked or Reserved. Else marks all matching slots as `StatusId = Delete` (soft delete).

### Doctor preferred locations (`DoctorPreferredLocationDomain`)

Schema (`DoctorPreferredLocations` table): `DoctorPreferredLocationId, DoctorId, LocationId, StatusId`.

Staff Supervisor toggles which `Location` rows the doctor accepts appointments at. Booking form's location dropdown is filtered through this.

### Doctor appointment types (`DoctorsAppointmentTypeDomain`)

Schema (`DoctorsAppointmentTypes` table): `DoctorsAppointmentTypeId, DoctorId, AppointmentTypeId, StatusId`.

Staff Supervisor toggles which `AppointmentType` rows the doctor accepts. Booking form's appointment-type dropdown is filtered through this.

### Doctor master (`DoctorDomain`)

`Doctors` table: `DoctorId, FirstName, LastName, Email, GenderId`. ONE row per OLD deploy. Created at app provisioning.

### Critical OLD behaviors

- **Slots are bulk-generated.** Supervisor enters a range, system generates per-slot rows.
- **No backfill.** Slots must exist BEFORE external user can book.
- **Booking form lookups filter through these:**
  - Locations dropdown: only rows in `DoctorPreferredLocations`
  - Appointment-types dropdown: only rows in `DoctorsAppointmentTypes`
  - Slot picker: only `DoctorsAvailabilities` rows where `BookingStatusId = Available` AND `AppointmentTypeId IS NULL OR matches selected type` AND `LocationId = selected location`
- **Slot booking transitions:** Available -> Reserved (external book) -> Booked (clinic-staff approval). Released -> Available on cancel-approve / reschedule-reject.
- **No "doctor login":** Doctor is an entity, not a user. Per role decision (see `project_role-model.md`).

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/DoctorManagementModule/DoctorsAvailabilityDomain.cs` (475 lines) | Slot CRUD + bulk generation + overlap validation |
| `PatientAppointment.Domain/DoctorManagementModule/DoctorPreferredLocationDomain.cs` (155 lines) | Doctor-location mapping CRUD |
| `PatientAppointment.Domain/DoctorManagementModule/DoctorsAppointmentTypeDomain.cs` (149 lines) | Doctor-appointment-type mapping CRUD |
| `PatientAppointment.Domain/DoctorManagementModule/DoctorDomain.cs` | Doctor master CRUD |
| `PatientAppointment.Domain/DoctorManagementModule/LocationDomain.cs` | Location master CRUD (IT Admin) |
| `PatientAppointment.Domain/DoctorManagementModule/AppointmentTypeDomain.cs` | AppointmentType master CRUD (IT Admin) |
| `patientappointment-portal/.../doctor-management/...` | UI components |

## NEW current state

- NEW has `Doctor` entity at `src/HealthcareSupport.CaseEvaluation.Domain/Doctors/`.
- NEW has `DoctorAvailability` entity (singular -- match OLD's plural? TO VERIFY naming conventions in NEW).
- NEW has `DoctorPreferredLocation` entity (TO VERIFY).
- NEW has `AppointmentTypes/` folder with `AppointmentType` entity.
- NEW has `Locations/` folder.
- Per `Appointments/CLAUDE.md`: lookup endpoints in NEW already filter through Doctor relations:
  - `GetAppointmentTypeLookupAsync` -> only types assigned to a Doctor
  - `GetLocationLookupAsync` -> only locations assigned to a Doctor
  - `GetDoctorAvailabilityLookupAsync` -> ALL availabilities unfiltered (gap)

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Slot bulk generation | OLD: `Add(DoctorsAvailability[])` with `GenerateDoctorsAvailabilityByDays` | NEW: TO VERIFY | **Add `IDoctorAvailabilityManagement.GenerateSlotsAsync(GenerateSlotsDto { DateRange, FromTime, ToTime, SlotDurationMinutes, LocationId, AppointmentTypeId? })`** | B |
| Slot overlap validation | OLD has 4 checks | NEW: TO VERIFY | **Add overlap + duplicate + booked-conflict checks** | B |
| Slot update guard | OLD: only Available is mutable | NEW: TO VERIFY | **Add status check** in update path | I |
| Bulk delete by date+location | OLD: id=0 + dates + locationId | NEW: TO VERIFY | **Add `BulkDeleteByDateAsync(date, locationId)`** with booked/reserved guard | I |
| `BookingStatusId` enum: Available / Reserved / Booked | OLD has all 3 | NEW: ?  Per booking audit, "Reserved" is missing | **Add Reserved value** | B |
| `AppointmentTypeId` nullable on slot (any-type slot) | OLD field | NEW: TO VERIFY | **Add nullable `AppointmentTypeId Guid?`** | I |
| Doctor preferred locations table + toggle UI | OLD entity + UI | NEW: TO VERIFY full CRUD | **Verify; add if missing** | I |
| Doctor appointment-types table + toggle UI | OLD entity + UI | NEW: TO VERIFY | **Verify; add if missing** | I |
| Slot picker filter: by location + type + Available | OLD applied | NEW: known gap (`GetDoctorAvailabilityLookupAsync` returns all) | **Add filter** | B |
| Permissions | -- | NEW: TO VERIFY | **Add `CaseEvaluation.DoctorManagement.{Default, ManageAvailability, ManageLocations, ManageAppointmentTypes}` permissions; gate to Staff Supervisor + IT Admin** | I |
| Soft delete via `StatusId` | OLD | NEW: ABP `ISoftDelete` | None -- use ABP's | -- |

## Internal dependencies surfaced

- `Locations` master (IT Admin slice -- separate audit, but small, can fold here)
- `AppointmentTypes` master (IT Admin slice; verify all 5 types seeded incl OTHER)
- `WcabOffices` master (lookup-only; small)
- The role-cleanup task #7 -- ensure no Doctor-as-user-role artifacts remain after cleanup

## Branding/theming touchpoints

- Calendar UI for slot generation (color scheme, slot status indicators)
- Doctor profile page (logo + photo + name display)

## Replication notes

### ABP wiring

- **`DoctorAvailability` entity:** `DoctorId, AppointmentTypeId? Guid?, AvailableDate, FromTime, ToTime, LocationId Guid, BookingStatus enum, IsActive bool`. Tenant-scoped.
- **`BookingStatus` enum:** Available, Reserved, Booked. Strict parity. Add a transition method `SetStatus(BookingStatus target)` with state-machine guard.
- **Bulk slot generation:** `IDoctorAvailabilityManager.GenerateSlotsAsync` -- iterates date range, generates per-slot rows, batches into a single `InsertManyAsync`.
- **Overlap validation:** before insert, query existing slots for the date range + locations; reject on overlap.
- **Lookup endpoints:** filter through Doctor preferences. Per branch CLAUDE.md `Appointments/CLAUDE.md`, this is partially done; close the gap on `GetDoctorAvailabilityLookupAsync`.
- **Permissions:** `Default` (read), `ManageAvailability`, `ManageLocations`, `ManageAppointmentTypes` -- granted to Staff Supervisor + IT Admin.

### Things NOT to port

- Stored procs.
- `vDoctorsAvailabilityRecord` / `vSystemParameter` views -- direct entity queries.
- The OLD soft-delete-via-`StatusId` pattern -- use ABP's `ISoftDelete`.

### Verification (manual test plan)

1. Staff Supervisor opens availability page -> calendar shows existing slots
2. Staff Supervisor generates slots for date range + location + 8AM-5PM + 60-min duration -> N slots created
3. Try to overlap slots -> rejected
4. Try to delete a slot that's Booked -> rejected
5. Bulk delete by date+location with no booked/reserved -> all slots removed
6. Toggle a Location off -> external user no longer sees it in booking form
7. Toggle an AppointmentType off -> external user no longer sees it in booking form
8. External user books slot -> Available -> Reserved (Pending appointment)
9. Clinic Staff approves -> Reserved -> Booked
10. Cancel approved appointment -> Booked -> Available
