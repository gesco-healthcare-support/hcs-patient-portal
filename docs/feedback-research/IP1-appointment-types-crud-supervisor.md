---
id: IP1
title: Grant Appointment Types CRUD to Staff Supervisor and above; keep Clinic Staff read-only
type: enhancement
components: [angular/src/app/appointment-types/, src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs, src/HealthcareSupport.CaseEvaluation.Application/AppointmentTypes/AppointmentTypesAppService.cs]
related_known_bugs: [OBS-10-appointment-types-vs-plan]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
Appointment Management -> Appointment Types must expose CRUD to Staff Supervisor and above;
Clinic Staff cannot CRUD. The CRUD machinery already exists end-to-end -- this is a
role-GRANT change plus a security-tightening of the read surface, NOT a build-CRUD task.

## Current behavior (from investigation)
- Full CRUD UI and backend already exist. Create/Edit/Delete buttons are gated on
  `CaseEvaluation.AppointmentTypes.Create/Edit/Delete`
  (appointment-type.component.html:5, 157, 165; bulk-delete line 77). Button visibility is
  computed in appointment-type.abstract.component.ts:54-58.
- Backend write methods are correctly method-gated:
  AppointmentTypesAppService.cs:46 (Delete), :52 (Create), :59 (Edit) -- verified.
- ROLE GAP vs feedback: per the seeder, today ONLY IT Admin holds the write permissions
  (AllEntities loop, InternalUserRoleDataSeedContributor.cs:142, 219-229). Staff Supervisor
  and Clinic Staff get only `AppointmentTypes.Default` read via the LookupReadEntities loop
  (InternalUserRoleDataSeedContributor.cs:197-204). So Clinic Staff already cannot CRUD
  (correct), but Staff Supervisor also cannot CRUD today (the gap to close).
- Contrast: Locations is already done the target way -- Staff Supervisor holds
  Locations.Create+Edit (InternalUserRoleDataSeedContributor.cs:308-309). That is the
  pattern to mirror for AppointmentTypes.
- READ ANOMALY (verified): AppointmentTypesAppService carries a bare class-level `[Authorize]`
  (AppointmentTypesAppService.cs:18) and GetListAsync/GetAsync (lines 30-44) have NO
  method-level permission attribute. So today ANY authenticated user -- including external
  Patient/Attorney roles -- can read the appointment-type list. The other three lookup
  entities (Languages, Locations, WcabOffices) restrict read via `[Authorize(...Default)]`
  at class level (e.g. AppointmentLanguagesAppService.cs:18).
- Permission registration: `CaseEvaluationPermissions.cs:30-36` defines
  AppointmentTypes.Default/Create/Edit/Delete; PermissionDefinitionProvider.cs:23-26
  registers them with no MultiTenancySides constraint (=> Both), so a tenant-scoped
  Staff Supervisor CAN be granted them (Locations proves this end-to-end).

## Relevant code locations
- src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs
  (StaffSupervisorGrants; mirror the Locations Create+Edit at lines 308-309)
- src/HealthcareSupport.CaseEvaluation.Application/AppointmentTypes/AppointmentTypesAppService.cs:18,30-44
  (bare [Authorize] -> tighten to AppointmentTypes.Default)
- src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:30-36
  (no change -- permissions already exist)
- angular/src/app/appointment-types/appointment-type/components/appointment-type.component.html
  (no change -- buttons already react to granted policies)

## Phase 3 cross-reference
- Bundle the read-anomaly fix (bare `[Authorize]` -> `[Authorize(...AppointmentTypes.Default)]`)
  with this grant change: it is the same file family, same audit, and closing the grant gap
  without tightening read leaves an external-readable lookup -- fix both in one pass.
- OBS-10-appointment-types-vs-plan: confirms the type list is admin-managed via this CRUD UI
  (not a fixed enum); no code change, but it validates that CRUD-by-role is the intended
  management path. NOTE: the global decision hard-deletes QME/Deposition/Record Review/
  Supplemental from the seed and renames Panel QME -> PQME; that seed work is a SEPARATE item
  -- do not fold seed mutation into this role-grant note.

## Research findings
- Internal patterns / prior art:
  - Locations already grants Staff Supervisor Create+Edit (no Delete) via StaffSupervisorGrants
    (InternalUserRoleDataSeedContributor.cs:308-309). This is the exact additive pattern and
    the proof that a tenant role can hold a Both-sided permission on a host-scoped lookup.
  - Project convention (Application/CLAUDE.md "Permissions"): class-level
    `[Authorize(...{Entity}.Default)]`, overridden by `.Create/.Edit/.Delete` per method. The
    AppointmentTypes bare `[Authorize]` is the documented-style deviation; tightening it
    realigns with the convention the other three lookups already follow.
- External docs (ABP) if relevant:
  - ABP permission definitions / role data seeding: grants are additive permission strings on
    a role; re-running the seeder (DbMigrator) applies new grants to existing seeded tenants.
    No new permission needs to be defined -- AppointmentTypes.Create/Edit/Delete already exist
    in the DefinitionProvider. (Confidence HIGH -- grounded in repo code, not inference.)

