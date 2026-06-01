# EntityFrameworkCore -- DbContexts, migrations, custom repos

EF Core persistence layer. Only project that touches SQL Server directly.

## What Lives Here

- `EntityFrameworkCore/CaseEvaluationDbContext.cs` -- host context (`MultiTenancySides.Both`); all entity `OnModelCreating` config is inline here
- `EntityFrameworkCore/CaseEvaluationTenantDbContext.cs` -- tenant context (`MultiTenancySides.Tenant`); entity config duplicated verbatim for every both-side entity
- `EntityFrameworkCore/CaseEvaluationEntityFrameworkCoreModule.cs` -- registers DbContexts and custom repos via `options.AddRepository<>`
- `Migrations/` -- code-first migrations; added via `dotnet ef migrations add`
- `{Feature}/EfCore{Entity}Repository.cs` -- custom repo impl per feature

## Conventions

### Dual-context entity config (IMPORTANT)

Every entity that lives in both the host DB and the tenant DB must have its `OnModelCreating`
block duplicated verbatim in `CaseEvaluationTenantDbContext`. Adding a both-side entity
requires edits in both files. Host-only entities (e.g. `Location`, `WcabOffice`,
`AppointmentType`, `AppointmentStatus`, `AppointmentLanguage`, `State`, `Patient`, `Doctor`)
are wrapped in `if (builder.IsHostDatabase())` in the host context and have NO block in the
tenant context. See docs/decisions/003-dual-dbcontext-host-tenant.md.

### Patient is NOT IMultiTenant -- PHI leak risk (IMPORTANT)

`Patient` does not implement `IMultiTenant`, so ABP's automatic tenant filter does NOT apply.
Every `Patient` query in a custom repository MUST add `.Where(p => p.TenantId == currentTenantId)`
manually. Omitting this filter exposes PHI across tenants. See docs/security/DATA-FLOWS.md.

### Repo registration

14 custom repos are registered via `options.AddRepository<Entity, EfCoreImpl>()` on
`CaseEvaluationDbContext` (see module file). `CaseEvaluationTenantDbContext` uses only
`AddDefaultRepositories`. A new `IRepository<T>` for a custom type requires both:
1. An `EfCore{Entity}Repository` class in the appropriate feature folder.
2. An `options.AddRepository<T, EfCore{Entity}Repository>()` call in the module.
Missing step 2 means DI resolves the untyped default repo, bypassing your custom joins.

### AppointmentPacket unique index

The index on `(TenantId, AppointmentId, Kind)` carries the filter
`[IsDeleted] = 0 AND [TenantId] IS NOT NULL`. Both conditions are required:
- `IsDeleted = 0` -- lets a soft-deleted row be replaced by a regenerated INSERT (BUG-036).
- `TenantId IS NOT NULL` -- excludes any host-scoped test rows from the constraint.
This index is declared in both DbContexts.

### Migrations

Run from repo root:
```
dotnet ef migrations add <Name> \
  --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore \
  --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host
```
Run DbMigrator before starting services to apply and seed.

### Navigation property pattern

Custom repo methods use explicit LINQ joins, not navigation properties, to populate
`{Entity}WithNavigationPropertiesDto`. This avoids lazy-loading and projection pitfalls.

## Gotchas

- `CaseEvaluationDbContextModelCreatingExtensions.cs` does NOT exist. All entity config is
  inline in `OnModelCreating` in each DbContext file -- do not create or reference that class.
- Filtered indexes (`HasFilter(...)`) are SQL Server syntax; they silently become no-ops on
  SQLite-backed test runners. Verify constraint behavior against SQL Server for uniqueness rules.
- Doctor has a filtered unique index on `TenantId` (`[TenantId] IS NOT NULL AND [IsDeleted] = 0`)
  enforcing one-doctor-per-tenant. It is declared in both DbContexts.

## Related

- docs/decisions/003-dual-dbcontext-host-tenant.md
- docs/security/DATA-FLOWS.md
- docs/database/EF-CORE-DESIGN.md
- docs/database/SCHEMA-REFERENCE.md
