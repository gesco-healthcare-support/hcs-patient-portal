---
id: UM3
title: Re-parent Applicant/Defense Attorney nav under User Management and add a new Claim Examiner master CRUD
type: enhancement
components: [angular/src/app/applicant-attorneys/, angular/src/app/defense-attorneys/, angular/src/app/route.provider.ts, angular/src/app/app.routes.ts, src/HealthcareSupport.CaseEvaluation.Domain/, src/HealthcareSupport.CaseEvaluation.Application/, src/HealthcareSupport.CaseEvaluation.Application.Contracts/, src/HealthcareSupport.CaseEvaluation.HttpApi/, src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/, src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json]
related_known_bugs: [OBS-8, BUG-041, BUG-042, OBS-32]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
Two structural moves under User Management:
1. Re-parent the existing Applicant Attorney and Defense Attorney CRUD pages so they live as children of the "User Management" nav node (today they are top-level nav items).
2. Add a brand-new Claim Examiner (CE) section: a per-user MASTER entity + manager + AppService + manual controller + permission + Angular CRUD, parallel to Applicant/Defense Attorney. No standalone CE master exists today.

The CE invite role and identity role already exist. The new master is the lookup source the per-appointment required CE references (per CI1). This OVERRIDES OBS-8 (which documented CE as per-claim, not per-user).

## Current behavior (from investigation)
- AA/DA CRUD pages are TOP-LEVEL nav entries with NO `parentName`:
  - `angular/src/app/applicant-attorneys/applicant-attorney/providers/applicant-attorney-base.routes.ts:3-12` (path `/applicant-attorneys`, name `::Menu:ApplicantAttorneys`, no parentName) -- verified.
  - `angular/src/app/defense-attorneys/defense-attorney/providers/defense-attorney-base.routes.ts:3-12` (symmetric, `/defense-attorneys`).
- Top-level routes registered at `angular/src/app/app.routes.ts:137-138` (`/applicant-attorneys`, `/defense-attorneys`).
- User Management parent + its only two children verified at `angular/src/app/route.provider.ts:35-65`: parent `::Menu:UserManagement` (requiredPolicy `CaseEvaluation.UserManagement`), children Invite External User (`/users/invite`, order 1) and Internal Users (`/internal-users`, order 2).
- NO standalone ClaimExaminer feature exists. Confirmed: no `Domain/ClaimExaminers` folder, no `class ClaimExaminer`, no `angular/src/app/claim-examiners` dir, no `ClaimExaminers` permission (findings UM3, lines 710-711).
- The only CE construct is per-appointment `AppointmentClaimExaminer` (`src/HealthcareSupport.CaseEvaluation.Domain/AppointmentClaimExaminers/AppointmentClaimExaminer.cs`; no IdentityUserId; stores Name/Email/Phone/Fax/address as free-text columns), plus `ExternalUserType.ClaimExaminer` (numeric 2) and the "Claim Examiner" identity role.
- Permissions: `ApplicantAttorneys` at `CaseEvaluationPermissions.cs:147-153`, `DefenseAttorneys` at `:163-169`, `AppointmentClaimExaminers` (per-appointment only, NOT a standalone master) at `:195-201` -- verified. No standalone `ClaimExaminers` permission class.
- Localization: `Menu:ApplicantAttorneys` and `Menu:DefenseAttorneys` exist at `en.json:469,487` -- verified. `Menu:UserManagement` / `InviteExternalUser` / `InternalUsers` at `en.json:573-575`.
- AA/DA CRUD is ABP-Suite-generated: abstract + concrete component, NgxDatatable list + modal detail. Attorney detail form (`applicant-attorney-detail.abstract.service.ts:41-67`) collects firmName/firmAddress/webAddress/phone/fax/street/city/zip/stateId/identityUserId, with `identityUserId` `Validators.required` and NO firstName/lastName (BUG-042).

## Relevant code locations
Nav re-parent (part 1):
- `angular/src/app/applicant-attorneys/applicant-attorney/providers/applicant-attorney-base.routes.ts:3-12`
- `angular/src/app/defense-attorneys/defense-attorney/providers/defense-attorney-base.routes.ts:3-12`
- `angular/src/app/app.routes.ts:137-138` (top-level routes)
- `angular/src/app/route.provider.ts:35-65` (User Management parent; child registration pattern to copy)

New CE master (part 2; parallel to AA/DA, by layer):
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/` -- CE consts (max lengths), `en.json` keys
- `src/HealthcareSupport.CaseEvaluation.Domain/ClaimExaminers/` (NEW) -- `ClaimExaminer` entity + `ClaimExaminerManager`
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ClaimExaminers/` (NEW) -- DTOs + `IClaimExaminersAppService`; `CaseEvaluationPermissions.cs` new nested `ClaimExaminers` class + DefinitionProvider registration
- `src/HealthcareSupport.CaseEvaluation.Application/ClaimExaminers/` (NEW) -- AppService impl + Mapperly mapper partial
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/ClaimExaminers/` (NEW) -- manual controller `api/app/claim-examiners`
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/` -- DbContext entity config + EF migration
- `angular/src/app/claim-examiners/` (NEW) -- CRUD feature dir + base.routes provider, registered under User Management
- `angular/src/app/proxy/` -- regenerate (never hand-edit)

