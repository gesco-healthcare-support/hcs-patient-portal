<!-- Last synced from src/HealthcareSupport.CaseEvaluation.Domain/WcabOffices/CLAUDE.md on 2026-04-08 -->

# WCAB Offices

Host-scoped lookup table for Workers' Compensation Appeals Board (WCAB) offices. Each office has a name, abbreviation, address, and active/inactive flag. Referenced by appointment workflows for jurisdiction tracking. Has Angular UI, bulk delete, and Excel export.

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

1. **Excel export is `[AllowAnonymous]`** -- `GetListAsExcelFileAsync` can be called without authentication. Potential security concern.
2. **Bulk delete** -- `DeleteByIdsAsync` and `DeleteAllAsync` available.

## Angular UI Surface

| Component | File | Route | Purpose |
|---|---|---|---|
| WcabOfficeComponent | `angular/src/app/wcab-offices/wcab-office/components/wcab-office.component.ts` | `/doctor-management/wcab-offices` | List view with bulk delete and Excel export |
| AbstractWcabOfficeComponent | `angular/src/app/wcab-offices/wcab-office/components/wcab-office.abstract.component.ts` | -- | Base directive with export logic |
| WcabOfficeDetailModalComponent | `angular/src/app/wcab-offices/wcab-office/components/wcab-office-detail.component.ts` | -- | Modal for create/edit |

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
- `*abpPermission="'CaseEvaluation.WcabOffices.Create'"` -- create button
- `*abpPermission="'CaseEvaluation.WcabOffices.Edit'"` -- edit action
- `*abpPermission="'CaseEvaluation.WcabOffices.Delete'"` -- delete (single and bulk)

**Services injected:**
- `ListService`, `WcabOfficeViewService`, `WcabOfficeDetailViewService`, `PermissionService`

## Known Gotchas

1. **AllowAnonymous on Excel export** -- anyone can download the WCAB office list
2. **No tests**

## Related Features

- [States](../states/overview.md) -- `StateId` FK references State (optional, SetNull)

## Links

- Feature CLAUDE.md: `src/HealthcareSupport.CaseEvaluation.Domain/WcabOffices/CLAUDE.md`
- Root architecture: [CLAUDE.md](../../../CLAUDE.md)
- UI detail: [ui.md](ui.md)

<!-- DOCS:MANUAL:START -->
<!-- DOCS:MANUAL:END -->
