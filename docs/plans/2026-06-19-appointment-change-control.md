---
feature: appointment-change-control
date: 2026-06-19
status: draft
base-branch: development
build-sequencing: post-multi-tenant (depends on per-tenant consent setting)
related-issues: []
---

## Goal

Give internal staff direct authority to edit / reschedule / cancel an appointment from
the moment it is requested, while every external-party change (date, type, cancel, or any
other form field) flows through a single request-and-approve path that can never apply
unilaterally -- high-impact changes additionally requiring opposing-party consent.

## Context

Yesterday's "pre-approval reschedule/cancel" change was lost (never committed) and the
reschedule/cancel guards still require an Approved appointment. Rather than re-bolt a
stopgap onto the wrong mechanism, we settle the policy "once and for all" and build on the
opposing-party consent infrastructure that already shipped (Group D, 2026-06-09).

Build is sequenced AFTER the multi-tenant implementation, because consent gating is
currently a compile-time const (`AppointmentChangeRequestConsts.ConsentGatingEnabled`)
that must become a per-tenant setting, and the change surface is tenant-scoped.

### What already exists (reuse, do not rebuild)

- `AppointmentChangeRequest` aggregate (`ChangeRequestType` Cancel=1 / Reschedule=2) with
  full consent fields: `RequestingSide`, `ConsentStatus`, `ConsentTokenHash`,
  `ConsentExpiresAt`, `ConsentRespondedAt/ByEmail`; methods
  `IssueConsent`/`RecordConsentDecision`/`MarkConsentExpired`/`IsConsentGranted`.
- `ChangeRequestSideResolver` -- classifies a party into Side A (patient + applicant
  attorney) or Side B (defense attorney + claim examiner) and resolves the opposing
  representative. Opposing party is derivable today; no new party model needed.
- `ChangeRequestConsentManager` -- 7-day SHA256 token, emails the opposing rep a Yes/No
  link served anonymously by `PublicChangeRequestConsentController`.
- `OpposingConsentValidator.EnsureConsentGranted` -- staff cannot finalize a consent-gated
  change unless `ConsentStatus == Approved`.
- Send-back (`AppointmentInfoRequest`) -- staff-initiated field-level edit loop with
  field-locking (`InfoRequestCorrectionLock`), before/after diff (`InfoRequestSnapshot`),
  per-round history, and the `FLAGGABLE_FIELDS` registry. Stays as the separate
  staff -> party "fix your info" loop; its field-apply/diff logic is extracted for reuse.
- Capacity-based slot model: a Pending appointment already reserves its slot
  (`EfCoreAppointmentRepository.GetActiveCountForSlotAsync` excludes only the 5 terminal
  slot-freed statuses). No interim-hold work needed for the request window.

### Constraints

- ABP Commercial 10 / .NET 10, Angular 20. Shared-DB multi-tenancy; `Appointment` and
  `AppointmentChangeRequest` are `IMultiTenant`. Research confirms an appointment cannot
  legitimately span tenants (all parties resolve within one `TenantId`), so no
  cross-tenant consent routing is required.
- Approval app service is `[RemoteService(IsEnabled=false)]`, exposed only via the manual
  `AppointmentChangeRequestApprovalController` -- new approval endpoints need controller
  edits, not just service methods.
- HIPAA: consent emails and notifications carry no field values, only a deep link
  (existing convention). All test data synthetic; SSN masked.
- Never hand-edit generated proxies; regenerate via `abp generate-proxy -t ng -u
  http://localhost:44377`.

## Policy (the wording, authoritative)

Governing principle: no external party may unilaterally alter an appointment -- whether
Requested (Pending) or Approved. Internal staff may.

Tiers:
- Tier A (high impact): appointment date/slot, appointment type, cancellation.
- Tier B: every other appointment form field.

| Change | Pending (Requested) | Approved (both sides agreed) |
| --- | --- | --- |
| Internal staff -- Tier B | Direct | Direct |
| Internal staff -- Tier A | Direct | Opposing-party consent required |
| External party -- Tier B | Staff approval | Staff approval |
| External party -- Tier A | Staff approval + opposing-party consent | Staff approval + opposing-party consent |

Rules:
- Mixed request: any Tier-A field present escalates the WHOLE request to staff +
  opposing-party consent, applied atomically.
- Self-withdrawal: a party cancelling their OWN still-Pending request still requires
  opposing-party consent (the other side may want the eval to proceed) -- no carve-out.
- The only place internal staff lose unilateral power is Tier-A on an already-Approved
  appointment, because both sides agreed to that date/type.

## Approach

Chosen: a single unified `AppointmentChangeRequest` represents every external-initiated
change. Rejected alternatives below.

### Domain shape

- The aggregate carries a `RequestedChanges` value object (persisted as owned columns +
  JSON for the field-edit list):
  - `NewDoctorAvailabilityId?` (date/slot change)
  - `NewAppointmentTypeId?` (type change)
  - `IsCancellation` (bool)
  - `FieldEdits: [{ Key, NewValue }]` (Tier-B form-field changes)
