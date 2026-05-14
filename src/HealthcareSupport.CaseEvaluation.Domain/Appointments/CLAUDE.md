# Appointments

The core scheduling feature. Links a patient to a doctor via a time-slotted DoctorAvailability at a specific Location for a workers'-compensation IME evaluation. Used by tenant-level admin staff (CRUD via the list page), bookers (full-page booking flow including patient/applicant-attorney upsert), and accessor-scoped attorneys (filtered list via the `AccessorIdentityUserId` query).

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Appointments/AppointmentConsts.cs` | Max lengths (PanelNumber=50, RequestConfirmationNumber=50, InternalUserComments=250) and default sort builder |
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/AppointmentStatusType.cs` | 13-value lifecycle enum (Pending=1 ... CancellationRequested=13) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/Appointment.cs` | Aggregate root: 5 required FKs, status, multi-tenant |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs` | DomainService -- Create/Update; does NOT touch AppointmentStatus, InternalUserComments, AppointmentApproveDate, IsPatientAlreadyExist on the Update path |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentWithNavigationProperties.cs` | POCO projection wrapper for eager-loaded queries (adds AppointmentApplicantAttorney) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/IAppointmentRepository.cs` | Custom repo interface: 4 methods, includes accessor-scoped filtering |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentCreateDto.cs` | Create input -- accepts RequestConfirmationNumber but AppService overrides it |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentUpdateDto.cs` | Update input -- no AppointmentStatus, no InternalUserComments, no AppointmentApproveDate, no IsPatientAlreadyExist |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentDto.cs` | Full output DTO (FullAuditedEntityDto + concurrency stamp) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/AppointmentWithNavigationPropertiesDto.cs` | Rich output: Patient, IdentityUser, AppointmentType, Location, DoctorAvailability, AppointmentApplicantAttorney nav props |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/GetAppointmentsInput.cs` | List filter input -- `AccessorIdentityUserId` enables attorney-scoped queries |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/ApplicantAttorneyDetailsDto.cs` | Attorney details flat DTO used by the booking upsert flow |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/IAppointmentsAppService.cs` | Service interface -- 14 methods including 3 attorney upsert/lookup helpers |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` | Business logic: GUID-empty guards, FK existence checks, slot validation (5 checks), `A#####` confirmation-number generation, slot Booked transition, attorney upsert; `[RemoteService(IsEnabled = false)]` -- exposed only via the manual controller |
| EF Core | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Appointments/EfCoreAppointmentRepository.cs` | 5-way LEFT JOIN over Patient/IdentityUser/AppointmentType/Location/DoctorAvailability + AppointmentAccessor subquery for accessor filter; loads AppointmentApplicantAttorney separately |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Appointments/AppointmentController.cs` | Manual controller at `api/app/appointments` -- 14 HTTP endpoints delegating to the AppService |
| Mappers | `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs` | 3 Riok.Mapperly partial classes for this feature (see Mapper Configuration) |
| Permissions | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs` | Nested `Appointments` static class with Default/Create/Edit/Delete constants |
| Permissions | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs` | Registers the 4 permission entries with localized labels |
| DbContext | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` | Inline `builder.Entity<Appointment>` config (outside `IsHostDatabase()` guard) |
| DbContext | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs` | Same inline config -- duplicated for the tenant context |
| Tests | `test/HealthcareSupport.CaseEvaluation.Application.Tests/Appointments/AppointmentsAppServiceTests.cs` | xUnit + Shouldly; 11 active facts (5 Create + 5 Update GUID-empty guards + 1 list empty-state + 1 patient-not-found) plus 4 `[Fact(Skip=...)]` gap-encoding tests |
| Angular | `angular/src/app/appointments/appointment-add.component.ts` | Standalone full-page booking form (~1,594 lines) -- patient demographics, employer, attorney, authorized users, slot picker with `minimumBookingDays = 3` |
| Angular | `angular/src/app/appointments/appointment/components/appointment.component.ts` | Concrete list page (extends abstract) -- ngx-datatable + advanced filters; template at `appointment.component.html` |
| Angular | `angular/src/app/appointments/appointment/components/appointment.abstract.component.ts` | Shared list directive: hookToQuery, action-button visibility from `Appointments.Edit` and `Appointments.Delete` policy checks |
| Angular | `angular/src/app/appointments/appointment/components/appointment-detail.component.ts` | Modal scaffold (template + styles in adjacent `.html`); pure shell that injects `AppointmentDetailViewService` |
| Angular | `angular/src/app/appointments/appointment/components/appointment-view.component.ts` | Standalone view/edit page (~969 lines) -- ngModel-driven form for status/dates/attorney/employer/authorized users |
| Angular | `angular/src/app/appointments/appointment/services/appointment.abstract.service.ts` | List data service (abstract base) -- hookToQuery, delete-with-confirm, filter management |
| Angular | `angular/src/app/appointments/appointment/services/appointment.service.ts` | Concrete `AppointmentViewService` extending the abstract list service |
| Angular | `angular/src/app/appointments/appointment/services/appointment-detail.abstract.service.ts` | Modal-form abstract service: FormBuilder, lookup delegates, create/update submission |
| Angular | `angular/src/app/appointments/appointment/services/appointment-detail.service.ts` | Concrete `AppointmentDetailViewService` |
| Angular | `angular/src/app/appointments/appointment/providers/appointment-base.routes.ts` | Menu config -- `requiredPolicy: 'CaseEvaluation.Appointments'`, icon `fas fa-file-alt` |
| Angular | `angular/src/app/appointments/appointment/providers/appointment-route.provider.ts` | App-init RoutesService registration |
| Angular | `angular/src/app/appointments/appointment/appointment-routes.ts` | Lazy `Routes`: `''` (list, `[authGuard, permissionGuard]`) and `'view/:id'` (view, `[authGuard]` only) |
| Proxy | `angular/src/app/proxy/appointments/appointment.service.ts` | Auto-generated REST client -- 14 methods including `getDownloadToken`, `getFile`, `uploadFile` |
| Proxy | `angular/src/app/proxy/appointments/models.ts` | Auto-generated TypeScript interfaces |

