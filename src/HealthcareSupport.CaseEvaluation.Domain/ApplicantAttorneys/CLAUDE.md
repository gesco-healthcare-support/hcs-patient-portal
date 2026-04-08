# ApplicantAttorneys

Attorney contact and firm information for workers' comp applicant attorneys. Linked to an IdentityUser account and optionally to a State for address purposes. Used in the appointment booking flow to associate attorneys with appointments via the AppointmentApplicantAttorney join entity.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/ApplicantAttorneys/ApplicantAttorneyConsts.cs` | Max lengths (FirmName=50, FirmAddress=100, WebAddress=100, PhoneNumber=20, FaxNumber=19, Street=255, City=50, ZipCode=10), default sort |
| Domain | `src/.../Domain/ApplicantAttorneys/ApplicantAttorney.cs` | Aggregate root entity — 2 FKs, multi-tenant, 8 string fields |
| Domain | `src/.../Domain/ApplicantAttorneys/ApplicantAttorneyManager.cs` | DomainService — create/update with length validation |
| Domain | `src/.../Domain/ApplicantAttorneys/ApplicantAttorneyWithNavigationProperties.cs` | Projection wrapper — State + IdentityUser nav props |
| Domain | `src/.../Domain/ApplicantAttorneys/IApplicantAttorneyRepository.cs` | Custom repo interface — nav-prop queries, filter by FirmName/PhoneNumber/City/StateId/IdentityUserId |
| Contracts | `src/.../Application.Contracts/ApplicantAttorneys/ApplicantAttorneyCreateDto.cs` | Creation input — all fields optional except IdentityUserId |
| Contracts | `src/.../Application.Contracts/ApplicantAttorneys/ApplicantAttorneyUpdateDto.cs` | Update input — implements IHasConcurrencyStamp |
| Contracts | `src/.../Application.Contracts/ApplicantAttorneys/ApplicantAttorneyDto.cs` | Full output DTO with concurrency stamp |
| Contracts | `src/.../Application.Contracts/ApplicantAttorneys/ApplicantAttorneyWithNavigationPropertiesDto.cs` | Rich output with State + IdentityUser nav props |
| Contracts | `src/.../Application.Contracts/ApplicantAttorneys/GetApplicantAttorneysInput.cs` | Filter input — FirmName, PhoneNumber, City, StateId, IdentityUserId |
| Contracts | `src/.../Application.Contracts/ApplicantAttorneys/IApplicantAttorneysAppService.cs` | Service interface — 8 methods including State and IdentityUser lookups |
| Application | `src/.../Application/ApplicantAttorneys/ApplicantAttorneysAppService.cs` | CRUD + lookups, permission-gated Create/Edit/Delete, IdentityUserId required validation |
| EF Core | `src/.../EntityFrameworkCore/ApplicantAttorneys/EfCoreApplicantAttorneyRepository.cs` | 2-way LEFT JOIN (State, IdentityUser), text filter on FirmName/PhoneNumber/City |
| HttpApi | `src/.../HttpApi/Controllers/ApplicantAttorneys/ApplicantAttorneyController.cs` | Manual controller (8 endpoints) at `api/app/applicant-attorneys` — delegates to AppService |
| Angular | `angular/src/app/applicant-attorneys/` | List page + detail modal (abstract/concrete pattern), routes with permissionGuard |
| Proxy | `angular/src/app/proxy/applicant-attorneys/` | Auto-generated REST client (12 methods including file upload/download) |

## Entity Shape

```
ApplicantAttorney : FullAuditedAggregateRoot<Guid>, IMultiTenant
├── TenantId      : Guid?              (tenant isolation)
├── FirmName      : string? [max 50]   (law firm name)
├── FirmAddress   : string? [max 100]  (firm mailing address)
├── WebAddress    : string? [max 100]  (firm website URL)
├── PhoneNumber   : string? [max 20]   (contact phone)
├── FaxNumber     : string? [max 19]   (contact fax)
├── Street        : string? [max 255]  (street address)
├── City          : string? [max 50]   (city)
├── ZipCode       : string? [max 10]   (postal code)
├── StateId       : Guid?              (FK → State, optional)
└── IdentityUserId : Guid              (FK → IdentityUser, required)
```

No status/state enum fields. All string fields are optional.

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `StateId` | State | SetNull | Optional. Host-scoped lookup for address state |
| `IdentityUserId` | IdentityUser | NoAction | Required. The user account for this attorney |

**Related entities** (not FKs on ApplicantAttorney, but linked to it):
- `AppointmentApplicantAttorney` → join entity linking ApplicantAttorney to Appointment (has `ApplicantAttorneyId` FK back)

## Multi-tenancy

**IMultiTenant: Yes.** Attorney records are tenant-scoped — each tenant manages its own attorneys.

