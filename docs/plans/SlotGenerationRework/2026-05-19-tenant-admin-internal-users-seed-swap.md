---
status: shipped
slug: tenant-admin-internal-users-seed-swap
created: 2026-05-19
shipped: 2026-05-19
owner: Adrian
---

> **Implementation note 2026-05-19:** during smoke test the explicit
> `GrantAllAsync(TenantAdminRoleName, ...)` call collided with ABP's
> auto-grant for the static admin role (unique-index violation on
> `AbpPermissionGrants`). Resolution: drop the explicit call; the
> permission scope change to `MultiTenancySides.Both` is enough --
> ABP auto-grants tenant-side permissions (including `Both`-scoped
> ones) to the tenant's static `admin` role.

# Plan: tenant admin creates internal users, IT/Host admin create tenants, seed swap

Two related changes bundled together because they touch the same files
(`InternalUserRoleDataSeedContributor.cs`, `InternalUsersDataSeedContributor.cs`,
`InternalUsersAppService.cs`, `CaseEvaluationPermissionDefinitionProvider.cs`,
plus the matching SPA form).

## Goals

1. Per-tenant `admin` role gains `CaseEvaluation.InternalUsers.Create` so a
   tenant admin can create internal users (Staff Supervisor, Clinic Staff)
   inside their OWN tenant.
2. IT Admin (host role) gains `AbpSaas.Tenants.Create` (and `AbpSaas.Tenants`
   default) so they can create new tenants. Host Admin (`admin@abp.io`)
   already has all permissions; no change needed.
