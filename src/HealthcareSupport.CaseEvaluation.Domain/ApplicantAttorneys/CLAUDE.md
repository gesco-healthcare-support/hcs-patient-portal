# ApplicantAttorneys

Attorney contact and firm information for workers' comp applicant attorneys. Linked to an IdentityUser account and optionally to a State for address purposes. Used in the appointment booking flow to associate attorneys with appointments via the `AppointmentApplicantAttorney` join entity.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/ApplicantAttorneys/ApplicantAttorneyConsts.cs` | Max lengths (FirmName=50, FirmAddress=100, WebAddress=100, PhoneNumber=20, FaxNumber=19, Street=255, City=50, ZipCode=10) + default sort `CreationTime desc` |
| Domain | `src/.../Domain/ApplicantAttorneys/ApplicantAttorney.cs` | Aggregate root entity, IMultiTenant, 8 string fields + 2 FKs |
| Domain | `src/.../Domain/ApplicantAttorneys/ApplicantAttorneyManager.cs` | DomainService -- Create/Update with `Check.Length` validation on every string field |
| Domain | `src/.../Domain/ApplicantAttorneys/ApplicantAttorneyWithNavigationProperties.cs` | Projection wrapper -- State + IdentityUser nav props |
| Domain | `src/.../Domain/ApplicantAttorneys/IApplicantAttorneyRepository.cs` | Custom repo interface -- nav-prop queries, filter by FirmName/PhoneNumber/City/StateId/IdentityUserId |
| Contracts | `src/.../Application.Contracts/ApplicantAttorneys/ApplicantAttorneyCreateDto.cs` | Creation input -- all string fields optional, IdentityUserId required |
| Contracts | `src/.../Application.Contracts/ApplicantAttorneys/ApplicantAttorneyUpdateDto.cs` | Update input -- implements IHasConcurrencyStamp |
| Contracts | `src/.../Application.Contracts/ApplicantAttorneys/ApplicantAttorneyDto.cs` | Full output DTO -- FullAuditedEntityDto + ConcurrencyStamp |
| Contracts | `src/.../Application.Contracts/ApplicantAttorneys/ApplicantAttorneyWithNavigationPropertiesDto.cs` | Rich output with State + IdentityUser nav props |
| Contracts | `src/.../Application.Contracts/ApplicantAttorneys/GetApplicantAttorneysInput.cs` | Filter input -- FilterText, FirmName, PhoneNumber, City, StateId, IdentityUserId |
| Contracts | `src/.../Application.Contracts/ApplicantAttorneys/IApplicantAttorneysAppService.cs` | Service interface -- 8 methods (CRUD + 2 lookups + 2 nav-prop reads) |
| Application | `src/.../Application/ApplicantAttorneys/ApplicantAttorneysAppService.cs` | CRUD + lookups, permission-gated Create/Edit/Delete, IdentityUserId required guard |
| EF Core | `src/.../EntityFrameworkCore/ApplicantAttorneys/EfCoreApplicantAttorneyRepository.cs` | 2-way LEFT JOIN (State, IdentityUser); text filter on FirmName/PhoneNumber/City |
| HttpApi | `src/.../HttpApi/Controllers/ApplicantAttorneys/ApplicantAttorneyController.cs` | Manual controller (8 endpoints) at `api/app/applicant-attorneys` -- delegates to AppService |
| Tests | `test/.../Application.Tests/ApplicantAttorneys/ApplicantAttorneysAppServiceTests.cs` | 12 xUnit + Shouldly tests -- CRUD, validation, IMultiTenant isolation, IdentityUser email lookup |
| Angular | `angular/src/app/applicant-attorneys/` | List page + detail modal (abstract/concrete pattern), routes with permissionGuard (12 .ts/.html files) |
| Proxy | `angular/src/app/proxy/applicant-attorneys/` | Auto-generated REST client (12 methods including unused file upload/download) |

## Entity Shape

