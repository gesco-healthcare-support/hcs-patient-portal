[Home](../../INDEX.md) > [Issues](../) > Research > DAT-07

# DAT-07: Missing Unique Constraints -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs`

---

## Current state (verified 2026-04-17)

No `HasIndex(...).IsUnique()` for `Appointment.RequestConfirmationNumber` or `Patient.Email`.

- `RequestConfirmationNumber` is generated per tenant (ABP tenant filter scopes generator query) so natural shape is `(TenantId, RequestConfirmationNumber)` unique per tenant.
- `Patient.Email` is used as de-facto lookup key in `GetPatientByEmailForAppointmentBookingAsync`. Patient has a `TenantId` column but does NOT implement `IMultiTenant` (per root CLAUDE.md), so uniqueness scope is a business decision.

---

## Official documentation

- [EF Core Indexes -- Index filter section](https://learn.microsoft.com/en-us/ef/core/modeling/indexes) -- `HasIndex(...).IsUnique().HasFilter("[Url] IS NOT NULL")`. "When using the SQL Server provider EF adds an `'IS NOT NULL'` filter for all nullable columns that are part of a unique index. To override this convention you can supply a `null` value." Standard recipe for per-tenant unique constraints.
- [SQL Server Filtered Indexes -- Limitations](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-filtered-indexes) -- "Filters can't be applied to primary key or unique constraints, but can be applied to indexes with the UNIQUE property." Use `HasIndex().IsUnique()`, not `HasAlternateKey`.
- [CREATE INDEX (T-SQL) -- Required SET options for filtered indexes](https://learn.microsoft.com/en-us/sql/t-sql/statements/create-index-transact-sql?view=sql-server-ver17) -- Required ON: `ANSI_NULLS`, `ANSI_PADDING`, `ANSI_WARNINGS`, `ARITHABORT`, `CONCAT_NULL_YIELDS_NULL`, `QUOTED_IDENTIFIER`. Required OFF: `NUMERIC_ROUNDABORT`. Single most common production trap.
- [MigrationBuilder.AddUniqueConstraint (MS Learn)](https://learn.microsoft.com/en-us/dotnet/api/microsoft.entityframeworkcore.migrations.migrationbuilder.adduniqueconstraint?view=efcore-8.0) -- creates true UNIQUE CONSTRAINT (not filtered unique index); use only for unfiltered global uniqueness.

## Community findings

- [ABP forum #8865 -- ABP Identity User Uniqueness](https://abp.io/support/questions/8865/ABP-Identity-User-Uniqueness) -- ABP team position: "this is not ABP's job and the developer should take care of it." Recommended: `Username + TenantId` with `WHERE IsDeleted = 0` filter. Applies identically here.
- [ASPNET Zero #7929](https://support.aspnetzero.com/QA/Questions/7929/AbpUsers-Username-and-Email-Unique-Constraints-Don't-Exist---General-question-about-Unique-Constraints) -- even for `AbpUsers`, uniqueness is app-layer only.
- [codegenes.net -- Stop EF Core filtered index on nullable](https://www.codegenes.net/blog/how-can-i-stop-ef-core-from-creating-a-filtered-index-on-a-nullable-column/) -- `HasFilter(null)` suppression notes.
- [dotnet/efcore #20136](https://github.com/dotnet/efcore/issues/20136) -- composite unique indexes sometimes need explicit `.HasFilter(null)` or multi-column filters.
- [Milan Jovanovic -- EF Core Migrations guide](https://www.milanjovanovic.tech/blog/efcore-migrations-a-detailed-guide) -- migration patterns with data cleanup.
- [Conrad Akunga -- Unique Constraints vs Unique Indexes](https://www.conradakunga.com/blog/entity-framework-core-unique-constraits-vs-unique-indexes/) -- filtered unique index is usually right for soft-delete + multi-tenant.

## Recommended approach

1. **`Appointment.RequestConfirmationNumber`**: per-tenant filtered unique index on `(TenantId, RequestConfirmationNumber)` with filter `WHERE IsDeleted = 0 AND RequestConfirmationNumber IS NOT NULL`. Matches ABP's own recommendation for multi-tenant + soft-delete. HIGH confidence.
2. **`Patient.Email`**: confirm intent first. INFERENCE: likely `(TenantId, Email)` because Patient has a TenantId column. If business allows same email across tenants (separate unions/employers), use `(TenantId, Email)`; if globally unique, use single-column filtered unique index on `Email` with `IS NOT NULL AND IsDeleted = 0`.
3. **Pre-migration duplicate audit**: run `GROUP BY ... HAVING COUNT(*) > 1` in every environment. Resolve duplicates manually before migration -- automated "pick first, delete rest" is unsafe for PHI-bearing Patient records and already-communicated confirmation numbers.

## Gotchas / blockers

- **SET options trap**: app connections not matching the required SET options will fail on INSERT/UPDATE with a rollback error. EF Core's default SqlClient satisfies them, but third-party ETL, SSMS sessions, and legacy tools may not -- verify before shipping.
- **Duplicate data**: migration fails if duplicates exist. Each environment needs its own audit + cleanup.
- **Soft delete semantics**: without `WHERE IsDeleted = 0`, soft-deleted rows still occupy the unique slot -- the exact reason ABP doesn't ship DB uniqueness.
- **Tenant filter vs unique filter**: ABP's `IMultiTenant` query filter is query-level, doesn't create the DB constraint. Filtered unique index is the only DB-level enforcement.
- Existing rows may have NULL `RequestConfirmationNumber` during partial creation -- decide if NULL counts or is a data bug to fix first.

## Open questions

- **Product**: is `Patient.Email` globally unique, per-tenant, or just a lookup helper?
- Does `RequestConfirmationNumber` generator (see [DAT-02](DAT-02.md)) retry on collision today? If not, adding uniqueness surfaces silent collisions as runtime INSERT failures.
- Any existing cross-tenant duplicate `RequestConfirmationNumber` rows the business wants to keep?
- What is current production data volume of Patient and Appointment? Determines whether audit + cleanup is online or needs a maintenance window.

## Related

- [DAT-02](DAT-02.md) -- confirmation number race makes the DB constraint a backstop
- [docs/issues/DATA-INTEGRITY.md#dat-07](../DATA-INTEGRITY.md#dat-07-missing-unique-constraints)
