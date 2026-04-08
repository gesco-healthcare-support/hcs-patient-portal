<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md on 2026-04-03 -->

# Doctor Availabilities — API

> Synced from feature CLAUDE.md. Update code-derived content there.

## Endpoints

Controller: `DoctorAvailabilityController` at `api/app/doctor-availabilities`
Attributes: `[RemoteService]`, `[Area("app")]`, `[Route("api/app/doctor-availabilities")]`
Delegates all methods to `IDoctorAvailabilitiesAppService`.

| Method | Route | Purpose | Auth |
|---|---|---|---|
| GET | `/api/app/doctor-availabilities` | Paged list with nav props (filters: date range, time range, bookingStatus, locationId, appointmentTypeId) | `[Authorize]` |
| GET | `/api/app/doctor-availabilities/{id}` | Single availability slot | `DoctorAvailabilities.Default` |
| GET | `.../with-navigation-properties/{id}` | Single with Location + AppointmentType | `DoctorAvailabilities.Default` |
| POST | `/api/app/doctor-availabilities` | Create single slot | `DoctorAvailabilities.Create` |
| PUT | `/api/app/doctor-availabilities/{id}` | Update slot (including BookingStatus) | `DoctorAvailabilities.Edit` |
| DELETE | `/api/app/doctor-availabilities/{id}` | Delete single slot by ID | `DoctorAvailabilities.Delete` |
| DELETE | `.../by-slot` | Delete by location + date + time range (query params) | `DoctorAvailabilities.Delete` |
| DELETE | `.../by-date` | Delete all slots for location + date (query params) | `DoctorAvailabilities.Delete` |
| GET | `.../location-lookup` | Location dropdown (searches by Name) | `DoctorAvailabilities.Default` |
| GET | `.../appointment-type-lookup` | AppointmentType dropdown (searches by Name) | `DoctorAvailabilities.Default` |
| POST | `.../preview` | Generate slot preview with conflict detection | `DoctorAvailabilities.Default` |

## DTOs

| DTO | Purpose | Notable fields |
|---|---|---|
| `DoctorAvailabilityCreateDto` | Single slot creation | AvailableDate, FromTime, ToTime, BookingStatusId, LocationId, AppointmentTypeId |
| `DoctorAvailabilityUpdateDto` | Update slot | Implements `IHasConcurrencyStamp`. Same fields as create + ConcurrencyStamp |
| `DoctorAvailabilityDto` | Full output | Extends `FullAuditedEntityDto<Guid>`. All fields + concurrency stamp |
| `DoctorAvailabilityWithNavigationPropertiesDto` | Rich output | Includes LocationDto + AppointmentTypeDto (optional) |
| `GetDoctorAvailabilitiesInput` | Filter/paging | Date range (min/max), time range (min/max), BookingStatusId, LocationId, AppointmentTypeId |
| `DoctorAvailabilityGenerateInputDto` | Bulk generation input | FromDate, ToDate, FromTime, ToTime, BookingStatusId, LocationId, AppointmentTypeId, AppointmentDurationMinutes (default: 15) |
| `DoctorAvailabilitySlotPreviewDto` | Single preview slot | All slot fields + TimeId + IsConflict flag |
| `DoctorAvailabilitySlotsPreviewDto` | Preview response | Dates, Days, MonthId, LocationName, Time, SameTimeValidation, DoctorAvailabilities list |
| `DoctorAvailabilityDeleteByDateInputDto` | Bulk delete by date | LocationId + AvailableDate |
| `DoctorAvailabilityDeleteBySlotInputDto` | Delete by slot | LocationId + AvailableDate + FromTime + ToTime |

## Repository

`EfCoreDoctorAvailabilityRepository` extends `EfCoreRepository<CaseEvaluationDbContext, DoctorAvailability, Guid>`

- 2-way LEFT JOIN: Location + AppointmentType
- Filters: date range (min/max), time range (min/max), BookingStatusId, LocationId, AppointmentTypeId
- Text filter searches: no text fields on entity (date/time only)

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
