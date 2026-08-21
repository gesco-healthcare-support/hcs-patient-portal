---
id: IP2
title: Grant Staff Supervisor full CRUD on Appointment Languages (Clinic Staff read-only)
type: enhancement
components: [angular/src/app/appointment-languages/, src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs]
related_known_bugs: [OBS-36]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
Staff Supervisor and above must be able to Create / Edit / soft-Delete Appointment Languages
via the existing Appointment Management -> Appointment Languages page. Clinic Staff stays
read-only. The CRUD page, controller, AppService, manager, and permission definitions already
exist; only the role grants are missing. Optionally backfill the missing list-page filter
form and bulk-delete column (the Languages list is the thinnest of the four lookup UIs) --
documented here as a minor follow-on, not required for the role change.

## Current behavior (from investigation)
- All four permissions (`AppointmentLanguages.Default/Create/Edit/Delete`) are defined and
  registered: `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs:46-52`
  and `.../CaseEvaluationPermissionDefinitionProvider.cs:31-34` (no `MultiTenancySides`
  constraint => Both).
- The full CRUD stack is built: AppService class-level `[Authorize(...AppointmentLanguages.Default)]`
  with method gates Create line 52, Edit line 59, Delete line 46
  (`src/HealthcareSupport.CaseEvaluation.Application/AppointmentLanguages/AppointmentLanguagesAppService.cs:18,46,52,59`);
  manual controller at `api/app/appointment-languages` GET/POST/PUT/DELETE
  (`.../HttpApi/Controllers/AppointmentLanguages/AppointmentLanguageController.cs`); domain
  guards in `.../Domain/AppointmentLanguages/AppointmentLanguageManager.cs` (Name-only, Check guards).
- Role grants today: `AppointmentLanguages` is in `LookupReadEntities`
  (`InternalUserRoleDataSeedContributor.cs:201`), so both Staff Supervisor
  (StaffSupervisorGrants loop, line 304-307) and Clinic Staff (ClinicStaffGrants loop,
  line 415-418) receive `Default` (read) only. `AppointmentLanguages` is also in `AllEntities`
  (line 146), so the ItAdminGrants loop (line 223-227) yields full CRUD => IT Admin already
  has Create/Edit/Delete.
- Net result: only IT Admin can mutate languages today; Staff Supervisor cannot, which the
  feedback flags as a gap.
- Frontend Create button is gated `CaseEvaluation.AppointmentLanguages.Create` (line 5),
  Edit line 51, Delete line 59 in
  `angular/src/app/appointment-languages/appointment-language/components/appointment-language.component.html`;
  action-button visibility reads Edit/Delete granted policies in
  `appointment-language.abstract.component.ts:54-60`. Detail form is a single Name field
  (`appointment-language-detail.component.html`). Nav entry under Appointment Management,
  `requiredPolicy CaseEvaluation.AppointmentLanguages`, order 3
  (`appointment-language-base.routes.ts:11-17`; `app.routes.ts:92`).
- The list page has NO filter form and NO bulk-delete/checkbox column -- the thinnest of the
  four lookup list UIs (Locations is the richest, with filters + bulk delete).
