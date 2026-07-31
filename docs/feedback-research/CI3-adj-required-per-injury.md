---
id: CI3
title: ADJ# (WcabAdj) required per injury on every appointment request
type: bug
components: [angular/src/app/appointments/sections/appointment-add-claim-information.component.ts, angular/src/app/appointments/sections/appointment-add-claim-information.component.html, src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentInjuryDetails/AppointmentInjuryDetailCreateDto.cs, src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentInjuryDetails/AppointmentInjuryDetailUpdateDto.cs, src/HealthcareSupport.CaseEvaluation.Domain/AppointmentInjuryDetails/AppointmentInjuryDetail.cs]
related_known_bugs: [BUG-043, BUG-024]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change
ADJ# (the WCAB ADJ case number, field `WcabAdj`) is optional everywhere today. It must be
REQUIRED to request an appointment. Because ADJ# lives on the per-injury
`AppointmentInjuryDetail`, "always required" means EACH Claim Information block (one per
Date-of-Injury / Claim-Number) must carry its own ADJ#. Enforce server-side (DTO `[Required]`
+ domain `Check.NotNullOrWhiteSpace`) AND in the UI (`Validators.required` + asterisk +
is-invalid), matching the BUG-043 two-layer precedent.

## Current behavior (from investigation)
ADJ# is optional across all four enforcement points:
- Client: `injuryWcabAdj` control declared with NO validators --
  `appointment-add-claim-information.component.ts:187` (`injuryWcabAdj: [src.wcabAdj]`).
- Client: label "ADJ#" has no asterisk and no invalid-state binding --
  `appointment-add-claim-information.component.html:189-198`.
- Client docstring lists always-required as dateOfInjury / claimNumber / bodyPartsSummary
  only; ADJ NOT included -- `appointment-add-claim-information.component.ts:160`.
- DTO: `WcabAdj` annotated `[StringLength]` only, NOT `[Required]`, on Create
  (`AppointmentInjuryDetailCreateDto.cs:22-23`) and Update (`AppointmentInjuryDetailUpdateDto.cs:23-24`).
- Domain: `WcabAdj` is `[CanBeNull]` -- `AppointmentInjuryDetail.cs:35-36`. Ctor param is
  optional/defaulted (`string? wcabAdj = null` at :55) and validated with
  `Check.Length(wcabAdj, ..., WcabAdjMaxLength, 0)` -- min length 0, so empty is valid (:63).
- Const: `WcabAdjMaxLength = 50`, no min constant
  (`AppointmentInjuryDetailConsts.cs:13`). Localization key exists:
  `en.json:523` (`"WcabAdj": "WCAB ADJ#"`).

By contrast, the sibling required fields are already two-layer enforced: client
`Validators.required` on `injuryDateOfInjury` (component.ts:168) and `injuryClaimNumber`
(:170); domain `Check.NotNullOrWhiteSpace(claimNumber, ...)` and
`Check.NotNullOrWhiteSpace(bodyPartsSummary, ...)` (AppointmentInjuryDetail.cs:59,61). CI3 is
making ADJ# follow that same established pattern.

## Relevant code locations
- `angular/src/app/appointments/sections/appointment-add-claim-information.component.ts` --
  add `Validators.required` to `injuryWcabAdj` (:187); update docstring (:160). Modal submit
  guard (`saveInjuryModal`) already blocks on invalid controls, so no extra blocking logic.
