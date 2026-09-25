# MultiTenancy -- cross-office work iteration (database-per-office infra)

Cross-cutting infrastructure for running work across every office under the
database-per-office model. Background jobs and the host dashboard cannot rely on the
ambient tenant (they run host-scoped or unattended), so they iterate offices explicitly
through this seam, which scopes each unit of work to one office's database in turn.

## What lives here

| File | Purpose |
|---|---|
| `ITenantWorkRunner.cs` | Seam: `ForEachOfficeAsync(work)` runs an action once per office; `AggregateAcrossOfficesAsync(selector)` collects one result per office. |
| `TenantWorkRunner.cs` | Default impl: reads office ids from the tenant registry at host scope (`CurrentTenant.Change(null)`), then runs each item under `CurrentTenant.Change(officeId)`. |

## Conventions

- Used by the C2 background jobs (reminders, digests, cleanup) and the C3 dashboard
  host-aggregation so each office's data is processed in its own database, scoped to that
  office only.
- The per-office connection-string seam itself lives in `Domain/Data`
  (`ITenantConnectionStringProvider`); routing of tenant queries stays with ABP's stock
  `MultiTenantConnectionStringResolver`. This folder is iteration, not routing.
- Isolation is verified by the Phase F multi-office test harness
  (`test/.../MultiOffice/`), whose runner test asserts both offices are visited and each
  sees only its own data.
