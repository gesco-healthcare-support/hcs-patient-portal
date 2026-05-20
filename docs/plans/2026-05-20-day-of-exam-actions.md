---
status: draft
issue: day-of-exam-actions
owner: AdrianG
created: 2026-05-20
approach: code on the AppService methods (the state-machine + email
  dispatch are already TDD-covered); test-after on the Angular
  today's-view component; manual UI verification for the four-button
  workflow.
sequence: Stage 1 #3 in docs/runbooks/ENGINEERING-ROADMAP.md
  (NOT part of the slot-rework chain; independent of the doctor
  invariant + slot rework series).
branch: create a new branch off `feat/replicate-old-app`. PR back to
  `feat/replicate-old-app`. Do not merge to `main` until verified
  manually on the dev stack.
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs (state transitions + email dispatch)
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentsController.cs (HTTP surface, PATCH-based)
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\list\appointment-list.component.{ts,html} (today-view UI)
parity-audit:
  - docs/parity/wave-1-parity/clinic-staff-check-in-check-out.md
---

# Day-of-exam actions: Check-In / Check-Out / No-Show / Billed + today's-appointments view

## Goal

Ship the four clinic-staff actions that fire on the day of the
exam, plus the today's-appointments list view that exposes them:

1. **Check-In** -- mark a patient as having arrived (Approved -> CheckedIn).
2. **Check-Out** -- mark the patient as having left after the exam
   (CheckedIn -> CheckedOut).
3. **No-Show** -- mark that the patient did not arrive (Approved -> NoShow).
4. **Billed** -- mark the appointment as billed (CheckedOut -> Billed),
   the terminal happy-path state.

Plus a "Today's appointments" Angular view at `/appointments/today`
that lists today's bookings (with date navigation), surfaces row-action
buttons for the four transitions per OLD's button-visibility rules,
prompts a confirmation dialog before each action, and shows the
result as a toast.

## Why

`docs/parity/wave-1-parity/clinic-staff-check-in-check-out.md` carries
this as Wave 1 / priority 3, strict-parity. The
`docs/runbooks/ENGINEERING-ROADMAP.md` lists it as Stage 1 #3
(internal-alpha-blocking) with a 4-6 day estimate. Status enums for
all four target states exist
(`Domain.Shared/Enums/AppointmentStatusType.cs:13 NoShow=4`, `:14
CheckedIn=9`, `:15 CheckedOut=10`, `:16 Billed=11`), and the state
machine in `Domain/Appointments/AppointmentManager.cs:247-271` already
permits the four transitions, but no AppService method, controller
endpoint, permission constant, or UI exists to fire them.

OLD reference: `AppointmentDomain.cs:312-345` (idempotency) and
`:406-573` (Update + side-effects); `:985-1027` (email dispatch).
OLD UI: `appointment-list.component.ts:113-191` (action methods) and
`appointment-list.component.html:81-129` (button visibility).

## Non-goals

- No changes to the state machine itself. The transitions already
  exist in `AppointmentManager.BuildMachine`.
- No changes to `StatusChangeEmailHandler`. The Checked-In /
  Checked-Out / No-Show email dispatchers are already wired (see
  `Application/Notifications/Handlers/StatusChangeEmailHandler.cs:198-219`,
  `:387` `DispatchCheckedInAsync`, `:424` `DispatchCheckedOutAsync`,
  `:463` `DispatchNoShowAsync`).
- No email handler for Billed (OLD ships none; see Q7 below).
- No automation. All four transitions are manually triggered by
  clinic staff. No appointment-time-based auto-no-show, no
  auto-check-in.
- No changes to the existing generic appointments list page
  (`/appointments`). The today's-view is a dedicated, parallel route
  with a different UX shape (date navigation, row-action buttons).
- No retroactive UI for existing terminal-state appointments
  (CheckedIn / CheckedOut / NoShow / Billed appointments stay visible
  in the today's-view but their action buttons hide per OLD rules;
  no separate "history" view).

## Decisions locked

These were resolved 2026-05-20 (questions Q1-Q9 in the research
summary):

