# ADR-003: Dual DbContext for Host and Tenant Databases

**Status:** Accepted
**Date:** 2026-04-10
**Verified by:** code-inspect

## Context

ABP Framework's multi-tenancy module supports separate databases per tenant. When using
this model, host-only entities (lookup tables shared across all tenants) and tenant-only
entities (transactional data scoped to one tenant) must be configured in separate
migration contexts. Otherwise, tenant databases would contain empty host tables, and host
migrations would include tenant-only schema changes.

This project uses ABP Commercial 10.0.2 with SQL Server. There are 6 host-only entities
(Location, State, WcabOffice, AppointmentType, AppointmentStatus, AppointmentLanguage)
and 7 tenant-scoped entities implementing `IMultiTenant` (Doctor, Appointment,
DoctorAvailability, ApplicantAttorney, AppointmentAccessor,
AppointmentApplicantAttorney, AppointmentEmployerDetail).

## Decision

Maintain two DbContext classes that both inherit from `CaseEvaluationDbContextBase<T>`:

1. **`CaseEvaluationDbContext`** -- `MultiTenancySides.Both`
   - Contains ALL entity DbSets (host + tenant)
   - Host-only entities are guarded with `if (builder.IsHostDatabase())` in
     `OnModelCreating`
   - Used for host database migrations (`src/.../Migrations/`)
   - Connection string: `"Default"`

2. **`CaseEvaluationTenantDbContext`** -- `MultiTenancySides.Tenant`
   - Contains only tenant-relevant DbSets (no Location, WcabOffice, Patient)
   - Re-declares read-only entity configs for host-scoped lookup tables (State,
     AppointmentType, AppointmentStatus, AppointmentLanguage) so tenant code can
     read them via FK joins
   - Used for tenant database migrations (`src/.../TenantMigrations/`)
   - Connection string: `"Default"`

Both share a common base (`CaseEvaluationDbContextBase`) that configures ABP module
tables (Identity, OpenIddict, SaaS, AuditLogging, etc.) and the Books demo entity.

Notable: The host DbContext configures Doctor join tables (DoctorAppointmentType,
DoctorLocation) with `DeleteBehavior.Cascade`, while the tenant DbContext uses
`DeleteBehavior.NoAction` for the same tables. This means cascading deletes work
differently depending on which database context is active.

## Consequences

**Easier:**
- Clean separation of host vs. tenant migrations -- no orphan tables
- ABP's `IsHostDatabase()` guard is a well-documented pattern with framework support
- Tenant databases are smaller and contain only relevant schema

**Harder:**
- Every new entity requires configuration in potentially both DbContexts
- Entity configurations that span both contexts (e.g., DoctorAvailability references
  host-scoped Location) must be carefully coordinated
- Delete behavior differences between host and tenant contexts can cause subtle bugs
  if not well understood
- Two migration folders (`Migrations/` and `TenantMigrations/`) must both be kept
  up to date

## Alternatives Considered

1. **Single DbContext with shared database** -- Rejected because the business requires
   tenant data isolation. A single database with `TenantId` filters works but does not
   provide the physical separation that ABP's per-tenant-database model enables.

2. **Single DbContext with `MultiTenancySides.Both` only** -- This is what
   `CaseEvaluationDbContext` already is, but using it alone would put host-only tables
   (Locations, States) into every tenant database. The tenant DbContext avoids this.

3. **Three DbContexts (host-only, tenant-only, shared)** -- Rejected as over-engineered.
   ABP's convention is exactly two contexts, and the framework tooling expects this
   structure.
