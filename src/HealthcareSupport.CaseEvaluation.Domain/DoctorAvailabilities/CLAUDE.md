# DoctorAvailabilities

Time-slot records that publish when a doctor is available for IME appointments at a given location. The doctor's-admin role generates slots in bulk (date range or weekday pattern) or one-at-a-time, then the appointment booking flow consumes individual slots and marks them Booked. The Angular UI groups slots by location + date and surfaces conflict detection during bulk preview.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/DoctorAvailabilities/DoctorAvailabilityConsts.cs` | Default sort -- `AvailableDate asc` (with optional `DoctorAvailability.` prefix) |
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/BookingStatus.cs` | Enum: `Available=8`, `Booked=9`, `Reserved=10` |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailability.cs` | Aggregate root -- date + time slot, booking status, two FKs, multi-tenant |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailabilityManager.cs` | DomainService -- thin Create/Update over the repository (no overlap or state validation) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/DoctorAvailabilityWithNavigationProperties.cs` | Projection wrapper -- carries `Location` + `AppointmentType` nav props |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/DoctorAvailabilities/IDoctorAvailabilityRepository.cs` | Custom repo interface -- nav-prop list/get, range filters on date/time, status, location, type |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/IDoctorAvailabilitiesAppService.cs` | Service interface -- 11 methods incl. preview generation and two bulk-delete modes |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilityCreateDto.cs` | Single-slot create input -- `BookingStatusId` defaults to first enum value (`Available=8`) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilityUpdateDto.cs` | Update input -- implements `IHasConcurrencyStamp` |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilityDto.cs` | Output DTO (`FullAuditedEntityDto<Guid>`, `IHasConcurrencyStamp`) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilityWithNavigationPropertiesDto.cs` | Rich output -- DoctorAvailability + Location + AppointmentType DTOs |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/GetDoctorAvailabilitiesInput.cs` | Paged query input -- date/time min-max, status, locationId, appointmentTypeId |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilityGenerateInputDto.cs` | Bulk preview input -- date range, time range, status, location, type, `AppointmentDurationMinutes=15` default |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilitySlotPreviewDto.cs` | Per-slot preview row -- includes `IsConflict` flag and `TimeId` |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilitySlotsPreviewDto.cs` | Per-day preview group -- `Dates`, `Days`, `MonthId`, `LocationName`, `Time`, `SameTimeValidation`, list of slots |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilityDeleteByDateInputDto.cs` | Bulk delete by `LocationId` + `AvailableDate` |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DoctorAvailabilities/DoctorAvailabilityDeleteBySlotInputDto.cs` | Bulk delete by location + date + exact `FromTime`/`ToTime` |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/DoctorAvailabilities/DoctorAvailabilitiesAppService.cs` | CRUD + lookups + preview + two bulk deletes; permission-gated; `[RemoteService(IsEnabled=false)]` so AppService is not auto-exposed (manual controller wraps it) |
| EF Core | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/DoctorAvailabilities/EfCoreDoctorAvailabilityRepository.cs` | LEFT JOIN to Location + AppointmentType; range filters on date/time; `filterText` arm is a no-op (`e => true`) |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/DoctorAvailabilities/DoctorAvailabilityController.cs` | Manual controller at `api/app/doctor-availabilities` -- 11 routes, delegates to AppService |
| Tests | `test/HealthcareSupport.CaseEvaluation.Application.Tests/DoctorAvailabilities/DoctorAvailabilitiesAppServiceTests.cs` | Abstract base -- 20 [Fact] tests (18 active + 2 Skip-tracked gaps); concrete `EfCoreDoctorAvailabilitiesAppServiceTests` lives under EntityFrameworkCore.Tests |
| Angular | `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability.component.ts` | Concrete list component (template + styles in adjacent .html/.scss) |
| Angular | `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability.abstract.component.ts` | Abstract base list component -- grouping by location+date, pagination, delete group/slot |
| Angular | `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-detail.component.ts` | Single-slot create/edit modal (concrete component, template adjacent) |
| Angular | `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts` | Bulk-generation form -- dates / weekdays mode + preview with conflict flags |
| Angular | `angular/src/app/doctor-availabilities/doctor-availability/services/doctor-availability.abstract.service.ts` | List view-service -- groups API result by location+date, counts slots by status |
| Angular | `angular/src/app/doctor-availabilities/doctor-availability/services/doctor-availability.service.ts` | Concrete list view-service |
| Angular | `angular/src/app/doctor-availabilities/doctor-availability/services/doctor-availability-detail.abstract.service.ts` | Detail view-service -- reactive form, time normalization, create/update |
| Angular | `angular/src/app/doctor-availabilities/doctor-availability/services/doctor-availability-detail.service.ts` | Concrete detail view-service |
| Angular | `angular/src/app/doctor-availabilities/doctor-availability/doctor-availability-routes.ts` | Lazy route definitions for the list/generate components |
| Angular | `angular/src/app/doctor-availabilities/doctor-availability/providers/doctor-availability-base.routes.ts` | Menu config -- under `::Menu:DoctorManagement`, icon `fas fa-calendar-check`, policy `CaseEvaluation.DoctorAvailabilities` |
| Angular | `angular/src/app/doctor-availabilities/doctor-availability/providers/doctor-availability-route.provider.ts` | APP_INITIALIZER provider that registers the menu route |
| Proxy | `angular/src/app/proxy/doctor-availabilities/doctor-availability.service.ts` | Auto-generated REST client (paired with `models.ts` and `index.ts`) |

## Entity Shape

```
DoctorAvailability : FullAuditedAggregateRoot<Guid>, IMultiTenant
  TenantId          : Guid?           (tenant isolation)
  AvailableDate     : DateTime        (the calendar date this slot is on)
  FromTime          : TimeOnly        (slot start)
  ToTime            : TimeOnly        (slot end)
  BookingStatusId   : BookingStatus   (Available=8, Booked=9, Reserved=10)
  LocationId        : Guid            (FK -> Location, required)
  AppointmentTypeId : Guid?           (FK -> AppointmentType, optional)