```
ApplicantAttorney : FullAuditedAggregateRoot<Guid>, IMultiTenant
- TenantId       : Guid?              (tenant isolation)
- FirmName       : string? [max 50]   (law firm name)
- FirmAddress    : string? [max 100]  (firm mailing address)
- WebAddress     : string? [max 100]  (firm website URL)
- PhoneNumber    : string? [max 20]   (contact phone)
- FaxNumber      : string? [max 19]   (contact fax)
- Street         : string? [max 255]  (street address)
- City           : string? [max 50]   (city)
- ZipCode        : string? [max 10]   (postal code)
- StateId        : Guid?              (FK -> State, optional)
- IdentityUserId : Guid               (FK -> IdentityUser, required)
```

No status/state enum fields. All string fields are optional.

**Intent note:** `docs/product/applicant-attorneys.md` (2026-04-24) confirms the MVP intent is symmetric portal experience for applicant AND defense attorneys -- both have a saved firm profile that pre-fills on every booking. The current code only models `ApplicantAttorney`; defense attorneys exist solely as `IdentityUser` + role with no domain entity. Closing that asymmetry (mirror entity vs. unified `Attorney` with side flag) is a developer-side architecture decision tracked in the gap analysis.

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `StateId` | State | SetNull | Optional. Host-scoped lookup for address state |
| `IdentityUserId` | IdentityUser | NoAction | Required. The user account for this attorney |

**Related entities** (not FKs on `ApplicantAttorney`, but linked to it):
- `AppointmentApplicantAttorney` -- join entity linking `ApplicantAttorney` to `Appointment` (carries `ApplicantAttorneyId` FK back to this entity).

## Multi-tenancy

**IMultiTenant: Yes.** Attorney records are tenant-scoped -- each tenant manages its own attorneys.

- DbContext config is **outside** any `IsHostDatabase()` guard -- the same `builder.Entity<ApplicantAttorney>` block exists in both `CaseEvaluationDbContext` (line 245) and `CaseEvaluationTenantDbContext` (line 155).
- `StateId` FK points to a host-scoped entity (`State` has no `IMultiTenant`); `SetNull` on State delete drops the address pointer.
- `EfCoreApplicantAttorneyRepository` relies on ABP's automatic `IMultiTenant` filter -- tests under `_currentTenant.Change(...)` confirm tenant isolation works (TenantA sees Attorney1 only, TenantB sees Attorney2 only).
- Host-admin cross-tenant access uses `IDataFilter.Disable<IMultiTenant>()`, same pattern as Doctors.

## Mapper Configuration

In `src/.../Application/CaseEvaluationApplicationMappers.cs` (Riok.Mapperly):

| Mapper Class | Source -> Destination | AfterMap? |
|---|---|---|
| `ApplicantAttorneyToApplicantAttorneyDtoMappers` | `ApplicantAttorney` -> `ApplicantAttorneyDto` | No |
| `ApplicantAttorneyWithNavigationPropertiesToApplicantAttorneyWithNavigationPropertiesDtoMapper` | `ApplicantAttorneyWithNavigationProperties` -> `ApplicantAttorneyWithNavigationPropertiesDto` | No (`RequiredMappingStrategy.None`) |
| `ApplicantAttorneyToLookupDtoGuidMapper` | `ApplicantAttorney` -> `LookupDto<Guid>` | Yes -- `destination.DisplayName = source.FirmName ?? string.Empty` |

All inherit `MapperBase<TSource, TDest>` and use `[Mapper]`.

## Permissions

Defined in `CaseEvaluationPermissions.cs`:
```
CaseEvaluation.ApplicantAttorneys          (Default -- menu visibility + list/get)
CaseEvaluation.ApplicantAttorneys.Create   (CreateAsync)
CaseEvaluation.ApplicantAttorneys.Edit     (UpdateAsync)
CaseEvaluation.ApplicantAttorneys.Delete   (DeleteAsync)
```