## Entity Shape

```
Appointment : FullAuditedAggregateRoot<Guid>, IMultiTenant
  TenantId                   : Guid?              tenant isolation
  PanelNumber                : string? [max 50]   case panel reference
  AppointmentDate            : DateTime           scheduled date/time
  IsPatientAlreadyExist      : bool               patient pre-existed at booking time
  RequestConfirmationNumber  : string [max 50, required]   auto-generated "A#####"
  DueDate                    : DateTime?          report due date
  InternalUserComments       : string? [max 250]  staff-only notes
  AppointmentApproveDate     : DateTime?          approval timestamp
  AppointmentStatus          : AppointmentStatusType   13-value enum
  PatientId                  : Guid               FK -> Patient (required)
  IdentityUserId             : Guid               FK -> IdentityUser (required)
  AppointmentTypeId          : Guid               FK -> AppointmentType (required)
  LocationId                 : Guid               FK -> Location (required)
  DoctorAvailabilityId       : Guid               FK -> DoctorAvailability (required)
```

State diagram (no enforced transitions in domain -- the entity setter is public):

```
Pending(1) -> Approved(2) -> CheckedIn(9) -> CheckedOut(10) -> Billed(11)
                          -> Rejected(3)
                          -> NoShow(4)
Pending -> RescheduleRequested(12) -> RescheduledNoBill(7) | RescheduledLate(8)
Pending -> CancellationRequested(13) -> CancelledNoBill(5) | CancelledLate(6)
```

WARNING: There is no domain-level state-machine guard. Any caller that holds the entity can set any status directly; the public setter does not validate the source state.

## Relationships

| FK Property | Target Entity | Required | OnDelete | Notes |
|---|---|---|---|---|
| `PatientId` | Patient | Yes | NoAction | Verified to exist in AppService.CreateAsync |
| `IdentityUserId` | IdentityUser | Yes | NoAction | The booking user |
| `AppointmentTypeId` | AppointmentType | Yes | NoAction | Host-scoped lookup |
| `LocationId` | Location | Yes | NoAction | Host-scoped lookup |
| `DoctorAvailabilityId` | DoctorAvailability | Yes | NoAction | Slot is set to `BookingStatus.Booked` on create |