- Computed `RequiresOpposingConsent` = true iff `NewDoctorAvailabilityId` or
  `NewAppointmentTypeId` is set, or `IsCancellation` is true (i.e., touches Tier A). This
  is the single source of the tier decision and drives the mixed-escalation rule for free.
- `ChangeRequestType` extended with members for queue/display (`TypeChange`,
  `FieldAmendment`, `Mixed`); existing `Cancel`/`Reschedule` rows remain valid
  (back-compat). The behavioral gate keys on `RequiresOpposingConsent`, not the label.
- Staff approval is ALWAYS required for external requests; consent is required ONLY when
  `RequiresOpposingConsent`.
- On finalize (staff approve, + consent granted if required), all proposed changes apply
  atomically via a shared field-apply helper extracted from send-back's
  `SaveCorrectionsAsync`; reschedule keeps the existing new-slot reservation.

### Lifecycle and parent status

- Requests are allowed when the parent is Pending OR Approved (replaces the Approved-only
  validators with status-aware + tier-aware rules).
- Parent-status handling on an open request is a build-time sub-decision (T2):
  recommended low-churn option = reuse `RescheduleRequested`/`CancellationRequested` for
  those intents, leave `Approved`/`Pending` unchanged for field-only changes, and surface
  "change pending" in the UI from the open request; alternative = introduce one generic
  `ChangePending` status (cleaner, more state-machine work).
- Internal direct path uses the `DirectCancel`/direct-edit triggers (DirectCancel=15/16
  already exist in the state machine graph but are not yet exposed) -- bypassing the
  request+consent flow for everything except Tier-A-on-Approved, which routes a staff
  member through the same consent gate.

### Reuse map

- Consent: `ChangeRequestSideResolver` + `ChangeRequestConsentManager` +
  `PublicChangeRequestConsentController` + `OpposingConsentValidator` as-is.
- Field edits/diff: extract send-back's apply + `InfoRequestSnapshot` diff into a shared
  helper; both staff send-back and the unified change-request consume it.
- Notifications: existing `StatusChangeEmailHandler` + ETOs; add a staff-facing
  "external requested a change" template and decision-outcome templates.

### Rejected alternatives

- Hybrid by tier (Tier-A via change-request, Tier-B via inverted send-back, mixed
  straddling both): maximizes in-place reuse but creates two external entry points and a
  cross-system seam for mixed requests; collapses toward the unified model anyway once
  mixed requests must carry field payloads. Rejected for durability.
- Everything through send-back/`AppointmentInfoRequest`: send-back has no consent/side
  concept and its semantics are staff-initiated corrections; would rebuild consent there.
  Rejected.

## Tasks

- T0: Per-tenant consent setting. Replace `ConsentGatingEnabled` compile-time const with
  an ABP `ISettingProvider` setting (per-tenant), default on.
  - approach: test-after
  - files-touched: [AppointmentChangeRequestConsts.cs, a new SettingDefinitionProvider, OpposingConsentValidator.cs, AppointmentChangeRequestsAppService.cs]
  - acceptance: a tenant with consent disabled finalizes Tier-A without a consent token; enabled tenant still blocks. Depends on multi-tenant work.

- T1: Unified `RequestedChanges` model + migration. Add the change-set fields + computed
  `RequiresOpposingConsent`; extend `ChangeRequestType`.
  - approach: tdd
  - files-touched: [AppointmentChangeRequest.cs, RequestStatusType.cs/ChangeRequestType.cs, CaseEvaluationDbContext.cs, new EF migration]
  - acceptance: a request touching any Tier-A field computes RequiresOpposingConsent=true; a field-only request computes false; persists and round-trips.

- T2: Status-aware + tier-aware submit validators. Replace Approved-only gates; allow
  Pending; decide parent-status handling.
  - approach: tdd
  - files-touched: [CancellationRequestValidators.cs, RescheduleRequestValidators.cs, AppointmentChangeRequestManager.cs]
  - acceptance: external request succeeds on Pending and Approved; tier drives consent issuance; self-withdrawal of a Pending request issues a consent token.

- T3: Atomic apply-on-finalize + shared field-apply helper. Extract send-back apply/diff;
  apply slot/type/cancel/field edits together.
  - approach: tdd
  - files-touched: [new SharedFieldApply helper, AppointmentInfoRequestsAppService.cs, AppointmentChangeRequestsAppService.Approval.cs, ChangeRequestApprovalValidator.cs]
  - acceptance: approving a mixed request applies all changes in one transaction; partial failure rolls back; diff history captured.

- T4: Fail-closed side resolution. When `ChangeRequestSideResolver` cannot resolve a side
  for a consent-required change, block and route to staff mediation instead of silently
  finalizing.
  - approach: tdd
  - files-touched: [ChangeRequestSideResolver.cs, AppointmentChangeRequestsAppService.cs, OpposingConsentValidator.cs]
  - acceptance: an unresolved-side Tier-A request is rejected with a clear mediation error; no consent bypass path remains.

