# EntityFrameworkCore Layer

EF Core DbContext definitions, migrations, and custom repository implementations. This is the only project that touches SQL Server directly.

## What Lives Here

- `EntityFrameworkCore/CaseEvaluationDbContext.cs` -- the main context (`MultiTenancySides.Both`), used by the host side for both host-only and tenant tables
- `EntityFrameworkCore/CaseEvaluationTenantDbContext.cs` -- tenant-only context used inside tenant sessions
- `Migrations/` -- EF Core code-first migrations
- One folder per feature (e.g. `Appointments/`, `Doctors/`) with custom repository implementations

## Conventions

1. **Dual DbContext.** `CaseEvaluationDbContext` covers both host and tenant tables; `CaseEvaluationTenantDbContext` is scoped to tenant-only data. Host-only entity configurations must be guarded with `if (builder.IsHostDatabase())` so they do not attempt to create tables in the tenant DB. See [ADR-003](../../docs/decisions/003-dual-dbcontext-host-tenant.md).
2. **Repositories use explicit LINQ joins, not navigation properties.** The `{Entity}WithNavigationPropertiesDto` pattern is populated by custom repository methods that compose joined LINQ queries. This avoids the lazy-loading and projection pitfalls of ABP's default repository.
3. **Migrations are added from the repo root**:
   ```bash
   dotnet ef migrations add <MigrationName> \
     --project src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore \
     --startup-project src/HealthcareSupport.CaseEvaluation.HttpApi.Host
   ```
4. **The DbMigrator applies migrations and seeds**. Do not rely on runtime `EnsureCreated()` or `Migrate()` calls -- run `dotnet run --project src/HealthcareSupport.CaseEvaluation.DbMigrator` before starting services.
5. **`IMultiTenant` filter is automatic.** Do not add manual `WHERE TenantId = X` predicates to tenant-scoped entities -- ABP applies the filter transparently. Exception: the `Patient` entity does NOT implement `IMultiTenant`, so its queries must manually include `TenantId` -- see [DATA-FLOWS.md](../../docs/security/DATA-FLOWS.md#cross-tenant-phi-risk-critical).

## Key Files

| File | Purpose |
|------|---------|
| `EntityFrameworkCore/CaseEvaluationDbContext.cs` | Host-side context; `MultiTenancySides.Both` |
| `EntityFrameworkCore/CaseEvaluationTenantDbContext.cs` | Tenant-only context |
| `EntityFrameworkCore/CaseEvaluationDbContextModelCreatingExtensions.cs` | Entity configuration (OnModelCreating) |
| `Migrations/` | Code-first migrations; add via `dotnet ef migrations add` |
| `{Feature}/Efcore{Entity}Repository.cs` | Custom repository implementations per feature |

## Related Docs

- [Root CLAUDE.md](../../CLAUDE.md) -- Database & Migrations section
- [docs/database/EF-CORE-DESIGN.md](../../docs/database/EF-CORE-DESIGN.md)
- [docs/database/SCHEMA-REFERENCE.md](../../docs/database/SCHEMA-REFERENCE.md)
- [docs/architecture/MULTI-TENANCY.md](../../docs/architecture/MULTI-TENANCY.md)
- [ADR-003: Dual DbContext](../../docs/decisions/003-dual-dbcontext-host-tenant.md)
