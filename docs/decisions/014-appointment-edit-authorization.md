# ADR-014: Appointment edit authorization (permission gate)

**Status:** Accepted
**Date:** 2026-06-06
**Verified by:** code-inspect + reflection test

## Context

`AppointmentsAppService.UpdateAsync` carried only `[Authorize]`, so any
authenticated user in the tenant could edit any appointment -- no permission and
no party check. This was a deferred security finding gating Phase 4.
(`CreateAsync`/`ReSubmitAsync`/`CreateRevalAsync` were already gated by
`Appointments.Create`; `DeleteAsync`/`Approve`/`Reject` by their own permissions;
and appointment READ was already party-scoped via `EnsureCanReadAsync`.)

The OLD app authorized appointment edit/update for **internal staff only** --
external parties (patient, attorneys, claim examiner) could not edit; they
submitted change-requests. NEW's role seeding matches: `Appointments.Edit` is
granted only to the internal roles (IT Admin, Staff Supervisor, Clinic Staff),
never to external roles.

## Decision

Gate `UpdateAsync` with `[Authorize(CaseEvaluationPermissions.Appointments.Edit)]`
(dual-decorator alongside the base `[Authorize]`, matching `CreateAsync`). ABP's
authorization interceptor enforces the attribute at runtime; because the Edit
permission is internal-only by seeding, this reproduces OLD's "internal users
edit, external parties use change-requests" model.

Guard the control with a reflection test
(`AppointmentsAppServiceAuthorizationTests`) asserting each mutation endpoint
carries its expected permission policy. Behavioral permission-denial tests are not
used: the SQLite integration harness does not seed role->permission grants, so
every behavioral `AbpAuthorizationException` test in the suite is a `[Fact(Skip)]`
stub. The reflection guard is deterministic and harness-independent. The two stale
`[Fact(Skip="KNOWN GAP")]` stubs for the Create/Update gap are removed (superseded).

## Consequences

- `UpdateAsync` now rejects callers without `Appointments.Edit` (external roles).
  The Angular edit affordance was already permission-gated, so no legitimate caller
  breaks. Intended behavior change.
- Edit authorization is role/permission-based (coarse: "internal staff may edit"),
  not resource-based (per-appointment). This matches OLD, where internal staff
  could edit any appointment in scope. Per-appointment edit scoping is intentionally
  not added.
- The accessor "Edit" access type (Group J) governs read + change-request flows via
  `AppointmentAccessRules`, not the core `UpdateAsync` endpoint -- consistent with
  Group J's design.

## Alternatives Considered

- **Party-based `EnsureCanEditAsync` (slim: internal + creator + Edit-accessor)** --
  rejected: more permissive than OLD (admits the booker and Edit-accessors to core
  edit). OLD allowed neither.
- **Full 7-pathway `CanEdit` (also admits patient / AA / DA / CE)** -- rejected:
  directly contradicts OLD's internal-only edit rule.
- **Behavioral permission-denial test** -- rejected: the test harness cannot seed
  role->permission grants, so it would be fragile or another skip stub; the
  reflection guard tests the same control deterministically.
