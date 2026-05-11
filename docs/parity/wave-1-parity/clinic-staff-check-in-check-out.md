---
feature: clinic-staff-check-in-check-out
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs (Update with status changes)
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\info\
old-docs:
  - socal-project-overview.md (lines 525-527)
audited: 2026-05-01
status: audit-only
priority: 3
strict-parity: true
internal-user-role: ClinicStaff / StaffSupervisor / ITAdmin
depends-on:
  - clinic-staff-appointment-approval   # checks-in only happen on Approved appointments
required-by: []
---

# Clinic Staff -- Check-In / Check-Out

## Purpose

Day-of-appointment flow. Clinic Staff opens a "Today's appointments" view, marks each patient as Checked-In on arrival and Checked-Out on departure. Allows navigation to previous/next day. Status updates are manual.

**Strict parity with OLD.**

## OLD behavior (binding)

Per spec lines 525-527:

- **Today view:** list of appointments with `AppointmentDate.Date == today` (or selected date), shows patient details + scheduled time + status.
- **Date navigation:** previous-day and next-day buttons.
- **Check-In action:** sets `AppointmentStatus = CheckedIn`, no other changes.
- **Check-Out action:** sets `AppointmentStatus = CheckedOut`.
- **No automation** -- all manual; staff clicks the action when patient arrives/leaves.

### Status idempotency

Per `AppointmentDomain.UpdateValidation`:

- Already CheckedIn -> "Appointment Already checked in"
- Already CheckedOut -> "Appointment Already checked out"

### Critical OLD behaviors

- **Manual transitions only.** No automatic check-in based on appointment time.
- **Idempotent buttons.** Repeat click rejected with idempotency message.
- **Visible to all internal users** but typically Clinic Staff operates this.
- **Sequential states.** Pending -> Approved -> CheckedIn -> CheckedOut -> Billed. Cannot skip CheckedIn.

## OLD code map

Reuses the appointment update flow.

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/AppointmentRequestModule/AppointmentDomain.cs` Update path | Status change handling |
| `patientappointment-portal/.../appointments/info/...` (or similar) | Today-view UI |

## NEW current state

- TO VERIFY whether NEW has a today-view UI.
- AppointmentManager.UpdateAsync currently does NOT touch `AppointmentStatus` (per `Appointments/CLAUDE.md`); needs status method.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| `CheckInAsync(Guid appointmentId)` AppService method | OLD: implicit via Update | -- | **Add method** -- transitions Approved -> CheckedIn via state machine | B |
| `CheckOutAsync(Guid appointmentId)` AppService method | OLD: implicit | -- | **Add method** -- transitions CheckedIn -> CheckedOut | B |
| Idempotency check | OLD validation | -- | **State-machine guard** rejects same-status transition | I |
| Today-view list endpoint | OLD: implicit via filter | -- | **`GetTodayAppointmentsAsync(DateTime? date)`** -- defaults to today; filter to date.Date == input.Date | I |
| Date-navigation UI | OLD | -- | **Angular component** with prev/next buttons + date picker | I |
| Permissions | -- | -- | **`CaseEvaluation.Appointments.CheckInOut`** -- internal users only | I |

## Internal dependencies surfaced

- AppointmentStatus state machine (already required).

## Branding/theming touchpoints

- Today-view UI (logo, primary color, status pill colors).

## Replication notes

### ABP wiring

- Methods on `IAppointmentsAppService`: `CheckInAsync`, `CheckOutAsync`. Each updates `AppointmentStatus` via entity's `SetStatus(...)` method (state-machine guard).
- `GetTodayAppointmentsAsync` -- queries appointments for given date.

### Verification

1. Approved appointment -> Check-In -> status = CheckedIn
2. Try Check-In again -> rejected (idempotent)
3. Check-Out -> status = CheckedOut
4. Try Check-In after Check-Out -> rejected
5. Date navigation -> see prior/next day's appointments