1. **Q1 -- NoShow idempotency.** OLD's `UpdateValidation` (`:312-345`)
   does NOT block double-no-show -- only `{Approved, Rejected, CheckedIn,
   CheckedOut, Billed}` get the "already X" error. NEW's state machine
   already prevents re-firing `MarkNoShow` from any state except
   Approved (per `BuildMachine`), so NoShow is implicitly idempotent.
   **No action needed.** Just document it.

2. **Q2 -- Today's-view default filter.** OLD's view filters by
   `appointmentStatusId = Approved` and rows vanish after each action
   (no way to see the same appointment for the next transition without
   changing the filter, which OLD's UI does not expose -- a UX bug).
   **NEW will show ALL of today's appointments regardless of status.**
   The status pill column carries the visual state; action buttons
   are conditionally visible per OLD's rules. The user can complete
   the full Check-In -> Check-Out -> Billed chain without leaving the
   view.

3. **Q3 -- Four separate permissions** (vs OLD's single
   `AppointmentCheckInCheckOut`). Matches NEW's existing per-action
   pattern (`Approve`, `Reject`, `RequestCancellation`,
   `RequestReschedule`). Finer-grained gates if NoShow / Billed
   ever need a more senior role.

4. **Q4 -- Short method names** (`CheckInAsync`, `CheckOutAsync`,
   `MarkNoShowAsync`, `MarkBilledAsync`) -- not `CheckInAppointmentAsync`.
   The controller route already carries `/appointments/{id}/`, so the
   "Appointment" infix would be redundant.

5. **Q5 -- POST endpoints** at `/api/app/appointments/{id}/check-in`,
   `/check-out`, `/mark-no-show`, `/mark-billed`. Matches the existing
   `RejectAppointmentAsync` route shape.

6. **Q6 -- Dedicated route** `/appointments/today` (a child route under
   the existing `APPOINTMENT_ROUTES`). Avoids overloading the generic
   list page with date-nav UI and per-row action buttons.

7. **Q7 -- Billed sends no email** (OLD parity:
   `AppointmentDomain.cs:985-1034` switch case has no Billed branch).
   `StatusChangeEmailHandler` should NOT add a Billed dispatcher.

8. **Q8 -- Confirmation dialog before every action.** Use ABP's
   `ConfirmationService` (already wired in the SPA for delete prompts);
   message text per OLD: "Checked In Appointment", "Checked Out
   Appointment", "No Show Appointment", "Billed Appointment".

9. **Q9 -- TDD scope.** AppService methods are 5-line wrappers; the
   state-machine + email dispatch they delegate to already have
   coverage. `approach: code` for the AppService methods; `test-after`
   for the Angular component; `tdd` only if a new domain-level
   validator gets added (none planned today).

## Files touched (with the exact change per file)

### 1. `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs`

Add four wrapper methods after the existing `RejectAsync` (line 179)
and `RequestRescheduleAsync` (line 191), mirroring the same shape.
Each delegates to the existing `TransitionAsync(id, trigger, reason,
actingUserId)` (line 193). No change to `BuildMachine`; the
transitions are already there.

```csharp
/// <summary>
/// 2026-05-20 -- Approved -> CheckedIn. Clinic staff marks the patient
/// as arrived for their appointment. Mirrors OLD's check-in trigger
/// (AppointmentDomain.cs:992-1003). Publishes
/// <see cref="AppointmentStatusChangedEto"/>; the StatusChangeEmailHandler
/// dispatches PatientAppointmentCheckedIn to all stakeholders.
/// </summary>
public virtual Task<Appointment> CheckInAsync(Guid id, Guid? actingUserId)
    => TransitionAsync(id, AppointmentTransitionTrigger.CheckIn, reason: null, actingUserId);

/// <summary>
/// 2026-05-20 -- CheckedIn -> CheckedOut. Clinic staff marks the patient
/// as having left after the exam. OLD :1004-1015.
/// </summary>
public virtual Task<Appointment> CheckOutAsync(Guid id, Guid? actingUserId)
    => TransitionAsync(id, AppointmentTransitionTrigger.CheckOut, reason: null, actingUserId);

/// <summary>
/// 2026-05-20 -- Approved -> NoShow. Clinic staff marks the patient as
/// having missed the appointment. NoShow emails are routed to internal
/// staff only (NoShowInternalRoles in StatusChangeEmailHandler) -- the
/// patient is NOT notified (OLD parity, :1016-1027).
/// </summary>
public virtual Task<Appointment> MarkNoShowAsync(Guid id, Guid? actingUserId)
    => TransitionAsync(id, AppointmentTransitionTrigger.MarkNoShow, reason: null, actingUserId);

/// <summary>
/// 2026-05-20 -- CheckedOut -> Billed. Terminal happy-path state.
/// No email is dispatched (OLD parity: no Billed case in the switch
/// at AppointmentDomain.cs:985-1034).
/// </summary>
public virtual Task<Appointment> MarkBilledAsync(Guid id, Guid? actingUserId)
    => TransitionAsync(id, AppointmentTransitionTrigger.Bill, reason: null, actingUserId);
```

Notes:
- All four call sites pass `reason: null`. None of these transitions
  carry a reason in OLD; the `RejectionNotes` field (`vemailSender.RejectionNotes`)
  surfaced in the CheckedIn / CheckedOut email body templates
  (`:997-1000`, `:1009-1012`) is a stale-data carryover from the
  approve/reject flow, not user input at the time of check-in.
- `actingUserId` propagates into `AppointmentStatusChangedEto` for
  audit-log purposes (the change-log handler already consumes it).

### 2. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs`

Add four new permission constants to the `Appointments` nested
static class (lines 88-100 region):

```csharp
public static class Appointments
{
    public const string Default = GroupName + ".Appointments";
    public const string Edit = Default + ".Edit";
    public const string Create = Default + ".Create";
    public const string Delete = Default + ".Delete";
    // Phase 2.5 (2026-05-01)
    public const string Approve = Default + ".Approve";
    public const string Reject = Default + ".Reject";
    public const string RequestCancellation = Default + ".RequestCancellation";
    public const string RequestReschedule = Default + ".RequestReschedule";
    // 2026-05-20 -- day-of-exam actions, internal-staff only.
    public const string CheckIn = Default + ".CheckIn";
    public const string CheckOut = Default + ".CheckOut";
    public const string MarkNoShow = Default + ".MarkNoShow";
    public const string MarkBilled = Default + ".MarkBilled";
}
```

### 3. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs`

Extend the existing `appointmentPermission.AddChild(...)` block (lines
55-65 region) with four more children:

```csharp
appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.CheckIn, L("Permission:CheckIn"));
appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.CheckOut, L("Permission:CheckOut"));
appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.MarkNoShow, L("Permission:MarkNoShow"));
appointmentPermission.AddChild(CaseEvaluationPermissions.Appointments.MarkBilled, L("Permission:MarkBilled"));
```

`MultiTenancySides` is inherited from the parent (`Tenant`); no
explicit override needed.

### 4. `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs`

Grant all four to **Staff Supervisor**, **Clinic Staff**, and
**IT Admin** (the three internal roles per
`docs/parity/wave-1-parity/clinic-staff-check-in-check-out.md`).

In `StaffSupervisorGrants()` (after the existing
`Approve("Appointments")` / `Reject("Appointments")` yields,
around line 225):

```csharp
yield return $"{Group}.Appointments.CheckIn";
yield return $"{Group}.Appointments.CheckOut";
yield return $"{Group}.Appointments.MarkNoShow";
yield return $"{Group}.Appointments.MarkBilled";
```

Same additions to `ClinicStaffGrants()` (the operational role that
runs the day-of-exam workflow per OLD's role mapping
`AppointmentDomain.cs:1021` `Roles.StaffSupervisor || Roles.ClinicStaff`).

IT Admin already gets the full `CaseEvaluation.*` tree via
`ItAdminGrants()`'s standard CRUD + Approve/Reject loop, so no
addition is needed there -- but verify the new permissions land in
the host pass by running the dedupe-probe SQL.

### 5. NEW file: `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.DayOfExam.cs`

Partial class on `AppointmentsAppService`. Four methods, each
identical shape to `RejectAppointmentAsync` in
`AppointmentsAppService.Approval.cs`:

```csharp
public partial class AppointmentsAppService
{
    [Authorize(CaseEvaluationPermissions.Appointments.CheckIn)]
    public virtual async Task<AppointmentDto> CheckInAsync(Guid id)
    {
        var appointment = await _appointmentManager.CheckInAsync(id, CurrentUser.Id);
        _logger.LogInformation(
            "AppointmentsAppService.CheckInAsync: appointment {AppointmentId} checked in by {UserId}.",
            id, CurrentUser.Id);
        return ObjectMapper.Map<Appointment, AppointmentDto>(appointment);
    }

    [Authorize(CaseEvaluationPermissions.Appointments.CheckOut)]
    public virtual async Task<AppointmentDto> CheckOutAsync(Guid id)
    {
        var appointment = await _appointmentManager.CheckOutAsync(id, CurrentUser.Id);
        _logger.LogInformation(
            "AppointmentsAppService.CheckOutAsync: appointment {AppointmentId} checked out by {UserId}.",
            id, CurrentUser.Id);
        return ObjectMapper.Map<Appointment, AppointmentDto>(appointment);
    }

    [Authorize(CaseEvaluationPermissions.Appointments.MarkNoShow)]
    public virtual async Task<AppointmentDto> MarkNoShowAsync(Guid id)
    {
        var appointment = await _appointmentManager.MarkNoShowAsync(id, CurrentUser.Id);
        _logger.LogInformation(
            "AppointmentsAppService.MarkNoShowAsync: appointment {AppointmentId} marked no-show by {UserId}.",
            id, CurrentUser.Id);
        return ObjectMapper.Map<Appointment, AppointmentDto>(appointment);
    }

    [Authorize(CaseEvaluationPermissions.Appointments.MarkBilled)]
    public virtual async Task<AppointmentDto> MarkBilledAsync(Guid id)
    {
        var appointment = await _appointmentManager.MarkBilledAsync(id, CurrentUser.Id);
        _logger.LogInformation(
            "AppointmentsAppService.MarkBilledAsync: appointment {AppointmentId} marked billed by {UserId}.",
            id, CurrentUser.Id);
        return ObjectMapper.Map<Appointment, AppointmentDto>(appointment);
    }
}
```

The `_appointmentManager` and `_logger` fields already exist on
`AppointmentsAppService` (used by the Approval partial). No new DI
parameters; no new constructor.

### 6. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Appointments/IAppointmentsAppService.cs`

Add the four method signatures to the interface so the proxy generator
picks them up:

```csharp
Task<AppointmentDto> CheckInAsync(Guid id);
Task<AppointmentDto> CheckOutAsync(Guid id);
Task<AppointmentDto> MarkNoShowAsync(Guid id);
Task<AppointmentDto> MarkBilledAsync(Guid id);
```

Place near the existing `ApproveAppointmentAsync` / `RejectAppointmentAsync`
declarations.

### 7. `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/Appointments/AppointmentController.cs`

Add four HTTP endpoints, each a one-liner delegating to the AppService:

```csharp
[HttpPost("{id}/check-in")]
public virtual Task<AppointmentDto> CheckInAsync(Guid id)
    => _appointmentsAppService.CheckInAsync(id);

[HttpPost("{id}/check-out")]
public virtual Task<AppointmentDto> CheckOutAsync(Guid id)
    => _appointmentsAppService.CheckOutAsync(id);

[HttpPost("{id}/mark-no-show")]
public virtual Task<AppointmentDto> MarkNoShowAsync(Guid id)
    => _appointmentsAppService.MarkNoShowAsync(id);

[HttpPost("{id}/mark-billed")]
public virtual Task<AppointmentDto> MarkBilledAsync(Guid id)
    => _appointmentsAppService.MarkBilledAsync(id);
```

Same shape as the existing `/{id}/approve` and `/{id}/reject` routes.
Route names are kebab-cased to match the existing controller conventions
(see the `/api/app/appointments/{id}/approve` pattern).

### 8. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`

Add four permission labels + four button labels + four confirmation
prompts + four success toasts. Place under appropriate existing
sections:

```jsonc
// Under "Permission:" keys (line ~30-70 area in en.json):
"Permission:CheckIn": "Check In",
"Permission:CheckOut": "Check Out",
"Permission:MarkNoShow": "Mark No-Show",
"Permission:MarkBilled": "Mark Billed",

// Under appointment action / button labels (new section if needed):
"Appointment:Action:CheckIn": "Check In",
"Appointment:Action:CheckOut": "Check Out",
"Appointment:Action:MarkNoShow": "Mark No-Show",
"Appointment:Action:MarkBilled": "Mark Billed",

// Confirmation dialog body text (OLD :177 "Checked In Appointment"):
"Appointment:Confirm:CheckIn": "Confirm: check in this appointment?",
"Appointment:Confirm:CheckOut": "Confirm: check out this appointment?",
"Appointment:Confirm:MarkNoShow": "Confirm: mark this appointment as a no-show?",
"Appointment:Confirm:MarkBilled": "Confirm: mark this appointment as billed?",

// Success-toast messages (OLD :208-228):
"Appointment:Success:CheckIn": "Appointment checked in.",
"Appointment:Success:CheckOut": "Appointment checked out.",
"Appointment:Success:MarkNoShow": "Appointment marked as no-show.",
"Appointment:Success:MarkBilled": "Appointment marked as billed.",

// Today's-view labels:
"Appointment:TodayView:Title": "Today's Appointments",
"Appointment:TodayView:DateLabel": "Date",
"Appointment:TodayView:Previous": "Previous",
"Appointment:TodayView:Next": "Next",
"Appointment:TodayView:Today": "Today",
"Appointment:TodayView:Empty": "No appointments scheduled for this date.",
```

ASCII only.

### 9. NEW Angular files: today's-appointments view

Create under `angular/src/app/appointments/today/`:

- `today-appointments.component.ts` -- standalone component, no
  `Abstract*Component` scaffold (the existing list page uses the ABP
  Suite scaffold but this is a different UX so we author it fresh).
- `today-appointments.component.html` -- date-nav bar + ngx-datatable
  with four action-button columns.
- `today-appointments.component.scss` (optional; styles inline if
  possible).

Component behavior (mirroring OLD's `appointment-list.component.ts`
but with the design decisions baked in):

- **State signals:** `selectedDate` (default `new Date()`),
  `appointments` (list), `isLoading` (boolean), `lastActionAppointmentId`
  (to disable buttons mid-flight).
- **Fetch:** call `appointmentService.getList({ appointmentDateMin:
  startOfDay, appointmentDateMax: endOfDay })` on mount + after each
  successful action. NO status filter (per Q2: show all of today's
  statuses).
- **Date nav:**
  - `Today` button -> reset `selectedDate = new Date()`, refetch.
  - `Previous` button -> `selectedDate -= 1 day`, refetch.
  - `Next` button -> `selectedDate += 1 day`, refetch.
  - Date picker (ngb-datepicker) -> onSelect, refetch.
- **Row actions** -- four buttons per row, conditional visibility:
  - "Check In" visible when `row.appointmentStatus == Approved`
  - "No-Show" visible when `row.appointmentStatus == Approved`
  - "Check Out" visible when `row.appointmentStatus == CheckedIn`
  - "Mark Billed" visible when `row.appointmentStatus == CheckedOut`
- **Confirmation prompt** via ABP's `ConfirmationService` before
  firing each action.
- **POST** the right endpoint via the regenerated proxy
  (`appointmentService.checkInAsync(id)` etc).
- **Toast** on success; refetch.
- **Error handling** -- if the server returns
  `CaseEvaluation:AppointmentInvalidTransition`, show a friendly
  message ("This appointment cannot be {action} from its current
  status; refresh and try again") because the most likely cause is
  a stale list view.

### 10. `angular/src/app/appointments/today/today-appointments-routes.ts` + register in `appointment-routes.ts`

Add a new lazy route segment:

```typescript
// today-appointments-routes.ts
import { authGuard, permissionGuard } from '@abp/ng.core';
import { Routes } from '@angular/router';

export const TODAY_APPOINTMENTS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () => import('./today-appointments.component')
      .then(c => c.TodayAppointmentsComponent),
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: 'CaseEvaluation.Appointments.CheckIn' },
  },
];
```

Register in `appointments/appointment/appointment-routes.ts` as a
sibling of the existing `''` (list) and `'view/:id'` routes:

```typescript
{ path: 'today', children: TODAY_APPOINTMENTS_ROUTES },
```

This produces the URL `/appointments/today`. The
`requiredPolicy: 'CaseEvaluation.Appointments.CheckIn'` gates the
route; tenant admins / Staff Supervisors / Clinic Staff all have it
post-seed; external roles do not.

### 11. `angular/src/app/route.provider.ts`

Add a sidebar menu entry under "Appointment Management" (the existing
parent menu container). After the existing
`'::Menu:AppointmentManagement'` registrations, add:

```typescript
{
  path: '/appointments/today',
  name: '::Menu:TodayAppointments',
  parentName: '::Menu:AppointmentManagement',
  iconClass: 'fas fa-calendar-day',
  order: 3,
  layout: eLayoutType.application,
  requiredPolicy: 'CaseEvaluation.Appointments.CheckIn',
},
```

Add `::Menu:TodayAppointments` -> "Today's Appointments" to en.json.

### 12. Regenerate Angular proxy

After backend builds with the new interface methods + controller
endpoints, run:

```bash
cd angular
yarn nswag refresh   # or `abp generate-proxy` per the project convention
```

Confirms `appointmentService.checkInAsync`, `.checkOutAsync`,
`.markNoShowAsync`, `.markBilledAsync` exist in
`angular/src/app/proxy/appointments/appointment.service.ts`.

## Test plan

### Unit + integration (backend)

Existing test file
`test/.../Appointments/AppointmentApprovalValidatorUnitTests.cs`
covers the approval/rejection validator. Add a sibling file:

`test/.../Appointments/DayOfExamFlowTests.cs` -- six `[Fact]` tests
covering the full state-machine reachability for the four new triggers:

| # | Test | Acceptance |
|---|------|------------|
| 1 | `CheckInAsync_FromApproved_TransitionsToCheckedIn` | Seed Approved appointment; `_appointmentManager.CheckInAsync(id, user)` returns appointment with status CheckedIn; `AppointmentStatusChangedEto` published with `fromStatus=Approved`, `toStatus=CheckedIn`. |
| 2 | `CheckInAsync_FromPending_ThrowsInvalidTransition` | Seed Pending appointment; `CheckInAsync` throws `BusinessException` with code `AppointmentInvalidTransition` and `WithData("from", Pending)`. |
| 3 | `CheckOutAsync_FromCheckedIn_TransitionsToCheckedOut` | Chain: Approved -> CheckIn -> CheckOut. Final status = CheckedOut. |
| 4 | `MarkNoShowAsync_FromApproved_TransitionsToNoShow` | Seed Approved; `MarkNoShowAsync` -> NoShow. |
| 5 | `MarkBilledAsync_FromCheckedOut_TransitionsToBilled` | Chain: Approved -> CheckIn -> CheckOut -> Bill. Final = Billed. |
| 6 | `MarkBilledAsync_FromCheckedIn_ThrowsInvalidTransition` | Chain: Approved -> CheckIn -> Bill -> throws (must Check-Out first). |

The state-machine + email dispatch are already covered by existing
tests on `StatusChangeEmailHandler`; we are NOT re-asserting those
here. The six new tests assert ONLY the trigger-reachability surface
the four new Manager methods open.

### Angular (test-after)

After the today-view ships, write Playwright MCP smoke scenarios:

- HRD-DAYOFEXAM-1: navigate to `/appointments/today` as
  `clistaff1@gesco.com`. Date defaults to today. Approved row shows
  Check-In + No-Show buttons. Status pill = Approved.
- HRD-DAYOFEXAM-2: click Check-In. Confirmation dialog renders.
  Click confirm. Toast shows "Appointment checked in." List
  refetches. Same row now shows Check-Out button only.
- HRD-DAYOFEXAM-3: chain Check-In -> Check-Out -> Mark Billed.
  Final row shows status pill = Billed, no action buttons visible.
- HRD-DAYOFEXAM-4: on a separate Approved row, click No-Show.
  Status flips to NoShow. Internal-staff inbox receives
  `PatientAppointmentNoShow` email (verify via API logs:
  `SendAppointmentEmailJob: delivered (StatusChange/NoShow/...)`);
  patient inbox does NOT.
- HRD-DAYOFEXAM-5: Previous-day button shows yesterday's
  appointments. Today-button resets.

### Manual UI verification (after Playwright passes)

1. `docker compose up -d --build` against a clean DB.
2. Seed a tenant + doctor (via the existing seed contributors).
3. Book three appointments for tomorrow, approve all three.
4. Set system date to tomorrow (or just navigate the date picker).
5. Run through the full Check-In -> Check-Out -> Mark Billed chain
   on appointment 1. Confirm status pill updates, action buttons
   change, emails dispatch (CheckedIn + CheckedOut), success toasts.
6. Mark appointment 2 as No-Show. Confirm internal-staff inbox
   gets the email, patient inbox does not.
7. Leave appointment 3 untouched. Confirm it stays Approved with
   Check-In + No-Show buttons visible.
8. Use Previous-day to navigate to yesterday. Empty state renders.
9. Use Today button to snap back.

## Risk and rollback

**Blast radius:**
- One new partial class (`AppointmentsAppService.DayOfExam.cs`), four
  new endpoints, four new permissions, four new state-machine wrappers,
  one new Angular component, one new sidebar menu entry.
- No schema changes. No EF Core migration. No data migration.
- No changes to the existing list page or to the existing approval
  flow.

**Rollback:**
- Revert the commit on the feature branch. Existing approval /
  rejection / reschedule flows are unaffected.
- The new permissions land in `AbpPermissionGrants` after the seed
  contributor runs; on rollback they become orphan grants
  (harmless; no AppService method references them, so they cannot
  be exercised). To clean up: re-run the seed contributor.

**Risk: stale list view causes apparent invalid transitions.** If
two clinic-staff users are looking at the same today-view and one
checks in a patient, the other's row still shows the Check-In
button. Clicking it after the row's underlying status has changed
will return `AppointmentInvalidTransition`. Mitigation: the toast
message ("This appointment cannot be {action} from its current
status; refresh and try again") + the post-action refetch keep the
view fresh for the active user. Concurrent-staff stale-state is a
known UX cost of refresh-on-action vs realtime push.

**Risk: email handler edge cases.** `StatusChangeEmailHandler`
already handles all three emailable statuses, but its `NoShow`
internal-roles filter (`NoShowInternalRoles` at
`StatusChangeEmailHandler.cs:81`) depends on the seed contributor
mapping `Staff Supervisor` and `Clinic Staff` role NAMES exactly
(case-sensitive). Verify post-seed: `SELECT Name FROM AbpRoles
WHERE TenantId = <Falkinstein>` returns the expected role list.

**Risk: stale `AppointmentDto` shape after status flip.** The DTO
returned by the AppService is the result of `ObjectMapper.Map<>` on
the post-transition entity, so its `AppointmentStatus` field
reflects the new state. The SPA should treat the response as the
authoritative new state (not re-query).

**Risk: today-view permission gate.** The route is gated by
`CaseEvaluation.Appointments.CheckIn`. If you want a separate "view
today's appointments but cannot act" surface, the gate would need
to relax to `Appointments.Default`. **Not in scope** -- if a user
should see today's appointments but not act, they should use the
generic list page with a date filter.

## Verification

End-to-end test procedure after all changes ship:

1. Clean docker stack: `docker compose down -v`.
2. Rebuild + migrate: `docker compose up -d --build`. Confirm the
   DbMigrator output shows the new permissions seeded.
3. Log in as `clistaff1@gesco.com` on
   `falkinstein.localhost:4200`. Expect "Today's Appointments"
   to appear in the sidebar under Appointment Management.
4. Click into `/appointments/today`. List renders for today.
5. Walk the full chain on the test appointments. Confirm:
   - Status pill updates after each transition.
   - Action buttons hide/show correctly.
   - Confirmation dialog renders before each action.
   - Success toast renders after.
   - List refetches.
   - Emails dispatch (CheckedIn + CheckedOut to all stakeholders;
     NoShow to internal staff only; Billed dispatches nothing).
6. Log in as a Patient user. Confirm `/appointments/today` returns a
   permission error (or routes to /home via the post-login redirect
   guard).
7. Log in as IT Admin on `admin.localhost:4200`. Today-view is
   reachable (IT Admin has the host-scope CheckIn permission via
   `ItAdminGrants` standard CRUD loop).

## How to apply

- Create a new branch off `feat/replicate-old-app`:
  `feat/day-of-exam-actions`.
- Land all 12 changes in a single PR back to `feat/replicate-old-app`.
- Squash-merge per project policy.
- This plan does not depend on the doctor-invariant work, the slot
  rework, or the parallel-worktree docker work. It can ship
  independently of any of those.
- Plan moves to `status: shipped` once the PR merges and the manual
  verification passes.