- `AppointmentLanguages` has no `DataSeedContributor`; the picker defaults to English via a
  null FK and the list is empty until an admin creates entries (Domain CLAUDE.md "Thin
  host-scoped lookups"). This makes the CRUD-by-Supervisor grant practically useful: someone
  below IT Admin should be able to seed the demo tenant's languages.

## Relevant code locations
- `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs`
  -- the ONLY required edit: extend `StaffSupervisorGrants()` to yield
  Create/Edit/Delete("AppointmentLanguages") (mirroring the existing Locations exception at
  line 308-309). Clinic Staff loop untouched (stays read-only via LookupReadEntities).
- (Optional list polish) `angular/src/app/appointment-languages/appointment-language/components/appointment-language.component.html`
  + `appointment-language.abstract.component.ts` -- add filter form + bulk-delete column to
  match the Locations pattern.
- (Optional bulk delete) `.../HttpApi/Controllers/AppointmentLanguages/AppointmentLanguageController.cs`
  + `AppointmentLanguagesAppService.cs` -- no bulk-delete endpoint exists today; one would be
  net-new if the list polish is taken.

## Phase 3 cross-reference
- IP1 (Appointment Types CRUD for Staff Supervisor) is the identical pattern on a sibling
  lookup; do both in the same seeder edit + same migration-free deploy to keep the role
  matrix coherent.
- IR1 (consolidate internal roles to 3; Staff Supervisor gets soft-Delete on all tenant
  entities + create-power) establishes the role model this grant slots into; IP2 must land
  after IR1's grant-structure decisions to avoid double-editing the seeder.
- OBS-36 (23 stub templates) is only tangential ("pending parity" surfaces); no code overlap,
  no action here.

## Research findings
- Internal patterns / prior art:
  - The seeder already has the exact precedent: Locations is in `LookupReadEntities` (read for
    all tenant roles) yet `StaffSupervisorGrants()` adds `Create("Locations")` + `Edit("Locations")`
    explicitly at lines 308-309 (no Delete). The same two/three lines for AppointmentLanguages
    is the established way to widen one lookup above the read-only default.
  - Soft-delete is free: every tenant entity is FullAudited/ISoftDelete, so granting
    `Delete("AppointmentLanguages")` performs a soft-delete (audit trail preserved), matching
    the locked ALL-deletes-are-soft decision -- no destructive hard-delete is built.
  - Grants are seeded data, not schema: editing the seeder requires NO EF migration. They
    re-apply on DbMigrator run; permission rows are upserted by the role data-seed contributor.
- External docs (ABP / Angular / EF Core):
  - ABP permission management seeds role grants via `IDataSeedContributor`; permission strings
    are idempotently granted on each seed pass. No DB schema change. (HIGH -- mirrors the
    existing contributor structure in this file.)

## Approaches considered (with tradeoffs)
- Chosen -- explicit per-entity yields in `StaffSupervisorGrants()` (Create/Edit/Delete for
  AppointmentLanguages), Clinic Staff unchanged. Smallest diff, mirrors the Locations
  precedent, no migration, no new permission strings (all four already defined). Wins on
  minimal blast radius and consistency with IP1.
- Rejected -- move `AppointmentLanguages` into a new "Supervisor-CRUD lookups" array and loop
  it. Cleaner long-term if many lookups graduate to Supervisor CRUD, but premature: only
  Types (IP1) + Languages (IP2) are in scope, and Locations already uses ad-hoc yields, so a
  new array would leave an inconsistent half-migration. Defer the refactor.
- Rejected -- grant Staff Supervisor Create/Edit only, no Delete (the Locations precedent).
  Conflicts with the locked decision that Staff Supervisor gets soft-Delete on ALL tenant
  entities; full CRUD including soft-Delete is intended. Locations not having Delete is
  pre-existing drift that IR1 may reconcile separately, not a model to copy here.
- Rejected -- frontend-only gating (hide/show buttons). The permission strings and server
  gates already exist; UI buttons key off granted policies automatically once the seeder
  grants them. No frontend change is needed for the role behavior.

## Decision (locked 2026-06-03)
Grant Staff Supervisor full CRUD (Create + Edit + soft-Delete) on AppointmentLanguages by
adding explicit yields in `StaffSupervisorGrants()`, mirroring the Locations exception.
Clinic Staff stays read-only (unchanged). IT Admin already has full CRUD. Server enforcement
is the existing AppService method gates; the UI reflects grants automatically. The optional
list-page filter + bulk-delete polish is noted but NOT required for this item. Do this
alongside IP1 and after IR1's role-model grants land.

## Implementation outline (no code)
1. (After IR1 seeder structure is settled) In `InternalUserRoleDataSeedContributor.cs`,
   inside `StaffSupervisorGrants()` add three yields: `Create("AppointmentLanguages")`,
   `Edit("AppointmentLanguages")`, `Delete("AppointmentLanguages")` -- placed next to the
   existing Locations yields (line 308-309) with a one-line comment citing IP2/2026-06-03.
   Leave `AppointmentLanguages` in `LookupReadEntities` so Clinic Staff keeps read.
2. NO EF migration (seed data only). NO proxy regen (no contract/DTO change).
3. Re-seed: DbMigrator run grants the new permissions to the Staff Supervisor role.
4. Server-vs-UI enforcement: server gates already enforced via AppService method
   `[Authorize]` attributes (lines 46/52/59); UI buttons (`appointment-language.component.html`
   lines 5/51/59) reflect granted policies automatically -- no frontend edit required.
5. (Optional, separate task) List-page polish: add filter form + checkbox/bulk-delete column
   to `appointment-language.component.html` + `.abstract.component.ts` to match Locations,
   plus a bulk-delete endpoint on the controller/AppService. Flag: this is net-new server
   surface (no bulk-delete endpoint exists today) -- scope/defer with IP1/IP4 list-UX work.

## Dependencies
- Depends on: IR1 (role-model consolidation; defines Staff Supervisor grant structure and
  soft-Delete-on-all-tenant-entities rule).
- Sibling of: IP1 (Appointment Types CRUD) -- same seeder edit, do together.

## Residual open questions
- none (the prior conflict with master-data-crud-design.md Section 7, which assigned Languages
  CRUD to IT Admin only, is superseded by the locked 2026-06-03 decision granting Staff
  Supervisor full CRUD; that doc should be updated to match when touched).
