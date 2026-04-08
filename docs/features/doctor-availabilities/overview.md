<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md on 2026-04-08 -->

# Doctor Availabilities

Time-slot records representing when a doctor is available for IME appointments at a specific location. Staff generate slots in bulk (by date range or weekday pattern), then the appointment booking flow marks individual slots as Booked. The Angular UI groups slots by location + date and supports conflict detection during generation.

## Entity Shape

```
DoctorAvailability : FullAuditedAggregateRoot<Guid>, IMultiTenant
├── TenantId          : Guid?           (tenant isolation)
├── AvailableDate     : DateTime        (the date this slot is on)
├── FromTime          : TimeOnly        (slot start time)
├── ToTime            : TimeOnly        (slot end time)
├── BookingStatusId   : BookingStatus   (Available=8, Booked=9, Reserved=10)
├── LocationId        : Guid            (FK → Location, required)
└── AppointmentTypeId : Guid?           (FK → AppointmentType, optional)
```

**BookingStatus enum** (`Domain.Shared/Enums/BookingStatus.cs`):
```
Available(8) → Booked(9)    (set by AppointmentsAppService.CreateAsync when booking)
Available(8) → Reserved(10) (manual — no automated flow uses this)
```

**Warning:** Slots transition from Available to Booked when an Appointment is created, but deleting the Appointment does NOT release the slot back to Available. There is no reverse transition.

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `LocationId` | Location | NoAction | Required. Host-scoped lookup |
| `AppointmentTypeId` | AppointmentType | SetNull | Optional. Host-scoped lookup |

**Inbound FK:** `Appointment.DoctorAvailabilityId` → references this entity. When an appointment is booked, this slot's `BookingStatusId` is set to `Booked`.

## Multi-tenancy

**IMultiTenant: Yes.** Availability slots are tenant-scoped — each tenant (doctor practice) manages its own schedule.

- DbContext config is **outside** `IsHostDatabase()` block — exists in both contexts
- FK targets (Location, AppointmentType) are **host-scoped** entities
- ABP's automatic tenant filter applies

## Mapper Configuration

In `src/.../Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly):

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `DoctorAvailabilityToDoctorAvailabilityDtoMappers` | `DoctorAvailability` → `DoctorAvailabilityDto` | No |
| `DoctorAvailabilityWithNavigationPropertiesToDoctorAvailabilityWithNavigationPropertiesDtoMapper` | `DoctorAvailabilityWithNavigationProperties` → `DoctorAvailabilityWithNavigationPropertiesDto` | No |
| `DoctorAvailabilityToLookupDtoGuidMapper` | `DoctorAvailability` → `LookupDto<Guid>` | No AfterMap — DisplayName not set |

## Permissions

Defined in `CaseEvaluationPermissions.cs`:
```
CaseEvaluation.DoctorAvailabilities          (Default — list/get + lookups + generate preview)
CaseEvaluation.DoctorAvailabilities.Create   (CreateAsync)
CaseEvaluation.DoctorAvailabilities.Edit     (UpdateAsync)
CaseEvaluation.DoctorAvailabilities.Delete   (DeleteAsync, DeleteBySlotAsync, DeleteByDateAsync)
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` as parent + 3 children.

**Angular menu:** Under parent `::Menu:DoctorManagement`, path `/doctor-management/doctor-availabilities`, policy `CaseEvaluation.DoctorAvailabilities`.

## Business Rules

1. **Bulk slot generation** — `GeneratePreviewAsync` accepts a list of `DoctorAvailabilityGenerateInputDto` (date range, time range, duration in minutes, location, type) and returns a preview with `IsConflict` flags for slots that overlap existing ones. The preview does NOT persist — the Angular UI submits individual `CreateAsync` calls for non-conflicting slots.

2. **Three delete modes** — `DeleteAsync` (single by ID), `DeleteBySlotAsync` (by location + date + time range), `DeleteByDateAsync` (by location + date). All three require `DoctorAvailabilities.Delete` permission.

3. **AppointmentDurationMinutes defaults to 15** — in the generation DTO. Slots are split into consecutive time blocks of this duration within the from/to time range.

4. **Location and AppointmentType lookups** — `GetLocationLookupAsync` searches by `Location.Name`, `GetAppointmentTypeLookupAsync` searches by `AppointmentType.Name`. These return ALL matching records (not filtered through doctor assignments like the Appointments feature does).

5. **BookingStatus is mutable on update** — `UpdateAsync` accepts `BookingStatusId`, so staff can manually change a slot's status. No validation prevents changing a Booked slot back to Available (even if an appointment references it).

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `Appointment.DoctorAvailabilityId` | NoAction | No | Required FK — booking references this slot |

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| DoctorAvailabilityComponent | `angular/src/app/doctor-availabilities/.../doctor-availability.component.ts` | `/doctor-management/doctor-availabilities` | Grouped list (location+date) with expandable slot rows |
| AbstractDoctorAvailabilityComponent | `angular/src/app/doctor-availabilities/.../doctor-availability.abstract.component.ts` | — | Base directive — grouping, pagination, delete group/slot |
| DoctorAvailabilityDetailModalComponent | `angular/src/app/doctor-availabilities/.../doctor-availability-detail.component.ts` | — | Single-slot create/edit modal |
| DoctorAvailabilityGenerateComponent | `angular/src/app/doctor-availabilities/.../doctor-availability-generate.component.ts` | `/doctor-management/doctor-availabilities/generate` | Bulk generation form — dates/weekdays mode, preview with conflicts |

**Pattern:** ABP Suite abstract/concrete for list; standalone component for generation

**Forms:**
- Detail modal: availableDate (datepicker), fromTime/toTime (time input), bookingStatusId (select), locationId (lookup), appointmentTypeId (lookup)
- Generate form: slotMode (dates/weekdays radio), locationId (required), fromDate/toDate or month+weekday selectors, fromTime/toTime, appointmentDurationMinutes (default 15), bookingStatusId, appointmentTypeId

**Permission guards:**
- Route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.DoctorAvailabilities`)
- `*abpPermission="'CaseEvaluation.DoctorAvailabilities.Create'"` — create/generate buttons
- `*abpPermission="'CaseEvaluation.DoctorAvailabilities.Delete'"` — delete group/slot actions

