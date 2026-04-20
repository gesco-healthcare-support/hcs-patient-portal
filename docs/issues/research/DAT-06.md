[Home](../../INDEX.md) > [Issues](../) > Research > DAT-06

# DAT-06: Missing Database Indexes on FK Columns -- Research

**Severity**: Medium
**Status**: Open (verified 2026-04-17)
**Source files**:
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs`

---

## Current state (verified 2026-04-17)

`OnModelCreating` declares zero explicit `HasIndex(...)` calls except composite keys on `DoctorAppointmentType(DoctorId, AppointmentTypeId)` (line 112) and `DoctorLocation(DoctorId, LocationId)` (line 121).

EF Core convention auto-creates single-column indexes on every FK declared via `HasOne`/`WithMany`, so simple single-FK lookups are covered. The real gap:

- No composite index for the hot "find available slot" query shape (`AvailableDate + BookingStatusId + LocationId` on `DoctorAvailability`).
- No index on `Appointment.AppointmentStatus` (enum column used to filter the 13-state lifecycle).
- No covering/INCLUDE columns to turn hot queries into index-only scans.

The source doc's blanket "missing indexes on FK columns" overstates the issue -- EF convention handles single-FK indexes. The real issues are composite shapes and enum-column filters.

---

## Official documentation

- [EF Core Indexes (MS Learn)](https://learn.microsoft.com/en-us/ef/core/modeling/indexes) -- "By convention, an index is created in each property (or set of properties) that are used as a foreign key." Documents composite `HasIndex(x => new { x.A, x.B })` and `.IncludeProperties(...)` for covering indexes.
- [EF Core Foreign and principal keys in relationships](https://learn.microsoft.com/en-us/ef/core/modeling/relationships/foreign-and-principal-keys) -- corroborates auto-index-on-FK.
- [SQL Server Filtered Indexes](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-filtered-indexes) -- good fit for "rows not yet processed" (e.g. `BookingStatusId = Available`).
- [SQL Server Indexes with Included Columns](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-indexes-with-included-columns) -- covering indexes avoid table lookups.
- [`sys.dm_db_missing_index_details`](https://learn.microsoft.com/en-us/sql/relational-databases/system-dynamic-management-views/sys-dm-db-missing-index-details-transact-sql?view=sql-server-ver17) -- DMV that ranks suggestions by `avg_total_user_cost * avg_user_impact * (user_seeks + user_scans)`. Caveat: resets on restart, capped at 600 rows.
- [Tune nonclustered indexes with missing-index suggestions](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/tune-nonclustered-missing-index-suggestions) -- order equality columns first by selectivity; add inequality columns; add covering columns via INCLUDE.

## Community findings

- [MSSQLTips -- Find SQL Server Missing Indexes with DMVs](https://www.mssqltips.com/sqlservertip/1634/find-sql-server-missing-indexes-with-dmvs/) -- practical DMV workflow.
- [MSSQLTips -- Uncover SQL Server missing indexes](https://www.mssqltips.com/sqlservertip/8061/uncover-sql-server-missing-indexes/) -- DMV suggestions are advisory; verify against plans.
- [microsoft/tigertoolbox -- Index-Creation script](https://github.com/microsoft/tigertoolbox/tree/master/Index-Creation) -- Microsoft-maintained de-duper; safer than hand-rolling.
- [dotnet/efcore #15854](https://github.com/dotnet/efcore/issues/15854) -- confirms auto-index-on-FK convention is still the default in EF Core 10.

## Recommended approach

1. Treat single-FK indexes as already handled by EF convention. Focus additions on (a) composite indexes matching actual `WHERE + ORDER BY` shapes of `DoctorAvailabilitiesAppService` and `AppointmentsAppService`; (b) an index on `Appointment.AppointmentStatus` (enum), possibly composite with `TenantId`; (c) INCLUDE columns on the availability search index.
2. Drive the list by evidence: run the DMV query against a load-tested environment and rank by the documented cost metric. Avoid "every FK looks lonely, add one" -- ABP's tenant filter often makes many FKs low-value leading columns.
3. For filtered indexes on `BookingStatusId = Available` or `IsDeleted = 0`, keep the filter narrow.

## Gotchas / blockers

- LocalDB dev data produces zero useful DMV output -- need representative data volume (tens of thousands of availabilities, realistic appointment distribution) for meaningful suggestions.
- ABP's `IMultiTenant` query filter injects `WHERE TenantId = @x` on every tenant-scoped query. Indexes not leading with or including `TenantId` may be ignored by the optimiser.
- SQL Server caps `sys.dm_db_missing_index_details` at 600 rows, resets on restart.
- Adding indexes increases write cost on `Appointment`/`DoctorAvailability` -- measure INSERT/UPDATE paths before committing.
- Index name collisions: `IX_<Table>_<Cols>` default can collide with EF's convention-created indexes on single FKs -- use explicit `HasDatabaseName(...)` when adding composites.

## Open questions

- Are actual execution plans available (Query Store, Application Insights) to target index shape by real cost?
- Does `AppointmentStatus` need a filtered index for the "active appointments only" views, or plain nonclustered?
- Patient does NOT implement `IMultiTenant` but has a `TenantId` property. Do Patient-bound queries use manual `TenantId` predicates, and should `TenantId` be the leading column of any Patient search index?

## Related

- [DAT-07](DAT-07.md) -- unique constraints are another index-layer concern
- [docs/issues/DATA-INTEGRITY.md#dat-06](../DATA-INTEGRITY.md#dat-06-missing-database-indexes-on-fk-columns)
