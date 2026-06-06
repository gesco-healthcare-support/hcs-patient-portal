---
feature: appointment-edit-authorization
date: 2026-06-06
status: in-progress
base-branch: feat/replicate-old-app
related-issues: []
---

## Goal

Close the deferred pre-Phase-4 security hole: `AppointmentsAppService.UpdateAsync`
carries only `[Authorize]`, so any authenticated tenant user can edit any
appointment. Gate it to the internal-staff `Appointments.Edit` permission,
matching the OLD app.

## Context

Deferred security finding (memory `project_parity-phase-progress`). Research
(2026-06-06) confirmed:
- `UpdateAsync` (AppointmentsAppService.cs:994) is bare `[Authorize]` -- the only
  un-gated appointment mutation. `CreateAsync`/`ReSubmitAsync`/`CreateRevalAsync`
  already carry `[Authorize(Appointments.Create)]`; `DeleteAsync`/`Approve`/`Reject`
  are permission-gated.
- The read deep-link the old CLAUDE.md gotcha describes is ALREADY closed:
  `GetWithNavigationPropertiesAsync` calls `EnsureCanReadAsync` (7-pathway
  party-scope guard, Group J / Phase 13). The gotcha is stale.
- OLD parity: appointment edit = internal staff only; external parties cannot
  edit (they use change-requests). NEW role seeding matches -- `Appointments.Edit`
  is granted only to internal roles (IT Admin, Staff Supervisor, Clinic Staff).
- Adrian decision (2026-06-06): Option A -- permission gate
  `[Authorize(Appointments.Edit)]` (not the party-based `CanEdit`, which would be
  more permissive than OLD).

## Approach

Add `[Authorize(CaseEvaluationPermissions.Appointments.Edit)]` to `UpdateAsync`
(dual-decorator style matching `CreateAsync`). ABP's authorization interceptor
enforces the attribute at runtime; the permission is internal-only by seeding, so
this reproduces OLD's "internal edits, external parties use change-requests".

Test via reflection on the `[Authorize(policy)]` attribute, not behavioral
permission-denial: every behavioral `AbpAuthorizationException` test in this suite
is a `[Fact(Skip)]` stub because the SQLite integration harness does not seed
role->permission grants. A reflection guard is deterministic, harness-independent,
and directly locks the control that ABP enforces.

**Alternatives rejected:** party-based `EnsureCanEditAsync` (more permissive than
OLD -- admits creator/accessors to core edit); full 7-pathway `CanEdit` (admits
external parties -- contradicts OLD). Behavioral permission-denial test (the
harness can't seed grants; would be fragile or another skip stub).

## Tasks

- **T1: Gate `UpdateAsync` + reflection authorization guard.**
  - approach: tdd
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs, test/HealthcareSupport.CaseEvaluation.Application.Tests/Appointments/AppointmentsAppServiceAuthorizationTests.cs, test/HealthcareSupport.CaseEvaluation.Application.Tests/Appointments/AppointmentsAppServiceTests.cs]
  - detail: write a pure reflection test asserting the mutation endpoints carry their policies (UpdateAsync->Edit red first; Create/ReSubmit/CreateReval->Create, Delete->Delete already green). Add `[Authorize(Appointments.Edit)]` to UpdateAsync to turn UpdateAsync green. Remove the two stale `[Fact(Skip="KNOWN GAP")]` stubs (Create/Update) now superseded by the real guard.
  - acceptance: all reflection facts green; the two skip stubs removed; full Application.Tests suite green.

- **T2: Fix stale authorization docs.**
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md]
  - detail: update business-rule #8 (Create/Update permission gap -> now closed; Update gated by Edit) and gotcha #1 (read deep-link -> now party-scoped via EnsureCanReadAsync).
  - acceptance: CLAUDE.md reflects current state; Docs Structure Check passes.

- **T3: ADR for the edit-authorization model.**
  - approach: code
  - files-touched: [docs/decisions/014-appointment-edit-authorization.md]
  - detail: ADR-014 (context: bare-[Authorize] hole; decision: Option A permission gate; alternatives: party-based / full 7-pathway rejected; OLD parity). ADR 013 = Group L reminders.
  - acceptance: ADR present; no internal-only paths in the commit body.

## Risk / Rollback

- Blast radius: one attribute on `UpdateAsync` + tests + docs. No schema, no
  DbContext, no Angular/proxy. The Angular edit button is already permission-gated,
  so no legitimate caller breaks.
- Behavior change: `UpdateAsync` now rejects callers without `Appointments.Edit`
  (external roles). This is the intended fix.
- Rollback: revert the branch (one attribute).

## Verification

1. Reflection guard: UpdateAsync requires Edit; Create/ReSubmit/CreateReval require
   Create; Delete requires Delete -- all green.
2. Full Application.Tests + EFCore.Tests + Domain.Tests green (no regressions from
   the removed stubs or the new attribute).
3. `dotnet build` clean.
