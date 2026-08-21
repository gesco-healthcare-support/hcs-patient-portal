---
id: IP5
title: Grant Staff Supervisor Create/Edit/soft-Delete on WcabOffices (Clinic Staff read-only)
type: enhancement
components: [angular/src/app/wcab-offices/, src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs, src/HealthcareSupport.CaseEvaluation.Application/WcabOffices/, src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/]
related_known_bugs: [none]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
Add CRUD for WCAB Offices available to Staff Supervisor and above; Clinic Staff cannot
create/edit/delete (read-only). The CRUD machinery (AppService, controller, Angular pages,
Excel export) already exists end to end; only the role grants need to change. Per the locked
role model, "delete" means SOFT delete (every tenant entity is FullAudited/ISoftDelete); no
hard-delete is built. Same shape as IP1 (Appointment Types) and IP2 (Appointment Languages).

## Current behavior (from investigation)
- WcabOffices is granted to Staff Supervisor AND Clinic Staff as **Default (read) only** --
  both roles include it via `LookupReadEntities` (`InternalUserRoleDataSeedContributor.cs:197-204`,
  read-loop at `:304-307` for Supervisor). Neither tenant role gets Create/Edit/Delete.
- Create/Edit/Delete on WcabOffices is granted **only to IT Admin** (host scope) via the
  `AllEntities` full-CRUD loop -- `WcabOffices` is listed at `InternalUserRoleDataSeedContributor.cs:148`,
  and the loop yields Default/Create/Edit/Delete for every AllEntities member at `:223-229`.
- So today the Angular Create/Edit/Delete buttons and bulk-delete are invisible to both tenant
  roles; they render only for IT Admin (host). Action visibility is policy-gated in
  `wcab-office.abstract.component.ts:58-60` (reads WcabOffices.Edit/Delete granted policies)
  and the list also exposes `exportToExcel()` at `:49-51`.
- Server-side authorization is intact: class-level `[Authorize(...WcabOffices.Default)]`
  (`WcabOfficesAppService.cs:25`) with per-verb guards -- Create `:80`, Edit `:87`, Delete `:74`,
  DeleteByIds `:111`, DeleteAll `:117`. The Excel export `GetListAsExcelFileAsync` is
  `[AllowAnonymous]` (`:94`) but token-guarded via `GetDownloadToken` (`:123`).
- WcabOffice is **host-scoped (no IMultiTenant)** -- 6 string fields, Excel export +
  download-token CSRF pattern (Domain/CLAUDE.md "Thin host-scoped lookups"). A tenant Staff
  Supervisor editing it mutates host-shared data visible to all tenants.
- Permission definitions already declare the full CRUD child-permission tree
  (`CaseEvaluationPermissions.cs:62-68`, `CaseEvaluationPermissionDefinitionProvider.cs:39-42`),
  so no new permission strings are needed.

