---
title: Cross-tenant data isolation audit
date: 2026-05-25
status: ready
audience: Adrian (presenter)
scope: src/HealthcareSupport.CaseEvaluation.{Application,EntityFrameworkCore,Domain}/
---

# Cross-Tenant Isolation Audit -- Patient Portal

Read-only audit of multi-tenant data-filter usage. **0 raw SQL,
0 `IgnoreQueryFilters`** anywhere under `src/`. 9 findings, ranked
medium-or-low. Net verdict: **ship Tuesday, fix 4 items pre-prod.**

## Categories

- (a) `IDataFilter<IMultiTenant>.Disable()` use
- (b) Raw SQL / `FromSqlRaw` / `IgnoreQueryFilters` -- **none found**
- (c) Host-context filter-off (overlap with (a))
- (d) AppService methods taking `TenantId` from input

## Findings

### F-1 LOW: DashboardAppService.GetHostCountersAsync
Disable at line 61 for cross-tenant aggregate counters. Gated on
`Dashboard.Host` permission, which is `MultiTenancySides.Host` only.
Tenants cannot reach the disable path. Recommend defense-in-depth
`CurrentTenant.Id == null` assert.

### F-2 MEDIUM: PatientsAppService 10 disable sites
Every read path wraps repo calls in `using (isHost ? _dataFilter.Disable() : null)`.
Most are gated by `[Authorize(Patients.Default)]`. Four booking-flow
methods (`GetPatientForAppointmentBookingAsync`, `GetPatientByEmailFor...`,
`GetOrCreatePatientForAppointmentBookingAsync`, self-service
`GetCurrentPatientWithNavigationAsync`) are `[Authorize]` only.

**Risk:** If production OAuth ever produces a null `CurrentTenant.Id`
for an authenticated principal (misconfigured AuthServer, missing
tenantid claim, admin-subdomain reuse), an external user could call
`GetPatientForAppointmentBookingAsync(id)` and the disable activates,
returning rows across every tenant.

**Recommendation:** Replace `isHost` runtime check with explicit
`Dashboard.Host` permission assertion, OR forbid disable on
booking-flow + profile methods entirely. Demo risk: low (no flow
strips tenant). Phase-2 must-fix.

### F-3 HIGH-if-misconfigured: ExternalSignupAppService dev helpers
`MarkEmailConfirmedAsync` (line 163) and `DeleteTestUsersAsync`
(line 221) cross-tenant lookup IdentityUser by email, gated only by
`EnsureDevelopmentOnly` (`IHostEnvironment.IsDevelopment()`). Both
are `[AllowAnonymous]`.

**Risk:** If Development env leaks to production (env var hygiene
failure in CI/CD), anyone can confirm-email or delete any user
across every tenant.

**Recommendation:** Tuesday demo: confirm `ASPNETCORE_ENVIRONMENT
!= Development` on the demo stack (verified: `dotnet.md` rule
sets it to Development for local; ensure prod ENV is different).
Add a second gate (IT-Admin permission) OR move helpers to a
module not compiled into Production builds.

### F-4 LOW: DoctorsAppService.GetListAsync
Same pattern as F-2, gated by class-level `[Authorize(Doctors.Default)]`.
Only GetList disables (not GetAsync). Doctor entity has no PHI.

### F-5 MEDIUM: InternalUsersAppService.GetTenantOptionsAsync
Returns full Tenant list (id + display name, no PHI) for the IT-Admin
create-user form dropdown. **Method is `[AllowAnonymous]`** to populate
dropdown before SPA guard fires. Anyone unauthenticated can enumerate
the tenant list (max 200).

**Recommendation:** Drop `[AllowAnonymous]`, require authentication.
Tenant-name enumeration is a fingerprinting vector with no benefit
since the form requires auth to submit.

### F-6 LOW: 8 background notification jobs
Each job's `GetDistinctTenantIdsAsync` disables the filter to
enumerate every tenant's `Appointment.TenantId` once, then re-enters
per-tenant context via `_currentTenant.Change(tenantId)` for the
actual work. Filter-off scope projects ID-only (no entity rows
leak). Correct pattern.

**Recommendation:** Consider DRYing into a shared helper to avoid
drift if a future copy widens the projection.

### F-7 MEDIUM: PatientCreateDto/UpdateDto.TenantId accepted as input
`PatientsAppService.CreateAsync` (line 468) and `UpdateAsync`
(line 487) forward `input.TenantId` to `PatientManager`. ABP's
`MultiTenantPropertySettingInterceptor` overrides on INSERT, so
Create is mitigated. **On UPDATE, ABP does NOT re-stamp `TenantId`** --
a host-context admin update could pass `input.TenantId` and re-home
a patient across tenants.

**Recommendation:** Drop `TenantId` from these DTOs (server picks
from `CurrentTenant.Id`), OR validate `input.TenantId ==
CurrentTenant.Id` before forwarding. Mirror the
`InternalUserTenantMismatch` pattern from F-9.

### F-8 LOW: ExternalSignupAppService.RegisterAsync TenantId input
`[AllowAnonymous]` register accepts `input.TenantId`. If invite
token present, server overrides from `acceptedInvitation.TenantId`.
Without token, `input.TenantId` is trusted (correct for tenant
subdomain registration). User can only create themselves under
named tenant -- no cross-tenant read.

### F-9 LOW (reference impl): InternalUsersAppService.CreateAsync
Explicit cross-tenant guard: rejects tenant caller sending different
`input.TenantId` than `CurrentTenant.Id` (`InternalUserTenantMismatch`
exception). **Use this as the reference pattern for F-7.**

## Summary

| Category | Count | High | Medium | Low |
|---|---|---|---|---|
| (a) Disable filter | 4 sites | 0 | 1 | 3 |
| (b) Raw SQL | 0 | -- | -- | -- |
| (c) Host-context filter-off | overlap | 0 (F-3 conditional) | 2 | 1 |
| (d) Tenant-id input | 3 | 0 | 1 | 2 |
| **Totals** | **9 findings** | **0** | **4** | **5** |

## Demo readiness

Comfortable showing this stack to a security-savvy viewer with three
proactive surfacing caveats:

1. The `isHost = CurrentTenant.Id == null` heuristic substitutes a
   state check for an authorization check. Cannot conclusively rule
   out as a leak path without inspecting the OAuth pipeline. If a
   token ever lands without a tenant claim on an authenticated
   principal, every patient across every tenant is reachable from
   booking endpoints.
2. Dev-only helpers (F-3) are fine for demo; production cutover MUST
   verify `ASPNETCORE_ENVIRONMENT != Development`.
3. `PatientCreateDto/UpdateDto.TenantId` lets host-context admin
   updates potentially re-home a patient across tenants (F-7). Not
   a demo-day risk; Phase-2 must-fix.

**Tuesday verdict: ship.**