Related entities (FK lives on the other entity):
- `AppointmentEmployerDetail.AppointmentId` -- employer info per appointment
- `AppointmentAccessor.AppointmentId` -- View/Edit grants for additional users (used by the AccessorIdentityUserId filter)
- `AppointmentApplicantAttorney.AppointmentId` -- attorney link

## Multi-tenancy

IMultiTenant: yes. Appointment data is tenant-scoped.

- `builder.Entity<Appointment>` configuration is OUTSIDE the `IsHostDatabase()` block in BOTH `CaseEvaluationDbContext.cs` (line 192) and `CaseEvaluationTenantDbContext.cs` (line 113) -- the entity exists in both contexts.
- Reference data FKs (`AppointmentTypeId`, `LocationId`) point to host-scoped entities.
- Tenant isolation is enforced by ABP's automatic `IMultiTenant` data filter; the repository contains no manual `WHERE TenantId = ...` clauses.
- The accessor-scope subquery (`AppointmentAccessor`) is also tenant-scoped, so attorney-filtering observes the same boundary.

## Mapper Configuration

Riok.Mapperly partial classes in `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs`:

| Mapper class | Source -> Destination | AfterMap |
|---|---|---|
| `AppointmentToAppointmentDtoMappers` (line 251) | `Appointment` -> `AppointmentDto` | No AfterMap |
| `AppointmentWithNavigationPropertiesToAppointmentWithNavigationPropertiesDtoMapper` (line 258) | `AppointmentWithNavigationProperties` -> `AppointmentWithNavigationPropertiesDto` | No AfterMap |
| `AppointmentToLookupDtoGuidMapper` (line 321) | `Appointment` -> `LookupDto<Guid>` | `destination.DisplayName = source.RequestConfirmationNumber` |

All three use `[Mapper]` attribute and inherit `MapperBase<TSource, TDest>`. The lookup mapper applies `[MapperIgnoreTarget(nameof(LookupDto<Guid>.DisplayName))]` to both `Map` overloads since the field is filled in `AfterMap`.

## Permissions

Defined in `CaseEvaluationPermissions.cs` (line 94):

```
CaseEvaluation.Appointments          (Default)
CaseEvaluation.Appointments.Create
CaseEvaluation.Appointments.Edit
CaseEvaluation.Appointments.Delete
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` (line 55) as a parent permission with three children, with localized labels `Permission:Appointments`, `Permission:Create`, `Permission:Edit`, `Permission:Delete`.

Localization keys (in `Domain.Shared/Localization/CaseEvaluation/en.json`):
- `Permission:Appointments` -> "Appointments"
- `Appointments` -> "Appointments"
- `Menu:Appointments` -> "Appointments"