Registered in `CaseEvaluationPermissionDefinitionProvider.cs` as parent + 3 children
(`var applicantAttorneyPermission = myGroup.AddPermission(...); applicantAttorneyPermission.AddChild(...)` x3).

The class-level `[Authorize(CaseEvaluationPermissions.ApplicantAttorneys.Default)]` gates all reads, and `Create`/`Edit`/`Delete` permissions are individually enforced on each mutation method via per-method `[Authorize(...)]` attributes.

## Business Rules

1. **`IdentityUserId` is required at the AppService boundary.** Both `CreateAsync` and `UpdateAsync` throw `UserFriendlyException` (with localized "The {0} field is required." message) if `input.IdentityUserId == Guid.Empty`. The DTO declares `Guid IdentityUserId` (non-nullable), but `Guid.Empty` is the runtime sentinel the AppService rejects -- DTO annotations alone do not enforce this.
2. **All string fields are optional.** No firm or contact field is required; an attorney can exist with only `IdentityUserId`.
3. **Length validation is enforced twice in the create path:** the Manager runs `Check.Length` on every string field before constructing the entity, and the entity constructor re-runs `Check.Length` on its three constructor-path fields (`FirmName`, `FirmAddress`, `PhoneNumber`). The post-construction five (`WebAddress`, `FaxNumber`, `Street`, `City`, `ZipCode`) are validated only by the Manager.
4. **State lookup filters by `Name.Contains(filter)`.** `GetStateLookupAsync` does a substring match on `State.Name` -- not exact match, not by abbreviation/code.
5. **IdentityUser lookup filters by `Email.Contains(filter)`, NOT by username/name.** `GetIdentityUserLookupAsync` matches `IdentityUser.Email` only (test `GetIdentityUserLookupAsync_FiltersByEmail_NotByUsername` asserts this directly, using `@test.local` as a filter that hits emails but not usernames).
6. **`UpdateAsync` accepts every settable field.** No frozen fields -- `StateId`, `IdentityUserId`, all 8 string fields, and `ConcurrencyStamp` flow into `Manager.UpdateAsync`. `ConcurrencyStamp` is applied via `SetConcurrencyStampIfNotNull`.
7. **Default sort is `CreationTime desc`.** Newest attorneys first; `ApplicantAttorneyConsts.GetDefaultSorting(bool withEntityName)` returns `"ApplicantAttorney.CreationTime desc"` (nav-prop query) or `"CreationTime desc"` (flat query).

## Inbound FKs

| Source Entity.Property | Delete Behavior | Host-only? | Notes |
|---|---|---|---|
| `AppointmentApplicantAttorney.ApplicantAttorneyId` | NoAction | No -- configured in both contexts (tenant-scoped) | Required FK; many appointment-attorney joins can reference one attorney |

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| `ApplicantAttorneyComponent` | `angular/src/app/applicant-attorneys/applicant-attorney/components/applicant-attorney.component.ts` | `/applicant-attorneys` | List view with filtering, table, action dropdown |
| `AbstractApplicantAttorneyComponent` | `angular/src/app/applicant-attorneys/applicant-attorney/components/applicant-attorney.abstract.component.ts` | -- | Base directive with CRUD wiring (list, create, update, delete) |
| `ApplicantAttorneyDetailModalComponent` | `angular/src/app/applicant-attorneys/applicant-attorney/components/applicant-attorney-detail.component.ts` | -- | Modal form for create/edit |

**Pattern:** ABP Suite abstract/concrete (`AbstractApplicantAttorneyComponent` -> `ApplicantAttorneyComponent`).

**Forms (detail modal):**
- `firmName`: text input (maxlength 50, autofocus)
- `firmAddress`: text input (maxlength 100)
- `phoneNumber`: text input (maxlength 20)
- `stateId`: `abp-lookup-select` (uses `service.getStateLookup`)
- `identityUserId`: `abp-lookup-select` (uses `service.getIdentityUserLookup`, label marked `*` -- required)