## Relevant code locations
- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs`
  -- the ONE file that must change: move WcabOffices Create/Edit/(soft)Delete to Staff Supervisor.
- `angular/src/app/wcab-offices/wcab-office/components/wcab-office.component.html` (list +
  action buttons), `wcab-office.abstract.component.ts:58-60` (visibility), `wcab-office-detail.component.html`
  (form: Name* maxlength50, Abbreviation* maxlength50, Address maxlength100, City maxlength50,
  ZipCode maxlength15, IsActive, State lookup), `wcab-office-detail.abstract.service.ts:43-51`
  (Name+Abbreviation required, rest maxLength), `providers/wcab-office-base.routes.ts:10-18`
  (nav, requiredPolicy CaseEvaluation.WcabOffices), `app.routes.ts:95`.
- Backend (no change expected, listed for grounding): `WcabOfficesAppService.cs`,
  `HttpApi/Controllers/WcabOffices/WcabOfficeController.cs`, `Domain/WcabOffices/WcabOfficeManager.cs`
  + `WcabOffice.cs`, `Application.Contracts/WcabOffices/WcabOfficeCreateDto.cs`.

## Phase 3 cross-reference
- **IP5 nav relocation:** route currently lives under "Doctor Management"
  (`wcab-office-base.routes.ts:10-18`). The DOCTOR decision HIDES the Doctors nav/page. If
  "Doctor Management" disappears, WcabOffices must reparent (logical home: a master-data /
  User Management or Appointment Management group, matching where Locations/Types live).
  Coordinate with IP3 (Doctor entity fate) so this lookup does not get orphaned. (Confirm target group.)
- **Excel export `[AllowAnonymous]` surface:** noted in HttpApi/CLAUDE.md item 7 as the
  reference impl for the download-token pattern. Out of scope for IP5 (token-guarded today),
  but flag while in this file in case access-control review wants it bundled.

## Research findings
- Internal patterns / prior art:
  - This is the SAME grant-only change as IP1/IP2. The seeder already separates Supervisor
    grants (`StaffSupervisorGrants()` `:300-362`) from IT Admin (`ItAdminGrants()` `:219-287`).
    Locations is the precedent for "lookup the supervisor can mutate": after the lookup-read
    loop, the seeder yields `Create("Locations")` + `Edit("Locations")` explicitly
    (`:308-309`). WcabOffices follows that exact shape, plus a soft-Delete grant.
  - Soft-delete is FREE: every tenant entity is FullAudited/ISoftDelete, so a `.Delete` grant
    routes through ABP's standard soft-delete (`IsDeleted=true`), preserving audit trail. The
    deep-dive confirms Option A "soft-delete for Staff Supervisor is FREE -- just add the
    .Delete permission grants" (wpxgq68y4.output, internal-roles topic).
  - Grants are hardcoded permission-string literals in the Domain seeder (Domain cannot
    reference Application.Contracts). Any permission change is seeder-only here; the permission
    strings already exist in `CaseEvaluationPermissions.cs`.
- External docs: none required -- no new ABP/EF/Angular surface; this is configuration of
  existing ABP permission grants applied via the data-seed contributor.

## Approaches considered (with tradeoffs)
1. **Grant-only in the seeder (CHOSEN).** Add WcabOffices Create/Edit/Delete to
   `StaffSupervisorGrants()`; leave Clinic Staff at Default (read). Zero new code, zero
   migration, soft-delete already works, server guards already enforce. Smallest blast radius.
2. **Add a NEW IT-Admin-only hard-delete (purge) on top.** Rejected -- the ROLES decision
   bans destructive hard-delete (modern PHI standard keeps audit trail). Hard-delete is net-new
   surface (new permission, AppService method, controller, proxy regen) with HIPAA/audit hazard.
   Not wanted for a host-scoped 6-field lookup.
3. **Keep WcabOffices IT-Admin-only (status quo / per master-data-crud-design.md Section 8).**
   Rejected -- directly contradicts the feedback. The design doc assigning WcabOffices to
   IT-Admin-only is superseded by the 2026-06-03 decision; update the doc to match.
4. **Block tenant Supervisor edits because WcabOffice is host-shared.** Considered (a tenant
   supervisor mutating host data is visible to all tenants). Rejected for Phase 1: the demo
   target is one tenant = one office (CLAUDE.md), so cross-tenant bleed is not yet a live
   concern. Documented as a residual question, not a blocker.

## Decision (locked 2026-06-03)
Grant Staff Supervisor Create + Edit + soft-Delete on WcabOffices via the data-seed
contributor, exactly like Locations and the IP1/IP2 lookups. Clinic Staff stays read-only
(Default), unchanged. No hard-delete. No backend/AppService/migration changes. Depends on IR1
(the role-consolidation / Supervisor grant rework) landing the grant scaffolding.

## Implementation outline (no code)
1. **(IR1 dependency)** Ensure IR1's Staff Supervisor grant rework is in place; IP5 piggybacks
   on it. Decide with IR1 whether master-data Create/Edit/Delete grants are factored into a
   shared "supervisor-mutable lookups" set or yielded one-off per entity (Locations style).
2. **Seeder grant (Domain):** in `StaffSupervisorGrants()` (`InternalUserRoleDataSeedContributor.cs`),
   after the existing `Create("Locations")`/`Edit("Locations")`, add `Create("WcabOffices")`,
   `Edit("WcabOffices")`, `Delete("WcabOffices")`. If IP1/IP2/IP4 land together, factor the
   four mutable lookups (AppointmentTypes, AppointmentLanguages, Locations, WcabOffices) into
   one loop to keep the seeder DRY. Update the method docstring (currently says "no .Delete").
3. **No permission-string additions** -- WcabOffices.Create/Edit/Delete already exist in
   `CaseEvaluationPermissions.cs:62-68` + provider `:39-42`.
4. **Re-seed:** existing seeded tenants need a re-seed (DbMigrator) to pick up new grants;
   note this in the PR test plan (blast_radius: "Existing seeded tenants need a re-seed").
5. **Enforcement:** SERVER side already enforced by `WcabOfficesAppService` `[Authorize]`
   per-verb guards -- no change. UI side: action buttons already `*abpPermission`-gated and
   read the granted policies (`wcab-office.abstract.component.ts:58-60`); they appear
   automatically once the grant lands. No Angular code change unless the nav reparent (Phase 3
   cross-ref) is bundled.
6. **No migration, no proxy regen** -- permissions/grants are data-seed, not schema; no
   DTO/contract change.
7. **Docs:** update `docs/design/master-data-crud-design.md` Section 8 (line ~249) to reflect
   Staff Supervisor CRUD instead of IT-Admin-only.

## Dependencies
- Depends on: **IR1** (Staff Supervisor grant rework / role consolidation scaffolding).
- Sibling (bundle to keep seeder DRY): **IP1** (Appointment Types), **IP2** (Appointment
  Languages), and any IP4 master-data grant -- same one-file seeder edit pattern.
- Coordinate with: **IP3** (Doctor entity fate) -- if "Doctor Management" nav is hidden,
  WcabOffices route must reparent.

## Residual open questions
- Nav reparent target once "Doctor Management" is hidden (User Management vs Appointment
  Management vs a master-data group). Minor; pick the group matching Locations/Types.
- Acceptable that a tenant Staff Supervisor mutates host-scoped WcabOffice data shared across
  all tenants? Fine for Phase 1 (one tenant = one office); revisit if multi-tenant goes live.
