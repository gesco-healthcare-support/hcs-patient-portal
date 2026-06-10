---
id: AF2
title: Remove the AME-requires-attorney booking gate so all roles can book all types
type: enhancement
components:
  - src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs
  - src/HealthcareSupport.CaseEvaluation.Application/Appointments/BookingFlowRoles.cs
  - src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentExceptionTranslator.cs
  - src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs
  - src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json
  - src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs
  - test/HealthcareSupport.CaseEvaluation.Application.Tests/Appointments/BookingFlowRolesUnitTests.cs
related_known_bugs: [OBS-23, OBS-24, BUG-021]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change

Remove the rule that blocks the Patient role (and any non-attorney external caller) from
booking an AME appointment. Every authenticated role -- internal or external -- can book any
appointment type. This is a direct, deliberate reversal of the OBS-23 fix that shipped on
2026-05-21.

## Current behavior (from investigation)

`AppointmentsAppService.CreateAsync` enforces the gate at lines 672-686
(`AppointmentsAppService.cs:679-686`): if the caller is NOT an internal user
(`BookingFlowRoles.IsInternalUserCaller`) AND the type name carries AME semantics
(`BookingFlowRoles.IsAmeAppointmentType`) AND the caller is NOT an attorney
(`BookingFlowRoles.IsAttorneyCaller`), it throws `UserFriendlyException` with code
`CaseEvaluationDomainErrorCodes.AppointmentAmeRequiresAttorneyRole` and message
`L["Appointment:AmeRequiresAttorneyRole"]`.

The supporting scaffolding:
- `BookingFlowRoles.IsAttorneyCaller` (`BookingFlowRoles.cs:117-137`) -- matches "Applicant
  Attorney" / "Defense Attorney".
- `BookingFlowRoles.IsAmeAppointmentType` (`BookingFlowRoles.cs:146-153`) -- broad
  case-insensitive substring match on "AME" (also matches a hypothetical "AME-REVAL").
- Error code constant `AppointmentAmeRequiresAttorneyRole`
  (`CaseEvaluationDomainErrorCodes.cs:140-143`).
- Localized message (`en.json:274`): "Only Applicant Attorneys and Defense Attorneys can
  request an AME or AME-REVAL appointment...".
- Exception-translator mapping (`AppointmentExceptionTranslator.cs:66-67`) -- maps the code
  to the `"Appointment:AmeRequiresAttorneyRole"` localization key. (Not noted in the
  findings; surfaced by grep.)
- HTTP-status registration (`CaseEvaluationHttpApiHostModule.cs:202-207`) -- maps the code to
  `BadRequest` (400). (Not noted in the findings; surfaced by grep.)
- 11 unit tests around the gate (`BookingFlowRolesUnitTests.cs`).

Live-verified during OBS-23: a Patient POST of an AME appointment returns HTTP 400. No UI-side
role filter exists -- the booking-form type dropdown is server-lookup-driven and unconditional
(`appointment-add.component.ts`); there is nothing to remove on the Angular side.

The substring match means the gate today only bites "AME"; "IME" does not contain "AME", so
the new IME type is unaffected even before removal.

## Relevant code locations

- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs:672-686`
  -- the gate block to delete (keep the lines 666-670 type-existence check and lines 688+).
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/BookingFlowRoles.cs:109-153`
  -- delete `IsAttorneyCaller` + `IsAmeAppointmentType`; KEEP `IsInternalUserCaller` (still
  used by the Pending/Approved split at `AppointmentsAppService.cs:720-724`).
- `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentExceptionTranslator.cs:66-67`
  -- remove the translator arm.
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs:140-143`
  -- remove the constant.
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json:274`
  -- remove the message key.
- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs:202-207`
  -- remove the status-code mapping.
- `test/HealthcareSupport.CaseEvaluation.Application.Tests/Appointments/BookingFlowRolesUnitTests.cs`
  -- delete the 11 tests covering the removed helpers.

## Phase 3 cross-reference

- OBS-23 -- this is the exact gate AF2 reverts (status "fixed" 2026-05-21). Mark OBS-23 as
  intentionally reverted by AF2 in the bug log so the reversal is not later "re-fixed."
- OBS-24 -- broader cluster of server-side booking validators (lead-time, max-horizon, slot
  semantics). Those stay; only the AME-role arm is removed. While in
  `AppointmentExceptionTranslator.cs` / the host module's status-mapping block, confirm no
  other OBS-24 validator shares the deleted code (none does -- each has its own code).
- BUG-021 ("ce-cannot-book") -- adjacent role/booking-permission finding. AF2 removes one
  role restriction but does NOT touch CE booking permissions; verify BUG-021 is a separate
  permission-level issue, not coupled to this gate (it is -- different layer).

## Research findings

- Internal patterns / prior art:
  - The gate mirrors a stub of OLD's `RoleAppointmentType` join (per the code comment at
    `AppointmentsAppService.cs:672-677`); that join table was never ported, so the only
    artifact to retire is this hardcoded allow-list -- no schema or migration is involved.
  - Error-code removal has a clean three-site shape in this codebase: constant
    (`CaseEvaluationDomainErrorCodes.cs`) -> translator arm
    (`AppointmentExceptionTranslator.cs`) -> HTTP-status mapping
    (`CaseEvaluationHttpApiHostModule.cs`). Per the HttpApi.Host CLAUDE.md
    "Exception-to-HTTP-status mapping" note, an unmapped `BusinessException` defaults to 403;
    removing the code AND its mapping together keeps that table coherent.
  - `IsInternalUserCaller` is load-bearing elsewhere (`AppointmentsAppService.cs:720-724`
    drives the Pending vs Approved initial status). Do NOT remove it.
- External docs (ABP / Angular / EF Core): none required. This is pure removal of
  application-layer business logic, localization, and host status mapping. No DTO contract
  change, so no `abp generate-proxy` regen is needed.

## Approaches considered (with tradeoffs)

1. Full removal of the gate and all its scaffolding (CHOSEN). Deletes the gate block, both
   helper methods, the error code, the en.json key, the translator arm, the status mapping,
   and the 11 tests. Tradeoff: more files touched now, but leaves zero dead code and no
   misleading "attorney-only" message keys that a future reader might wire back up.
2. Neutralize only the gate block, leave helpers/code/tests dormant. Smaller diff, easy
   re-enable. Rejected: leaves dead `IsAttorneyCaller` / `IsAmeAppointmentType`, an orphaned
   error code with a live HTTP mapping, and 11 tests asserting behavior that no longer runs --
   exactly the kind of stale scaffolding `.claude/rules/code-standards.md` says to clean when
   the file is touched. The "all roles book all types" decision is locked, so there is no
   near-term re-enable to preserve for.
3. Make the allow-list configurable (SystemParameter / per-tenant). Most flexible. Rejected:
   over-engineering for a locked "everyone can book everything" decision; adds config surface
   and a new validation path for a rule we are explicitly deleting.

## Decision (locked 2026-06-03)

Remove the AME-requires-attorney gate entirely. Delete the gate block at
`AppointmentsAppService.cs:672-686`, the `IsAttorneyCaller` and `IsAmeAppointmentType` helpers
in `BookingFlowRoles.cs`, the `AppointmentAmeRequiresAttorneyRole` error code, its `en.json`
message, its translator arm, its host HTTP-status mapping, and the 11 `BookingFlowRolesUnitTests`.
Keep `IsInternalUserCaller` and the internal-vs-external Pending/Approved split untouched. No UI
change (no role filter exists). No migration, no proxy regen.

## Implementation outline (no code)

1. Application layer -- delete the gate block at `AppointmentsAppService.cs:672-686` (retain
   the type-existence guard at 666-670 and everything from 688). Confirm `CurrentUser` is
   still referenced afterward at 720-724; if the local `bookingCallerRoles` var at 678 becomes
   unused, remove it.
2. `BookingFlowRoles.cs` -- delete `IsAttorneyCaller` (117-137) and `IsAmeAppointmentType`
   (146-153) and their doc comments. Keep `IsInternalUserCaller`.
3. `AppointmentExceptionTranslator.cs` -- remove the `AppointmentAmeRequiresAttorneyRole`
   arm (66-67).
4. `CaseEvaluationHttpApiHostModule.cs` -- remove the `options.Map(...)` registration and its
   OBS-23 comment (202-207).
5. Domain.Shared -- remove the `AppointmentAmeRequiresAttorneyRole` constant
   (`CaseEvaluationDomainErrorCodes.cs:140-143`) and the `en.json:274` message key.
6. Tests -- delete `BookingFlowRolesUnitTests.cs` entries asserting the removed gate (the 11
   tests); if any test in that file also exercises `IsInternalUserCaller`, keep those and
   remove only the attorney/AME cases.
7. Server-vs-UI enforcement: this is a pure server-side removal; there is no UI affordance to
   change. Verify via `dotnet build` (Mapperly/source-gen and the error-code mapping unit
   test `CaseEvaluationHttpApiHostModuleTests` must still pass) that no dangling reference to
   the deleted code remains.
8. Documentation: update the OBS-23 entry in the bug log to "reverted by AF2 (approved
   2026-06-03)" so the reversal is intentional and traceable.

Flags: no EF migration; no proxy regen; no DTO change. The only cross-layer concern is keeping
the error-code -> translator -> HTTP-status triplet consistent (remove all three together).

## Dependencies

- Independent of AF1 (type-set rename). The gate's substring match only catches "AME"; the
  AF1 rename of "Panel QME" -> "PQME" and the new "IME" do not interact with it. AF2 can ship
  before, after, or alongside AF1 with no ordering constraint.
- Does not block or depend on AF3/AF4 (panel-number conditionals) or AF5 (strike list).
- OBS-23 log update is a doc-only follow-on, not a code dependency.

## Residual open questions

None. The findings' three open questions are resolved by the locked decision: (a) no UI role
filter exists, so the change is backend-only; (b) the scaffolding is deleted entirely, not left
dormant; (c) the internal-vs-external Pending/Approved split (`IsInternalUserCaller`) is a
separate rule and stays.