Note: the modal template only renders the five fields above. The remaining backend fields (`webAddress`, `faxNumber`, `street`, `city`, `zipCode`) are present in the DTOs and the proxy service but are not exposed in the UI form -- `Manager.UpdateAsync` will write them as `null` if the modal is the only edit path.

**List filters (page):** `firmName`, `phoneNumber`, `city` (text), `stateId`, `identityUserId` (lookup-select).

**Permission guards:**
- Route (`applicant-attorney-routes.ts`): `[authGuard, permissionGuard]`. Required policy declared in `applicant-attorney-base.routes.ts` as `requiredPolicy: 'CaseEvaluation.ApplicantAttorneys'`.
- `*abpPermission="'CaseEvaluation.ApplicantAttorneys.Create'"` -- New button.
- `*abpPermission="'CaseEvaluation.ApplicantAttorneys.Edit'"` -- Edit dropdown item.
- `*abpPermission="'CaseEvaluation.ApplicantAttorneys.Delete'"` -- Delete dropdown item.
- `AbstractApplicantAttorneyComponent.checkActionButtonVisibility()` hides the Actions column entirely when neither Edit nor Delete is granted (uses `PermissionService.getGrantedPolicy`).

**Services injected** (abstract component): `ListService`, `ApplicantAttorneyViewService`, `ApplicantAttorneyDetailViewService`, `PermissionService`.

## Known Gotchas

1. **Constructor sets 3/8 string fields; remaining 5 set post-construction by Manager (code-gen artifact).** `ApplicantAttorney(Guid id, Guid? stateId, Guid identityUserId, string? firmName, string? firmAddress, string? phoneNumber)` sets `FirmName`, `FirmAddress`, `PhoneNumber` (all guarded by `Check.Length` inside the ctor). `WebAddress`, `FaxNumber`, `Street`, `City`, `ZipCode` are assigned directly in `Manager.CreateAsync()` after the ctor returns. Tests `CreateAsync_SetsAllEightStringFields_AndBothFks` and `ApplicantAttorneyManager_CreateAsync_WhenWebAddressExceedsMax_ThrowsArgumentException` document and assert this split.
2. **Modal form omits 5 backend fields.** The detail modal exposes only 5 of 10 settable fields (see Angular UI Surface). The proxy `getList` params object also lists `firmAddress`, `webAddress`, `faxNumber`, `street`, `zipCode` as filter parameters that the backend `GetApplicantAttorneysInput` does NOT accept -- those names are silently dropped server-side. Either the modal must grow or the backend must accept the extra filters; today they are inconsistent.
3. **Proxy has unused file operations.** `getDownloadToken`, `getFile`, `uploadFile` exist in `applicant-attorney.service.ts` (proxy) but the controller has no matching endpoints and the Angular UI does not call them. Code-gen leftover -- safe to ignore until file attachments are scoped.
4. **Domain entity collapses firm-level and attorney-level data.** Two attorneys at the same firm carry duplicated `FirmName`/`FirmAddress`/`WebAddress`/`PhoneNumber`/`FaxNumber`/`Street`/`City`/`ZipCode`. Whether to extract a `Firm` entity is an open intent question -- see `docs/product/applicant-attorneys.md` "Open" rules.
5. **Defense attorneys have no domain entity today.** Per the gap analysis (FEAT-02:193) and the product intent doc, MVP requires symmetric defense-attorney support (saved firm profile, pre-fill on booking). The current single-entity model is an intent/code gap, not a deliberate asymmetry. Resolution path is a developer-side architecture decision: parallel `DefenseAttorney` entity vs. unified `Attorney` with a side/type flag.

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)
- Reference pattern: [CLAUDE.md#reference-pattern--appointments](/CLAUDE.md#reference-pattern--appointments)
- Product intent: [docs/product/applicant-attorneys.md](/docs/product/applicant-attorneys.md)
- Docs: [docs/features/applicant-attorneys/overview.md](/docs/features/applicant-attorneys/overview.md) (if exists)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