- DbContext config is **outside** `IsHostDatabase()` block — exists in both `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext`
- StateId FK points to a **host-scoped** entity (State has no `IMultiTenant`)
- `EfCoreApplicantAttorneyRepository` relies on ABP's automatic tenant filter

## Mapper Configuration

In `src/.../Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly):

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `ApplicantAttorneyToApplicantAttorneyDtoMappers` | `ApplicantAttorney` → `ApplicantAttorneyDto` | No |
| `ApplicantAttorneyWithNavigationPropertiesToApplicantAttorneyWithNavigationPropertiesDtoMapper` | `ApplicantAttorneyWithNavigationProperties` → `ApplicantAttorneyWithNavigationPropertiesDto` | No |
| `ApplicantAttorneyToLookupDtoGuidMapper` | `ApplicantAttorney` → `LookupDto<Guid>` | Yes — sets `DisplayName = source.FirmName` |

All use `[Mapper]` attribute with `MapperBase<TSource, TDest>` inheritance.

## Permissions

Defined in `CaseEvaluationPermissions.cs`:
```
CaseEvaluation.ApplicantAttorneys          (Default — menu visibility + list/get)
CaseEvaluation.ApplicantAttorneys.Create   (CreateAsync)
CaseEvaluation.ApplicantAttorneys.Edit     (UpdateAsync)
CaseEvaluation.ApplicantAttorneys.Delete   (DeleteAsync)
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` as parent + 3 children.

**Note:** Unlike Appointments, Create/Edit/Delete permissions ARE properly enforced in the AppService via `[Authorize(Permission)]` attributes on each method.

## Business Rules

1. **IdentityUserId is required** — both `CreateAsync` and `UpdateAsync` throw `UserFriendlyException` if `IdentityUserId == default`. This is validated at the AppService level, not just via DTO annotations.

2. **All string fields are optional** — unlike some entities, no firm or contact fields are required. An attorney record can exist with just an IdentityUserId.

3. **State lookup filters by name** — `GetStateLookupAsync` searches State entities by `Name.Contains(filter)`.

4. **IdentityUser lookup filters by email** — `GetIdentityUserLookupAsync` searches IdentityUser by `Email.Contains(filter)`, not by username or name.

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `AppointmentApplicantAttorney.ApplicantAttorneyId` | NoAction | No | Required FK — many appointments can reference one attorney |

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| ApplicantAttorneyComponent | `angular/src/app/applicant-attorneys/applicant-attorney/components/applicant-attorney.component.ts` | `/applicant-attorneys` | List view with filtering and table |
| AbstractApplicantAttorneyComponent | `angular/src/app/applicant-attorneys/applicant-attorney/components/applicant-attorney.abstract.component.ts` | — | Base directive with CRUD wiring |
| ApplicantAttorneyDetailModalComponent | `angular/src/app/applicant-attorneys/applicant-attorney/components/applicant-attorney-detail.component.ts` | — | Modal form for create/edit |

**Pattern:** ABP Suite abstract/concrete (`AbstractApplicantAttorneyComponent` → `ApplicantAttorneyComponent`)

**Forms:**
- firmName: text (maxLength: 50, autofocus)
- firmAddress: text (maxLength: 100)
- phoneNumber: text (maxLength: 20)
- webAddress: text (maxLength: 100)
- faxNumber: text (maxLength: 19)
- street: text (maxLength: 255)
- city: text (maxLength: 50)
- zipCode: text (maxLength: 10)
- stateId: lookup select (`getStateLookup`)
- identityUserId: lookup select (required)

**Permission guards:**
- Route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.ApplicantAttorneys`)
- `*abpPermission="'CaseEvaluation.ApplicantAttorneys.Create'"` — create button
- `*abpPermission="'CaseEvaluation.ApplicantAttorneys.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.ApplicantAttorneys.Delete'"` — delete action

**Services injected:**
- `ListService` (@abp/ng.core), `ApplicantAttorneyViewService`, `ApplicantAttorneyDetailViewService`, `PermissionService`, `ConfirmationService`

## Known Gotchas

1. **Constructor sets 3/8 string fields; remaining set post-construction by Manager (code-gen artifact)** — `ApplicantAttorney(id, stateId, identityUserId, firmName, firmAddress, phoneNumber)` sets FirmName, FirmAddress, PhoneNumber via constructor. WebAddress, FaxNumber, Street, City, ZipCode are set directly in `Manager.CreateAsync()`.

2. **No tests** — no test files found for ApplicantAttorneys in any test project.

3. **Proxy has unused file operations** — proxy service includes `getDownloadToken`, `getFile`, `uploadFile` methods not wired in the Angular UI.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern--appointments](/CLAUDE.md#reference-pattern--appointments)
- Docs: [docs/features/applicant-attorneys/overview.md](/docs/features/applicant-attorneys/overview.md) (if exists)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
