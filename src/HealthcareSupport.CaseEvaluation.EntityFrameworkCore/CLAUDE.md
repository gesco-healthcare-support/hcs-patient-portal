# EntityFrameworkCore -- DbContexts, migrations, custom repos

EF Core persistence layer. Only project that touches SQL Server directly.

## What Lives Here

- `EntityFrameworkCore/CaseEvaluationDbContext.cs` -- host context (`MultiTenancySides.Both`); all entity `OnModelCreating` config is inline here
- `EntityFrameworkCore/CaseEvaluationTenantDbContext.cs` -- tenant context (`MultiTenancySides.Tenant`); entity config duplicated verbatim for every both-side entity
- `EntityFrameworkCore/CaseEvaluationEntityFrameworkCoreModule.cs` -- registers DbContexts and custom repos via `options.AddRepository<>`
- `Migrations/` -- code-first migrations; see docs/database/MIGRATION-GUIDE.md for the `dotnet ef` command and required project flags
- `{Feature}/EfCore{Entity}Repository.cs` -- custom repo impl per feature

## Conventions

### Dual-context entity config (IMPORTANT)

Every entity that lives in both the host DB and the tenant DB must have its `OnModelCreating`
block duplicated verbatim in `CaseEvaluationTenantDbContext`. Adding a both-side entity
requires edits in both files. Host-only entities (e.g. `Location`, `WcabOffice`,
`AppointmentType`, `AppointmentStatus`, `AppointmentLanguage`, `State`, `Patient`, `Doctor`)
are wrapped in `if (builder.IsHostDatabase())` in the host context and have NO block in the
tenant context. See docs/decisions/003-dual-dbcontext-host-tenant.md.

### Patient IS IMultiTenant, and the explicit filters stay anyway (IMPORTANT)

`Patient` implements `IMultiTenant` as of FEAT-09 (ADR-006 T4, 2026-05-05), so ABP's automatic
tenant filter DOES apply. Before that change it carried a manual `TenantId` column with no
auto-filter, which let any caller holding the Patients permission read every tenant's patients.

**This section previously said the opposite, and that error outlived the fix by four months.**
It was cited by production code (`AppointmentsAppService`) as the reason for a scoping decision.
If you are reading a claim about tenancy anywhere in this repository, check the entity.

`EfCorePatientRepository` still applies an explicit `TenantId` filter on its custom queries.
**Keep it.** It is defence in depth, not redundancy, and two of those methods take the tenant as
a parameter so a host-scope caller can name the office deliberately. Do not remove an explicit
filter on the grounds that the framework now covers it.

See docs/security/DATA-FLOWS.md.

### Repo registration

A new `IRepository<T>` for a custom type requires both:

1. An `EfCore{Entity}Repository` class in the appropriate feature folder.
2. An `options.AddRepository<T, EfCore{Entity}Repository>()` call in the module.

Missing step 2 means DI resolves the untyped default repo, bypassing your custom joins.
`CaseEvaluationTenantDbContext` uses only `AddDefaultRepositories`.

### AppointmentPacket unique index

The index on `(TenantId, AppointmentId, Kind)` carries the filter
`[IsDeleted] = 0 AND [TenantId] IS NOT NULL`. Both conditions are required:

- `IsDeleted = 0` -- lets a soft-deleted row be replaced by a regenerated INSERT (BUG-036).
- `TenantId IS NOT NULL` -- excludes any host-scoped test rows from the constraint.
This index is declared in both DbContexts.

### Navigation property pattern

Custom repo methods use explicit LINQ joins, not navigation properties, to populate
`{Entity}WithNavigationPropertiesDto`. This avoids lazy-loading and projection pitfalls.

## Gotchas

- CaseEvaluationDbContextModelCreatingExtensions.cs does NOT exist. (Written without backticks on
  purpose: in this file a backticked path means "this exists now", which is what lets the CI drift
  checker verify every one of them. A name being documented as absent must not wear them.) It is
  the ABP template's usual home for entity configuration, so people look for it. All entity config
  is inline in `OnModelCreating` in each DbContext file -- do not create or reference that class.
- Filtered indexes (`HasFilter(...)`) are SQL Server syntax; they silently become no-ops on
  SQLite-backed test runners. Verify constraint behavior against SQL Server for uniqueness rules.
- Doctor has a filtered unique index on `TenantId` (`[TenantId] IS NOT NULL AND [IsDeleted] = 0`)
  enforcing one-doctor-per-tenant. It is declared in both DbContexts.

## Related

- docs/decisions/003-dual-dbcontext-host-tenant.md
- docs/security/DATA-FLOWS.md
- docs/database/EF-CORE-DESIGN.md
- docs/database/SCHEMA-REFERENCE.md
- docs/database/MIGRATION-GUIDE.md
