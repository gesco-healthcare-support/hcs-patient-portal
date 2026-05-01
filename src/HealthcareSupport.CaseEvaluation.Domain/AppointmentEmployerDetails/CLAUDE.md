# AppointmentEmployerDetails

Captures the patient's employer information for a workers'-compensation IME appointment -- employer name, occupation, and optional contact/address fields. Exactly one record per appointment, entered on the booking form by whoever books (patient, applicant attorney, defense attorney, or claim examiner). Whether the employer is itself a notification recipient depends on the case: self-insured employers and employers otherwise directly party to the claim receive the all-parties notifications; employers off the active case (when a carrier or TPA handles the claim end-to-end) are stored as data only. Once submitted, the record is locked from edit except via the Gesco-side proper-process path. See [docs/product/appointment-employer-details.md](/docs/product/appointment-employer-details.md) for full intent.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentEmployerDetails/AppointmentEmployerDetailConsts.cs` | Max lengths (EmployerName=255, Occupation=255, PhoneNumber=12, Street=255, City=255, ZipCode=10) + DefaultSorting |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentEmployerDetails/AppointmentEmployerDetail.cs` | Aggregate root -- FullAuditedAggregateRoot<Guid>, IMultiTenant |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentEmployerDetails/AppointmentEmployerDetailManager.cs` | DomainService -- CreateAsync / UpdateAsync with Check.Length guards on every string field |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentEmployerDetails/AppointmentEmployerDetailWithNavigationProperties.cs` | Composite type bundling entity + Appointment + State for list/detail reads |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentEmployerDetails/IAppointmentEmployerDetailRepository.cs` | Custom repo: GetWithNavigationPropertiesAsync, GetListWithNavigationPropertiesAsync, GetListAsync, GetCountAsync |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentEmployerDetails/AppointmentEmployerDetailDto.cs` | Read DTO -- FullAuditedEntityDto<Guid>, IHasConcurrencyStamp |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentEmployerDetails/AppointmentEmployerDetailCreateDto.cs` | Create DTO -- [Required] EmployerName + Occupation, [StringLength] on each |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentEmployerDetails/AppointmentEmployerDetailUpdateDto.cs` | Update DTO |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentEmployerDetails/AppointmentEmployerDetailWithNavigationPropertiesDto.cs` | List/detail read DTO |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentEmployerDetails/GetAppointmentEmployerDetailsInput.cs` | Filter/paging input |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentEmployerDetails/IAppointmentEmployerDetailsAppService.cs` | Service contract -- 8 methods |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/AppointmentEmployerDetails/AppointmentEmployerDetailsAppService.cs` | CRUD + lookups; mixed auth (see Permissions / Known Gotchas) |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/AppointmentEmployerDetails/AppointmentEmployerDetailController.cs` | 8 endpoints at `api/app/appointment-employer-details` |

## Entity Shape

```text
AppointmentEmployerDetail : FullAuditedAggregateRoot<Guid>, IMultiTenant
  - TenantId       : Guid?              (tenant isolation)
  - EmployerName   : string  [required, max 255]   -- patient's employer at time of claim
  - Occupation     : string  [required, max 255]   -- patient's occupation at time of claim
  - PhoneNumber    : string? [max 12]              -- optional employer contact phone
  - Street         : string? [max 255]
  - City           : string? [max 255]
  - ZipCode        : string? [max 10]
  - AppointmentId  : Guid                          (FK -> Appointment, required, 1:1 logically)
  - StateId        : Guid?                         (FK -> State, optional, host-scoped lookup)
