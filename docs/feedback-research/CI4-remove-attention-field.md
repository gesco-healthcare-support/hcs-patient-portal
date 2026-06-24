---
id: CI4
title: Remove the Attention field from the Insurance section entirely (incl. DB column)
type: enhancement
components: [angular/src/app/appointments/sections/appointment-add-claim-information.component.html, angular/src/app/appointments/sections/appointment-add-claim-information.component.ts, angular/src/app/appointments/appointment-add.component.ts, angular/src/app/proxy/appointment-primary-insurances/models.ts, src/HealthcareSupport.CaseEvaluation.Domain/AppointmentPrimaryInsurances/AppointmentPrimaryInsurance.cs, src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentPrimaryInsurances/AppointmentPrimaryInsuranceConsts.cs, src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs, src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs, src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentPrimaryInsurances/, src/HealthcareSupport.CaseEvaluation.Application/]
related_known_bugs: [OBS-41, PF-001, CI1, CI2]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change

Remove the "Attention" field from the Insurance section of the booking form entirely --
not just hidden in the UI, but removed end to end including the database column. Existing
Attention data is lost; that is acceptable.

## Current behavior (from investigation)

Attention exists end-to-end on the Insurance (`AppointmentPrimaryInsurance`) entity only;
the Claim Examiner section has no Attention field, and the appointment-view never reads it
(findings line 401).

- UI: plain text input (max 255) inside the Insurance card
  (`appointment-add-claim-information.component.html:263-272`).
- Transient form shape: carried in `AppointmentInjuryDraft.primaryInsurance.attention`
  (`appointment-add-claim-information.component.ts:44`), as `injuryInsuranceAttention`
  control (`:190`), seeded `attention:null` in `makeEmptyInjuryDraft` (`:250`), and read in
  `serializeInjuryForm` (`:343`).
- POST: sent in the per-injury insurance POST (`appointment-add.component.ts:2764`).
- Proxy DTOs: `attention?: string | null` on Create/Dto/Update interfaces
  (`proxy/appointment-primary-insurances/models.ts:7,21,36`) -- verified.
- Entity: `[CanBeNull] public virtual string? Attention`
  (`AppointmentPrimaryInsurance.cs:33-34`) -- verified (line 34 is the property; 33 is the
  `[CanBeNull]` attribute).
- Const: `AttentionMaxLength = 255` (`AppointmentPrimaryInsuranceConsts.cs:15`).
- EF config (dual-context): `CaseEvaluationTenantDbContext.cs:556`, plus a duplicate config
  in `CaseEvaluationDbContext.cs`.
- Mapperly mapper in `src/HealthcareSupport.CaseEvaluation.Application` maps Attention.
- No read-side display: `appointment-view.component.ts` `loadInjuryDetails` (1227-1230)
  reads only the insurance name (findings line 401).

## Relevant code locations

Frontend:
- `angular/src/app/appointments/sections/appointment-add-claim-information.component.html:263-272`
- `angular/src/app/appointments/sections/appointment-add-claim-information.component.ts:44,190,250,343`
- `angular/src/app/appointments/appointment-add.component.ts:2764`
- `angular/src/app/proxy/appointment-primary-insurances/models.ts:7,21,36` (regen, do not hand-edit)

