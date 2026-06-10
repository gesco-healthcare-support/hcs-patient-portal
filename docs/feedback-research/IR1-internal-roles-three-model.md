---
id: IR1
title: Consolidate internal roles to a 3-tier model with Staff Supervisor as top tenant role
type: enhancement
components: [src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs, src/HealthcareSupport.CaseEvaluation.Application/InternalUsers/InternalUsersAppService.cs, src/HealthcareSupport.CaseEvaluation.Application/Appointments/BookingFlowRoles.cs, src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs, src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUsersDataSeedContributor.cs]
related_known_bugs: [none]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change

Present exactly three custom internal roles and make Staff Supervisor the genuine top
tenant role with broad CRUD:

- IT Admin (host-scoped) -- unchanged.
- Staff Supervisor (top tenant) -- gains soft-Delete on all tenant entities, gains
  InternalUsers.Create, and may create both Supervisors and Clinic Staff.
- Clinic Staff (tenant) -- unchanged.

Retire the Volo SaaS per-tenant admin as a presented persona (keep it as break-glass only;
stop relying on the admin demo login as the tenant-top). All deletes remain SOFT (modern
PHI/audit standard); no destructive hard-delete is built.

## Current behavior (from investigation)

- Exactly THREE custom internal roles already exist as string consts:
  `InternalUserRoleDataSeedContributor.cs:40-42` -- `ItAdminRoleName = "IT Admin"`,
  `StaffSupervisorRoleName = "Staff Supervisor"`, `ClinicStaffRoleName = "Clinic Staff"`.
  No `RoleConsts` file and no role enum exist.
- The apparent FOURTH role is the per-tenant Volo SaaS static admin role (OLD Office
  Manager / Tenant Admin). It is NOT defined by this app -- ABP/Volo SaaS auto-creates it
  per tenant and auto-grants every tenant-side permission. The app treats it as internal:
  `InternalUsersDataSeedContributor.cs:131-136` seeds the admin email into the admin role.
- IT Admin is seeded only on the host pass (tenantId=null) at
  `InternalUserRoleDataSeedContributor.cs:65-72`; Staff Supervisor + Clinic Staff are
  seeded on the per-tenant pass (lines 79-85).
- Staff Supervisor grants today (lines ~300-362): Default/Create/Edit but NO .Delete on
  the 14 OperationalEntities, NO InternalUsers.Create.
- The creatable-role allow-list is exactly Clinic Staff + Staff Supervisor
  (`InternalUsersAppService.cs:70-71`), but only IT Admin / tenant-admin can actually
  reach InternalUsers.Create today; Staff Supervisor cannot
  (`CaseEvaluationPermissionDefinitionProvider.cs:188-191`, InternalUsers is
  MultiTenancySides.Both).
- `BookingFlowRoles.InternalUserRoles` (`BookingFlowRoles.cs:24-31`) lists FIVE names:
  `admin`, `Clinic Staff`, `Staff Supervisor`, `IT Admin`, `Doctor` (verified: "Doctor"
  at line 30). No Doctor user role is seeded, so "Doctor" is a dead entry.
- Soft-delete is ALREADY universal: every tenant entity extends
  FullAuditedAggregateRoot/FullAuditedEntity (ISoftDelete). Hard-delete code does NOT
  exist anywhere (grep clean). So Staff Supervisor "delete" = soft delete with zero entity
  changes once the .Delete grants are added.
- Grants are hardcoded permission-string literals in the Domain seeder because Domain
  cannot reference Application.Contracts (`InternalUserRoleDataSeedContributor.cs:33-36`).
  Any permission change must be edited in BOTH that seeder and `CaseEvaluationPermissions.cs`.

## Relevant code locations

- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs`
  -- add the .Delete grant loop + InternalUsers.Create to `StaffSupervisorGrants()`.
- `src/HealthcareSupport.CaseEvaluation.Application/InternalUsers/InternalUsersAppService.cs`
  -- confirm/extend the creatable-role allow-list (lines 70-71) so a Staff Supervisor caller
  may create Supervisors + Clinic Staff.
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/BookingFlowRoles.cs:24-31`
  -- remove the dead "Doctor" entry; decide admin retention (see below).
- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUsersDataSeedContributor.cs`
  -- admin-email seeding (lines 131-136); stop presenting admin as the demo top-tenant login.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs`
  -- the canonical permission-string source the seeder literals must mirror.
- Angular role-gated UI / route guards branching on `admin` vs `Staff Supervisor`.

## Phase 3 cross-reference

- BookingFlowRoles dead "Doctor" entry (`BookingFlowRoles.cs:30`): remove while here, since
  this is the only role-list sweep planned and IR1 already touches the file. Aligns with the
  DOCTOR decision (entity dormant, no Doctor user role).
- Audit "admin" string special-cases (`BookingFlowRoles.cs:24-31`,
  `InternalUsersDataSeedContributor.cs:131-136`, Angular guards): bundle the sweep here so a
  guard still keyed on `admin` without `Staff Supervisor` does not silently break a
  supervisor workflow once the admin demo login is retired.

## Research findings