## Approaches considered (with tradeoffs)
- CHOSEN: Add AppointmentTypes Create + Edit + soft-Delete to StaffSupervisorGrants(); leave
  Clinic Staff on the read-only LookupReadEntities loop; tighten the bare class `[Authorize]`
  to `AppointmentTypes.Default`.
  - Pros: minimal blast radius, no Angular change (buttons already react to policies), reuses
    the Locations precedent, realigns the read guard with the other three lookups.
  - Tradeoff: deliberate authority widening (a tenant role mutates a host-shared lookup row).
    Acceptable for Phase-1 single demo tenant.
- REJECTED -- Create+Edit only, no Delete (the Locations precedent literally followed): the
  global ROLES decision (2026-06-03) explicitly gives Staff Supervisor soft-Delete on ALL
  tenant entities, and every entity is FullAudited/ISoftDelete so delete is already soft and
  audited. Withholding Delete here would contradict the locked role model with no upside.
- REJECTED -- keep IT-Admin-only per master-data-crud-design.md Section 8 (line 249) /
  Section 7 (lines 237-238): that design doc predates this session. The 2026-06-03 decision
  to make Staff Supervisor the top tenant role with soft-Delete on all tenant entities
  SUPERSEDES the IT-Admin-only matrix. The doc must be reconciled, not followed.
- REJECTED -- leave the bare `[Authorize]` read open: granting write without tightening read
  leaves the type list externally readable, an unnecessary information-exposure gap; the fix
  is one line and same-file.

## Decision (locked 2026-06-03)
1. Grant Staff Supervisor `AppointmentTypes.Create`, `.Edit`, and soft-`Delete` in
   StaffSupervisorGrants(), mirroring the Locations grant. Delete stays SOFT (FullAudited/
   ISoftDelete -- audit trail preserved); no destructive hard-delete is built.
2. Clinic Staff stays read-only (`AppointmentTypes.Default` via LookupReadEntities) --
   satisfies "Clinic Staff cannot CRUD".
3. Tighten AppointmentTypesAppService class guard from bare `[Authorize]` to
   `[Authorize(CaseEvaluationPermissions.AppointmentTypes.Default)]`, AFTER confirming no
   anonymous/external booking-form path reads appointment-types from this AppService.
   (Verification note: a separate deep-dive established the booking form's type dropdown is
   served by AppointmentsAppService.GetAppointmentTypeLookupAsync, NOT by
   AppointmentTypesAppService.GetListAsync -- so tightening this service's read is safe for
   the booking flow. Confirm at build time that no other anonymous caller hits GetList/GetAsync.)
4. This overrides master-data-crud-design.md's IT-Admin-only assignment for AppointmentTypes.

## Implementation outline (no code)
1. Backend role grant: in StaffSupervisorGrants() (InternalUserRoleDataSeedContributor.cs),
   add AppointmentTypes.Create, .Edit, .Delete to the Staff Supervisor grant set (mirror the
   Locations entries at lines 308-309). This is the IR1 role-model change -- coordinate so
   AppointmentTypes is included in whatever Delete-grant loop IR1 establishes for tenant
   entities rather than hand-listed twice.
2. Backend read tighten: change AppointmentTypesAppService.cs:18 from `[Authorize]` to
   `[Authorize(CaseEvaluationPermissions.AppointmentTypes.Default)]`. Leave the per-method
   Create/Edit/Delete guards as-is (they already override correctly).
3. SERVER-side enforcement is authoritative (the permission grants + AppService guards). The
   Angular buttons are UI affordances only and already react to granted policies -- NO Angular
   change required.
4. Re-seed: run DbMigrator so existing seeded tenants pick up the new Staff Supervisor grants
   (grants are seed-applied, not migration-applied).
5. NO EF migration (no schema change). NO proxy regen (no DTO/endpoint signature change;
   the read-guard attribute is server-only and does not alter the generated proxy).
6. Verify: log in as Staff Supervisor -> Create/Edit/Delete buttons visible and functional;
   log in as Clinic Staff -> read-only, no write buttons; confirm an external role can no
   longer hit GetListAsync after the guard tighten; confirm the booking form type dropdown
   still populates.

## Dependencies
- DEPENDS ON IR1 (3-role model: Staff Supervisor as top tenant role with soft-Delete on all
  tenant entities + the Delete-grant loop). Implement the grant change as part of / after IR1
  so AppointmentTypes is folded into the IR1 grant structure, not bolted on separately.
- Sibling pattern with IP2 (Appointment Languages) and IP5 (Wcab Offices) -- same role-grant
  shape; can share the IR1 grant loop. IP4 (Locations) already done.

## Residual open questions
- Confirm at build time that NO anonymous/external caller (beyond the booking form, already
  cleared) depends on AppointmentTypesAppService.GetList/GetAsync before tightening the read
  guard. Low risk; verify rather than assume.