Backend:
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentPrimaryInsurances/AppointmentPrimaryInsurance.cs:33-34`
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentPrimaryInsurances/AppointmentPrimaryInsuranceConsts.cs:15`
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationTenantDbContext.cs:556`
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/EntityFrameworkCore/CaseEvaluationDbContext.cs` (duplicate config)
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentPrimaryInsurances/AppointmentPrimaryInsuranceCreateDto.cs / UpdateDto.cs / Dto.cs`
- `src/HealthcareSupport.CaseEvaluation.Application/` (Mapperly mapper mapping Attention)

## Phase 3 cross-reference

- CI1 (insurance/CE out of per-claim modal -> appointment level): same `AppointmentPrimaryInsurance`
  entity and the same Insurance card. CI1 re-points the insurance FK
  (AppointmentInjuryDetailId -> Appointment) and already requires an EF migration, dual-context
  edits, Mapperly/DTO/proxy regen, and the Insurance card rework (findings cluster_notes,
  line 292). Fold the Attention DropColumn into the CI1 migration so there is one migration,
  one proxy regen, and one Insurance-card edit pass.
- CI2 (stop bundling insurance/CE per injury): trims the same `serializeInjuryForm` /
  `makeEmptyInjuryDraft` / `buildInjuryForm` paths Attention lives in; removing Attention there
  while those methods are already being trimmed avoids touching them twice.
- OBS-41 (Claim Information modal rework): the findings explicitly call out bundling Attention
  removal with the modal restructure to avoid two migrations (findings line 404).
- PF-001 / Issue 2.3 (InsuranceNumber -> Suite RenameColumn on this exact entity,
  `AppointmentPrimaryInsurance.cs:24-29`): precedent for a column-level EF migration on this
  table; DropColumn follows the same pattern (findings line 405).

## Research findings

- Internal patterns / prior art: PF-001 / Issue 2.3 already performed a column-level EF
  migration on this entity (`RenameColumn` for InsuranceNumber -> Suite), with the dual-context
  rule in force; a `DropColumn` is the symmetric operation. The dual-DbContext convention is
  documented (`.claude/rules/dotnet.md`, root CLAUDE.md): the Attention config exists in BOTH
  `CaseEvaluationTenantDbContext.cs:556` and `CaseEvaluationDbContext.cs`, so both must be
  edited or the migration diff will be inconsistent. `AppointmentPrimaryInsurance` is
  `IMultiTenant` (entity:15), so the column lives in the tenant DB; the host-context config is
  the dual-registration the convention warns about.
- Mapperly: mappers are source-generated with `RequiredMappingStrategy.Target`
  (`.claude/rules/dotnet.md`), so a leftover `Attention` member on either side after partial
  removal is a compile-time error, not a silent runtime miss -- the build will catch an
  incomplete removal.
- Proxy: `models.ts` is auto-generated; regenerate via `abp generate-proxy` after the DTO
  change. Never hand-edit (root CLAUDE.md, angular rules).
- External docs: EF Core migrations `DropColumn` is the standard generated operation when a
  mapped property is removed; no special handling needed beyond running `dotnet ef migrations add`
  for the tenant context. (Microsoft Learn, EF Core migrations -- HIGH confidence, standard API.)

## Approaches considered (with tradeoffs)

1. Full end-to-end removal incl. DropColumn (CHOSEN). Drops the HTML control + form control +
   serialize/POST; removes the entity property + const + Mapperly mapping + DTOs; DropColumn
   migration in both DbContexts; proxy regen. Tradeoff: existing Attention values are lost
   (acceptable per the locked decision; no downstream reader exists, findings line 401). Wins
   because the feedback says "entirely" and leaving a dead column violates the "keep it simple"
   posture and accrues schema cruft for a field nobody reads.
2. UI-only hide, retain column (REJECTED). Lower blast radius (HTML + form-control + serialize
   only, no migration, no proxy regen). Rejected: it does not satisfy "entirely," leaves a dead
   nullable column and an orphaned const/DTO/mapping that the next reader must rediscover, and
   the dual-context EF config keeps generating empty-column writes.
3. Standalone migration just for Attention (REJECTED). Cleanest isolation, but produces a
   second migration against the same entity that CI1 already migrates, doubling the migration +
   proxy-regen churn for one tenant. Rejected per findings line 404 (bundle to avoid two
   migrations).

## Decision (locked 2026-06-03)

Full removal including the DB column. Drop the HTML control + form control + serialize/POST;
remove `AppointmentPrimaryInsurance.Attention` + `AttentionMaxLength` const + the Mapperly
mapping + the Create/Update/Dto members; `DropColumn` migration covering BOTH DbContexts;
regenerate the proxy. Existing Attention data is lost (acceptable). Bundle the DropColumn with
the CI1 migration so there is a single migration and a single proxy regen.

## Implementation outline (no code)

Order backend-first so the proxy regen reflects the removed DTO member; bundle the EF step with
CI1.

1. Domain: remove the `Attention` property from `AppointmentPrimaryInsurance.cs:33-34`.
2. Domain.Shared: remove `AttentionMaxLength = 255` from `AppointmentPrimaryInsuranceConsts.cs:15`.
3. Application.Contracts: remove the `Attention` member from
   `AppointmentPrimaryInsuranceCreateDto.cs`, `UpdateDto.cs`, and `Dto.cs`.
4. Application: remove the `Attention` mapping from the Mapperly insurance mapper. (Source-gen
   build error is the safety net if a member is missed.)
5. EF (BOTH contexts): remove the Attention column config from `CaseEvaluationTenantDbContext.cs:556`
   and the duplicate in `CaseEvaluationDbContext.cs`. Generate ONE migration bundled with CI1
   (`dotnet ef migrations add` for the tenant context with `DOTNET_ENVIRONMENT=Development`);
   confirm the diff contains a `DropColumn("Attention", ...)`. MIGRATION -- destructive, drops
   data; acceptable per decision.
6. Proxy: regenerate via `abp generate-proxy`; verify `attention?` is gone from
   `models.ts:7,21,36`. PROXY REGEN -- do not hand-edit.
7. Angular UI: remove the Attention input from `appointment-add-claim-information.component.html:263-272`.
8. Angular form/draft: remove `attention` from `AppointmentInjuryDraft.primaryInsurance`
   (`...component.ts:44`), the `injuryInsuranceAttention` control (`:190`), the
   `makeEmptyInjuryDraft` seed (`:250`), and the `serializeInjuryForm` read (`:343`).
9. Angular POST: remove `attention` from the insurance POST payload
   (`appointment-add.component.ts:2764`).
10. Tests: update `appointment-add-claim-information.component.spec.ts` if it references the
    Attention control (CI1 already lists this spec as in scope).

Server-vs-UI enforcement: N/A -- this is a pure field removal, not a validation rule. The only
hard correctness gate is the Mapperly source-gen build, which fails the build if any
`Attention` member survives on one side of the mapper.

## Dependencies

- Bundles with: CI1 (shared EF migration + proxy regen + Insurance card edit) and the OBS-41
  modal rework. Sequence Attention removal into the SAME migration/regen pass as CI1.
- Soft overlap: CI2 trims the same `serializeInjuryForm` / `makeEmptyInjuryDraft` /
  `buildInjuryForm` methods; coordinate edits to avoid conflicting touches.
- Blocks: nothing.

## Residual open questions

None. The two findings open questions (line 408 "drop column vs hide" and line 409 "data loss
acceptable") are resolved by the 2026-06-03 decision: full DropColumn, data loss acceptable.