```

No status enum -- not a state machine. Lifecycle is governed by the parent Appointment's submit lock (see Business Rules).

## Relationships

| FK Property | Target Entity | Cardinality | Delete Behavior | Notes |
|---|---|---|---|---|
| `AppointmentId` | Appointment | 1:1 (one employer-detail per appointment) | NoAction | Required. UserFriendlyException thrown if `Guid.Empty` on Create/Update |
| `StateId` | State | many-to-one | SetNull | Optional. State is a host-scoped lookup |

Navigation bundle: `AppointmentEmployerDetailWithNavigationProperties` carries the entity plus its `Appointment` and `State` for list/detail rendering. No inbound FKs found in either DbContext.

## Multi-tenancy

**IMultiTenant: Yes.** Every appointment's employer info is tenant-scoped (each Gesco tenant sees only its own bookings).

DbContext registration: `builder.Entity<AppointmentEmployerDetail>(b => { ... })` appears in BOTH `CaseEvaluationDbContext.cs` (host) and `CaseEvaluationTenantDbContext.cs` (tenant), and is OUTSIDE the `if (builder.IsHostDatabase())` guard in the host context -- standard tenant-scoped pattern. Indexes/constraints are identical in both registrations.

## Mapper Configuration

Riok.Mapperly partial classes in `CaseEvaluationApplicationMappers.cs`:

- `AppointmentEmployerDetailToAppointmentEmployerDetailDtoMappers : MapperBase<AppointmentEmployerDetail, AppointmentEmployerDetailDto>` (note: trailing `Mappers` plural is the existing class name)
- `AppointmentEmployerDetailWithNavigationPropertiesToAppointmentEmployerDetailWithNavigationPropertiesDtoMapper : MapperBase<AppointmentEmployerDetailWithNavigationProperties, AppointmentEmployerDetailWithNavigationPropertiesDto>`

No mapper to `LookupDto<Guid>` -- this entity is never the lookup target. The two lookup endpoints (`GetAppointmentLookupAsync`, `GetStateLookupAsync`) consume Appointment / State mappers from those features.

No `AfterMap()` overrides on either employer-detail mapper.

## Permissions

Defined in `CaseEvaluationPermissions.cs` (lines 102-...):

```text
CaseEvaluation.AppointmentEmployerDetails          (Default)
CaseEvaluation.AppointmentEmployerDetails.Create
CaseEvaluation.AppointmentEmployerDetails.Edit
CaseEvaluation.AppointmentEmployerDetails.Delete
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` lines 59-62 with localization keys `Permission:AppointmentEmployerDetails`, `Permission:Create`, `Permission:Edit`, `Permission:Delete`.

Endpoint authorization actually applied:

| Method | Attribute | Notes |
|---|---|---|
| `GetListAsync` | `[Authorize]` | any signed-in user |
| `GetWithNavigationPropertiesAsync` | `[Authorize(...AppointmentEmployerDetails.Default)]` | read |
| `GetAsync` | `[Authorize(...AppointmentEmployerDetails.Default)]` | read |
| `GetAppointmentLookupAsync` | `[Authorize(...AppointmentEmployerDetails.Default)]` | dropdown source |
| `GetStateLookupAsync` | `[Authorize(...AppointmentEmployerDetails.Default)]` | dropdown source |
| `CreateAsync` | `[Authorize]` (NOT `.Create`) | see Known Gotchas |
| `UpdateAsync` | `[Authorize]` (NOT `.Edit`) | see Known Gotchas |
| `DeleteAsync` | `[Authorize(...AppointmentEmployerDetails.Delete)]` | only Delete is permission-gated as defined |

## Business Rules

Anchored to `docs/product/appointment-employer-details.md` (Adrian-confirmed 2026-04-24). Code state vs. intent diverges in places -- see Known Gotchas.

- **Required on every booking, no exceptions.** Intent: the booking form refuses submit without employer info; retired/self-employed patients still name the relevant (former or at-injury) employer for the claim. Code enforcement: `EmployerName` and `Occupation` are `[Required]` + `Check.NotNullOrWhiteSpace`; the AppService throws `UserFriendlyException("The {0} field is required.", L["Appointment"])` if `AppointmentId == Guid.Empty`. Phone, street, city, zip, state remain optional in code; whether MVP intent keeps them optional is OPEN per the product doc.
- **Notification participation is case-dependent.** Intent: when the employer is self-insured or otherwise a legal party, they receive the all-parties notification (ex-parte rule extends to them) using the strict legal-evidence email format from `appointments.md`; when a carrier or TPA handles the claim end-to-end, no email goes to the employer but their data still appears in the case record. The signal that drives this decision is OPEN (booker picks / derived from carrier vs self-insured / other). Code today carries no Email field and no `notify-employer` flag, so this branching is not yet implementable from this entity.
- **Submit-lock universal rule.** Intent: once the appointment request is submitted (and the all-parties email has fired), employer data becomes part of the legal record. Neither booker nor practice-side admin can self-edit; any post-submit change requires a Gesco-side admin running the proper process. Same rule covers patients, attorneys, insurance + adjuster, appointment type, location -- universal across the booking form. Code today does NOT enforce this lock at the AppService layer: `UpdateAsync` is open to any `[Authorize]` caller; the lock is intended to live higher up (booking-flow controller / submit-state check) and is not yet implemented here.
- **Length guards mirrored in domain + DTO + DB.** `AppointmentEmployerDetailManager.CreateAsync` / `UpdateAsync` call `Check.Length(...)` against every `*MaxLength` const; the Create/Update DTOs carry matching `[StringLength(...)]`; the DbContext applies `HasMaxLength(...)`. Triple-layer enforcement is intentional ABP Suite scaffold.
- **Concurrency stamping on update.** `UpdateAsync` calls `SetConcurrencyStampIfNotNull(input.ConcurrencyStamp)` -- last-write-wins is rejected when stamps disagree.
- **No tests.** Per `FEAT-07` -- coverage gap noted in the product doc.

## Angular UI Surface

No Angular UI -- this entity is managed via API only during the appointment booking flow. Intent (per product doc): employer info is a section of the booking form, not a separate screen. The 8 controller endpoints exist for ABP scaffold completeness; only Create is exercised today, by the booking submission flow.

## Known Gotchas

1. **Mixed auth on CRUD.** `CreateAsync` and `UpdateAsync` decorate with bare `[Authorize]` (any signed-in user) instead of `[Authorize(...AppointmentEmployerDetails.Create)]` / `.Edit`. `DeleteAsync` is the only mutating endpoint that uses its specific permission. Intent on who is allowed to create/edit is OPEN per the product doc -- likely the same as other booking-form-captured data. If a Create/Edit permission gate is wanted, swap the attribute and redeploy.
2. **Class name typo: `...DtoMappers` (plural).** The DTO mapper class is named `AppointmentEmployerDetailToAppointmentEmployerDetailDtoMappers` -- trailing `s` is unique among the feature's mappers and inconsistent with the WithNavigationProperties mapper (singular `Mapper`). Cosmetic; not a behavior bug.
3. **No Email field on the entity.** Intent allows the employer to be a notification recipient when self-insured or otherwise party to the claim. The entity has no Email column, so the notify-employer path is not implementable from current schema. The product doc flags this as an `[observed, not authoritative]` discrepancy.
4. **No `notify-employer` flag.** Even with an Email field added, the system has no signal to decide whether a given booking's employer should be notified. Booker-picked vs. derived-from-carrier-status is OPEN.
5. **Submit-lock not enforced here.** The universal "data locks at submit" rule is intent, not code. `UpdateAsync` accepts any authenticated caller; the Gesco-only proper-process gate must be enforced upstream of this AppService.
6. **No tests.** Per FEAT-07; documented in the product doc's Known Discrepancies.
7. **Constructor sets 5/9 settable fields.** `AppointmentEmployerDetail(Guid id, Guid appointmentId, Guid? stateId, string employerName, string occupation)` covers Id + 4 fields; `PhoneNumber`, `Street`, `City`, `ZipCode` are set post-construction by the Manager (code-gen artifact -- standard ABP Suite pattern). Not a bug.

## Links

- Product intent: [docs/product/appointment-employer-details.md](/docs/product/appointment-employer-details.md)
- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern-appointments](/CLAUDE.md#reference-pattern-appointments)
- Related entity: [Appointments CLAUDE.md](/src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md)
- Related entity: [States CLAUDE.md](/src/HealthcareSupport.CaseEvaluation.Domain/States/CLAUDE.md)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