3. Replace the two demo seed entries `SoftwareOne/Two@evaluators.com`
   (currently seeded as tenant admin) with:
   - `stafsuper1@gesco.com` → Staff Supervisor (Patrick O'Neal)
   - `clistaff1@gesco.com` → Clinic Staff (Rachel Kim)

## Out of scope

- Removing the existing DB rows for `SoftwareOne/Two@evaluators.com` and their
  attached domain records (Adrian will do this later via reseed / cleanup).
- Adding a "Create tenant" UI in the SPA. Host Admin and IT Admin can use the
  Volo SaaS Tenants page at `/saas/tenants`; the permission grant is enough.

## Files to change

| Layer | File | Change |
| --- | --- | --- |
| Permissions | `src/.../Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs` | Change `InternalUsers.Default` from `MultiTenancySides.Host` → `MultiTenancySides.Both` so the permission can be granted to tenant `admin`. `InternalUsers.Create` inherits parent scope. |
| Role seed | `src/.../Domain/Identity/InternalUserRoleDataSeedContributor.cs` | (a) In the per-tenant pass, grant `InternalUsers.Default` + `InternalUsers.Create` to the static `admin` role (Volo SaaS auto-creates it). (b) In the host pass, grant `AbpSaas.Tenants` + `AbpSaas.Tenants.Create` to IT Admin so they can create tenants. |
| User seed | `src/.../Domain/Identity/InternalUsersDataSeedContributor.cs` | Replace `ExtraTenantAdminEmails` (a flat string array hard-wired to the `admin` role) with a richer struct `ExtraSeededUsers` carrying `(EmailPrefix, RoleName)` per entry. Seed `stafsuper1@gesco.com` → Staff Supervisor, `clistaff1@gesco.com` → Clinic Staff. Drop the SoftwareOne/Two entries entirely (existing DB rows untouched per Adrian). |
| User seed | `src/.../Domain/Identity/InternalUsersDataSeedContributor.cs` | Update `BuildInternalUserDisplayName` switch: drop `softwareone/softwaretwo` cases, add `stafsuper1` → ("Patrick", "O'Neal"), `clistaff1` → ("Rachel", "Kim"). |
| User seed | `src/.../Domain/Identity/InternalUsersDataSeedContributor.cs` | Update `BuildSeedPhoneNumber` switch: drop `softwareone/softwaretwo` cases, add `stafsuper1` / `clistaff1` with deterministic 555-prefix numbers. |
| AppService | `src/.../Application/InternalUsers/InternalUsersAppService.cs` | Loosen the tenant-id validation so a tenant-admin caller works: if `CurrentTenant.Id != null` and `input.TenantId == Guid.Empty`, default to `CurrentTenant.Id`. If both are set and they differ, reject with `InternalUserTenantMismatch` (new error code). Existing IT Admin path (host scope, requires `input.TenantId`) is unchanged. |
| Domain errors | `src/.../Domain.Shared/CaseEvaluationDomainErrorCodes.cs` | Add `InternalUserTenantMismatch` constant + matching localized message in `en.json`. |
| SPA | `angular/src/app/internal-users/components/internal-users-form.component.{ts,html}` | When the current user is in a tenant scope (read `currentTenant` from `ConfigStateService`), render the tenant field as a **disabled** dropdown pre-filled with the current tenant's name so the user can see which tenant they're operating in. The reactive form patches in `currentTenant.id` so the request body always carries the correct tenant. IT Admin (host scope, no `currentTenant.id`) keeps the existing editable dropdown behavior. |

## Risk to watch

- ABP's tenant-side admin role grant path: `InternalUserRoleDataSeedContributor`
  already uses `permissionManager.SetAsync(..., providerKey: "admin")` for
  Staff Supervisor / Clinic Staff, so the same pattern applies to the
  static `admin` role. Volo's tenant-create handler re-runs the seed
  contributor on tenant creation, so brand-new tenants pick up the grant
  automatically.
- Menu localization: the existing `::Menu:InternalUsers` key is used; no
  new key needed because tenant admins now hit the same route, just with
  a different field-render branch in the form.

## Tasks

1. Permission scope change + IT Admin tenant-create grant
   - `approach: code` (config-only edit; tested via Phase 5 manual flow)
2. Tenant admin grant for InternalUsers
   - `approach: code`
3. Seed swap (SoftwareOne/Two → stafsuper1 / clistaff1)
   - `approach: code`
4. AppService tenant-id defaulting
   - `approach: tdd` (this is logic + a new error code; add a unit test
     that exercises the three branches: host w/ tenantId, tenant w/ empty,
     tenant w/ mismatched tenantId)
5. SPA form: hide picker when scoped
   - `approach: test-after` (UI; manual Playwright check)
6. End-to-end smoke: log in as `admin@falkinstein.test` (post-seed) and
   create a new Clinic Staff user, confirm welcome email arrives, confirm
   the new user can log in and change their password.

## Rollback

- All changes are additive (new error code, new permission grants, new
  seed entries). Removing the new permission grants + reverting the
  permission scope change rolls back tenant-admin access; the
  AppService still works for IT Admin because the new `CurrentTenant` /
  `input.TenantId` defaulting only kicks in when the existing required
  branch (`input.TenantId != Guid.Empty`) is skipped.
- The seed swap is idempotent at the next reseed; orphan rows for
  SoftwareOne/Two stay in the DB but no longer get refreshed on subsequent
  seeds. To fully roll back: restore `ExtraTenantAdminEmails` and the two
  switch arms in `BuildInternalUserDisplayName` / `BuildSeedPhoneNumber`.

## Acceptance

- `admin@falkinstein.test` sees the "Internal Users" sidebar entry and
  can create a Clinic Staff user; the new user appears under the
  Falkinstein tenant only.
- `it.admin@hcs.test` sees both Internal Users + SaaS Tenants menu
  entries; can create a new tenant and a new internal user across
  tenants.
- A fresh reseed produces:
  - Per tenant: `admin@<slug>.test`, `supervisor@<slug>.test`,
    `staff@<slug>.test`, `stafsuper1@gesco.com` (Staff Supervisor),
    `clistaff1@gesco.com` (Clinic Staff). No SoftwareOne/Two rows added.
- The Responsible-User dropdown on Approve loses SoftwareOne/Two after
  a DB reseed (existing rows persist until manual cleanup).