```

**BookingStatus state diagram** (observed code paths only):

```
Available(8) -> Booked(9)     (set by AppointmentsAppService when an appointment is created)
Available(8) -> Reserved(10)  (no automated path writes Reserved; only reachable via direct UpdateAsync)
```

No reverse transition is wired: deleting an Appointment does NOT release its slot back to `Available`.
Per `docs/product/doctor-availabilities.md`, the intended semantics for `Reserved` are still under
manager review (working hypothesis: pending-review state for a request); the code today writes
`Booked` directly on appointment creation.

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `LocationId` | Location | NoAction | Required; Location is host-scoped lookup |
| `AppointmentTypeId` | AppointmentType | SetNull | Optional; AppointmentType is host-scoped lookup |

`DoctorAvailabilityWithNavigationProperties` is the projection used for nav-prop reads; the EF
repository materialises it via a LEFT JOIN over Location and AppointmentType.

## Multi-tenancy

- `IMultiTenant`: yes -- each tenant (doctor practice) manages its own schedule.
- DbContext registration: configured in BOTH `CaseEvaluationDbContext` (host) AND
  `CaseEvaluationTenantDbContext`, in each case OUTSIDE the `IsHostDatabase()` guard.
  ABP's automatic tenant filter scopes reads/writes by `TenantId`.
- FK targets (`Location`, `AppointmentType`) are host-scoped lookup entities.

## Mapper Configuration

In `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs`
(Riok.Mapperly, NOT AutoMapper):

| Mapper Class | Source -> Destination | AfterMap? |
|---|---|---|
| `DoctorAvailabilityToDoctorAvailabilityDtoMappers` | `DoctorAvailability` -> `DoctorAvailabilityDto` | No |
| `DoctorAvailabilityWithNavigationPropertiesToDoctorAvailabilityWithNavigationPropertiesDtoMapper` | `DoctorAvailabilityWithNavigationProperties` -> `DoctorAvailabilityWithNavigationPropertiesDto` | No |
| `DoctorAvailabilityToLookupDtoGuidMapper` | `DoctorAvailability` -> `LookupDto<Guid>` | Yes -- sets `destination.DisplayName = $"{source.AvailableDate:yyyy-MM-dd} {source.FromTime}-{source.ToTime}"` (DisplayName is `[MapperIgnoreTarget]` on the partial Map methods, set only via AfterMap) |

## Permissions

Defined in `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs`:

```
CaseEvaluation.DoctorAvailabilities          (Default)
CaseEvaluation.DoctorAvailabilities.Create
CaseEvaluation.DoctorAvailabilities.Edit
CaseEvaluation.DoctorAvailabilities.Delete
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` as the parent
`Permission:DoctorAvailabilities` plus three children (`Create`, `Edit`, `Delete`).

AppService decoration (observed):

| Method | Authorization |
|---|---|
| `GetListAsync` | `[Authorize]` only -- ANY authenticated user, no `DoctorAvailabilities.Default` check |
| `GetWithNavigationPropertiesAsync`, `GetAsync`, `GetLocationLookupAsync`, `GetAppointmentTypeLookupAsync`, `GeneratePreviewAsync` | `DoctorAvailabilities.Default` |
| `CreateAsync` | `DoctorAvailabilities.Create` |
| `UpdateAsync` | `DoctorAvailabilities.Edit` |
| `DeleteAsync`, `DeleteBySlotAsync`, `DeleteByDateAsync` | `DoctorAvailabilities.Delete` |

Angular menu (`doctor-availability-base.routes.ts`): parent `::Menu:DoctorManagement`, path
`/doctor-management/doctor-availabilities`, `requiredPolicy: 'CaseEvaluation.DoctorAvailabilities'`,
icon `fas fa-calendar-check`, order 3.

## Business Rules

1. **Bulk preview is stateless and read-only.** `GeneratePreviewAsync` accepts a list of
   `DoctorAvailabilityGenerateInputDto`, validates each item, generates per-day per-duration slots,
   queries existing slots in `[minDate, maxDate]`, and flags overlaps with `IsConflict=true`.
   It does NOT persist; the Angular UI submits non-conflicting slots one at a time via
   `CreateAsync` after the user reviews the preview.
2. **Per-item validation in `GeneratePreviewAsync`** (UserFriendlyException on each):
   `LocationId == Guid.Empty`, `AppointmentDurationMinutes <= 0`,
   `ToDate.Date < FromDate.Date`, `ToTime <= FromTime`.
3. **Per-item validation in `CreateAsync`/`UpdateAsync`/`DeleteBySlotAsync`/`DeleteByDateAsync`:**
   `LocationId == Guid.Empty` throws `UserFriendlyException` with "Location" in the message.
   No other domain-level checks (no overlap detection, no past-date guard, no booked-slot guard).
4. **`AppointmentDurationMinutes` defaults to 15** in `DoctorAvailabilityGenerateInputDto`. Slots
   are generated as consecutive non-overlapping blocks of that duration starting from `FromTime`,
   stopping when `currentTime + duration > ToTime`.
5. **Conflict classification (same-location only).** Within `GeneratePreviewAsync`, the overlap
   query is scoped by `LocationId`, so cross-location overlaps are not conflicts. Same-location
   overlap with a `Booked` or `Reserved` slot sets `IsConflict=true` and the
   `"Time slot is already booked or reserved at this location."` message; same-location overlap
   with any other status (typically `Available`) sets `IsConflict=true` and the
   `"Time slot already exists at this location."` message. Both messages are written onto
   `previewList[0].SameTimeValidation`. Different locations may host independently overlapping
   wall-clock slots.
6. **Three delete modes, all `Delete` permission**: `DeleteAsync` (single id), `DeleteBySlotAsync`
   (location + date + exact time range), `DeleteByDateAsync` (location + date, all slots that day).
7. **`BookingStatusId` is mutable on update.** `UpdateAsync` accepts `BookingStatusId` and overwrites
   it directly. There is no guard preventing flipping a `Booked` slot back to `Available` while an
   Appointment still references it (tracked as a Skip-test gap and a Known Gotcha).
8. **`CreateDto.BookingStatusId` defaults to `Enum.GetValues<BookingStatus>()[0]`** which is
   `Available=8` (current enum order). Subtle: if the enum is reordered, the default silently changes.
9. **Lookups are unfiltered.** `GetLocationLookupAsync` and `GetAppointmentTypeLookupAsync` filter
   only by `Name.Contains(filter)`; they do NOT scope to the doctor or to slots that already exist
   (unlike Appointments, which filters lookups by the doctor's assignments).
10. **`GetListAsync` is open to any authenticated user** (just `[Authorize]`, not
    `[Authorize(...DoctorAvailabilities.Default)]`). Single-record reads still require Default.
11. **Default sort is `AvailableDate asc`** (`DoctorAvailabilityConsts.GetDefaultSorting`).

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `Appointment.DoctorAvailabilityId` | NoAction | No -- configured in both `CaseEvaluationDbContext` (outside IsHostDatabase) and `CaseEvaluationTenantDbContext` | Required FK on Appointment; appointment booking marks the slot `Booked` |

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| `DoctorAvailabilityComponent` | `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability.component.ts` | `/doctor-management/doctor-availabilities` | List page -- groups by location + date, expandable rows, delete group/slot |
| `AbstractDoctorAvailabilityComponent` | `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability.abstract.component.ts` | -- | Base directive -- grouping, pagination, delete group/slot |
| `DoctorAvailabilityDetailModalComponent` | `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-detail.component.ts` | -- (modal) | Single-slot create/edit |
| `DoctorAvailabilityGenerateComponent` | `angular/src/app/doctor-availabilities/doctor-availability/components/doctor-availability-generate.component.ts` | `/doctor-management/doctor-availabilities/generate` | Bulk generation -- dates / weekdays mode, preview with conflicts |

**Pattern detection:** ABP Suite scaffold (abstract/concrete pair) -- no `standalone: true` on
the components inspected. Falls under "Sub-path A -- ABP Suite scaffold" in the skill.

### Sub-path A -- ABP Suite scaffold

**Forms (from .html templates):**

- Detail modal: `availableDate` (date picker), `fromTime` / `toTime` (time inputs),
  `bookingStatusId` (select bound to `BookingStatus` enum), `locationId`
  (`abp-lookup-select` against `getLocationLookup`), `appointmentTypeId`
  (`abp-lookup-select` against `getAppointmentTypeLookup`, optional).
- Generate form: `slotMode` radio (`dates` or `weekdays`); date inputs (`fromDate`/`toDate` for
  dates mode, month + weekday checkboxes for weekdays mode); `fromTime`/`toTime`;
  `appointmentDurationMinutes` (numeric, default 15); `bookingStatusId`; `locationId` (required);
  `appointmentTypeId` (optional).

**Permission guards:**

- Route guards: `authGuard`, `permissionGuard` -- requires policy `CaseEvaluation.DoctorAvailabilities`.
- Template directives: `*abpPermission="'CaseEvaluation.DoctorAvailabilities.Create'"` on
  create/generate buttons; `*abpPermission="'CaseEvaluation.DoctorAvailabilities.Delete'"` on
  delete-group and delete-slot actions; `*abpPermission="'CaseEvaluation.DoctorAvailabilities.Edit'"`
  on row-level edit.

**Services injected (constructor + `inject()`):**

- `ListService` (ABP), `DoctorAvailabilityService` (proxy), `DoctorAvailabilityViewService`
  (concrete view-service), `DoctorAvailabilityDetailViewService`, `PermissionService` (ABP),
  `ConfigStateService` (for status enum option binding).

## Known Gotchas

1. **`GetListAsync` is missing the `DoctorAvailabilities.Default` check** that every other read
   method has -- only `[Authorize]` (any authenticated user). Likely a code-gen miss; flag if
   tightening is required.
2. **No domain-layer validation.** `DoctorAvailabilityManager.CreateAsync`/`UpdateAsync` only
   construct/update the entity. Overlap detection, past-date guards, and booked-slot guards all
   live in the AppService (and only partially -- see #3 and #4). Adding a real domain manager
   is the natural target if the booked-slot invariant is to be enforced.
3. **GeneratePreview + Create is not atomic.** Two callers can both preview the same date/time
   with no conflict, then both call `CreateAsync`, producing duplicate overlapping slots. Pinned
   as a Skip-tagged test (`GeneratePreviewAsync_ConcurrentCalls_PreventDuplicateSlotCreation`)
   that references this section.
4. **`UpdateAsync` can flip `Booked` -> `Available`** even while an `Appointment` references the
   slot (no guard). Pinned as a Skip-tagged test
   (`UpdateAsync_ChangeBookedStatusBackToAvailable_WhenSlotStillBooked_ShouldThrow`).
5. **`filterText` repository arm is a no-op.** Both `ApplyFilter` overloads include
   `WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true)` -- the predicate is `true`, so
   `filterText` does nothing even though the parameter flows through DTO/AppService/repo.
6. **Repository `GetListAsync` (without nav props) is not currently called by the AppService.**
   The AppService always uses `GetListWithNavigationPropertiesAsync` and `GetCountAsync` (which
   itself goes through the nav-properties query). The flat `GetListAsync` is part of the
   interface contract but unused.
7. **No reverse transition** from `Booked` to `Available` exists in any code path. Deleting an
   appointment does not release its slot. Per `docs/product/doctor-availabilities.md`, the
   intended state machine is under review and may eventually require this transition (Q1/Q17).
8. **Conflict messaging clobbers itself.** If both `isAlreadyExist` and `isBookedByUser` are
   true, only the second message is preserved on `previewList[0].SameTimeValidation` (sequential
   assignment, not aggregation).
9. **`Reserved` status has no automated writer.** Code defines the value but no service path
   sets it; per the product doc, the intended meaning is "pending office review" (manager
   to confirm). Today, `Reserved` is only reachable via a direct manual `UpdateAsync`.
10. **Constructor sets 7/7 settable non-audit fields.** Manager `CreateAsync` does not assign
    properties post-construction. No code-gen artifact here.
11. **AppService `[RemoteService(IsEnabled = false)]`.** Auto-API generation is disabled on the
    AppService -- the manual `DoctorAvailabilityController` is the public surface; both routes
    point at the same service. Standard ABP pattern but worth noting if hunting for "missing"
    endpoints.
12. **Bulk-preview UI is browser-side.** The Angular abstract service groups a flat API page
    by `location + date` after fetching; there is no server-side grouping endpoint. Large slot
    counts may degrade list performance.

## Test Coverage

Test file: `test/HealthcareSupport.CaseEvaluation.Application.Tests/DoctorAvailabilities/DoctorAvailabilitiesAppServiceTests.cs`
(abstract base; concrete `EfCoreDoctorAvailabilitiesAppServiceTests` under
`test/HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Tests/...` supplies the SQLite startup
module). Wave 1 scope: validation guards + `GeneratePreviewAsync` slot math + empty list, used to
pin the contract that the planned P-11 refactor of the 41-cognitive-complexity
`GeneratePreviewAsync` must preserve.

20 [Fact] tests in total -- 18 active + 2 Skip-tagged gap-encoders.

LocationId-empty validation guards (4 active):

1. `CreateAsync_WhenLocationIdIsEmpty_Throws`
2. `UpdateAsync_WhenLocationIdIsEmpty_Throws`
3. `DeleteBySlotAsync_WhenLocationIdIsEmpty_Throws`
4. `DeleteByDateAsync_WhenLocationIdIsEmpty_Throws`

`GeneratePreviewAsync` -- null/empty input (2 active):

5. `GeneratePreviewAsync_WhenInputIsNull_ThrowsAbpValidation` (pins the observable behaviour --
   ABP's validation interceptor rejects the null parameter before the in-method `null` branch
   runs; that branch is dead code)
6. `GeneratePreviewAsync_WhenInputIsEmpty_ReturnsEmpty`

`GeneratePreviewAsync` -- per-item guard clauses (5 active):

7. `GeneratePreviewAsync_WhenLocationIdIsEmpty_Throws`
8. `GeneratePreviewAsync_WhenDurationIsZero_Throws`
9. `GeneratePreviewAsync_WhenDurationIsNegative_Throws`
10. `GeneratePreviewAsync_WhenToDateBeforeFromDate_Throws`
11. `GeneratePreviewAsync_WhenToTimeEqualsFromTime_Throws`
12. `GeneratePreviewAsync_WhenToTimeBeforeFromTime_Throws`

`GeneratePreviewAsync` -- slot math + boundaries (5 active):

13. `GeneratePreviewAsync_SingleDay_60MinuteSlot_InOneHourRange_Returns1Slot`
14. `GeneratePreviewAsync_SingleDay_30MinuteSlots_InOneHourRange_Returns2Slots` (asserts both
    `09:00-09:30` and `09:30-10:00` produced in order)
15. `GeneratePreviewAsync_MultiDay_ReturnsOnePreviewPerDay` (3 days, asserts `Dates` formatted
    as `"MM-dd-yyyy"` and one slot per day)
16. `GeneratePreviewAsync_DurationLongerThanRange_Returns0Slots` (60 min duration in 30 min
    range -- preview list is fully empty, NOT a single empty group)
17. `GeneratePreviewAsync_Boundary_60MinuteInExactly60MinuteRange_Returns1Slot`

`GetListAsync` empty-state (1 active):

18. `GetListAsync_WhenNoSlotsSeeded_ReturnsZeroCount`

Skip-tagged gap-encoders (2, both reference this CLAUDE.md):

19. `GeneratePreviewAsync_ConcurrentCalls_PreventDuplicateSlotCreation` -- references Known
    Gotcha #3 (preview/create non-atomicity)
20. `UpdateAsync_ChangeBookedStatusBackToAvailable_WhenSlotStillBooked_ShouldThrow` --
    references Business Rule #7 (mutable BookingStatus; no booked-slot guard)

Deferred to Wave 2 (per file header comment): seeded CRUD happy-path, conflict-detection (needs
seeded existing slots), repository nav-property joins -- pending resolution of SQLite FK
posture for `Location` and `AppointmentType` seeds.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern-appointments](/CLAUDE.md#reference-pattern-appointments)
- Product intent: [docs/product/doctor-availabilities.md](/docs/product/doctor-availabilities.md)
- Feature docs: [docs/features/doctor-availabilities/overview.md](/docs/features/doctor-availabilities/overview.md) (if exists)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
