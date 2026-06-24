---
id: CI1
title: Move Insurance + Claim Examiner from per-injury to per-appointment (one each)
type: tech-debt
components: [angular/src/app/appointments/sections/appointment-add-claim-information.component.ts, angular/src/app/appointments/sections/appointment-add-attorney-section.component.ts, angular/src/app/appointments/appointment-add.component.ts, angular/src/app/appointments/appointment/components/appointment-view.component.ts, src/HealthcareSupport.CaseEvaluation.Domain/AppointmentClaimExaminers/AppointmentClaimExaminer.cs, src/HealthcareSupport.CaseEvaluation.Domain/AppointmentPrimaryInsurances/AppointmentPrimaryInsurance.cs, src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs, src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs]
related_known_bugs: [OBS-17, BUG-045, OBS-35, BUG-042, CI2, CI3, CI4, UM3]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
Move the Insurance and Claim Examiner (CE) sections out of the per-injury "Claim Information"
modal and render each as a single section on the main booking form, BELOW the Attorney
(AA/DA) sections. There is exactly ONE insurance and ONE CE per appointment request, not one
per injury. CE simultaneously becomes a first-class REQUIRED party (per UM3); insurance stays
optional. This is a data-model migration (FK move + backfill), not a UI relocation.

## Current behavior (from investigation)
- Insurance/CE are children of `AppointmentInjuryDetail`, not `Appointment`. Both entities
  carry `Guid AppointmentInjuryDetailId` (AppointmentClaimExaminer.cs:19; AppointmentPrimaryInsurance.cs:19).
- EF wires each as a required many-to-one to injury:
  `b.HasOne<AppointmentInjuryDetail>().WithMany().IsRequired().HasForeignKey(x => x.AppointmentInjuryDetailId)`
  (CaseEvaluationTenantDbContext.cs:546 for CE, :562 for insurance); the block is duplicated
  in CaseEvaluationDbContext.cs per the dual-context rule.
- The SPA bundles `primaryInsurance` + `claimExaminer` into every `AppointmentInjuryDraft`
  (appointment-add-claim-information.component.ts:25-64) with their FormControls in the
  per-injury flat FormGroup (:188-207); the two cards render INSIDE the modal
  (appointment-add-claim-information.component.html:233-486).
- Persist path POSTs one insurance row + one CE row PER injury draft inside
  `persistInjuryDraftsIfProvided` (appointment-add.component.ts:2752-2802).
- Read path returns one insurance/CE per injury via `.First()/.FirstOrDefault()`
  (EfCoreAppointmentInjuryDetailRepository.cs:36-37, 54-59, 73-74); view renders per injury row
  (appointment-view.component.ts:1196-1235).
- Drift to reconcile: a top-level `Appointment.ClaimExaminerEmail` field already exists, plus
  vestigial top-level claimExaminer* booker controls (appointment-add.component.ts:473-475),
  and the CE email fan-out currently reads `injuryDrafts[0].claimExaminer.email`
  (appointment-add.component.ts:1149-1151).
- OBS-17 records that per-injury-modal placement was a DELIBERATE prior decision
  (status resolved-not-a-bug). CI1 reverses it.

