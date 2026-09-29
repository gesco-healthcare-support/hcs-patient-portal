---
id: BUG-046
title: The Case Tracker attendance POST is the only path for NoShow / NotSeen into the portal, every inbound attempt fails non-2xx and retries unbounded, and the portal emits no signal that it is failing
severity: high
issue: 694
found: 2026-09-04 (cross-system review with the Case Tracker maintainer; portal mechanism code-verified, production state measured on the box by Adrian)
flow: appointment-lifecycle (attendance outcomes), case-tracker-integration
component: src/HealthcareSupport.CaseEvaluation.HttpApi.Host/Controllers/Integration/CaseTrackerAttendanceController.cs; src/HealthcareSupport.CaseEvaluation.Domain/Integration/CaseTracker/CaseTrackerAttendanceService.cs; src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs (MarkAttendanceOutcomeAsync)
parity: not a parity issue -- new integration surface (phase 5, 2026-08-07)
---

# BUG-046 - Attendance path is single-routed, failing, and unobservable

> Tracked in [#694](https://github.com/gesco-healthcare-support/hcs-patient-portal/issues/694). Status lives in the issue; this file holds the
> reproduction and diagnosis.

## Symptom

No appointment in the production database has ever been marked `NoShow` or
`NotSeen`. Not one, in the life of the database. Six appointments sit
`Approved` with a date already past, where a patient cannot rebook.

Nothing on the portal side reports this. As of 2026-09-08 the cause is known and
it is not an unused branch: inbound attendance is being attempted constantly and
failing every time. See the 2026-09-08 corrected premise below.

## Corrected premise (2026-09-08) -- the path is NOT unexercised

**Measured by Levon on the Case Tracker side. This supersedes the "never fired"
framing that this file, its title and issue #694 all originally carried.**

| Measured                             | Value                                                      |
| ------------------------------------ | ---------------------------------------------------------- |
| Attendance attempts since 2026-08-19 | **2,565**                                                  |
| Of those returning `401`             | **zero**                                                   |
| Case Tracker token state             | **set** -- 64 chars, in their startup log since 2026-08-17 |
| Their give-up window                 | 96 hours, **never fires**                                  |
| Longest stranded outcome             | one No Show from **2026-08-19**, retrying for twenty days  |

Two consequences:

1. **Token configuration is required on neither side.** Zero `401`s across 2,565
   attempts proves both ends hold the token and authenticate. The 2026-09-04
   section below closed the portal-side half of that question; this closes the
   Case Tracker half. The token hypothesis is finished.
2. **Every attempt fails non-2xx and the retry never terminates**, because their
   96-hour give-up window does not fire. That is the live blocker, and it is a
   different defect from the one this file was opened for.

**The open question that decides the fix.** What non-2xx status are those
attempts receiving?

- `409` -- then the contract fix merged in #706 is the whole remedy: section K
  now documents the `{ status, retryable }` body, so the caller can honour
  `retryable: false` and stop. Nothing further is needed on the portal side.
- `400` / `404` / `500` -- then there is a separate portal-side defect that #706
  does not touch, and it gets diagnosed here.

Asked of Levon 2026-09-08; only he can pull a response body from their side.

**Harm classification is unchanged.** The counts are real, but the appointment
concerned is synthetic and staff go-live has not happened -- see Blast radius
below. This stays a latent defect and is not a PHI or reportable event. What
changed is the mechanism and the urgency, not the harm.

## Corrected premise (2026-09-04)

An earlier draft of this finding claimed the portal's
`CaseTracker:IntegrationToken` was empty in production and that inbound
attendance calls were being rejected. **That was wrong.** Two sources misled
it, and both are being corrected:

- `secrets.md`, circulated to the incoming maintainer, was stale against the
  server.
- `docs/integration/case-tracker-open-items.md:83` (item I7) still says
  "Still EMPTY in production, failing closed".

The token _is_ set in `secrets/env.prod`, the running `api` container has it,
and the endpoint authenticates correctly. Verified from the box:

| Request       | Result                                               |
| ------------- | ---------------------------------------------------- |
| no token      | 401                                                  |
| wrong token   | 401                                                  |
| correct token | 404 (authenticated; that appointment does not exist) |

So there is no portal-side deadlock, and the caller's latched-401 concern does
not apply -- the portal answers correctly, so a first call will not latch the
client off. This section then concluded that the missing piece was the matching
token on the Case Tracker side. **That conclusion was also wrong** -- see the
2026-09-08 section above, which measured their token as set and authenticating.

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

**No observability, and the 2026-09-08 measurement makes this the stronger
half of the finding rather than the weaker.** The original text here said the
path had been live and unexercised since phase 5 (2026-08-07), with zero
outcomes ever arriving. The truth is worse: **2,565 inbound attempts since
2026-08-19 all failed, and not one raised a warning, logged an anomaly or
surfaced a counter on this side.** Nobody here would have found this; it took
the maintainer of the other system reading his own logs.

An integration branch failing thousands of times is indistinguishable, from
inside the portal, from one that simply has no traffic. That is the defect,
independent of whatever status code the failures carry, and it is why this
issue stays open even if #706 turns out to be the whole transport fix.

## Blast radius (measured on the box, 2026-09-04)

Production carries one office database, `falkinstein`, with 15 live
appointments: 12 `Approved`, 1 cancelled-late, 1 rescheduled-no-bill,
1 rescheduled-late.

Six are `Approved` with a date already past:

| Appointment | Date   |
| ----------- | ------ |
| A00001      | 23 Jul |
| A00003      | 31 Jul |
| A00004      | 4 Aug  |
| A00005      | 5 Aug  |
| A00006      | 25 Aug |
| A00007      | 26 Aug |

Every record on that box is synthetic and created by the team for testing, and
staff go-live has not happened. So this is a latent defect rather than active
harm. It becomes real the moment a practice takes a real booking, which is the
argument for closing it now rather than at go-live. A00005 is a synthetic
record already scheduled for deletion.

## Recommended fix (high level)

1. **Get the non-2xx status from a recent inbound attempt.** Nothing else should
   be built until this is known -- it decides whether #706 already fixed this or
   whether there is a portal-side defect underneath. Superseded step: this item
   used to read "configure the matching token on the Case Tracker side", which
   the 2026-09-08 measurement refuted.
2. Add observability for a **failing** integration branch, not merely a silent
   one -- the original wording here asked for a warning when no outcome had ever
   been received, which 2,565 failed attempts would not have triggered. It needs
   to fire on inbound attendance that is arriving and being rejected. Silence
   should not be the success case, and neither should a 401-free error stream.
3. Consider a portal-side manual override for attendance outcomes, so a
   transport failure between two systems cannot strand an appointment with no
   recourse.
4. Sweep the six stranded `Approved` records above once the path is proven.

## Related

- `docs/integration/case-tracker-open-items.md` -- item I7. **Corrected in #706**
  (2026-09-08) and now carries the same measurements as this file; it is no
  longer the stale source the 2026-09-04 section warns about.
- `docs/integration/case-tracker-api-contract.md` -- contract section F, and
  **section K**, which #706 rewrote to document the `409 { status, retryable }`
  body. If the inbound failures turn out to be `409`s, section K is the fix.
- [[BUG-045]] -- separate defect, same integration surface.
