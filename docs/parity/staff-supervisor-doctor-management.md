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
re-verified: 2026-05-03
status: in-progress
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

> **Phase 7 re-verification (2026-05-03):** The earlier draft assumed
> several gaps existed that turn out to already be implemented. Notably,
> NEW's `GeneratePreviewAsync` already covers OLD's 4 conflict checks
> with a cleaner single-predicate overlap query, and `BookingStatus`
> already has all three values. The actual gaps (booking-flow lookup,
> validation guards on update / bulk-delete / single-delete, missing
> `DoctorPreferredLocation` entity) are tracked below with implementation
> annotations. See `_slot-generation-deep-dive.md` for the verbatim OLD
> behavior reverse-engineered from `DoctorsAvailabilityDomain.cs`.

| Aspect | OLD | NEW | Action | Sev | Status |
|--------|-----|-----|--------|-----|--------|
| Slot bulk generation | OLD: `Add(DoctorsAvailability[])` with `GenerateDoctorsAvailabilityByDays` | NEW: `GeneratePreviewAsync` (line 159 of AppService) | -- | B | [IMPLEMENTED 2026-04-29 - via prior W2 work] -- preview-then-confirm UX matches OLD; per-day Math.Floor slot subdivision matches OLD line 310. |
| Slot overlap validation | OLD has 4 checks (DoctorsAvailabilityDomain.cs:46-99) | NEW: single predicate (`x.FromTime < new.ToTime && x.ToTime > new.FromTime`) | -- | B | [IMPLEMENTED 2026-04-29 - via prior W2 work; OLD-BUG-FIX] -- Cleaner functionally-equivalent overlap detection. NEW also fixes OLD's cross-location-vs-same-location inconsistency (audit deep-dive lines 408 vs 59); NEW always scopes by Location. |
| Slot update guard | OLD: only Available is mutable (DoctorsAvailabilityDomain.cs:126-130) | NEW: ADDED 2026-05-03 | **Add status check** in update path | I | [IMPLEMENTED 2026-05-03 - pending testing] -- `DoctorAvailabilitiesAppService.UpdateAsync` reads existing entity; throws `DoctorAvailabilityCannotUpdateBookedOrReserved` if Reserved/Booked. |
| Bulk delete by date+location | OLD: id=0 + dates + locationId (DoctorsAvailabilityDomain.cs:143-150) | NEW: ADDED 2026-05-03 | **Add booked/reserved guard** | I | [IMPLEMENTED 2026-05-03 - pending testing] -- `DeleteByDateAsync` queries the date+location set; throws `DoctorAvailabilityCannotBulkDeleteWithBookedSlots` if any slot has in-flight status. |
| Single delete reference guard | OLD: rejects if vAppointment / vAppointmentChangeRequestRecord references it (DoctorsAvailabilityDomain.cs:151-154) | NEW: ADDED 2026-05-03 | **Add reference check** | I | [IMPLEMENTED 2026-05-03 - pending testing] -- `DeleteAsync` checks both `IRepository<Appointment>.AnyAsync(DoctorAvailabilityId)` and `IRepository<AppointmentChangeRequest>.AnyAsync(NewDoctorAvailabilityId)`; throws `DoctorAvailabilityCannotDeleteReferenced` if either matches. |
| `BookingStatusId` enum: Available / Reserved / Booked | OLD has all 3 | NEW: present (Available=8, Booked=9, Reserved=10) | -- | B | [IMPLEMENTED prior - via Phase 1.7] -- enum values match OLD verbatim. |
| `AppointmentTypeId` nullable on slot (any-type slot) | OLD field | NEW: present (`Guid?`) | -- | I | [IMPLEMENTED prior - via Phase 1.7] |
| Doctor preferred locations table + toggle UI | OLD entity + UI | NEW: missing | **Add `DoctorPreferredLocation` entity + AppService Toggle** | I | [DESCOPED 2026-05-03 - Phase 7b follow-up] -- entity not yet present. Booking-form Location filter via existing `GetLocationLookupAsync`; per-doctor scoping deferred. |
| Doctor appointment-types table + toggle UI | OLD entity + UI | NEW: entity exists (`DoctorAppointmentType`) but no service surface | **Add AppService Toggle endpoints** | I | [DESCOPED 2026-05-03 - Phase 7b follow-up] -- entity present at `Domain/Doctors/DoctorAppointmentType.cs`. Service-side toggle endpoint deferred. |
| Slot picker filter: by location + type + Available | OLD: stored proc `spm.spDoctorsAvailabilitiesLookups` | NEW: ADDED 2026-05-03 | **Add `GetDoctorAvailabilityLookupAsync`** | B | [IMPLEMENTED 2026-05-03 - pending testing] -- new method on `IDoctorAvailabilitiesAppService`; filter: LocationId required, AppointmentTypeId optional (loose-or-strict per OLD), `BookingStatus.Available`, AvailableDate >= today + `SystemParameter.AppointmentLeadTime`. Open to any authenticated user (booking form needs read access). |
| Read endpoint authorization | -- | NEW: `GetListAsync` had only `[Authorize]` (any authenticated) | **Tighten to `.Default`** | I | [IMPLEMENTED 2026-05-03 - pending testing] -- now `[Authorize(DoctorAvailabilities.Default)]` matching all other read endpoints. |
| Permissions: ManageAvailability / BulkGenerate / BulkDelete | -- | NEW: standard CRUD only | **Add fine-grained gates** | I | [DESCOPED 2026-05-03 - Phase 7b follow-up] -- existing Edit / Delete keys cover all current use cases. Splitting into bulk-specific gates is incremental and not blocking. |
| Soft delete via `StatusId` | OLD | NEW: ABP `ISoftDelete` | None -- use ABP's | -- | [IMPLEMENTED prior] |
| Pure helpers extracted for unit testing | -- | -- | **Extract `HasInFlightStatus`, `ComputeNumberOfSlotsPerDay`, `IsValidSlotTimeRange`, `IsValidSlotDateRange`** | C | [IMPLEMENTED 2026-05-03 - pending testing] -- internal-static helpers added; 16 pure unit tests cover boundary cases (zero / negative / inverted / boundary times). |

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
