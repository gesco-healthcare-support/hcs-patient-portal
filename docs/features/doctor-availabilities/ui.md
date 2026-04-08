<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md on 2026-04-08 -->

# Doctor Availabilities — UI

> Synced from feature CLAUDE.md. Update code-derived content there.

## Angular Component Architecture

Three distinct UI flows for managing doctor availability slots:

### Flow 1: Grouped List View (`doctor-availability.component`)
- **Route:** `/doctor-management/doctor-availabilities`
- **Purpose:** View all availability slots grouped by location + date
- **Key behavior:**
  - Client-side grouping: `AbstractDoctorAvailabilityViewService.buildGroupedData()` groups flat API results into `DoctorAvailabilityGroupedRow` objects
  - Each group row shows: location name, date, counts by status (Available/Booked/Reserved), total slots
  - Expandable rows reveal individual time slots with delete buttons
  - Filters: date range, time range, booking status, location, appointment type
  - Delete group: deletes all slots for a location+date via `DeleteByDateAsync`
  - Delete slot: deletes individual slot by ID

### Flow 2: Detail Modal (`doctor-availability-detail.component`)
- **Purpose:** Create or edit a single availability slot
- **Form fields:** Available Date, From Time, To Time, Booking Status, Location (lookup), Appointment Type (lookup)
- **Services:** `DoctorAvailabilityDetailViewService` manages form state with `FormBuilder`
- **Time handling:** `normalizeTime()` and `normalizeDate()` helpers ensure correct format for API

### Flow 3: Bulk Generation (`doctor-availability-generate.component`)
- **Routes:** `/doctor-management/doctor-availabilities/generate` and `.../add`
- **Purpose:** Generate multiple availability slots at once
- **Two modes:**
  - **Dates mode:** Select a specific date range (from/to date)
  - **Weekdays mode:** Select a month + specific weekdays (Mon-Sun checkboxes), uses `buildWeekdayDates()` to compute actual dates
- **Form fields:** Location, date range or month+weekdays, from time, to time, appointment type, booking status, appointment duration minutes
- **Preview:** Calls `GeneratePreviewAsync` which returns slots with `IsConflict` flags
  - Conflicting slots shown with visual indicator
  - Users can remove individual slots from preview before submitting
  - Submit button disabled when conflicts exist (`canSubmit` computed property)
- **Submission:** Creates slots one-by-one via individual `CreateAsync` calls (not a batch endpoint)

## Routes

| Path | Component | Guards | Notes |
|---|---|---|---|
| `/doctor-management/doctor-availabilities` | `DoctorAvailabilityComponent` | `authGuard`, `permissionGuard` | Grouped list view |
| `.../generate` | `DoctorAvailabilityGenerateComponent` | (child route) | Bulk generation |
| `.../add` | `DoctorAvailabilityGenerateComponent` | (child route) | Same as generate |

## Services

| Service | Scope | Purpose |
|---|---|---|
| `DoctorAvailabilityViewService` | Component-provided | Grouped list data — groups by location+date, counts by BookingStatus |
| `DoctorAvailabilityDetailViewService` | Component-provided | Modal form state — time normalization, create/update |
| `DoctorAvailabilityService` (proxy) | Root singleton | Auto-generated REST client |

## Menu Configuration

- Path: `/doctor-management/doctor-availabilities`
- Parent menu: `::Menu:DoctorManagement`
- Icon: `fas fa-calendar-check`
- Required policy: `CaseEvaluation.DoctorAvailabilities`
- Order: 3

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
