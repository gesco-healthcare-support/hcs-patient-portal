# AppointmentDocumentTypes -- tenant-scoped document-category master

Per-tenant, per-appointment-type list of document categories. Restores (and
extends) the legacy `AppointmentDocumentType` lookup dropped at MVP. Internal
staff (IT Admin / Staff Supervisor) curate one list per office; the list drives
the document-upload picker on the appointment-documents form. Modeled on the
host-scoped `AppointmentStatuses` lookup-CRUD scaffold, but `IMultiTenant`
because each office owns its own list.

## What lives here

| Layer | Key file | Purpose |
|---|---|---|
| Domain | `AppointmentDocumentType.cs` | Aggregate root: `FullAuditedAggregateRoot<Guid>`, `IMultiTenant`; `Name`, `AppointmentTypeId?`, `IsSystem`, `IsActive` |
| Domain | `AppointmentDocumentTypeManager.cs` | Create/update/delete with name-uniqueness + reserved-row guards |
| Domain | `AppointmentDocumentTypeDataSeedContributor.cs` | Seeds one reserved `IsSystem` row ("Generated Packet") per tenant |
| Domain | `IAppointmentDocumentTypeRepository.cs` | `NameExistsAsync`, `GetListAsync`, `DeleteAllAsync` |
| Contracts | `Application.Contracts/AppointmentDocumentTypes/` | `*CreateDto`/`*UpdateDto`/`*Dto`, `Get*Input`, `IAppointmentDocumentTypesAppService` |
| Application | `Application/AppointmentDocumentTypes/AppointmentDocumentTypesAppService.cs` | CRUD + `DeleteByIdsAsync`/`DeleteAllAsync`; `[RemoteService(IsEnabled=false)]` |
| EF Core | `EntityFrameworkCore/AppointmentDocumentTypes/EfCoreAppointmentDocumentTypeRepository.cs` | Bound to `CaseEvaluationDbContext`; active-row case-insensitive uniqueness |
| HttpApi | `HttpApi/Controllers/AppointmentDocumentTypes/AppointmentDocumentTypeController.cs` | Manual controller `api/app/appointment-document-types` |
| Angular | `angular/src/app/appointment-document-types/` | Standalone admin page (list, create/edit modal, filters, bulk delete) |
| Tests | `*/AppointmentDocumentTypes/*ManagerTests.cs` | Tenant-stamp regression guard (see Conventions) |

## Conventions

### IMPORTANT: stamp TenantId explicitly on create

`AppointmentDocumentTypeManager.CreateAsync` MUST pass `tenantId: CurrentTenant.Id`
to the entity constructor. The constructor assigns `TenantId`, and ABP only
auto-stamps `IMultiTenant.TenantId` on insert for entities whose constructor
leaves it unset -- an explicitly-assigned value (including the default `null`)
is taken as-is. A plain create therefore persisted a `null`-tenant row that was
invisible to the tenant-scoped list, and the `null` tenant also tripped ABP's
`AuditPropertySetter` cross-tenant guard (it skips `CreatorId` when
`entity.TenantId != CurrentUser.TenantId`), so `CreatorId` came out null too.
The data seeder stamps its tenant the same explicit way. Pinned by
`AppointmentDocumentTypeManagerTests.Manager_CreateAsync_StampsCurrentTenantOnTheRow`.

### Reserved system row ("Generated Packet")

One `IsSystem = true` row named per `AppointmentDocumentTypeConsts.GeneratedPacketName`
is seeded per tenant. It is hidden from the upload picker and cannot be edited
or deleted: `AppointmentDocumentTypeManager.EnsureNotSystem` throws
`AppointmentDocumentTypeSystemReadOnly`. The bulk-delete paths
(`DeleteByIdsAsync`, `DeleteAllAsync`) route through the manager / a
`!IsSystem` filter so a hand-crafted request cannot delete it either.

### Name uniqueness -- active rows only

`NameExistsAsync` enforces uniqueness case-insensitively among `IsActive` rows
scoped to the same `AppointmentTypeId` (null is its own "applies to all types"
scope). A retired (`IsActive = false`) row does not block re-using its name --
intentional "retire then recreate" behavior.

### AppointmentTypeId is a loose reference (no FK)

`AppointmentType` is host-scoped and absent from tenant databases, so a FK
cannot span the two. `AppointmentTypeId` is a plain nullable `Guid` column;
null means the category applies to every appointment type.

### Dual-DbContext config

`AppointmentDocumentType` is `IMultiTenant`, so its `OnModelCreating` block is
declared in BOTH `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext`
(verbatim) -- NOT wrapped in `if (builder.IsHostDatabase())`. See
docs/decisions/003-dual-dbcontext-host-tenant.md.

### Permissions

`AppointmentDocumentTypes.Default` parent with `Create`/`Edit`/`Delete`
children; granted to IT Admin and Staff Supervisor only.

## Related

- docs/plans/2026-06-03-document-type-master.md
- docs/decisions/003-dual-dbcontext-host-tenant.md
