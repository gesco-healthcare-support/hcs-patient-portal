---
id: UM2
title: Let Staff Supervisor (and above) create internal users under User Management
type: enhancement
components: [angular/src/app/internal-users/components/internal-users-form.component.ts, src/HealthcareSupport.CaseEvaluation.Application/InternalUsers/InternalUsersAppService.cs, src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs]
related_known_bugs: [none]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
Staff Supervisors (and roles above them) should have an "add internal users" form under the
User Management section, parallel to the "Invite External User" section. The FORM and its
placement already exist -- this is an AUTHORIZATION change (grant the create permission to
Staff Supervisor), not a build-form task.

## Current behavior (from investigation)
- An internal-user add form ALREADY EXISTS and is ALREADY under User Management.
  `InternalUsersFormComponent` (angular/src/app/internal-users/components/internal-users-form.component.ts)
  posts to `POST /api/app/internal-users`, branches host-IT-Admin (editable tenant picker) vs
  tenant-admin (locked tenant), auto-generates a temp password emailed via Hangfire, and is
  registered as a sibling of Invite External User under the `::Menu:UserManagement` nav parent
  (route.provider.ts:52-64; route at app.routes.ts:156-164).
- The form is gated by `CaseEvaluation.InternalUsers.Create`. The permission definition's
  doc-comment (CaseEvaluationPermissions.cs:324-342) states it is granted ONLY to IT Admin and
  that "Staff Supervisor + Clinic Staff intentionally do NOT receive this permission -- OLD
  parity is that only IT Admin creates internal accounts."
- `InternalUsers.Create` is registered `MultiTenancySides.Both`
  (CaseEvaluationPermissionDefinitionProvider.cs:188-191), so a TENANT-scoped Staff Supervisor
  CAN be granted it (no scope obstacle). Today it is held by IT Admin (host pass) and
  auto-granted to the per-tenant Volo admin role by ABP (the explicit seeder grant was removed
  to avoid a unique-index collision -- InternalUserRoleDataSeedContributor.cs:87-96). Staff
  Supervisor does NOT hold it today.
- The server-authoritative creatable-role allow-list is exactly Clinic Staff + Staff Supervisor
  (InternalUsersAppService.cs:70-71), validated Ordinal (lines 117-123). IT Admin self-creation
  is rejected (seed-only); external roles go through the separate InviteExternalUser surface.
- So both the form and the role allow-list already support creating Clinic Staff + Staff
  Supervisor; the ONLY gap is that Staff Supervisor lacks `InternalUsers.Create`, so a logged-in
  Staff Supervisor cannot see/use the form today.

## Relevant code locations
- src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs
  (StaffSupervisorGrants -- add InternalUsers.Create; this is part of the IR1 role-model change)
- src/HealthcareSupport.CaseEvaluation.Application/InternalUsers/InternalUsersAppService.cs:70-71
  (creatable-role allow-list already includes Staff Supervisor -- confirm, no change expected)
- angular/src/app/internal-users/components/internal-users-form.component.ts (no change -- the
  form already gates on InternalUsers.Create and renders once the policy is granted)
- src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:324-342
  (permission already defined; reconcile the IT-Admin-only doc-comment)

## Phase 3 cross-reference
- No existing BUG/OBS/SEED maps directly. This is the internal-user sibling of UM1 (invite names)
  under the same User Management nav node; the two share the User Management surface but are
  independent changes.
- Reconcile the IT-Admin-only doc-comment (CaseEvaluationPermissions.cs:333-342) and the OLD
  parity note (InternalUsersAppService.cs ~269-277) as part of this change -- the 2026-06-03
  role decision supersedes them.

## Research findings
- Internal patterns / prior art:
  - The form, the Hangfire temp-password welcome-email flow, and the User Management nav
    placement are all already built; this mirrors exactly the IP1/IP2/IP5 pattern where the
    machinery exists and only the role grant is missing.
  - The creatable-role allow-list (InternalUsersAppService.cs:70-71) is the server-side guard
    that bounds WHICH roles can be minted; it already lists Clinic Staff + Staff Supervisor, so
    "a Supervisor creates a Supervisor or Clinic Staff" is already supported once the Supervisor
    holds InternalUsers.Create.