- T5: External unified submit surface. One endpoint accepting any combination of changes;
  permission + per-row `EnsureCanEditAsync`; self-withdrawal modeled as IsCancellation.
  - approach: test-after
  - files-touched: [AppointmentChangeRequestsAppService.cs, Application.Contracts inputs/DTOs, HttpApi controller]
  - acceptance: each actor/tier/state cell in the policy matrix returns the expected outcome via API.

- T6: Internal direct surface. Expose direct edit/reschedule/type/cancel for internal
  (Pending: all; Approved Tier-B: direct; Approved Tier-A: routes through consent). Gate
  the currently un-gated `UpdateAsync`.
  - approach: test-after
  - files-touched: [AppointmentsAppService.cs, AppointmentManager.cs (expose DirectCancel), AppointmentReadAccessGuard.cs]
  - acceptance: internal staff change a Pending appointment directly; an Approved Tier-A staff change creates a consent-gated request; UpdateAsync enforces edit access.

- T7: Approval surface for the unified set. Extend approve/reject + consent gate +
  concurrency stamp; manual controller edits.
  - approach: test-after
  - files-touched: [AppointmentChangeRequestsAppService.Approval.cs, ChangeRequestApprovalValidator.cs, AppointmentChangeRequestApprovalController.cs]
  - acceptance: staff approve only when consent satisfied (when required); reject bounces to requester; optimistic concurrency guards races.

- T8: Consent endpoint hardening. Per-token rate limit on the public consent controller.
  - approach: test-after
  - files-touched: [PublicChangeRequestConsentController.cs, rate-limit policy config]
  - acceptance: repeated token hits beyond the limit are throttled; valid single use unaffected.

- T9: Regenerate Angular proxy for new DTOs/enums.
  - approach: code
  - files-touched: [angular/src/app/proxy/appointment-change-requests/*]
  - acceptance: proxy compiles; only models.ts + generate-proxy.json committed (index.ts EOL no-ops discarded).

- T10: External "Request changes" UI. Unified change form on
  `external-appointment-detail` (prefilled from current appointment; choose date / type /
  cancel / field edits); submit -> change request; shows pending-consent state.
  - approach: test-after
  - files-touched: [external-appointment-detail.component.ts/html, appointment-view.component.ts, new change-request form component]
  - acceptance: an external party submits a mixed request; UI reflects staff + consent pending; screenshot-verified.

- T11: Internal direct edit/reschedule/cancel/type UI. Tier-A-on-Approved routes to the
  consent flow with a visible pending state.
  - approach: test-after
  - files-touched: [internal-appointment-detail.component.ts, internal-detail.util.ts]
  - acceptance: internal direct change on Pending applies immediately; Approved Tier-A shows consent-pending; screenshot-verified.

- T12: Staff approval inbox. Render the unified change-set + consent status; approve/reject.
  - approach: test-after
  - files-touched: [internal-change-request-inbox.component.ts, related templates]
  - acceptance: inbox shows each proposed change and consent state; approve/reject works end to end.

- T13: Server-side `FieldName`/field-key validation against a shared catalog (kill the
  Angular-registry-only drift).
  - approach: tdd
  - files-touched: [shared field catalog constant, InfoRequestCorrectionLock.cs, change-request submit validator]
  - acceptance: an unknown field key is rejected server-side for both send-back and change-request.

- T14: Notifications. Staff-facing "external requested a change" template + decision-outcome
  templates; wire to ETOs.
  - approach: test-after
  - files-touched: [StatusChangeEmailHandler.cs, new email templates, ETO handlers]
  - acceptance: each lifecycle transition emits the right email to the right recipient; no field values in payloads.

## Risk / Rollback

- Blast radius: touches the live opposing-party consent flow, the appointment edit/approve
  paths, and the slot model. High-value, security-sensitive (consent bypass, un-gated
  update). Mitigated by per-tenant setting (T0) and tdd on all domain/security tasks.
- Rollback: disable the per-tenant consent setting to fall back to current finalize
  behavior; the EF migration (T1) is additive (nullable columns + JSON) and reversible;
  new endpoints are independent of existing reschedule/cancel routes until the FE switches.

## Verification

End-to-end on a two-tenant stack (post-multi-tenant), walking every cell of the policy
matrix per actor (internal staff, applicant attorney, defense attorney, claim examiner,
patient) x tier (A/B) x parent state (Pending/Approved), plus: mixed-request escalation,
self-withdrawal consent, fail-closed unresolved side, consent reject -> staff mediation,
consent expiry, and concurrent-approve race. UI cells screenshot-verified per the
verify-with-screenshots rule. No regression to staff send-back.
```

## Out of scope

- Multi-tenant infrastructure itself (prerequisite, separate work).
- Capacity-overbook concurrency guard (pre-existing; track separately unless it surfaces
  during T6).
- The yesterday bug list (separate, runs first this weekend).
