---
name: booking-window-and-decision-sla
date: 2026-06-11
status: in-progress
branch: feat/booking-window-and-decision-sla
base: main @ 7adb19c (#305 + #306)
scope: (1) role-based booking horizon 60 external / 90 internal; (2) decision deadline 3 days, server-authoritative, escalate/notify (no auto status change)
---

# Plan: booking horizon (60/90) + 3-day decision SLA

Two related scheduling-rule changes. Locked decisions (from Adrian, 2026-06-11):
- **D1:** internal 90-day max is a **new admin-editable SystemParameter** (default 90), alongside
  the existing per-type external max (currently 60).
- **D2:** external users **see** slots 60-90 days out and get a **"contact staff" pop-up** if they
  pick one > 60 days (matches the written spec) -- they are NOT hidden.
- **D3:** at the 3-day decision deadline the API **escalates / notifies** (flags overdue Pending +
  surfaces to staff/dashboard); **no automatic approve/reject** (the legal 5-day limit is never breached).

---

## 1. Current-state map (verified 2026-06-11 on main)

**Part 1 -- booking horizon (mostly already built):**
- `BookingPolicyValidator.EvaluateBookingPolicy` (`src/.../Application/Appointments/BookingPolicyValidator.cs`)
  already enforces a **max-future horizon**, currently **60 days for every type** (AF1, 2026-06-03:
  `DefaultAppointmentMaxTime{PQME,AME,OTHER}=60` in `SystemParameterConsts.cs`). Role-BLIND -- internal
  staff are also capped at 60 today. Error: `AppointmentBookingDatePastMaxHorizon` (UserFriendlyException).
- Min lead-time = `SystemParameter.AppointmentLeadTime` (default 3); enforced by the same validator
  (`IsSlotWithinLeadTime`). Past-date guard also in `AppointmentManager.EnsureAppointmentDateNotInPast`.
- All three booking entry points (`CreateAsync` / `ReSubmitAsync` / `CreateRevalAsync`) converge at
  `AppointmentsAppService.CreateAppointmentInternalAsync` (~line 676); `CurrentUser.Roles` is in scope
  there (line 753) and `BookingFlowRoles.IsInternalUserCaller` is the canonical internal/external check.
- Angular: `sections/appointment-add-schedule.component` uses an `ngbDatepicker` driven by the parent's
  `markAppointmentDateDisabled`; **no max-future ceiling client-side**; min-lead-time hardcoded as
  `minimumBookingDays = 3` in `appointment-add.component.ts`. Reusable modal: `confirmationService.warn(...)`
  (the AA/DA toggle pattern, ~line 870). Date-select handler: `onAppointmentDateChanged` (~line 3488).

**Part 2 -- decision deadline (barely built, inconsistent):**
- The "5 days" is a hardcoded `private const int DecisionDueDays = 5` in
  `PendingDailyDigestEmailHandler.cs` (line 40) -- **display only** (a "Decision due" column in the
  daily pending-digest email to the intake-staff inbox via `PendingDailyDigestJob`, cron `0 9 * * *`).
  No enforcement, no auto-action.
- `SystemParameter.PendingAppointmentOverDueNotificationDays` (default **3**, `SystemParameterConsts.cs`
  line 25) exists, is validated, and is in the DTO -- but appears **unwired** (no job/handler consumes it).
- `Appointment.DueDate` = post-approval **document** deadline (unrelated). Dashboard "CCR Sec 31.5 / 60-day"
  = Pending sitting >= 60 days by `CreationTime` (`DashboardAppService.cs` ~line 80) -- unrelated.
- Reminder cadence settings live in `CaseEvaluationSettingDefinitionProvider` (`Reminders.*`,
  `RemindersEnabled` default false).

---

## 2. Part 1 -- role-based booking horizon (external 60 / internal 90, hard cap 90)

- **P1-T1 [code]** New SystemParameter `AppointmentMaxTimeInternal` (default 90):
  `SystemParameterConsts.DefaultAppointmentMaxTimeInternal = 90`; add the int property to the
  `SystemParameter` entity + both DbContext configs + **host migration**; add to the SystemParameter
  DTO(s) + AppService map + the range validator (mirror the existing `AppointmentMaxTime*`); seed the
  default in `SystemParameterDataSeedContributor`.
  - **DEVIATION (2026-06-11):** there is **no hand-written Angular admin form** for SystemParameter
    (only the auto-generated proxy + an informational comment in `appointment-add.component.ts`). The
    existing per-type maxes are likewise edited only through the `api/app/system-parameters` PUT /
    auto-generated proxy. So the new field is "exposed" via **proxy regeneration at deploy**, exactly
    like the existing maxes -- no SPA form edit is needed or possible. No behavior change.
- **P1-T2 [tdd]** Make `BookingPolicyValidator` role-aware: add `bool isInternalCaller` to
  `ValidateAsync` + `EvaluateBookingPolicy`; `maxDays = isInternalCaller ? sp.AppointmentMaxTimeInternal
  : ResolveMaxTimeDaysForType(category, sp)`. Reuse `AppointmentBookingDatePastMaxHorizon`. The internal
  max (90) is the hard ceiling -- nobody books beyond it. Tests: external 60 boundary (60 ok, 61 blocked),
  internal 90 boundary (90 ok, 91 blocked), lead-time still wins.
- **P1-T3 [code]** Thread the caller flag at `CreateAppointmentInternalAsync`:
  `isInternalCaller = BookingFlowRoles.IsInternalUserCaller(callerRoles)` -> pass to `ValidateAsync`.
  Covers Create / ReSubmit / CreateReval (all converge here).
- **P1-T4 [code]** Angular: add `maxBookingDays` getter (`isInternalBooker ? 90 : 60`); in
  `onAppointmentDateChanged`, if days-out > `maxBookingDays` -> open the "contact staff" info modal +
  clear date/time/doctorAvailabilityId; **cap the date-picker at 90** for everyone via
  `markAppointmentDateDisabled` (so slots 60-90 are visible+selectable-with-modal for external, bookable
  for internal; >90 disabled for all). 60/90 hardcoded client-side (mirrors the existing
  `minimumBookingDays=3`); the server stays authoritative.
- **P1-T5 [code]** Localization (`Domain.Shared/Localization/CaseEvaluation/en.json`): keys for the
  contact-staff modal title + message; optionally make `Appointment:BookingDatePastMaxHorizon` friendlier
  for the server-side fallback. Draft copy: *"Online booking is available up to 60 days in advance. For
  an appointment further out, please contact our office and our staff will schedule it for you."*

---

## 3. Part 2 -- 3-day decision deadline (server-authoritative, escalate/notify)

- **P2-T1 [code]** Make 3 days the server source of truth: the digest's hardcoded `DecisionDueDays = 5`
  reads `SystemParameter.PendingAppointmentOverDueNotificationDays` (default 3) instead, so the digest
  "Decision due" column = `RequestedAt + 3`.
- **P2-T2 [code/tdd]** Escalation (the "enforce"): enhance the existing `PendingDailyDigestJob` to flag
  Pending requests overdue (`CreationTime < now - OverDueNotificationDays`) in a distinct **OVERDUE**
  section/highlight in the daily staff digest. **No status change.** (Lean: reuses the job that already
  runs + lists Pending. A dedicated supervisor-escalation email can be added if you want a separate
  notice -- flag at review.) TDD the overdue predicate.
- **P2-T3 [code]** Dashboard: add a **"Decision overdue (> 3d)"** metric to `DashboardAppService`
  (Pending with `CreationTime < now - OverDueNotificationDays`), mirroring the existing 60-day legal
  tile + an Angular dashboard tile.
- **P2-T4 [code]** Pending list/view UI: per Pending row show a **"decision due in N days / OVERDUE"**
  badge so staff see the 3-day countdown at a glance.
- **P2-T5 [tdd]** Unit tests: the overdue predicate + the digest reading the setting (not the const).

**UI options summary (answer to "what we can do on the UI side"):** dashboard "Decision overdue" tile
(P2-T3), per-row decision-due/overdue badge + countdown (P2-T4), and the highlighted OVERDUE section in
the daily digest email (P2-T2). All read the same server-side 3-day setting.

---

## 4. Migration

One **host-only** EF migration for the new `AppointmentMaxTimeInternal` SystemParameter column (additive,
non-null int default 90), applied via the Dockerized DbMigrator. Matches the established host-only
precedent. No migration needed for Part 2 (reuses the existing `PendingAppointmentOverDueNotificationDays`
column).

## 5. Test plan

- TDD: `BookingPolicyValidator.EvaluateBookingPolicy` role matrix (P1-T2); the Part-2 overdue predicate +
  digest-reads-setting (P2-T5). Plus existing `BookingPolicyValidator` / SystemParameter tests updated for
  the new param + signature. Full Application.Tests suite green. Build + run via Docker only.

## 6. Risks / rollback

- **Lowering nobody / raising internal:** external stays 60 (unchanged); internal rises 60->90. Verify the
  validator's role branch + that the per-type external max is untouched. Covered by the test matrix.
- **Client/server drift:** the Angular 60/90 are hardcoded; if an admin changes the settings the modal
  threshold can be stale, but the server remains authoritative (worst case: a friendly-modal mismatch, not
  a wrong booking). Acceptable; can fetch the settings later.
- **Escalation noise:** the overdue flag rides the existing daily digest (no new recurring job unless we
  add a supervisor escalation) -- low blast radius, no status changes.
- **Rollback:** revert the PR; the migration `Down()` drops the one added column.

## 7. STOP-and-report gates

1. After this plan -> STOP for Adrian's approval before any code. (current)
2. Before any Docker rebuild -> `docker compose ps`; if active, STOP + coordinate.
3. When build + unit tests are green -> STOP, post a summary, WAIT for go-ahead before committing /
   pushing / opening the PR into main.