## Phase 3 cross-reference
- OBS-8 (firmname-aa-da-only / CE-is-per-claim): this item OVERRIDES it. Note in the OBS-8 record that the per-user CE master is now the decided model (CE is per-appointment via the new master, supersedes per-claim/per-carrier rationale).
- BUG-042 (attorney-name-not-persisted): AA/DA master tables have no FirstName/LastName column. While re-touching AA/DA here, bundle BUG-042's name-column fix so the new CE master and the relocated attorney CRUDs share a consistent name-bearing shape (CE master should be designed WITH FirstName/LastName from day one).
- BUG-041 (authorized-user-picker-parity-gap / D-2 exclusion): a new CE master surfaces CE profiles; this partially reverses the D-2 decision that DA/CE saved profiles are excluded from lookups. Reconcile the picker behavior when CI1 wires the per-appointment CE selector to the new master.
- OBS-32 (booker-aa-section-prefill-first-name-only): same single-name-field defect at booking prefill; same root as BUG-042, fix together.

## Research findings
- Internal patterns / prior art:
  - Child-nav registration: copy the exact `route.provider.ts:43-64` pattern (set `parentName: '::Menu:UserManagement'`, give an `order`, keep `requiredPolicy`). Re-parenting an ABP `ABP.Route[]` entry is purely adding `parentName` to the base.routes object.
  - CRUD scaffolding shape: AA/DA are the canonical ABP-Suite abstract+concrete + modal-detail pattern (`applicant-attorney.component.ts`, `applicant-attorney-detail.component.ts`, `applicant-attorney-detail.abstract.service.ts:41-67`). The new CE feature mirrors this layer-for-layer.
  - Permission pattern: nested static class in `CaseEvaluationPermissions.cs` (Default/Edit/Create/Delete) AND registration in `CaseEvaluationPermissionDefinitionProvider.cs` (per Application.Contracts CLAUDE.md gotcha: unregistered permissions never appear in the admin UI).
  - Backend wiring rules (project dotnet.md): AppService extends `CaseEvaluationAppService` + `[RemoteService(IsEnabled=false)]`; paired manual controller `AbpController` implementing `IClaimExaminersAppService` at `api/app/claim-examiners`; Riok.Mapperly mapper registered in `CaseEvaluationApplicationMappers.cs` (never AutoMapper / `ObjectMapper.Map<>`).
  - Localization rule (Domain.Shared CLAUDE.md): every new `L()` / `| abpLocalization` key must exist in `en.json` first; localization is additive.
  - CE entity is tenant-scoped like AA/DA -> add to tenant DbContext (`CaseEvaluationTenantDbContext`), not host. Make CE FullAudited/ISoftDelete to match the all-deletes-stay-soft global decision.
- External docs: standard ABP application-service + permission + EF Core code-first migration patterns; no novel framework behavior. ABP nav child relationship documented via `ABP.Route.parentName`.

## Approaches considered (with tradeoffs)
1. CE section = NEW per-user master entity (CHOSEN). Parallel to AA/DA, with its own table/AppService/permission/proxy/Angular CRUD.
   - Pro: gives CI1 a real lookup source for the now-required single per-appointment CE; consistent with how AA/DA are managed; supports reuse across appointments; aligns with "first-class party" decision for CE.
   - Con: heaviest option (new entity + migration + full stack); reverses OBS-8 and partly BUG-041 (accepted, both explicitly overridden by the 2026-06-03 decisions).
2. CE section = list/edit existing "Claim Examiner"-role IdentityUsers (REJECTED).
   - Pro: no new entity.
   - Con: no place to store CE-specific profile fields as a reusable master; identity users are not a profile store; would not give CI1 a clean per-appointment lookup parallel to AA/DA.
3. CE section = surface the per-appointment `AppointmentClaimExaminer` free-text rows (REJECTED).
   - Pro: reuses existing data.
   - Con: those rows are per-appointment snapshots (no IdentityUserId, free-text), not a deduplicated master; cannot serve as a CRUD-managed reusable directory.
4. Re-path AA/DA URLs under `/users/...` vs keep existing `/applicant-attorneys`, `/defense-attorneys` and only re-parent the menu node.
   - Decision keeps it as a nav re-parent + re-path of the two base.routes entries per the locked decision; re-pathing is noted as breaking saved bookmarks (minor, internal-only pages).