- Internal patterns / prior art:
  - Grant tables are plain literal lists per role in one seeder; adding a grant = adding a
    literal (no migration). Soft-delete is the framework default for FullAudited entities,
    so no DeleteAsync override is needed.
  - Docs already treat Staff Supervisor as top tenant role
    (`internal-user-dashboard-design.md:274`, `staff-supervisor-doctor-management-design.md:53`);
    only the grants lag (`it-admin-user-management-design.md:188-196`).
  - The OLD-parity note (`InternalUsersAppService.cs:269-277`) says only the admin tier
    mints internal users; widening to Staff Supervisor is the one deliberate policy change
    -- now DECIDED.
- External docs (ABP) if relevant:
  - The Volo SaaS static tenant admin role cannot be deleted without fighting the SaaS
    module; ABP re-creates and auto-grants it per tenant. Retirement is presentational
    (suppress the demo login), not a code deletion. (MEDIUM -- ABP/Volo SaaS module behavior.)

## Approaches considered (with tradeoffs)

- Option A (CHOSEN) -- Premise correction: 3 custom roles already exist; add Staff
  Supervisor's missing grants and suppress the Volo admin demo login.
  - Pros: smallest contained change (one seeder + one allow-list); soft-delete is free;
    does not fight ABP; IT Admin unchanged. Effort S, risk Low.
  - Cons: "remove a role to reach three" is satisfied by reframing the fourth as the
    suppressed ABP admin, not by deleting code.
- Option B (rejected for now) -- Merge the Volo admin tenant role fully into Staff
  Supervisor (true 3-persona consolidation, retire the seeded admin email).
  - Why rejected: same grant changes as A plus a full admin-string sweep and demo-runbook
    updates. The required "admin" special-case audit is already pulled into A's Phase 3
    cross-reference, so B becomes a presentational fast-follow, not a separate decision.
    Effort M, risk Med (incomplete sweep could silently break a supervisor workflow).
- Option C (rejected) -- Add IT-Admin-only HARD delete on top of universal soft delete.
  - Why rejected: net-new permission(s), AppService purge methods, controllers, proxy regen,
    Angular plumbing; bypasses ISoftDelete on PHI entities (Patient/Appointment), destroying
    audit history; cross-tenant purge on Patient (NOT IMultiTenant) is delicate. Nothing in
    code or docs asks for physical purge. Effort L, risk High. Explicitly out: ALL deletes
    stay soft.

A wins because the codebase already provides the three custom roles and universal soft
delete; the gap is purely missing grants for Staff Supervisor plus retiring the redundant
admin persona. It delivers the locked decision at minimum surface area with no PHI/audit
hazard.

## Decision (locked 2026-06-03)

Three custom roles: IT Admin (host, unchanged), Staff Supervisor (top tenant), Clinic Staff
(tenant). Grant Staff Supervisor soft-Delete on all 14 OperationalEntities + Locations +
AppointmentDocuments, plus InternalUsers.Create, and ensure it can create Supervisors +
Clinic Staff. Retire the Volo admin as a presented persona (break-glass only; stop relying
on the admin demo login as tenant-top). Remove the dead "Doctor" entry from
BookingFlowRoles.InternalUserRoles and audit admin-string special-cases. ALL deletes remain
SOFT; no hard-delete is built. IT Admin stays host-scoped. Re-seed existing tenants.

## Implementation outline (no code)

1. Application.Contracts: confirm the .Delete and InternalUsers.Create permission names
   exist in `CaseEvaluationPermissions.cs` (they already do for IT Admin); no new
   permissions are introduced.
2. Domain seeder (`InternalUserRoleDataSeedContributor.cs`): in `StaffSupervisorGrants()`
   add .Delete literals for the 14 OperationalEntities + Locations + AppointmentDocuments,
   and add InternalUsers.Create. Mirror exactly the literals in `CaseEvaluationPermissions.cs`
   (two-file rule, lines 33-36). No migration -- grants are seed data, not schema.
3. Application allow-list (`InternalUsersAppService.cs:70-71`): ensure a Staff Supervisor
   caller may create Supervisors + Clinic Staff. ENFORCE server-side (this is an
   integrity/authority rule, not a UI affordance).
4. BookingFlowRoles (`BookingFlowRoles.cs:24-31`): remove "Doctor"; decide admin retention
   (retain in the internal fast-path list as break-glass, but do not present the login).
5. Admin-string sweep: audit `InternalUsersDataSeedContributor.cs:131-136` and Angular
   route/permission guards keyed on `admin`; ensure `Staff Supervisor` is honored everywhere
   the retired admin login used to be the only working path. Mirror authority gating in the
   Angular UI (UI affordance, server remains source of truth).
6. Re-seed: existing seeded tenants must re-run the data seeder (DbMigrator) to pick up the
   new Staff Supervisor grants; new grants are idempotent on re-run.
7. Proxy regen: only if any AppService surface signature changes (the allow-list change is
   internal logic, so likely NO proxy regen unless a DTO/contract changes).

## Dependencies

- Foundation for IP1 / IP2 / IP4 / IP5 (permission-tier work) and UM2 (User Management
  relocation). Those items depend on Staff Supervisor holding the broad CRUD + InternalUsers
  grants defined here.
- No upstream blockers; this item can land first.

## Residual open questions

- none (the four prior follow-ups -- fourth-role identity, hard-delete, supervisor-creates-
  supervisors, admin break-glass, runbook updates -- are all settled by the locked decision).