- External docs (ABP) if relevant:
  - ABP role data seeding: grants are additive permission strings re-applied by DbMigrator on
    re-seed; `MultiTenancySides.Both` permits a tenant role to hold the permission. No new
    permission definition is needed. (Confidence HIGH -- grounded in repo code.)

## Approaches considered (with tradeoffs)
- CHOSEN: grant `InternalUsers.Create` to Staff Supervisor in StaffSupervisorGrants() (folded
  into the IR1 role-model change); keep the existing creatable-role allow-list (Clinic Staff +
  Staff Supervisor) so a Supervisor can create Supervisors + Clinic Staff but NOT IT Admins.
  - Pros: no form/UI work, no new permission, smallest blast radius, reuses the existing
    allow-list as the create-power boundary.
  - Tradeoff: deliberate authority widening (Supervisor can mint internal accounts, including
    peer Supervisors). Explicitly approved 2026-06-03 (answer A.c).
- REJECTED -- a NEW narrower permission (e.g. InternalUsers.CreateLimited) so Supervisor can
  create only Clinic Staff: the approved decision is that Staff Supervisor is the top tenant
  role and may create peers; a second permission adds surface for no requested benefit.
- REJECTED -- keep IT-Admin-only per the OLD-parity doc-comment: the 2026-06-03 decision
  explicitly supersedes that parity stance; the comment must be reconciled, not followed.

## Decision (locked 2026-06-03)
1. Grant `CaseEvaluation.InternalUsers.Create` to Staff Supervisor (as part of the IR1
   3-role model change).
2. Keep the server-side creatable-role allow-list as Clinic Staff + Staff Supervisor so a
   Staff Supervisor may create other Staff Supervisors and Clinic Staff (per approved A.c), but
   NOT IT Admin (host, seed-only).
3. Reconcile the IT-Admin-only doc-comment in CaseEvaluationPermissions.cs and the OLD-parity
   note in InternalUsersAppService.cs to reflect the new model.
4. No form/UI change -- the existing InternalUsersFormComponent renders once the policy is held.

## Implementation outline (no code)
1. Backend role grant: add `InternalUsers.Create` to StaffSupervisorGrants()
   (InternalUserRoleDataSeedContributor.cs). Do this inside the IR1 grant work so it is one
   coherent role-model edit, not a separate bolt-on.
2. Confirm the creatable-role allow-list (InternalUsersAppService.cs:70-71) already includes
   Staff Supervisor (it does) so a Supervisor can create Supervisors + Clinic Staff; no change
   expected, just verify.
3. Reconcile the two stale parity comments (CaseEvaluationPermissions.cs:333-342;
   InternalUsersAppService.cs ~269-277).
4. SERVER-side is authoritative (the permission grant + the AppService allow-list). The Angular
   form is a UI affordance that already reacts to the granted policy -- NO Angular change.
5. Re-seed via DbMigrator so existing seeded tenants pick up the new Staff Supervisor grant.
6. NO EF migration (no schema change). NO proxy regen (no DTO/endpoint change).
7. Verify: log in as Staff Supervisor -> "Add Internal User" form visible under User Management
   and functional, can create a Clinic Staff and a Staff Supervisor (temp-password welcome email
   sent), cannot create an IT Admin; log in as Clinic Staff -> form not available.

## Dependencies
- DEPENDS ON IR1 (3-role model; Staff Supervisor as top tenant role). Implement this grant as
  part of the IR1 grant set rather than separately.
- Sibling surface with UM1 (invite external user names) under User Management; independent
  change, no ordering constraint between them.

## Residual open questions
- None. "And above" resolves to: Staff Supervisor + IT Admin hold InternalUsers.Create; a
  Supervisor may create Supervisors + Clinic Staff (approved A.c); IT Admin remains seed-only
  and host-scoped.