## Relevant code locations
Backend:
- src/HealthcareSupport.CaseEvaluation.Domain/AppointmentClaimExaminers/AppointmentClaimExaminer.cs:19,57-62 (FK field + ctor)
- src/HealthcareSupport.CaseEvaluation.Domain/AppointmentPrimaryInsurances/AppointmentPrimaryInsurance.cs:19 (FK field)
- src/HealthcareSupport.CaseEvaluation.Domain/AppointmentInjuryDetails/AppointmentInjuryDetailWithNavigationProperties.cs:17-18 (CE + insurance hang off injury)
- src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs:533-564 (both configs)
- src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs (duplicate config)
- src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/AppointmentInjuryDetails/EfCoreAppointmentInjuryDetailRepository.cs:36-37,54-59,73-74 (read joins by AppointmentInjuryDetailId)
- src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentPrimaryInsurances/* and AppointmentClaimExaminers/* (Create/Update/Dto carry appointmentInjuryDetailId)
- src/HealthcareSupport.CaseEvaluation.Application/AppointmentPrimaryInsurances/AppointmentPrimaryInsurancesAppService.cs, .../AppointmentClaimExaminers/AppointmentClaimExaminersAppService.cs
- Mapperly: CaseEvaluationApplicationMappers.cs partials (re-point FK member)
Frontend:
- angular/src/app/appointments/sections/appointment-add-claim-information.component.ts:25-64,188-207
- angular/src/app/appointments/sections/appointment-add-claim-information.component.html:233-486
- angular/src/app/appointments/sections/appointment-add-attorney-section.component.ts (new sections sit below this)
- angular/src/app/appointments/appointment-add.component.ts:473-475,1030-1032,1149-1151,2752-2802
- angular/src/app/appointments/appointment/components/appointment-view.component.ts:1196-1235
- angular/src/app/appointments/sections/appointment-add-claim-information.component.spec.ts (insurance/CE validator tests move out)
- angular/src/app/proxy/appointment-primary-insurances/models.ts, angular/src/app/proxy/appointment-claim-examiners/* (regen after FK change)

## Phase 3 cross-reference
- BUG-045: internal auto-approve booking drops insurance/CE via a 409 on the per-injury
  attach. Restructuring the persist path to one-per-appointment removes the per-injury attach
  that triggers the 409, so FIX it in the same pass to avoid leaving dead code.
- OBS-35: CE scope filter keys off `injury.ClaimExaminerEmail`. Moving CE to appointment level
  changes what the scope filter must match; converge it on `Appointment.ClaimExaminerEmail`
  here so scope and fan-out read one source.
- CI2: lift insurance/CE out of the repeatable injury modal (the per-injury draft loses
  those members) -- the natural twin of CI1; ship together.
- CI4: drops the "Attention" column on AppointmentPrimaryInsurance (visible at
  CaseEvaluationTenantDbContext.cs:556). Bundle the DropColumn into the SAME migration as the
  FK move to avoid two migrations on this table.
- UM3: CE becomes first-class + REQUIRED + a new ClaimExaminer MASTER entity under User
  Management; CI1 provides the appointment-level CE slot UM3 fills.

## Research findings
- Internal patterns / prior art:
  - AA/DA already model the appointment-level relationship CI1 wants for CE/insurance: join
    entities (`AppointmentApplicantAttorney`, `AppointmentDefenseAttorney`) link attorneys to
    the appointment, rendered via the shared appointment-add-attorney-section.component. The
    new CE/insurance sections mirror this placement (below attorney sections).
  - BUG-042 precedent: per-injury CE stores Name/Email/Suite/etc as plain text columns
    (AppointmentClaimExaminer.cs:22-49) and renders fine; the new appointment-level section
    inherits that same column shape -- no value-object rework needed.
  - PF-001 / Issue 2.3 (AppointmentClaimExaminer.cs:24-29; same on insurance) is precedent for
    column-level EF migrations on these exact entities (RenameColumn). CI1 + CI4 follow the
    same migration mechanics (the FK move is a column re-point + new FK; Attention is DropColumn).
  - Dual-context rule (EntityFrameworkCore/CLAUDE.md): both DbContexts must change verbatim;
    both already declare the CE/insurance blocks (Tenant :533-564, host duplicate).
- External docs (EF Core): renaming/re-pointing a FK with data preservation = a migration with
  an explicit data-backfill step (raw SQL `UPDATE` to populate the new AppointmentId from the
  old injury row's AppointmentId) BEFORE dropping the old FK column. EF will scaffold the
  schema delta but NOT the backfill; the backfill must be hand-authored in the migration
  `Up()`. Confirm against docs/database/MIGRATION-GUIDE.md for the project's `dotnet ef` flags.

## Approaches considered (with tradeoffs)
1. Move the FK to `Appointment` + backfill (CHOSEN). Single source of truth: one insurance
   row + one CE row link directly to the appointment. Read/persist/scope/fan-out all collapse
   to one lookup. Cost: schema migration with backfill across both contexts, DTO/proxy/mapper
   churn. Wins because the data model then MATCHES the product reality (one each per
   appointment) instead of papering over a per-injury shape.
2. Keep per-injury FK, enforce single-row + relocate UI only. Cheapest (no migration), but
   leaves a per-injury table modeling an appointment-level fact: ambiguous "which injury owns
   the CE" for packet/email/scope, and the single-row rule is only an app-layer convention the
   schema does not guard. Rejected -- perpetuates the OBS-17 mismatch the feedback is killing.
3. Drop the CE/insurance tables entirely; fold fields onto `Appointment` columns. Smallest row
   count, but CE is becoming a first-class entity with a master record (UM3) and insurance has
   ~10 fields; flattening fights UM3 and bloats the Appointment table. Rejected.

## Decision (locked 2026-06-03)
Migrate the FK on BOTH `AppointmentPrimaryInsurance` and `AppointmentClaimExaminer` from
`AppointmentInjuryDetailId` to `AppointmentId` (one each per appointment) with a backfill of
existing rows. Update both DbContexts, Mapperly mappers, Create/Update/Dto contracts, proxy
regen, the persist path (one POST per appointment, not per injury), and the view read path.
Render insurance + CE as single form sections below the attorney sections. CE becomes
first-class + REQUIRED (UM3) and converges on `Appointment.ClaimExaminerEmail` for fan-out +
scope. Supersedes OBS-17. Bundle CI2 (per-injury decoupling) and CI4 (Attention DropColumn)
into the same migration/PR.

## Implementation outline (no code)
1. Domain: re-point FK on AppointmentClaimExaminer.cs:19,57-62 and AppointmentPrimaryInsurance.cs:19
   from `AppointmentInjuryDetailId` to `AppointmentId` (field + ctor param).
2. EF config: change both `b.HasOne<AppointmentInjuryDetail>()...HasForeignKey(...InjuryDetailId)`
   blocks (Tenant :546,:562) to `HasOne<Appointment>().WithMany().HasForeignKey(x => x.AppointmentId)`;
   mirror verbatim in CaseEvaluationDbContext.cs. Keep CE required, insurance optional per UM3.
3. Migration (FLAG: schema + data): add `AppointmentId`, backfill via raw SQL from each row's
   injury -> injury.AppointmentId, add new FK, drop old `AppointmentInjuryDetailId` FK + column.
   In the SAME migration, DropColumn `Attention` (CI4). Verify FK delete behavior vs the existing
   NoAction. Confirm flags against docs/database/MIGRATION-GUIDE.md.
4. Repo: EfCoreAppointmentInjuryDetailRepository.cs:36-37,54-59,73-74 -- stop joining CE/insurance
   by injury; move the single appointment-level read to the appointment read repo / nav-props DTO.
   Remove CE/insurance from AppointmentInjuryDetailWithNavigationProperties.cs:17-18.
5. Contracts + mappers: re-point Create/Update/Dto FK member to appointmentId; update the two
   AppointmentPrimaryInsurances/AppointmentClaimExaminers Mapperly partials. Regenerate Angular
   proxy (FLAG: proxy regen -- never hand-edit angular/src/app/proxy/).
6. Frontend: strip primaryInsurance/claimExaminer from AppointmentInjuryDraft (component.ts:25-64)
   and the per-injury FormGroup (:188-207); remove the two cards from the modal HTML
   (:233-486). Add two new form-level sections rendered below appointment-add-attorney-section.
   Reconcile vestigial top-level CE controls (appointment-add.component.ts:473-475) and the
   fan-out read (:1149-1151) onto the new single CE section.
7. Persist path: replace the per-injury insurance/CE POST loop (appointment-add.component.ts:2752-2802)
   with one insurance + one CE POST per appointment. This removes the BUG-045 per-injury 409 path.
8. View path: appointment-view.component.ts:1196-1235 reads the single appointment-level
   insurance/CE instead of per-injury rows.
9. Validation (server + UI): CE REQUIRED enforced in DTO + AppointmentManager AND mirrored in
   the form section (UM3); insurance optional. Panel-number/strike-list rules are separate items.
10. Tests: move insurance/CE validator coverage out of
    appointment-add-claim-information.component.spec.ts to the new sections.

## Dependencies
- Blocks/co-ships: CI2 (per-injury decoupling), CI4 (Attention DropColumn -- same migration).
- Tightly coupled: UM3 (CE first-class master + required) -- CI1 provides the slot it fills;
  align field shape and required rule.
- Fixes while here: BUG-045 (per-injury 409 attach), OBS-35 (scope filter source).
- Supersedes: OBS-17.

## Residual open questions
- Confirm the FK delete behavior on the new Appointment-level FK (existing config uses
  NoAction; all deletes are soft per the role decision, so cascade is moot, but keep NoAction
  explicit). Minor.
- UM3 finalizes whether the per-appointment CE row references the new ClaimExaminer master by
  id or copies text columns (BUG-042 shape). CI1 keeps the text-column shape unless UM3 says
  otherwise; resolve in UM3, not here.