**Services injected:**
- `ListService`, `DoctorAvailabilityViewService`, `DoctorAvailabilityDetailViewService`, `PermissionService`, `DoctorAvailabilityService` (proxy)

## Known Gotchas

1. **No domain manager validation** — `DoctorAvailabilityManager.CreateAsync` and `UpdateAsync` only construct/update the entity. No overlap checking, no date validation, no business rules in the domain layer. All slot logic is in the AppService and Angular UI.

2. **Generate preview is stateless** — `GeneratePreviewAsync` checks for conflicts but the actual creation happens slot-by-slot via separate `CreateAsync` calls from the Angular UI. Race conditions possible if two users generate for the same location/date simultaneously.

3. **Grouped UI is client-side** — the Angular abstract service groups API results by location+date after fetching a flat list. No server-side grouping endpoint exists. Large slot counts may cause performance issues.

4. **No tests** — no test files found for DoctorAvailabilities.

5. **Two generation modes in Angular** — "dates" mode (specific date range) and "weekdays" mode (select weekdays within a month). Both produce the same preview format but use different date calculation logic (`buildWeekdayDates`).

## Related Features

- [Locations](../locations/overview.md) — `LocationId` FK for exam location (host-scoped, required)
- [Appointment Types](../appointment-types/overview.md) — `AppointmentTypeId` FK for IME type (host-scoped, optional)
- [Appointments](../appointments/overview.md) — inbound FK `Appointment.DoctorAvailabilityId` references this slot when booking

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/DoctorAvailabilities/DoctorAvailabilityConsts.cs` | Default sort (CreationTime desc) |
| Domain.Shared | `src/.../Domain.Shared/Enums/BookingStatus.cs` | Enum: Available(8), Booked(9), Reserved(10) |
| Domain | `src/.../Domain/DoctorAvailabilities/DoctorAvailability.cs` | Aggregate root entity — date/time slot, booking status, 2 FKs, multi-tenant |
| Domain | `src/.../Domain/DoctorAvailabilities/DoctorAvailabilityManager.cs` | DomainService — create/update (no special validation beyond construction) |
| Domain | `src/.../Domain/DoctorAvailabilities/DoctorAvailabilityWithNavigationProperties.cs` | Projection wrapper — Location + AppointmentType nav props |
| Domain | `src/.../Domain/DoctorAvailabilities/IDoctorAvailabilityRepository.cs` | Custom repo interface — nav-prop queries, filter by date/time ranges, bookingStatus, locationId, appointmentTypeId |
| Contracts | `src/.../Application.Contracts/DoctorAvailabilities/DoctorAvailabilityCreateDto.cs` | Single slot creation input |
| Contracts | `src/.../Application.Contracts/DoctorAvailabilities/DoctorAvailabilityUpdateDto.cs` | Update input — implements IHasConcurrencyStamp |
| Contracts | `src/.../Application.Contracts/DoctorAvailabilities/DoctorAvailabilityDto.cs` | Full output DTO with concurrency stamp |
| Contracts | `src/.../Application.Contracts/DoctorAvailabilities/DoctorAvailabilityWithNavigationPropertiesDto.cs` | Rich output with Location + AppointmentType nav props |
| Contracts | `src/.../Application.Contracts/DoctorAvailabilities/GetDoctorAvailabilitiesInput.cs` | Filter input — date/time ranges, bookingStatus, locationId, appointmentTypeId |
| Contracts | `src/.../Application.Contracts/DoctorAvailabilities/DoctorAvailabilityGenerateInputDto.cs` | Bulk generation input — date range, time range, duration minutes, location, type |
| Contracts | `src/.../Application.Contracts/DoctorAvailabilities/DoctorAvailabilitySlotPreviewDto.cs` | Single slot in preview — includes IsConflict flag |
| Contracts | `src/.../Application.Contracts/DoctorAvailabilities/DoctorAvailabilitySlotsPreviewDto.cs` | Preview response — grouped by date with conflict info |
| Contracts | `src/.../Application.Contracts/DoctorAvailabilities/DoctorAvailabilityDeleteByDateInputDto.cs` | Bulk delete by location + date |
| Contracts | `src/.../Application.Contracts/DoctorAvailabilities/DoctorAvailabilityDeleteBySlotInputDto.cs` | Delete by location + date + time range |
| Contracts | `src/.../Application.Contracts/DoctorAvailabilities/IDoctorAvailabilitiesAppService.cs` | Service interface — 11 methods including generate preview, bulk delete by date/slot |
| Application | `src/.../Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` | CRUD + bulk generation preview + bulk delete, permission-gated |
| EF Core | `src/.../EntityFrameworkCore/DoctorAvailabilities/EfCoreDoctorAvailabilityRepository.cs` | 2-way LEFT JOIN (Location, AppointmentType), date/time range filtering |
| HttpApi | `src/.../HttpApi/Controllers/DoctorAvailabilities/DoctorAvailabilityController.cs` | Manual controller (11 endpoints) at `api/app/doctor-availabilities` |
| Angular | `angular/src/app/doctor-availabilities/.../doctor-availability.component.ts` | List page — grouped by location+date, expandable rows, slot counts by status |
| Angular | `angular/src/app/doctor-availabilities/.../doctor-availability.abstract.component.ts` | Base list logic — grouping, pagination, delete group/slot |
| Angular | `angular/src/app/doctor-availabilities/.../doctor-availability-detail.component.ts` | Modal for single-slot create/edit |
| Angular | `angular/src/app/doctor-availabilities/.../doctor-availability-generate.component.ts` | Bulk generation form — dates/weekdays mode, duration, preview with conflict detection |
| Angular | `angular/src/app/doctor-availabilities/.../doctor-availability.abstract.service.ts` | List data service — groups slots by location+date, counts Available/Booked/Reserved |
| Angular | `angular/src/app/doctor-availabilities/.../doctor-availability-detail.abstract.service.ts` | Modal form service — time normalization, create/update |
| Angular | `angular/src/app/doctor-availabilities/.../doctor-availability-base.routes.ts` | Menu config — under DoctorManagement parent, icon `fas fa-calendar-check` |
| Proxy | `angular/src/app/proxy/doctor-availabilities/` | Auto-generated REST client + TypeScript interfaces |

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)
- API detail: [api.md](api.md)
- UI detail: [ui.md](ui.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