Angular usage:
- List page button (`appointment.component.html`): `*abpPermission="'CaseEvaluation.Appointments.Create'"`
- Row Edit action: `*abpPermission="'CaseEvaluation.Appointments.Edit'"`
- Row Delete action: `*abpPermission="'CaseEvaluation.Appointments.Delete'"`
- Menu / list route: `requiredPolicy: 'CaseEvaluation.Appointments'`
- View route (`/appointments/view/:id`): only `authGuard` -- NO permissionGuard
- `appointment-add` standalone page: no client-side permission check (relies on its containing route's auth)

AppService authorization annotations (only `Default` and `Delete` are checked at the API):

| Method | Attribute | Effective requirement |
|---|---|---|
| `GetListAsync` | `[Authorize]` | any authenticated user |
| `GetWithNavigationPropertiesAsync` | `[Authorize]` | any authenticated user |
| `GetAsync` | `[Authorize(...Appointments.Default)]` | `Appointments` permission |
| `GetIdentityUserLookupAsync` | `[Authorize(...Appointments.Default)]` | `Appointments` permission |
| `GetPatientLookupAsync` / `GetAppointmentTypeLookupAsync` / `GetLocationLookupAsync` / `GetDoctorAvailabilityLookupAsync` | `[Authorize]` | any authenticated user |
| `CreateAsync` | `[Authorize]` | any authenticated user (NOT Create) |
| `UpdateAsync` | `[Authorize]` | any authenticated user (NOT Edit) |
| `DeleteAsync` | `[Authorize(...Appointments.Delete)]` | `Appointments.Delete` |
| `GetApplicantAttorneyDetailsForBookingAsync` / `GetAppointmentApplicantAttorneyAsync` / `UpsertApplicantAttorneyForAppointmentAsync` | `[Authorize]` | any authenticated user |

## Business Rules

1. **RequestConfirmationNumber is auto-generated, client value ignored.** `CreateAsync` calls `GenerateNextRequestConfirmationNumberAsync()` which queries the max existing `A#####` row and produces the next value as `"A" + 5 zero-padded digits` (`{A}{n:D5}`). The DTO accepts `RequestConfirmationNumber` for binding compatibility but the AppService silently overrides whatever the client sent.

2. **Hardcoded format constants.** `RequestConfirmationPrefix = "A"`, `RequestConfirmationDigits = 5`. Numeric overflow is checked: if `nextValue > 99999` the AppService throws `UserFriendlyException(L["Request confirmation number limit reached."])`.

3. **Five-step slot validation gate (CreateAsync).** Before booking, `ValidateDoctorAvailabilityForBooking` enforces:
   - Slot's `BookingStatusId` must equal `BookingStatus.Available`
   - Slot's `LocationId` must equal `input.LocationId`
   - If slot has an `AppointmentTypeId`, it must equal `input.AppointmentTypeId`
   - Slot's `AvailableDate.Date` must equal `input.AppointmentDate.Date`
   - Time component of `input.AppointmentDate` must satisfy `FromTime <= time < ToTime`

4. **Slot booking is one-way.** `CreateAsync` sets `doctorAvailability.BookingStatusId = BookingStatus.Booked` and saves the DoctorAvailability. `DeleteAsync` does NOT release the slot back to `Available`. There is no cancel/reschedule flow that frees a slot today.

5. **Five GUID-empty guard checks fire before FK lookups.** Both `CreateAsync` and `UpdateAsync` reject `Guid.Empty` for Patient, IdentityUser, AppointmentType, Location, DoctorAvailability with localized `UserFriendlyException` messages naming the field.

6. **Lookup endpoints filter through Doctor relations.**
   - `GetAppointmentTypeLookupAsync` returns only `AppointmentType`s assigned to a Doctor (via `Doctor.AppointmentTypes` join).
   - `GetLocationLookupAsync` returns only `Location`s assigned to a Doctor (via `Doctor.Locations` join).
   - `GetPatientLookupAsync` and `GetIdentityUserLookupAsync` filter by `Email` substring only.
   - `GetDoctorAvailabilityLookupAsync` returns ALL availabilities unfiltered.

7. **Update freezes status, comments, approve date, and patient-existence flag.** `AppointmentManager.UpdateAsync` does not accept `AppointmentStatus`, `InternalUserComments`, `AppointmentApproveDate`, or `IsPatientAlreadyExist` parameters; the `AppointmentUpdateDto` does not surface these fields. They can only be set via direct entity mutation or at create time.

8. **Permission gap on Create/Update at the API.** Both methods carry only `[Authorize]` -- any authenticated user can create or update appointments via the API. The Angular UI checks the `Create`/`Edit` permissions but the server does not enforce them. The test file encodes this gap as two `[Fact(Skip="KNOWN GAP: ...")]` tests waiting for the permission attributes to be applied.

9. **List filter has dual semantics around `IdentityUserId`.** When `AccessorIdentityUserId` is set, the `IdentityUserId` clause is suppressed and the query instead matches rows where `Appointment.CreatorId == accessorUserId` OR an `AppointmentAccessor` row exists for that user; this is the attorney-scoped path.

10. **Attorney upsert lives in this AppService.** `UpsertApplicantAttorneyForAppointmentAsync` either creates an `ApplicantAttorney` (if `ApplicantAttorneyId` is null/empty) or updates the existing record, then upserts the single `AppointmentApplicantAttorney` link row -- one-attorney-per-appointment by construction (uses the first row from a `maxResultCount: 10` fetch).

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `AppointmentEmployerDetail.AppointmentId` | NoAction | No (tenant-scoped) | One-to-one employer detail per appointment |
| `AppointmentAccessor.AppointmentId` | NoAction | No (tenant-scoped) | View/Edit grants powering the accessor filter |
| `AppointmentApplicantAttorney.AppointmentId` | NoAction | No (tenant-scoped) | Attorney link |

All three are configured in BOTH `CaseEvaluationDbContext.cs` and `CaseEvaluationTenantDbContext.cs`, outside any `IsHostDatabase()` guard.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| `AppointmentComponent` | `angular/src/app/appointments/appointment/components/appointment.component.ts` | `/appointments` | List page (extends abstract directive) |
| `AbstractAppointmentComponent` | `.../appointment/components/appointment.abstract.component.ts` | -- | Shared list logic + Edit/Delete visibility check |
| `AppointmentDetailModalComponent` | `.../appointment/components/appointment-detail.component.ts` | -- | Quick create/edit modal launched from the list |
| `AppointmentAddComponent` | `angular/src/app/appointments/appointment-add.component.ts` | `/appointments/add` (registered elsewhere) | Standalone full-page booking flow with ~30 form controls + employer + attorney + authorized-users sub-flows |
| `AppointmentViewComponent` | `.../appointment/components/appointment-view.component.ts` | `/appointments/view/:id` | Standalone view/edit page (ngModel) |

**Pattern:** Mixed.
- The list flow is an ABP Suite scaffold (abstract + concrete pair, no `standalone: true`).
- The Add page (`AppointmentAddComponent`) is a standalone Angular component (`standalone: true`).
- The View page (`AppointmentViewComponent`) is a standalone Angular component (`standalone: true`).
- The detail modal is a non-standalone scaffold component used only inside the list-page imports.

### Sub-path A -- ABP Suite scaffold (list flow)

**Forms (modal):** the modal shell injects `AppointmentDetailViewService`; FormBuilder controls live in `appointment-detail.abstract.service.ts` -- patientId, identityUserId, appointmentTypeId, locationId, doctorAvailabilityId, appointmentDate, panelNumber, dueDate, plus appointment-status select.

**Permission guards:**
- List route (`appointment-routes.ts` `path: ''`): `[authGuard, permissionGuard]` (route-level policy `CaseEvaluation.Appointments`)
- Menu (`appointment-base.routes.ts`): `requiredPolicy: 'CaseEvaluation.Appointments'`
- Template directives: `*abpPermission="'CaseEvaluation.Appointments.Create'"` (button), `*abpPermission="'CaseEvaluation.Appointments.Edit'"` and `*abpPermission="'CaseEvaluation.Appointments.Delete'"` (row actions)

**Services injected:**
- Abstract list directive: `ListService`, `AppointmentViewService`, `AppointmentDetailViewService`, `PermissionService`
- Concrete list component: same set, providers add `ListService`, `AppointmentViewService`, `AppointmentDetailViewService`, `NgbDateAdapter`, `NgbTimeAdapter`
- Modal shell: only `AppointmentDetailViewService`

### Sub-path B -- Standalone components (Add and View)

**Imports (Add page):** `CommonModule`, `FormsModule`, `ReactiveFormsModule`, `NgxDatatableModule`, `LocalizationPipe`, `TopHeaderNavbarComponent`, `LookupSelectComponent`, `NgxValidateCoreModule`, `NgbDatepickerModule`, `NgbNavModule`. Providers: `ListService`, `AppointmentViewService`, `NgbDateAdapter` -> `DateAdapter`, `NgbTimeAdapter` -> `TimeAdapter`.

**Imports (View page):** `CommonModule`, `FormsModule`, `ReactiveFormsModule`, `LocalizationPipe`, `LookupSelectComponent`, `NgbDatepickerModule`.

**Forms (Add page, FormBuilder):** patient demographics block, employer detail block, applicant-attorney block, authorized-users sub-table; slot picker constrained by `minimumBookingDays = 3` (client-side rule); access types `[ { value: 23, label: 'View' }, { value: 24, label: 'Edit' } ]`.

**Forms (View page, ngModel):** appointment, patient, employer, applicant-attorney, authorized-users -- mutated via plain object bindings rather than reactive forms. Inconsistent with the modal and Add page.

**Permission guards (Add and View):**
- Add page: no `*abpPermission` directives on the route or template; relies on whichever feature route registers it.
- View route (`'view/:id'` in `appointment-routes.ts`): `[authGuard]` only -- intentional or otherwise, ANY authenticated user can navigate to `/appointments/view/<id>`.

**Services injected (Add):** `FormBuilder`, `Router`, `ConfigStateService`, `RestService`, `AppointmentViewService` (via providers).

**Services injected (View):** `ActivatedRoute`, `Router`, `ConfigStateService`, `AppointmentService` (proxy), `RestService`.

## Known Gotchas

1. **Three parallel form approaches.** Modal (FormBuilder reactive), Add page (FormBuilder reactive, ~1,594 lines), View page (plain ngModel). The View-page divergence is the one likely to drift; reactive validation and async slot lookup do not apply to the ngModel form.

2. **`view/:id` route has no `permissionGuard`.** Only `authGuard`. Any authenticated user can deep-link to any appointment they know the id of. Server lookup is via `GetWithNavigationPropertiesAsync` which is `[Authorize]` only. ABP's automatic tenant filter still scopes the data to the user's tenant, but cross-role read access inside the same tenant is not gated.

3. **`console.log('Date check:', ...)` left in `appointment-add.component.ts` line 1546.** Production-bound debug log inside the date-validation path.

4. **Constructor sets all 11 settable fields it accepts; the remaining 3 (`InternalUserComments`, `AppointmentApproveDate`, `IsPatientAlreadyExist`) are intentionally not constructor params.** They are also absent from `AppointmentUpdateDto` and from `AppointmentManager.UpdateAsync`. There is currently no code path that writes them after creation -- they are schema fields without producers.

5. **Confirmation-number race is closed.** Unique index `IX_AppEntity_Appointments_TenantId_RequestConfirmationNumber` on `(TenantId, RequestConfirmationNumber)` was added in migration `20260504170956_Phase11f_AppointmentConfirmationNumberUniqueIndex`. The Create path wraps generation + insert in `ConfirmationNumberRetryPolicy.RunWithRetryAsync` (5 attempts, no backoff). Two concurrent bookers no longer collide -- the loser's SaveChangesAsync throws and the policy regenerates.

6. **Past-date bookings are rejected at the domain layer.** `AppointmentManager.EnsureAppointmentDateNotInPast` (Issue #115, 2026-05-13) throws `BusinessException(AppointmentBookingDateInsideLeadTime, leadTimeDays=0)` on Create unconditionally and on Update when the date is changing. The Angular datepicker's `minimumBookingDays = 3` is now a UX hint, not the only enforcement -- direct API calls also fail.

7. **Proxy `getList` accepts more filter parameters than the input DTO defines.** `GetAppointmentsInput.cs` exposes 7 filters (`FilterText`, `PanelNumber`, `AppointmentDateMin/Max`, `IdentityUserId`, `AccessorIdentityUserId`, `AppointmentTypeId`, `LocationId`), but `appointment.service.ts` `getList` sends 18 query params (e.g., `isPatientAlreadyExist`, `requestConfirmationNumber`, `dueDateMin/Max`, `appointmentApproveDateMin/Max`, `appointmentStatus`, `patientId`, `doctorAvailabilityId`). The server simply ignores the extras; the proxy was generated against a richer expected shape and is now out of sync with the trimmed-down `GetAppointmentsInput`.

8. **`AppointmentsAppService` carries `[RemoteService(IsEnabled = false)]`.** ABP's auto-API conventions are disabled; HTTP exposure is provided exclusively through the manual `AppointmentController` -- there is no auto-generated REST surface that can drift behind the controller.

9. **Test coverage is intentionally narrow.** `AppointmentsAppServiceTests` ships 11 active facts (Guid-empty guards + one happy-path-empty-state) and 4 `[Fact(Skip="KNOWN GAP: ...")]` placeholders that explicitly cite this CLAUDE.md (slot-release on delete, status state-machine, Create/Update permission gaps). The Skip messages are how the gaps stay visible in CI output.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern--appointments](/CLAUDE.md#reference-pattern--appointments)
- Product intent: [docs/product/appointments.md](/docs/product/appointments.md)
- Feature docs: [docs/features/appointments/overview.md](/docs/features/appointments/overview.md) (if present)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
