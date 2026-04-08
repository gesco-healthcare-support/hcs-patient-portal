<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md on 2026-04-03 -->

# Appointments — API

> Synced from feature CLAUDE.md. Update code-derived content there.

## Endpoints

Controller: `AppointmentController` at `api/app/appointments`
Attributes: `[RemoteService]`, `[Area("app")]`, `[Route("api/app/appointments")]`
Delegates all methods to `IAppointmentsAppService`.

| Method | Route | Purpose | Auth |
|---|---|---|---|
| GET | `/api/app/appointments` | Paged list with nav props (filters: panelNumber, date range, userId, accessorUserId, typeId, locationId) | `[Authorize]` |
| GET | `/api/app/appointments/{id}` | Single appointment | `Appointments.Default` |
| GET | `/api/app/appointments/with-navigation-properties/{id}` | Single with all related entities | `[Authorize]` |
| POST | `/api/app/appointments` | Create — validates slot availability, generates "A#####" confirmation, marks slot Booked | `[Authorize]` |
| PUT | `/api/app/appointments/{id}` | Update — delegates to AppointmentManager (does NOT re-validate slot) | `[Authorize]` |
| DELETE | `/api/app/appointments/{id}` | Delete (does NOT release slot back to Available) | `Appointments.Delete` |
| GET | `.../patient-lookup` | Patient dropdown data | `[Authorize]` |
| GET | `.../identity-user-lookup` | User dropdown data | `Appointments.Default` |
| GET | `.../appointment-type-lookup` | Types filtered through Doctor's assigned types | `[Authorize]` |
| GET | `.../location-lookup` | Locations filtered through Doctor's assigned locations | `[Authorize]` |
| GET | `.../doctor-availability-lookup` | Available time slots | `[Authorize]` |
| GET | `.../applicant-attorney-details-for-booking?identityUserId=&email=` | Resolve attorney by userId or email | `[Authorize]` |
| GET | `.../{ appointmentId}/applicant-attorney` | Get linked attorney for appointment | `[Authorize]` |
| POST | `.../{appointmentId}/applicant-attorney` | Create/update attorney link for appointment | `[Authorize]` |

## DTOs

| DTO | Purpose | Notable fields |
|---|---|---|
| `AppointmentCreateDto` | Creation input | All FKs required. `RequestConfirmationNumber` accepted but overridden server-side |
| `AppointmentUpdateDto` | Update input | Implements `IHasConcurrencyStamp`. No status, no comments, no approve date |
| `AppointmentDto` | Full output | Extends `FullAuditedEntityDto<Guid>`. All entity fields + concurrency stamp |
| `AppointmentWithNavigationPropertiesDto` | Rich output | Includes PatientDto, IdentityUserDto, AppointmentTypeDto, LocationDto, DoctorAvailabilityDto, attorney nav props |
| `GetAppointmentsInput` | Filter/paging | `AccessorIdentityUserId` — filters via AppointmentAccessor for attorney-scoped access |
| `ApplicantAttorneyDetailsDto` | Attorney upsert | Used for both read and write in the attorney booking flow |

## AppService Business Logic

`AppointmentsAppService` — `[RemoteService(IsEnabled = false)]`, `[Authorize]`

**CreateAsync flow:**
1. Validate all 5 FK references exist (Patient, User, Type, Location, Availability)
2. Validate DoctorAvailability has `BookingStatus.Available`
3. Validate location matches, type matches (if set), date matches, time within slot range
4. Generate next `RequestConfirmationNumber` ("A" + 5-digit sequential, max A99999)
5. Create appointment via `AppointmentManager.CreateAsync`
6. Mark `DoctorAvailability.BookingStatusId = Booked`

**UpdateAsync:** Delegates to `AppointmentManager.UpdateAsync`. Does NOT re-validate slot or re-book. Does NOT update status, comments, or approve date.

**Lookup delegation:** Type and Location lookups query through `Doctor.AppointmentTypes` and `Doctor.Locations` join tables — only returns values assigned to at least one doctor, not all reference data.

## Repository

`EfCoreAppointmentRepository` extends `EfCoreRepository<CaseEvaluationDbContext, Appointment, Guid>`

- `GetListWithNavigationPropertiesAsync` — 5-way LEFT JOIN (Patient, IdentityUser, AppointmentType, Location, DoctorAvailability)
- `AccessorIdentityUserId` filter — subquery into `AppointmentAccessor` table, also matches `CreatorId`
- `GetWithNavigationPropertiesAsync` — separate fetch for `AppointmentApplicantAttorney` chain (attorney + attorney's IdentityUser)

## Mapper Configuration

Riok.Mapperly classes in `CaseEvaluationApplicationMappers.cs`:

| Class | Mapping | AfterMap |
|---|---|---|
| `AppointmentToAppointmentDtoMappers` | Entity → DTO | No |
| `AppointmentWithNavigationProperties...DtoMapper` | NavProps → NavPropsDto | No |
| `AppointmentToLookupDtoGuidMapper` | Entity → LookupDto | `DisplayName = RequestConfirmationNumber` |

- Back to overview: [overview.md](overview.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
