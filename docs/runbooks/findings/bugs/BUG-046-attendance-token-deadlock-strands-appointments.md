---
id: BUG-046
title: The Case Tracker attendance POST is the only path for NoShow / NotSeen into the portal, it has never been exercised, and the portal cannot detect that it is dead
severity: high
issue: 694
found: 2026-09-04 (cross-system review with the Case Tracker maintainer; portal mechanism code-verified, production state measured on the box by Adrian)
flow: appointment-lifecycle (attendance outcomes), case-tracker-integration
component: src/HealthcareSupport.CaseEvaluation.HttpApi.Host/Controllers/Integration/CaseTrackerAttendanceController.cs; src/HealthcareSupport.CaseEvaluation.Domain/Integration/CaseTracker/CaseTrackerAttendanceService.cs; src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs (MarkAttendanceOutcomeAsync)
parity: not a parity issue -- new integration surface (phase 5, 2026-08-07)
---

# BUG-046 - Attendance path is single-routed, unexercised and unobservable

> Tracked in [#694](https://github.com/gesco-healthcare-support/hcs-patient-portal/issues/694). Status lives in the issue; this file holds the
> reproduction and diagnosis.

## Symptom

No appointment in the production database has ever been marked `NoShow` or
`NotSeen`. Not one, in the life of the database. Six appointments sit
`Approved` with a date already past, where a patient cannot rebook.

Nothing on either side reports this. The portal has no signal that a whole
lifecycle branch has never fired.

## Corrected premise (2026-09-04)

An earlier draft of this finding claimed the portal's
`CaseTracker:IntegrationToken` was empty in production and that inbound
attendance calls were being rejected. **That was wrong.** Two sources misled
it, and both are being corrected:

- `secrets.md`, circulated to the incoming maintainer, was stale against the
  server.
- `docs/integration/case-tracker-open-items.md:83` (item I7) still says
  "Still EMPTY in production, failing closed".

The token *is* set in `secrets/env.prod`, the running `api` container has it,
and the endpoint authenticates correctly. Verified from the box:

| Request | Result |
|---|---|
| no token | 401 |
| wrong token | 401 |
| correct token | 404 (authenticated; that appointment does not exist) |

So there is no portal-side deadlock, and the caller's latched-401 concern does
not apply -- the portal answers correctly, so a first call will not latch the
client off. What is missing is the matching token on the Case Tracker side.

## What remains a portal-side defect

The premise changed; the structural risk did not.

**Single route, no recourse.** `AppointmentManager.MarkAttendanceOutcomeAsync`
has exactly one production caller, `CaseTrackerAttendanceService.cs:112`. The
only other reference is the generated Angular proxy, not a UI path. There is no
staff screen, no admin override and no back-office action that reaches it. The
service's own summary states this deliberately: intake staff record these
outcomes on the Case Tracker side, so that endpoint is the only way either
status enters the portal. A transport failure between two systems therefore
strands an appointment with no human recourse inside the portal.

**No observability.** The path has been live and unexercised since phase 5
(2026-08-07). Zero attendance outcomes have ever arrived. Nothing raised a
warning, logged an anomaly or surfaced a counter. An integration branch that
has never once fired is indistinguishable, from inside the portal, from one
that simply has no traffic.

## Blast radius (measured on the box, 2026-09-04)

Production carries one office database, `falkinstein`, with 15 live
appointments: 12 `Approved`, 1 cancelled-late, 1 rescheduled-no-bill,
1 rescheduled-late.

Six are `Approved` with a date already past:

| Appointment | Date |
|---|---|
| A00001 | 23 Jul |
| A00003 | 31 Jul |
| A00004 | 4 Aug |
| A00005 | 5 Aug |
| A00006 | 25 Aug |
| A00007 | 26 Aug |

Every record on that box is synthetic and created by the team for testing, and
staff go-live has not happened. So this is a latent defect rather than active
harm. It becomes real the moment a practice takes a real booking, which is the
argument for closing it now rather than at go-live. A00005 is a synthetic
record already scheduled for deletion.

## Recommended fix (high level)

1. Configure the matching token on the Case Tracker side, then exercise the
   path end to end once and confirm a status actually lands. The remaining
   configuration work is on the Case Tracker side.
2. Add observability for a dead integration branch -- at minimum a warning when
   `CaseTrackerPushEnabled` is true for an office and no attendance outcome has
   ever been received. Silence should not be the success case.
3. Consider a portal-side manual override for attendance outcomes, so a
   transport failure between two systems cannot strand an appointment with no
   recourse.
4. Sweep the six stranded `Approved` records above once the path is proven.

## Related

- `docs/integration/case-tracker-open-items.md:83` -- item I7, currently
  incorrect about production state.
- `docs/integration/case-tracker-api-contract.md` (contract section F).
- [[BUG-045]] -- separate defect, same integration surface.
