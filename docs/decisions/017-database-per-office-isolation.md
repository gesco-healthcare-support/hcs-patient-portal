# ADR-017: Database-per-office isolation model

**Status:** Accepted
**Date:** 2026-06-25
**Verified by:** code-inspect + multi-office isolation test harness (Phase F)

## Context

Gesco runs many doctors' offices on one deployment. Each office's data is Protected
Health Information (PHI); a cross-office read is a HIPAA breach. The platform was migrated
(epic phases A-F) from a single shared database with row-level tenant filters to a
**database per office**: every office ("tenant") gets its own physical database, and the
host retains a small host database for cross-office concerns (SaaS tenants, operator
assignments, branding).

Phase F is the security/HIPAA gate for that migration: it must establish -- and prove --
that no office can read another office's data through any pathway, and that a
misconfiguration fails closed rather than silently exposing data.

## Decision

1. **One database per office.** Each office's connection string is stored on its SaaS
   tenant record via `tenant.SetDefaultConnectionString(...)`
   (`DoctorTenantAppService`, `FalkinsteinTenantDataSeedContributor`). ABP's stock
   `MultiTenantConnectionStringResolver` routes every tenant-scoped query to that
   office's database; the host database is used when there is no current tenant. No
   custom resolver is needed. The cloud-agnostic seam is
   `ITenantConnectionStringProvider` (dev derives `CaseEvaluation_{slug}` from the host
   Default; production can resolve a managed-store secret).

2. **Dual DbContext (continued from ADR-003).** `CaseEvaluationDbContext`
   (`MultiTenancySides.Both`) maps the host-shaped schema, including `IsHostDatabase()`
   blocks (SaaS, `IntakeOfficeAssignment`, `OfficeBranding`).
   `CaseEvaluationTenantDbContext` (`MultiTenancySides.Tenant`) maps the office-shaped
   schema with no SaaS tenant table and no Tenant foreign key on operational entities.

3. **Catalogs are per-office (Phase A).** Appointment types, states, locations,
   languages, statuses, document types and notification-template types are `IMultiTenant`
   and live in each office database, so one office's catalog edits are invisible to
   another.

4. **Defense in depth (does not rest on physical isolation alone).**
   - `Patient` is NOT `IMultiTenant` (a known PHI leak risk), so every Patient list/count
     query applies an explicit `Where(p => p.TenantId == currentTenantId)` filter
     (`EfCorePatientRepository`).
   - Operational reads pass through `AppointmentReadAccessGuard` /
     `AppointmentVisibilityService`, which assert `p.TenantId == CurrentTenant.Id` plus a
     party check (creator / patient identity / accessor / booked email).
   - Office provisioning fails closed: `OfficeDatabaseProvisioner` throws if an office has
     no Default connection string rather than seeding into the host database.
   - `ITenantConnectionStringProvider` implementations must never log the connection
     string.

5. **Host operators (Phase D) + branding (Phase E).** Cross-office work runs through
   `ITenantWorkRunner`, which iterates offices from the tenant registry and scopes each
   unit of work to one office. Host operators reach an office only by switching into it
   (Supervisor -> office admin; Intake -> limited, and only for assigned offices). Per
   office branding lives in a host-database `OfficeBranding` entity resolved pre-auth by
   subdomain.

## How isolation is verified

- **Automated (Phase F multi-office harness).** A test harness gives the host and each
  office their own named shared-cache in-memory SQLite database (one keeper connection
  per database, held for the process lifetime), routed through the same stock resolver as
  production. The harness's self-validation test proves an office-A row is invisible to
  office B **even with the `IMultiTenant` filter disabled** (asserted via
  `IDbContextProvider` in a `requiresNew` unit of work) -- i.e. genuine physical
  separation, not filter-only. A negative-test matrix then exercises operational data,
  PHI (Patient + full-SSN reveal), catalogs, host aggregation, background jobs and
  branding for deny-by-default cross-office access.
- **Manual (final real-database check).** Before go-live, the SQLite-vs-SQL-Server
  fidelity gap is closed by the procedure in
  `docs/runbooks/database-per-office-go-live-isolation-gate.md` (provision two real office
  databases, attempt cross-office access through every pathway, and connect to each
  database to confirm physical separation).

## Alternatives considered

- **Shared database with row-level tenant filters only.** Rejected: a single missed
  filter (especially on the non-`IMultiTenant` `Patient`) leaks PHI across offices; the
  blast radius of a bug is every office at once.
- **Testcontainers (real SQL Server) for the isolation tests.** Rejected for the harness:
  slow, requires Docker-in-CI, and diverges from the existing ~1300-test in-memory SQLite
  suite. The fidelity gap is instead closed by the manual real-database check at go-live.

## Consequences

- Strong physical isolation: a cross-office query opens a different database, so a logic
  bug fails to *find* data rather than *exposing* it.
- Operational cost: N office databases to provision, migrate, back up and monitor;
  provisioning must fail closed (it does).
- Test fidelity: SQLite approximates SQL Server; filtered unique indexes are no-ops on
  SQLite, so uniqueness rules are verified against SQL Server. The go-live runbook covers
  the residual gap.
- The deny-by-default isolation gate (any cross-office PHI read = blocking) is now an
  automated, repeatable check that guards against regressions in future changes.
