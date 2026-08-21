---
id: UM4
title: Attorney + Claim Examiner CRUD mirror the simplified record-based Patients model
type: enhancement
components:
  - angular/src/app/applicant-attorneys/applicant-attorney/services/applicant-attorney-detail.abstract.service.ts
  - angular/src/app/defense-attorneys/defense-attorney/services/defense-attorney-detail.abstract.service.ts
  - src/HealthcareSupport.CaseEvaluation.Application.Contracts/ApplicantAttorneys/ApplicantAttorneyCreateDto.cs
  - src/HealthcareSupport.CaseEvaluation.Application.Contracts/ApplicantAttorneys/ApplicantAttorneyUpdateDto.cs
  - src/HealthcareSupport.CaseEvaluation.Application.Contracts/DefenseAttorneys/ (symmetric Create/Update DTOs)
  - src/HealthcareSupport.CaseEvaluation.Application/ApplicantAttorneys + DefenseAttorneys (AppService + Mapperly mappers)
  - src/HealthcareSupport.CaseEvaluation.Domain/ClaimExaminers/ (NET-NEW master entity, parallel to ApplicantAttorney)
related_known_bugs: [BUG-042, OBS-32, OBS-8, BUG-041]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
Make the Applicant Attorney and Defense Attorney CRUD -- and the net-new Claim Examiner
master CRUD -- mirror the simplified record-based Patients model from IP6: a record can be
created and edited WITHOUT a linked login account, First/Last Name are first-class
persisted+displayed fields, and the identity link is attached later on self-registration
(by email), exactly like patients. SSN and audited-reveal stay Patient-only and are NOT
mirrored (attorneys/CE hold no SSN). The CE master is structurally identical to the
attorney masters but without firm-specific fields.

## Current behavior (from investigation)
BUG-042 is only HALF done. The fix landed at the entity + read-DTO layer but never reached
the write path or the UI:

- Entity layer DONE. `ApplicantAttorney.cs:22-26` and `DefenseAttorney.cs:24-28` already
  carry `FirstName`/`LastName` (BUG-042 comment in-file). `IdentityUserId` is already
  `Guid?` on both (`ApplicantAttorney.cs:54`, `DefenseAttorney.cs:56`). The
  ApplicantAttorneys CLAUDE.md confirms "No symmetric divergence remains in the entity shape."
- Read DTO DONE. `ApplicantAttorneyDto.cs:10,12` expose `FirstName`/`LastName`.
- Write DTOs are the GAP. `ApplicantAttorneyCreateDto.cs` has NO FirstName/LastName and
  declares `public Guid IdentityUserId` (line 35) -- a non-nullable struct, so the value is
  effectively required and a record cannot be created without an identity. Same on
  `ApplicantAttorneyUpdateDto.cs:36`. So even though the column exists, names supplied on
  create/edit are silently dropped, and standalone (no-login) creation is impossible.
- Angular form is the other GAP. `applicant-attorney-detail.abstract.service.ts:55-66`
  builds firmName/firmAddress/webAddress/phoneNumber/faxNumber/street/city/zipCode/stateId
  and `identityUserId: [..., [Validators.required]]` (line 65). There are NO firstName/lastName
  controls, and identityUserId is hard-required -- so the UI cannot create a record without
  picking an existing IdentityUser, and cannot enter a name. DA form is symmetric.
- ClaimExaminer master DOES NOT EXIST. UM3 deep-dive confirms no `Domain/ClaimExaminers`
  folder, no entity/permission/AppService/proxy/Angular feature. The only CE artifact is
  the per-appointment `AppointmentClaimExaminer` (which correctly stores Name/Email as text,
  per BUG-042 note in findings) -- that is a different concern from a reusable CE master.
- Patients reference shape (IP6 target): record-based, name fields, NO required
  identityUserId, identity linked on self-registration by email; SSN-input + audited
  GetFullSsnAsync reveal are Patient-only (PatientsAppService SSN masking).
- ExternalUserType already enumerates `ClaimExaminer=2` (angular CLAUDE.md external-users
  note), so the invite-to-register path that links identity-by-email already understands CE.

