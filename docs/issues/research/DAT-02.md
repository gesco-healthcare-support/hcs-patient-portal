[Home](../../INDEX.md) > [Issues](../) > Research > DAT-02

# DAT-02: Duplicate Confirmation Numbers Possible -- Research

**Severity**: Critical
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs` lines 254-282

---

## Current state (verified 2026-04-17)

```csharp
private async Task<string> GenerateNextRequestConfirmationNumberAsync()
{
    var query = await _appointmentRepository.GetQueryableAsync();
    var latestNumber = await AsyncExecuter.FirstOrDefaultAsync(
        query.Where(x => x.RequestConfirmationNumber != null
                      && x.RequestConfirmationNumber.StartsWith(RequestConfirmationPrefix)
                      && x.RequestConfirmationNumber.Length == requiredLength)
             .OrderByDescending(x => x.RequestConfirmationNumber)
             .Select(x => x.RequestConfirmationNumber));
    // ... parse, +1, return "A" + n.ToString("D5")
}
```

Facts:

- Read-max-then-increment with no lock -- canonical anti-pattern.
- No UNIQUE index on `RequestConfirmationNumber` (see DAT-07 Medium).
- ABP tenant filter scopes the query automatically, so numbers are per-tenant (T1=A00001, T2=A00001 both exist).
- E2E test B15 did not reproduce a collision serially but code analysis confirms the vulnerability.

---

## Official documentation

- [SQL Server CREATE SEQUENCE](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-sequence-transact-sql) -- schema-bound numeric generator independent of tables; "application can obtain next sequence number without inserting the row by calling the NEXT VALUE FOR". **Gap caveat**: "Sequence numbers are generated outside the scope of the current transaction. They're consumed whether the transaction using the sequence number is committed or rolled back."
- [EF Core Sequences](https://learn.microsoft.com/en-us/ef/core/modeling/sequences) -- `modelBuilder.HasSequence<int>("OrderNumbers")` + `.HasDefaultValueSql("NEXT VALUE FOR OrderNumbers")`; configurable `StartsAt`, `IncrementsBy`, schema.
- [SQL Server Filtered Indexes](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-filtered-indexes) -- UNIQUE filtered indexes with a `WHERE` predicate. "Filters can't be applied to primary key or unique constraints, but can be applied to indexes with the UNIQUE property." Useful for per-tenant uniqueness.
- [EF Core EnableRetryOnFailure](https://learn.microsoft.com/dotnet/api/microsoft.entityframeworkcore.infrastructure.sqlserverdbcontextoptionsbuilder.enableretryonfailure) -- defaults: 6 retries, 30s max delay. Retries transient errors only; duplicate-key is NOT transient by default.
- [Polly RetryStrategyOptions](https://www.pollydocs.org/strategies/retry) -- `ShouldHandle = new PredicateBuilder().Handle<DbUpdateException>()` for wrapping duplicate-key retries.

## Community findings

- [Marios Siati -- Race Conditions and Entity Framework Core](https://medium.com/kpmg-uk-engineering/race-conditions-and-entity-framework-core-5f4ea8b308f6) -- describes exactly this pattern and failure mode.
- [Vladimir Khorikov -- Handling unique constraint violations](https://enterprisecraftsmanship.com/posts/handling-unique-constraint-violations/) -- keep upfront check for UX, make DB the source of truth; catch expected exceptions at lowest level and convert to Result.
- [Jonathan Crozier -- Catch and parse a SQL Server Duplicate Key Exception](https://jonathancrozier.com/blog/how-to-catch-and-parse-a-sql-server-duplicate-key-exception) -- parse `SqlException.Number == 2627 / 2601` to decide retry.
- [DevExpress XAF -- Sequential number for a persistent object with EF Core](https://supportcenter.devexpress.com/ticket/details/e2829/xaf-how-to-generate-a-sequential-number-for-a-persistent-object-within-a-database) -- counter-table pattern with `UPDLOCK` when contiguous (no-gap) numbers are required.

## Recommended approach

1. **Move to SQL SEQUENCE** (preferred when gaps are acceptable). Create a per-tenant or host-level sequence; read via `NEXT VALUE FOR`; format in app as `"A" + n.ToString("D5")`. If per-tenant is required, either create a sequence per tenant (operationally heavy) or a single sequence + filtered unique index on `(TenantId, RequestConfirmationNumber)`.
2. **Add a UNIQUE (filtered) index on `(TenantId, RequestConfirmationNumber)`** regardless -- hard stop even if the sequence is bypassed. Create via EF migration.
3. **Short-term stopgap** if sequence migration is blocked: wrap the existing read-then-write in the same `IAbpDistributedLock` as DAT-01 (`"confirmation-number:{tenantId}"`). Keeps it working while the real fix ships.
4. **Duplicate-key retry loop**: Polly `RetryStrategyOptions` filtered on `SqlException.Number == 2627 || 2601` for belt-and-braces. `EnableRetryOnFailure` alone does not cover this -- duplicate key is not in the default transient list.

## Gotchas / blockers

- **Sequences produce gaps on rollback/restart** (documented explicitly). If business requires strictly contiguous A-numbers, use a counter table + `UPDLOCK` (DevExpress pattern), not a sequence.
- `SEQUENCE` objects are host/DB-scoped, not tenant-scoped. Either a sequence per tenant (operationally heavy), or a shared sequence + filtered unique index per `(TenantId, Number)`.
- Filtered indexes require specific `SET` options (`ANSI_NULLS`, `QUOTED_IDENTIFIER`, `ARITHABORT` all ON at query time) -- default SqlClient sets these, but verify for any custom connections.
- Retrying duplicate-key insertions inside the default retry execution strategy has a store-generated keys warning: "if store-generated keys are used, this could lead to adding a duplicate row" (MS Connection Resiliency).
- Per-tenant sequence creates a schema-management concern: new tenant provisioning must also create the sequence (add to `CaseEvaluationTenantDatabaseMigrationHandler`).

## Open questions

- **Product decision**: are contiguous A-numbers required (for regulatory / billing / WCAB), or are gaps acceptable? Determines sequence vs counter-table.
- **Product decision**: per-tenant uniqueness or global? Current behaviour (per-tenant) may cause confusion in cross-tenant reporting.
- Do external systems (billing, WCAB reporting) assume specific A-number ranges or formats already in use?
- Is A99999 overflow ever reachable in the lifetime of a tenant? Current code throws `UserFriendlyException` when hit but has no recovery plan.

## Related

- [DAT-01](DAT-01.md) -- same lock can serve this sequence path short-term
- [DAT-07 (Medium)](../DATA-INTEGRITY.md#dat-07-missing-unique-constraints) -- unique index is the defensive backstop
- [docs/issues/DATA-INTEGRITY.md#dat-02](../DATA-INTEGRITY.md#dat-02-duplicate-confirmation-numbers-possible)