- `angular/src/app/appointments/sections/appointment-add-claim-information.component.html:189-198`
  -- add required asterisk on the ADJ# label + is-invalid binding on the input.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentInjuryDetails/AppointmentInjuryDetailCreateDto.cs:22-23`
  -- add `[Required]` to `WcabAdj`.
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentInjuryDetails/AppointmentInjuryDetailUpdateDto.cs:23-24`
  -- add `[Required]` to `WcabAdj`.
- `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentInjuryDetails/AppointmentInjuryDetail.cs:35-36,55,63`
  -- make `WcabAdj` non-null (`[NotNull]`), make ctor param non-optional, add
  `Check.NotNullOrWhiteSpace(wcabAdj, nameof(wcabAdj))`; keep the existing length check (bump
  min from 0 to 1 or rely on NotNullOrWhiteSpace).
- (Optional) `src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentInjuryDetails/AppointmentInjuryDetailConsts.cs:13`
  -- no change needed unless a min-length constant is desired.

## Phase 3 cross-reference
- CI4 (remove "Attention" field) edits the SAME modal/component
  (appointment-add-claim-information.component.*) and ships an EF migration -- bundle to avoid
  touching the file twice. CI3 itself ships no migration (see below).
- CI1 / CI2 (Insurance + CE lifted to appointment level; per-injury block reduced to
  cumulative flag / DOI / claim number / WCAB office / ADJ# / body parts) restructure the same
  per-injury block. Land CI3's required-ADJ rule on whatever the final per-injury shape is so
  the validator is not orphaned during the restructure.
- OBS-41 (repeatable structured body parts) already established the "seed one required row,
  block submit until filled" pattern in this exact modal -- the ADJ# required rule rides the
  same submit gate.

## Research findings
- Internal patterns / prior art:
  - BUG-043 ("at least one claim required") is the direct precedent: fixed BOTH client-side
    (appointment-add.component.ts:1091) AND in `AppointmentManager` (injuryCount<1 throws).
    CI3 is the per-field analog within each claim. Same two-layer philosophy.
  - BUG-024 (reject-accepts-empty-reason) is cited by BUG-043 as the same "missing required
    validation" family -- confirms the house convention is server + UI, not UI-only.
  - The required-field machinery for this entity already exists: `Check.NotNullOrWhiteSpace`
    in the AppointmentInjuryDetail ctor (claimNumber, bodyPartsSummary) and `Validators.required`
    in `buildInjuryForm`. CI3 reuses both verbatim for `WcabAdj`.
- External docs: none needed. This uses existing ABP `Check` guards
  (Volo.Abp.Check.NotNullOrWhiteSpace), standard DataAnnotations `[Required]`, and Angular
  `Validators.required` -- all already in use in this file. No new pattern introduced.

## Approaches considered (with tradeoffs)
1. UI-only `Validators.required` (rejected). Smallest diff, but non-SPA / direct-API callers
   (and the AppService write path used by the view page) could still persist a null ADJ#.
   Violates the server-side enforcement rule for integrity fields and breaks parity with the
   sibling required fields already guarded in the domain ctor.
2. Server-only `[Required]` + `Check.NotNullOrWhiteSpace` (rejected). Correct integrity floor,
   but the user only learns of the failure after submit via a 4xx -- poor UX for a booking
   form where the asterisk/is-invalid affordance is the established convention.
3. Two-layer: UI validator + asterisk/is-invalid AND DTO `[Required]` + domain
   `Check.NotNullOrWhiteSpace` (CHOSEN). Matches BUG-043 exactly, gives immediate inline
   feedback while keeping the server authoritative, and reuses code already present in the same
   files. Cost is touching four points, but they are one-line changes each.

## Decision (locked 2026-06-03)
ADJ# (`WcabAdj`) is REQUIRED, per injury (one ADJ# per Date-of-Injury / Claim-Number block).
Enforce in two layers:
- UI: `Validators.required` on `injuryWcabAdj` + required asterisk on the "ADJ#" label +
  is-invalid binding on the input.
- Server: `[Required]` on both Create and Update DTO `WcabAdj`; domain ctor makes `wcabAdj`
  non-optional and adds `Check.NotNullOrWhiteSpace`.
Applies to NEW bookings; existing rows may carry null ADJ (see Residual). The "always
required" rule is UNCONDITIONAL across appointment types -- it does not defer to any
`AppointmentTypeFieldConfig` that might optionalize/hide WcabAdj.

## Implementation outline (no code)
1. Domain (`AppointmentInjuryDetail.cs`): change `WcabAdj` to `[NotNull] string` (init
   `null!`), make ctor param required (drop the `= null` default at :55), add
   `Check.NotNullOrWhiteSpace(wcabAdj, nameof(wcabAdj))` before the existing length check
   (:63). Verify every caller of the ctor now passes a non-null wcabAdj.
2. Contracts: add `[Required]` to `WcabAdj` on `AppointmentInjuryDetailCreateDto` (:22-23) and
   `AppointmentInjuryDetailUpdateDto` (:23-24).
3. UI form (`appointment-add-claim-information.component.ts`): add `[Validators.required]` to
   `injuryWcabAdj` (:187); update the always-required list in the docstring (:160).
4. UI template (`...component.html:189-198`): add the required asterisk to the ADJ# label and
   the standard is-invalid binding on the input (mirror the claim-number / DOI markup in the
   same modal).
5. Proxy regen: run `abp generate-proxy` after the DTO change so the generated
   AppointmentInjuryDetail models reflect required `WcabAdj`. Do not hand-edit `proxy/`.
6. NO EF migration required: `[Required]` on the DTO and `[NotNull]` on the entity are
   validation-layer; the column nullability is a separate EF mapping concern. Making the COLUMN
   non-nullable WOULD require a migration AND a backfill of existing null rows -- deliberately
   deferred (see Residual) to keep CI3 contained and avoid breaking historical rows.
7. Enforcement summary: server (DTO + domain) is authoritative; UI is the mirrored affordance.

## Dependencies
- Soft-coupled to CI1/CI2 (per-injury block restructure) and CI4 (Attention removal + its
  migration), all in the same modal/entity -- sequence CI3 after or alongside them to avoid
  rework, but CI3 has no hard prerequisite. CI3 blocks nothing.

## Residual open questions
- Column nullability + backfill: the entity column stays nullable for now; the change applies
  to new bookings only. If product later wants the DB column NOT NULL, that needs a separate EF
  migration plus a backfill/decision for existing null-ADJ rows (acceptable to defer -- flagged,
  not blocking).
