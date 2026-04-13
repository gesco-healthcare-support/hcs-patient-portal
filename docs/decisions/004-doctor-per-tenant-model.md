# ADR-004: One Doctor Per Tenant Multi-Tenancy Model

**Status:** Accepted
**Date:** 2026-04-10
**Verified by:** code-inspect

## Context

The Patient Portal manages IME (Independent Medical Examination) appointments for
workers' compensation cases. The business model is that each doctor operates as an
independent practice. Gesco (the company) onboards each doctor as a separate client.

The application uses ABP Framework's multi-tenancy with per-tenant databases. The
question was how to model the relationship between doctors and tenants: should a tenant
represent a clinic (multiple doctors) or a single doctor?

## Decision

Each ABP tenant maps to exactly one Doctor entity. The doctor IS the tenant.

Evidence from the codebase:

- `Doctor` implements `IMultiTenant` with a `TenantId` property that links to the ABP
  `Tenant` entity via an explicit FK (`HasOne<Tenant>().HasForeignKey(x => x.TenantId)`)
- The `DoctorTenantAppService` extends ABP's `TenantAppService` and overrides
  `CreateAsync` to atomically: (a) create a SaaS tenant, (b) create/find an admin
  IdentityUser with a "Doctor" role, (c) create a Doctor profile linked to that user
  and tenant
- `DoctorAvailability` (time slots) has a `TenantId` but no direct FK to Doctor --
  slots belong to the tenant, and since there is only one doctor per tenant, the
  relationship is implicit
- `Appointment` similarly links to the doctor through `DoctorAvailabilityId`, not a
  direct `DoctorId` FK
- The Doctor CLAUDE.md explicitly states: "one doctor per tenant (the doctor IS the
  tenant)"

## Consequences

**Easier:**
- Tenant-scoped queries (appointments, availability slots) automatically filter to
  the correct doctor without needing an explicit DoctorId filter
- Strong data isolation -- each doctor's practice data lives in a separate database
- Doctor onboarding is a single atomic operation (create tenant + user + doctor)
- No need for intra-tenant doctor selection UI

**Harder:**
- Cannot support a multi-doctor clinic as a single tenant without restructuring
- If the business model changes to support group practices, the entire tenancy model
  would need reworking
- DoctorAvailability and Appointment lack a direct FK to Doctor, which makes
  cross-tenant reporting queries more complex (must join through tenant context)
- The `DoctorsAppService` must use `IDataFilter.Disable<IMultiTenant>()` to query
  doctors across tenants for the host admin view

## Alternatives Considered

1. **Clinic-per-tenant model (multiple doctors per tenant)** -- Rejected because Gesco's
   current business model is strictly one doctor per practice. Adding multi-doctor
   support would add complexity (doctor selection in scheduling, per-doctor permissions)
   with no current business need.

2. **No multi-tenancy (single database, role-based access)** -- Rejected because ABP
   multi-tenancy provides physical data isolation, which is valuable for a healthcare
   application handling sensitive appointment and patient data. Each doctor's data
   resides in a separate database.

3. **Doctor as a separate module outside ABP tenancy** -- Rejected because it would
   duplicate ABP's existing tenant management (user provisioning, database management,
   subscription features) rather than leveraging it.
