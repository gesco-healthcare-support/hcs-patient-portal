# WcabOffices

Host-scoped lookup table for Workers' Compensation Appeals Board (WCAB) offices. Each office has a name, abbreviation, address, and active/inactive flag. Referenced by appointment workflows for jurisdiction tracking. Has Angular UI, bulk delete, and Excel export.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/.../Domain.Shared/WcabOffices/WcabOfficeConsts.cs` | Max lengths (Name=50, Abbreviation=50, Address=100, City=50, ZipCode=15) |
| Domain | `src/.../Domain/WcabOffices/WcabOffice.cs` | Aggregate root — no IMultiTenant |
| Domain | `src/.../Domain/WcabOffices/WcabOfficeManager.cs` | DomainService — create/update |
| Domain | `src/.../Domain/WcabOffices/IWcabOfficeRepository.cs` | Custom repo interface |
| Contracts | `src/.../Application.Contracts/WcabOffices/` | DTOs, service interface, Excel export DTO |
| Application | `src/.../Application/WcabOffices/WcabOfficesAppService.cs` | CRUD + bulk delete + Excel export |
| HttpApi | `src/.../HttpApi/Controllers/WcabOffices/WcabOfficeController.cs` | 11 endpoints at `api/app/wcab-offices` |
| Angular | `angular/src/app/wcab-offices/` | List + detail modal |

## Entity Shape

```
WcabOffice : FullAuditedAggregateRoot<Guid>     (NO IMultiTenant — host-scoped)
├── Name         : string [max 50, required]
├── Abbreviation : string [max 50, required]
├── Address      : string? [max 100]
├── City         : string? [max 50]
├── ZipCode      : string? [max 15]
├── IsActive     : bool
└── StateId      : Guid?   (FK → State, optional)
```

## Relationships

| FK Property | Target Entity | Delete Behavior | Notes |
|---|---|---|---|
| `StateId` | State | SetNull | Optional. Host-scoped |

## Multi-tenancy

**IMultiTenant: No.** Host-scoped. DbContext config inside `IsHostDatabase()`.

## Permissions

```
CaseEvaluation.WcabOffices          (Default)
CaseEvaluation.WcabOffices.Create
CaseEvaluation.WcabOffices.Edit
CaseEvaluation.WcabOffices.Delete   (covers Delete, DeleteByIds, DeleteAll)
```

## Mapper Configuration

| Mapper Class | Source → Destination | AfterMap? |
|---|---|---|
| `WcabOfficeToWcabOfficeDtoMappers` | Entity → DTO | No |
| `WcabOfficeToWcabOfficeExcelDtoMappers` | Entity → ExcelDto | No |
| `WcabOfficeWithNavProps...DtoMapper` | NavProps → NavPropsDto | No |

## Business Rules

1. **Excel export is `[AllowAnonymous]`** — `GetListAsExcelFileAsync` can be called without authentication. Potential security concern.
2. **Bulk delete** — `DeleteByIdsAsync` and `DeleteAllAsync` available.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| WcabOfficeComponent | `angular/src/app/wcab-offices/wcab-office/components/wcab-office.component.ts` | `/doctor-management/wcab-offices` | List view with bulk delete and Excel export |
| AbstractWcabOfficeComponent | `angular/src/app/wcab-offices/wcab-office/components/wcab-office.abstract.component.ts` | — | Base directive with export logic |
| WcabOfficeDetailModalComponent | `angular/src/app/wcab-offices/wcab-office/components/wcab-office-detail.component.ts` | — | Modal for create/edit |

**Pattern:** ABP Suite abstract/concrete with `exportToExcel()` method. Supports bulk selection and delete.

**Forms:**
- name: text (maxLength: 50, required)
- abbreviation: text (maxLength: 50, required)
- address: text (maxLength: 100)
- city: text (maxLength: 50)
- zipCode: text (maxLength: 15)
- isActive: checkbox
- stateId: lookup select

**Permission guards:**
- Route: `authGuard`, `permissionGuard` (requires `CaseEvaluation.WcabOffices`)
- `*abpPermission="'CaseEvaluation.WcabOffices.Create'"` — create button
- `*abpPermission="'CaseEvaluation.WcabOffices.Edit'"` — edit action
- `*abpPermission="'CaseEvaluation.WcabOffices.Delete'"` — delete (single and bulk)

**Services injected:**
- `ListService`, `WcabOfficeViewService`, `WcabOfficeDetailViewService`, `PermissionService`

## Known Gotchas

1. **AllowAnonymous on Excel export** — anyone can download the WCAB office list
2. **No tests**

## Links

- Root architecture: [CLAUDE.md](/CLAUDE.md)

<!-- MANUAL:START -->
<!-- MANUAL:END -->
