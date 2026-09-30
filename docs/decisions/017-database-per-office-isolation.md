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
   - `Patient` **is** `IMultiTenant`, so ABP's automatic query filter scopes every read by
     `CurrentTenant.Id`. That filter is the PRIMARY control. `EfCorePatientRepository`
     additionally applies an explicit `Where(p => p.TenantId == currentTenantId)` on the
     list and count paths, and its own comments label that explicit filter "defence in
     depth on top of ABP's" -- it is the second layer, not the only one. Cross-office
     visibility for host and IT-Admin paths is deliberate and goes through
     `IDataFilter<IMultiTenant>.Disable()`. **See the amendment at the foot of this ADR:
     this bullet said the opposite until 2026-09-29.**
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

## Amendment 2026-09-29: the `Patient` tenancy claim was false when written

**The decision is unchanged. One factual claim inside it was wrong, and wrong in the
direction that matters, so it is corrected in place rather than only footnoted.**

Defense-in-depth bullet 1 previously read:

> `Patient` is NOT `IMultiTenant` (a known PHI leak risk), so every Patient list/count
> query applies an explicit `Where(p => p.TenantId == currentTenantId)` filter
> (`EfCorePatientRepository`).

Both halves are wrong. `Patient` implements `IMultiTenant`:

```
grep -n "class Patient" src/HealthcareSupport.CaseEvaluation.Domain/Patients/Patient.cs
  27:public class Patient : FullAuditedAggregateRoot<Guid>, IMultiTenant
```

It has done so since FEAT-09 / ADR-006 T4 on **2026-05-05**, which `Patient.cs:19-21`
records, noting the entity was "previously host-only with a manual TenantId column but no
auto-filter". This ADR is dated **2026-06-25**, seven weeks later, so the claim was false
when written rather than having drifted afterwards.

The repository states the correct ordering itself, at `EfCorePatientRepository:110` and
`:162`: the explicit `TenantId` filter is "defence in depth on top of ABP's" automatic
filter. The original bullet inverted that, denying the primary control existed and
presenting the second layer as the only thing separating two offices' PHI.

**Why the ordering is worth correcting rather than shrugging at.** The risk is not that
somebody forgets to add a filter. It is that somebody reading this ADR treats ABP's global
filter as absent for `Patient` -- and so removes it as dead configuration, or writes an
`IgnoreQueryFilters` or raw-SQL query believing it changes nothing here. It also
misdescribes the system's actual PHI control to anyone consulting these records for a
compliance question.

`docs/architecture/MULTI-TENANCY.md:134` already carried the correct statement, having been
fixed by `#1112`, which found the same inverted-classification defect across seven entities
on that page. This instance survived because `docs/decisions/` had never been audited.