Why the chosen direction wins: the CE became a first-class REQUIRED appointment party (CI1) and needs a managed, reusable directory exactly like AA/DA. Only a real master entity provides that; the two reuse-existing-data alternatives cannot back a clean per-appointment lookup or hold CE profile fields.

## Decision (locked 2026-06-03)
- Re-parent AA + DA nav under User Management by adding `parentName: '::Menu:UserManagement'` to both base.routes entries and updating the two routes (`app.routes.ts:137-138` plus base.routes), keeping their existing entity permissions.
- Add a NEW `ClaimExaminer` MASTER entity (per-user, like AA/DA): entity + `ClaimExaminerManager` + DTOs + `IClaimExaminersAppService` + AppService + Mapperly mapper + manual controller (`api/app/claim-examiners`) + new `ClaimExaminers` permission (registered in DefinitionProvider) + Angular CRUD registered under User Management.
- CE master is the lookup source for the single required per-appointment `AppointmentClaimExaminer` (CI1). CE invite role already exists; no role work here.
- CE master is tenant-scoped, FullAudited/ISoftDelete (soft delete only). Design it WITH FirstName/LastName (bundle BUG-042 naming consistency). CE has NO firm fields (OBS-8: firm is attorney-specific).
- Overrides OBS-8. CRUD shape mirrors whatever UM4/IP6 settle for Patients/attorney CRUD (depends on that shape).

## Implementation outline (no code)
1. Nav re-parent (frontend-only, no migration):
   - Add `parentName: '::Menu:UserManagement'` (+ `order`) to `applicant-attorney-base.routes.ts:3-12` and `defense-attorney-base.routes.ts:3-12`.
   - Update/confirm routes at `app.routes.ts:137-138` per the locked re-path.
   - Keep `requiredPolicy` as `CaseEvaluation.ApplicantAttorneys` / `.DefenseAttorneys` (gating unchanged).
2. CE master backend (server-authoritative):
   - Domain.Shared: add CE consts + `en.json` keys (`Menu:ClaimExaminers`, `ClaimExaminer`, `NewClaimExaminer`, field labels, `Permission:ClaimExaminers`).
   - Domain: `ClaimExaminer` entity (FullAudited, ISoftDelete, tenant-scoped; FirstName/LastName/Email/Phone/Fax/address fields; NO firm) + `ClaimExaminerManager` (invariants/uniqueness orchestration).
   - Application.Contracts: `ClaimExaminerCreateDto` / `ClaimExaminerUpdateDto` / `ClaimExaminerDto` / `GetClaimExaminersInput` + `IClaimExaminersAppService`; new `ClaimExaminers` nested permission class in `CaseEvaluationPermissions.cs` AND register it in `CaseEvaluationPermissionDefinitionProvider.cs`.
   - Application: AppService (`[RemoteService(IsEnabled=false)]`, extends `CaseEvaluationAppService`) + Mapperly mapper partial registered in `CaseEvaluationApplicationMappers.cs`.
   - HttpApi: manual controller `AbpController` implementing `IClaimExaminersAppService`, route `api/app/claim-examiners`, thin delegation.
   - EntityFrameworkCore: entity config on `CaseEvaluationTenantDbContext` + EF migration (MIGRATION: new tenant table; run code-first add-migration).
3. Proxy regen: run `abp generate-proxy` (never hand-edit `angular/src/app/proxy/`).
4. CE master frontend: new `angular/src/app/claim-examiners/` feature (abstract + concrete list, modal detail, base.routes with `parentName: '::Menu:UserManagement'`), gated by the new `CaseEvaluation.ClaimExaminers` policy; mirror UM4/IP6-decided Patient/attorney CRUD shape.
5. Server-vs-UI enforcement: required/format validation enforced in DTO + `ClaimExaminerManager` (server-authoritative) and mirrored in the Angular reactive form; pure UX affordances (field ordering, labels) UI-only.

## Dependencies
- DEPENDS ON: UM4 / IP6 (the decided Patients/attorney CRUD shape the CE master and relocated attorney CRUD must mirror -- e.g. whether `identityUserId` is required, whether records are free-standing).
- BLOCKS / FEEDS: CI1 (per-appointment single required CE selects from this new CE master; the lookup wiring depends on this entity existing).
- BUNDLE-WITH: BUG-042 / OBS-32 (attorney name columns) -- design CE with names and fix attorney names in the same pass for shape consistency.
- INTERACTS: BUG-041 (D-2 picker-exclusion) -- reconcile when CI1 surfaces CE profiles in the per-appointment picker.

## Residual open questions
- Final URL path for relocated attorneys (keep `/applicant-attorneys`,`/defense-attorneys` vs re-path under `/users/...`): the locked decision says re-path; confirm the exact new path string when wiring (breaks internal bookmarks only).
- Whether the new `ClaimExaminers` permission should be grouped under the existing entity-permission group or under the UserManagement permission group (UM3 open question 4); does not block the entity build -- defaults to its own entity-permission group like AA/DA.