## Relevant code locations
Attorneys (write path + UI):
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ApplicantAttorneys/ApplicantAttorneyCreateDto.cs:35` (add names, nullable identity)
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ApplicantAttorneys/ApplicantAttorneyUpdateDto.cs:36` (same)
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/DefenseAttorneys/` (symmetric Create/Update DTOs)
- `src/HealthcareSupport.CaseEvaluation.Application/ApplicantAttorneys/*AppService.cs` + DefenseAttorneys (CreateAsync/UpdateAsync orchestration)
- Mapperly mappers in `CaseEvaluationApplicationMappers.cs` partial set (Create/Update -> entity must map the new fields)
- `src/HealthcareSupport.CaseEvaluation.Domain/ApplicantAttorneys/ApplicantAttorneyManager.cs` (assigns post-ctor fields; add FirstName/LastName assignment)
- `angular/src/app/applicant-attorneys/applicant-attorney/services/applicant-attorney-detail.abstract.service.ts:55-66`
- `angular/src/app/defense-attorneys/defense-attorney/services/defense-attorney-detail.abstract.service.ts` (symmetric)
- `angular/src/app/{applicant-attorneys,defense-attorneys}/.../components/*-detail.component.html` (add name inputs)
- `angular/src/app/proxy/` (REGENERATE -- never hand-edit)

Claim Examiner (NET-NEW, parallel to ApplicantAttorney):
- `src/.../Domain/ClaimExaminers/ClaimExaminer.cs` + `ClaimExaminerManager.cs` + repo interface
- `src/.../Domain.Shared/ClaimExaminers/ClaimExaminerConsts.cs` (field max-lengths)
- `src/.../Application.Contracts/ClaimExaminers/` (Dto/Create/Update/Input/IAppService + permission)
- `src/.../Application/ClaimExaminers/ClaimExaminersAppService.cs` + Mapperly mappers
- `src/.../EntityFrameworkCore/` config in BOTH DbContexts (IMultiTenant, no IsHostDatabase guard) + migration
- `src/.../HttpApi/` manual controller `api/app/claim-examiners`
- `src/.../Application.Contracts/Permissions/CaseEvaluationPermissions.cs` (add ClaimExaminers group)
- `angular/src/app/claim-examiners/` feature + `route.provider.ts` nav entry (UM3 places it under User Management)

## Phase 3 cross-reference
- BUG-042 (attorney-name-not-persisted): finish it here. Entity + read DTO are done; this item
  closes the write path (Create/Update DTOs, mapper, manager, UI inputs). Do NOT re-add columns.
- OBS-32 (booker-aa-section-prefill-first-name-only): same single-name-field defect at booking
  prefill; once Create/Update carry First/Last, the booker prefill can round-trip both names.
- OBS-8 (firmname-aa-da-only): firm fields are attorney-only; the CE master must OMIT
  FirmName/FirmAddress/WebAddress/FaxNumber. Use OBS-8 as the field-shape boundary for CE.
- BUG-041 (authorized-user-picker-parity-gap): D-2 lookup-exclusion intent; a CE master + the
  invite/self-register linkback changes how CE records surface in pickers -- verify alignment
  when wiring CE, but do not reopen D-2 here.

## Research findings
- Internal patterns / prior art:
  - Patients (IP6) is the canonical mirror: record-based, no required identity, identity linked
    on self-registration by email. Reuse that decision verbatim, minus SSN.
  - AA/DA constructor 4-vs-7 split (ApplicantAttorneys CLAUDE.md): the ctor sets only 4 string
    fields; the other 7 (incl FirstName/LastName) are assigned in `Manager.CreateAsync` AFTER
    the ctor. So name persistence on create depends on the MANAGER assigning them, not the ctor.
  - Identity-by-email linkback already exists for external users: `ExternalSignupAppService`
    `InviteExternalUserAsync` + `RegisterAsync` mint the IdentityUser on redemption and link it;
    `ExternalUserType.ClaimExaminer=2` is already a recognized invite type. AA/DA AutoLink hooks
    in ExternalSignupAppService are the template for attaching identity to a pre-existing record.
  - CE master is structurally a "firm-less attorney": copy the ApplicantAttorney entity/manager/
    repo/AppService/DTO scaffold, drop firm-specific fields per OBS-8.
- External docs (ABP / Angular / EF Core):
  - ABP entity + DataSeed conventions: new IMultiTenant master needs EF config in BOTH
    DbContexts (no IsHostDatabase guard, per dotnet.md) and a migration.
  - ABP proxy regen: `abp generate-proxy` after DTO/AppService changes (project rule; never
    hand-edit `angular/src/app/proxy/`).

## Approaches considered (with tradeoffs)
1. CHOSEN: Finish BUG-042 write path on AA/DA + build a parallel firm-less ClaimExaminer
   master, all three record-based with identity-linked-on-registration.
   - Pros: matches IP6 mental model exactly; one consistent record+claim model across all
     external parties; reuses the already-built invite/linkback pipeline and ExternalUserType
     enum; closes BUG-042/OBS-32 for real; CE finally has a reusable master.
   - Cons: net-new entity + migration for CE; proxy regen; touches Create/Update DTOs + mappers.
2. REJECTED: List/edit existing "Claim Examiner"-role IdentityUsers instead of a CE master.
   - Why it loses: a role list is not a contact record; cannot hold firm-less profile fields,
     cannot exist before login, and breaks the symmetry with Applicant/Defense Attorney CRUD
     that the feedback explicitly asks to mirror.
3. REJECTED: Surface per-appointment AppointmentClaimExaminer rows as the "CE CRUD".
   - Why it loses: those are appointment-scoped snapshots, not a reusable master; editing one
     would not propagate, and there is no master to invite/link an identity to.
4. REJECTED: Keep identityUserId required and only add names.
   - Why it loses: directly contradicts IP6 (records must exist without a login) and leaves the
     admin unable to create an attorney/CE for a third party who never logs in.

## Decision (locked 2026-06-03)
Mirror the simplified Patients model (IP6) for Applicant Attorney, Defense Attorney, and a
NET-NEW Claim Examiner master:
- Add FirstName/LastName to the AA/DA Create + Update DTOs, map them through Mapperly, assign
  them in the manager, and add the inputs to the Angular detail forms (BUG-042 write path).
- Drop the required identityUserId: Create/Update DTOs use `Guid?`; the Angular forms drop
  `Validators.required` on identityUserId. Records may be created standalone; identity is
  linked later on self-registration by email (reusing the external invite/AutoLink path).
- Build ClaimExaminer as a firm-less copy of ApplicantAttorney (entity/manager/repo/DTOs/
  AppService/Mapperly/permission/controller/Angular feature), IMultiTenant, FullAudited/soft-delete.
- SSN + audited reveal stay Patient-only -- NOT mirrored onto attorneys or CE.
- Sequence AFTER IP6 (Patients shape is the reference) and AFTER UM3 (which relocates AA/DA
  under User Management and places the CE nav entry there).

## Implementation outline (no code)
1. (Prereq) Confirm IP6 has pinned the Patients record-based shape and UM3 has the CE nav slot.
2. AA/DA write path (closes BUG-042):
   a. Add `string? FirstName`, `string? LastName` to ApplicantAttorney + DefenseAttorney
      Create AND Update DTOs; change `IdentityUserId` from `Guid` to `Guid?` in both.
   b. Update Mapperly Create/Update -> entity mappers to map the two name fields (target-required
      strategy means a missing map is a COMPILE error -- this surfaces any miss at build).
   c. In ApplicantAttorneyManager/DefenseAttorneyManager CreateAsync, assign FirstName/LastName
      after the ctor (the 4-vs-7 split means the ctor will not set them).
   d. SERVER enforcement: none new required for names (optional); identity becomes optional.
      Keep existing Check.Length guards.
3. AA/DA UI:
   a. Add firstName/lastName form controls (maxLength 50) to both detail abstract services.
   b. Remove `Validators.required` from identityUserId on both forms (UI mirror of the
      now-optional server field). Add name inputs to the detail HTML.
4. ClaimExaminer master (NET-NEW):
   a. Domain.Shared: ClaimExaminerConsts (FirstName/LastName/PhoneNumber/FaxNumber/Street/
      City/ZipCode/Email max-lengths; OMIT firm fields per OBS-8).
   b. Domain: ClaimExaminer entity (IMultiTenant, FullAuditedAggregateRoot, nullable
      IdentityUserId, nullable StateId), ClaimExaminerManager, IClaimExaminerRepository.
   c. EF Core: config in BOTH DbContexts (no IsHostDatabase guard) + MIGRATION (new table).
   d. Application.Contracts: Dto/Create/Update/Input/IAppService (Create/Update use Guid?
      identity), permission group in CaseEvaluationPermissions.cs + DefinitionProvider.
   e. Application: ClaimExaminersAppService ([RemoteService(IsEnabled=false)], extends
      CaseEvaluationAppService) + Mapperly mappers.
   f. HttpApi: manual controller `api/app/claim-examiners` implementing IClaimExaminersAppService.
   g. Localization: add Menu/permission keys to Domain.Shared en.json BEFORE any L() reference.
   h. REGENERATE proxy (`abp generate-proxy`); build the Angular claim-examiners feature
      (abstract+concrete list + modal detail, mirroring attorney shape minus firm fields).
5. Wire CE identity-by-email linkback via the existing external invite/RegisterAsync path
   (ExternalUserType.ClaimExaminer=2 already exists).
6. Build + verify Mapperly compile, run migration, smoke the three CRUDs in Docker.

## Dependencies
- DEPENDS ON IP6 (Patients record-based shape is the mirror target) -- sequence AFTER.
- DEPENDS ON UM3 (relocates AA/DA under User Management; defines the CE nav slot) -- sequence AFTER.
- COMPLETES BUG-042 (write path) and unblocks OBS-32 (two-name booker prefill).
- INTERACTS WITH the CLAIM EXAMINER per-appointment decision (CE first-class on every
  appointment): the CE master here is the lookup source for that per-appointment CE selection.

## Residual open questions
- Migration must seed nothing for the new ClaimExaminer table (per no-seed rules); confirm the
  CE table is fine empty at first run.
- Minor: whether the CE master keeps StateId (no firm, but a mailing state may still apply) --
  default to keeping it for parity with patients/attorneys; trivially droppable if undesired.
