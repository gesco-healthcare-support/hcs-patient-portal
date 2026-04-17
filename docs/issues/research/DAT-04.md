[Home](../../INDEX.md) > [Issues](../) > Research > DAT-04

# DAT-04: Non-Transactional Tenant Creation -- Research

**Severity**: High
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application/Doctors/DoctorTenantAppService.cs` lines 49-71

---

## Current state (verified 2026-04-17)

```csharp
public override async Task<SaasTenantDto> CreateAsync(SaasTenantCreateDto input)
{
    Check.NotNull(input, nameof(input));
    // ... input validation ...

    SaasTenantDto tenant;
    using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
    {
        tenant = await base.CreateAsync(input);   // host DB: creates tenant row
        await uow.CompleteAsync();                // tenant COMMITTED here
    }
    using (CurrentTenant.Change(tenant.Id))
    {
        var adminUser = await CreateDoctorUserAsync(input);       // tenant DB
        await CreateDoctorProfileAsync(adminUser, input);         // tenant DB
        await EnsureRoleAsync("Doctor");                          // tenant DB
    }
    return tenant;
}
```

Failure window: if any of the three calls in the `CurrentTenant.Change` block throws, the tenant row is already committed in host DB with no associated admin user, no Doctor profile, no Doctor role. There is **no compensating delete** of the tenant.

This is a known ABP pattern failure mode -- see support threads below.

---

## Official documentation

- [ABP Unit of Work (latest)](https://abp.io/docs/latest/framework/architecture/domain-driven-design/unit-of-work) -- `IUnitOfWorkManager.Begin(requiresNew, isTransactional, isolationLevel, timeOut)`. `requiresNew: true` starts a new, isolated UoW; `isTransactional` defaults `false`.
- [AbpUnitOfWorkDefaultOptions.TransactionBehavior](https://abp.io/docs/abp/3.0/api/Volo.Abp.Uow.AbpUnitOfWorkDefaultOptions.html) -- enum is `Auto | Enabled | Disabled`. Default `Auto` starts a transaction on non-GET HTTP requests. (Note: NOT `Required/RequiresNew` -- that is MS `TransactionScope` terminology.)
- [Understanding Transactions in ABP Unit of Work](https://abp.io/community/articles/understanding-transactions-in-abp-unit-of-work-0r248xsr) -- ambient-scope reuse by default; non-transactional UoWs persist changes immediately and cannot be rolled back.
- [ABP Multi-Tenancy](https://abp.io/docs/latest/framework/architecture/multi-tenancy) -- `ICurrentTenant.Change(tenantId)` is canonical; tenant DB filter automatic inside that scope.
- [ABP Connection Strings](https://docs.abp.io/en/abp/latest/Connection-Strings) -- `IConnectionStringResolver` + `MultiTenantConnectionStringResolver` reads tenant connection from `ITenantStore`; falls back to host.
- [ABP Commercial SaaS module](https://docs.abp.io/en/commercial/latest/modules/saas) -- `TenantAppService`/`TenantManager` manage per-tenant connection strings; publishes `TenantEto` events; tenant DBs created/migrated lazily on first connection-string set.
- [ABP Data Migrations](https://abp.io/docs/latest/framework/data/entity-framework-core/migrations) -- documents the `TenantDatabaseMigrationHandler` pattern handling `TenantCreatedEto` / `TenantConnectionStringUpdatedEto`.
- [Volo.Abp.TenantManagement.TenantAppService source (OSS sibling of Volo.Saas)](https://github.com/abpframework/abp/blob/dev/modules/tenant-management/src/Volo.Abp.TenantManagement.Application/Volo/Abp/TenantManagement/TenantAppService.cs) -- `CreateAsync` calls `CurrentUnitOfWork.SaveChangesAsync()`, then `DistributedEventBus.PublishAsync(new TenantCreatedEto(...))`, then seeding in `using (CurrentTenant.Change(...))`. Relies on ambient Auto transaction. Confidence MEDIUM that `Volo.Saas.Host.TenantAppService` behaves identically -- Volo.Saas is closed source.
- [Azure Compensating Transaction pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/compensating-transaction) -- canonical pattern when strong distributed transaction is not feasible. Compensations must be **idempotent** and do not have to run in reverse order.
- [Azure Saga pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/saga) -- choreographed vs orchestrated variants.
- [SQL Server cross-DB distributed transactions](https://learn.microsoft.com/en-us/sql/database-engine/availability-groups/windows/configure-availability-group-for-distributed-transactions?view=sql-server-ver17) -- MSDTC required whenever `TransactionScope` opens connections to two different SQL Server databases. **Azure SQL does NOT support MSDTC.**

## Community findings

- [ABP Support #2240 -- Admin user not created when tenant is created (ABP 5.0 RC)](https://abp.io/support/questions/2240/Admin-user-not-created-when-tenant-is-created) -- root cause: `TenantDatabaseMigrationHandler` opened `Begin(requiresNew: true, isTransactional: true)` which could not resolve the just-created tenant's connection string. ABP's fix: change to `requiresNew: false` so ambient connection-string context is reused. Directly relevant.
- [ABP Support #7271 -- Admin user not added when creating new tenant from module (7.3.2)](https://abp.io/support/questions/7271/Admin-user-not-added-when-creating-new-tenant) -- `TenantDatabaseMigrationHandler.HandleEventAsync` only fires when creation goes through the tenant-management UI. Calling `TenantAppService.CreateAsync` from another module can miss the event. Confirms event-bus ordering is not a hard guarantee.
- [ABP Support #217 -- Seed tenant DB after being created using override of CreateAsync](https://abp.io/support/questions/217/Seed-tenant-database-after-being-created-using-override-of-CreateAsync) -- ABP's recommended pattern: `using (_currentTenant.Change(tenant.Id)) { await MigrateTenantDatabasesAsync(tenant); await _dataSeeder.SeedAsync(tenant.Id); }`. Thread **does not address rollback if seeding fails** -- documented gap.
- [ABP GH #11071 -- Auto-migrate on custom connection string (5.0)](https://github.com/abpframework/abp/issues/11071) -- canonical pattern uses `Begin(requiresNew: true, isTransactional: false)` around `MigrateAsync()`. Confirms `isTransactional: false` is intentional for migration UoWs (DDL breaks transactions).
- [ABP Support #4586 -- Tenant admin not getting created (7.0.1)](https://abp.io/support/questions/4586/Tenants-Admin-User-not-getting-create) -- same failure shape; cross-version recurrence = pattern is fragile.
- [ABP GH #22094 -- Multiple DB migration issues in multi-tenant](https://github.com/abpframework/abp/issues/22094) -- migration/seed error surface for separate-DB tenants.
- [EF Core Transactions Guide 2025](https://amarozka.dev/ef-transaction-csharp-examples/) + [MSDTC overview](https://thedbahub.com/understanding-msdtc-transactions-and-isolation-levels-in-sql-server/) -- opening two SQL Server connections to different DBs inside `TransactionScope` auto-promotes to MSDTC; unsupported on Azure SQL.

## Recommended approach

Three options, in increasing infrastructure cost:

1. **Compensation / saga in the AppService (lowest risk, most portable)** -- preferred for this stack. Keep the two UoWs as-is; wrap the second block in:
   ```csharp
   try { /* admin user + doctor profile + role */ }
   catch { await CompensateTenantAsync(tenant.Id); throw; }
   ```
   Make `CompensateTenantAsync` idempotent (safe to call if tenant already deleted). Matches [Azure compensating-transaction guidance](https://learn.microsoft.com/en-us/azure/architecture/patterns/compensating-transaction) and ABP's own posture (framework offers no built-in rollback -- see support #217).

2. **Outer `TransactionScope` with MSDTC** -- strong atomicity, heavy infra. Wrap both UoWs in a `TransactionScope` with `TransactionScopeAsyncFlowOption.Enabled`. Auto-promotes to MSDTC when touching two SQL Server DBs. Requires MSDTC on host; fails on Azure SQL; incompatible with DDL (migrations) -- which is precisely why ABP uses `isTransactional: false`. Not recommended.

3. **Event-driven with `TenantCreatedEto` + handler-owned rollback** -- let base `TenantAppService.CreateAsync` commit + publish; subscribe in a `TenantDatabaseMigrationHandler`-style handler; if seeding fails inside the handler, publish a `TenantCreationFailedEto` consumed by a compensator. Aligns with ABP's event plumbing but adds asynchrony -- the caller sees "success" before admin exists. Appropriate only if moving to microservices.

Recommendation: **Option 1.** Lowest-risk fit for single SQL Server + LocalDB dev + .NET 10.

## Gotchas / blockers

- `requiresNew: true` breaks tenant connection-string resolution for just-created tenants when the tenant row isn't visible to the new UoW's `ITenantStore` snapshot (root cause of #2240). Current code uses `requiresNew: true, isTransactional: false` only for the outer host block -- safe. Flagged because it will bite if flipped.
- `isTransactional: false` around migrations is intentional. EF Core migration batches issue DDL; mixing DDL with an ambient transaction plus MSDTC promotion is unreliable (ABP #11071). If you add an outer transaction for the tenant row, keep migration/seed OUTSIDE.
- **Event bus ordering is not atomic with tenant insert.** `TenantCreatedEto` publishes after `SaveChangesAsync`. If process crashes between save and publish, you orphan. ABP's outbox pattern is the mitigation if you go the distributed-event route.
- **Cross-DB transaction limits.** MSDTC requires Network DTC Access, per-host, NOT supported on Azure SQL. If tenant DBs ever move to Azure SQL, Option 2 is off the table.
- `[RemoteService(IsEnabled = false)]` + inheriting from `Volo.Saas.Host.TenantAppService` is the official extension path (per #217) -- but base-class changes in Volo.Saas minor versions can silently alter UoW wrapping. Regression-test on ABP upgrades.
- **Compensation is not truly atomic.** If `DeleteAsync` itself fails (DB down), you need retry + alert per Azure compensating-transaction pattern. "Eventual cleanup" via a reconcile job is the honest safety net.

## Open questions

- **Architecture**: is the solution using shared host DB or per-tenant DB? `CaseEvaluationTenantDbContext` exists (per root CLAUDE.md) but whether connection-string provisioning runs per tenant is unclear from the visible code.
- Does `Volo.Saas.Host.TenantAppService.CreateAsync` in 10.0.2 still mirror the OSS `Volo.Abp.TenantManagement.TenantAppService` flow? (INFERENCE from OSS sibling.)
- Does the solution register a `TenantDatabaseMigrationHandler`-equivalent for `TenantCreatedEto`? If yes, migrations run asynchronously vs. your admin/profile/role creation -- race risk.
- Reconcile / cleanup job: any plan for a periodic "orphan-tenant sweeper" detecting tenants with no admin users after N minutes? Valuable even with Option 1 in place.
- Downstream `TenantCreatedEto` consumers: moving to in-process compensation may break them. Audit event subscribers first.

## Related

- [docs/issues/DATA-INTEGRITY.md#dat-04](../DATA-INTEGRITY.md#dat-04-non-transactional-tenant-creation)
- [ARC-03](ARC-03.md) -- `DoctorTenantAppService.CreateDoctorProfileAsync` also hardcodes Gender
