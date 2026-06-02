---
id: BUG-045
title: Internal / auto-approved booking 409s the post-create attorney + claim attach and silently drops Applicant Attorney, Defense Attorney, Claim Examiner, and the entire injury/claim; external (Pending) bookings persist everything
severity: high
status: open
found: 2026-06-02 (UI-seed population run; live-replicated as supervisor + API-GET-verified)
flow: appointment-booking (internal / auto-approved path)
component: angular/src/app/appointments/appointment-add.component.ts (post-create attach calls); src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs (UpsertApplicantAttorneyForAppointmentAsync, UpsertDefenseAttorneyForAppointmentAsync, injury-details create); src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs (CreateAsync internal auto-approve fast-path that stamps AppointmentApproveDate / sets initial Status=Approved)
parity: data-loss regression -- OLD persisted attorney + claim for staff-entered bookings
---

# BUG-045 - Internal auto-approved booking drops attorney + claim via a post-create 409

## Symptom

When an INTERNAL user (staff/supervisor) books an appointment, the booking
form fills the appointment core + patient + employer + Applicant Attorney +
Defense Attorney + a full Claim (claim number, body part, insurance, Claim
Examiner) and submits. The appointment is created and auto-Approved, but:

- `POST /api/app/appointments/{id}/applicant-attorney` returns **409 Conflict**.
- The Applicant Attorney, Defense Attorney, Claim Examiner, claim number,
  body parts, and insurance are **silently dropped** -- the appointment
  view later shows those sections empty.

Replicated 2026-06-02 booking A00001 (QME, Demo Clinic North) as
`supervisor@falkinstein.test` with Applicant Attorney Marcus Bennett
(`appatty1@gesco.com`), Defense Attorney Gregory Stone (`defatty1@gesco.com`),
and a full claim (Sentinel Casualty, Claim Examiner Henry Caldwell). All
synthetic data.

## Evidence

Browser console at submit:
```
POST /api/app/appointments/{id}/applicant-attorney  -> 409 Conflict
```

API container log at the same moment:
```
[17:37:28 WRN] ... Volo.Abp.Data.AbpDbConcurrencyException:
  The database operation was expected to affect 1 row(s), but actually
  affected 0 row(s); data may have been modified or deleted since
  entities were loaded.
```

Post-hoc API GETs against A00001 (data-loss confirmed):
```
GET /api/app/appointments/{id}/applicant-attorney  -> 204 (empty)
GET /api/app/appointments/{id}/defense-attorney    -> 204 (empty)
GET /api/app/appointment-injury-details/by-appointment/{id} -> [] (empty)
```
PERSISTED: appointment core (QME / date / time / location), patient
(name + SSN), employer. DROPPED: applicant attorney, defense attorney,
claim examiner, claim number, body parts, insurance.

## Diagnostic that isolates the path (2026-06-02)

Booking the SAME fully-populated appointment as an EXTERNAL user
(`patient@falkinstein.test`) -> appointment `df7b3474-...` -> the attach
calls returned cleanly:
```
POST /api/app/appointments/{id}/applicant-attorney  -> 204
POST /api/app/appointments/{id}/defense-attorney    -> 204
POST /api/app/appointment-injury-details            -> 200
```
No concurrency exception; attorney + claim fully persisted and visible on
the view. External bookings land **Pending** (no auto-approve).

=> The bug is SPECIFIC to the INTERNAL / AUTO-APPROVED create path.
External (Pending) bookings are unaffected. This was the workaround used
for the entire UI-seed run: all attorney/claim-bearing appointments were
booked as external users and then staff-approved (see findings F-A).

## Root cause (hypotheses, ranked)

1. **Internal auto-approve stamps the Appointment aggregate, racing the
   post-create sub-resource attach.** `AppointmentManager.CreateAsync` has an
   internal fast-path that sets the initial status to Approved and stamps
   `AppointmentApproveDate` (see Domain CLAUDE.md: "AppointmentApproveDate is
   stamped ... in CreateAsync when the initial status is already Approved").
   That write bumps the appointment `ConcurrencyStamp`. The SPA then fires the
   post-create attach calls (applicant-attorney / defense-attorney /
   injury-details) carrying the pre-approve stamp -> first writer wins, the
   rest fail the version check with `expected 1 row, actually 0` -> 409 and
   the attach is silently dropped. External bookings skip the auto-approve
   stamp, so the attach succeeds. (Most consistent with the external-vs-internal
   split.)

2. **The post-create attach calls fire in parallel and each saves the
   Appointment aggregate**, colliding on the shared stamp. The view-page logs
   show the three attach calls starting within the same millisecond, i.e. a
   parallel pattern.

3. **get-or-create patient mid-flow bumps a stamp** that the subsequent
   attach reads stale.

(1) is the leading theory; the external/internal split is the deciding
evidence.

## Functional impact

**HIGH -- silent data loss of core IME data.** Attorney and claim/injury are
mandatory IME data. Any staff-entered (internal) booking with attorneys/claim
loses all of it without an error surfaced to the user (the appointment still
"succeeds"). Downstream AttyCE packets, attorney-facing emails, and the claim
record are all degraded. Invisible unless someone re-opens the appointment.

## Recommended fix (high level)

- Do NOT auto-approve-on-create in a way that bumps the appointment stamp
  before the SPA's post-create attach completes. Options: (a) sequence the
  attach calls server-side as part of the create transaction; (b) have the
  SPA attach attorney/claim BEFORE triggering approve; or (c) make the attach
  endpoints re-read the current appointment stamp instead of trusting the
  client-supplied one.
- Make the attach endpoints FAIL LOUD (surface the 409 to the user) instead
  of the appointment "succeeding" with the sub-resources dropped.
- Add an integration test: internal booking with AA + DA + Claim -> assert all
  three persist (currently they do not).

## Related

- [[BUG-042]] attorney name dropped on persist (no name column) -- different
  defect on the SAME attorney attach path; both must be fixed together for
  internal bookings to carry attorney data.
- [[BUG-040]] cumulative-trauma / ToDateOfInjury not persisting -- claim/injury
  persistence sibling.
- [[BUG-008]] PUT /me concurrency-stamp staleness -- same optimistic-concurrency
  family (synchronous request path).
- [[BUG-033]] / [[BUG-036]] -- the ASYNC (Hangfire packet/email) analog of the
  same "concurrent writes to the Appointment aggregate collide on the stamp"
  theme. This bug is the SYNCHRONOUS booking-attach variant.
