---
id: OBS-12
title: Reschedule + Cancellation UI not built for Patient detail view (per parity audit)
severity: observation
status: deferred
deferred-to: W3
found: 2026-05-14 during Workflow E
resolved: 2026-05-22
flow: appointment-change-request
---

> **Resolution 2026-05-22 (deferred to W3).** Verified absence: grep across `angular/src/app/appointments` for `Request Reschedule` / `Request Cancellation` / `requestReschedule` / `requestCancellation` returns zero matches -- buttons genuinely not present in any template. This is intentional per the existing parity audit (Wave 3 gates the change-request feature). The Supervisor-dashboard "Pending Change Requests" tile placeholder remains and will be wired when W3 ships. No action needed; the W3 workstream will produce its own design docs when scoped.

# OBS-12 — Reschedule + Cancellation UI status

Confirms the existing parity audit's "UI not built" assertion.

## Patient view of an Approved appointment (`/appointments/view/{id}` as SoftwareThree)
Visible action buttons on A00001 (Approved):
- Save
- Upload Documents
- Back
- Load Defense Attorney
- Add
- Upload
- Download (×2 — Patient packet + Doctor packet)

**Missing:** `Request Reschedule`, `Request Cancellation`. Confirmed via DOM enumeration of visible buttons.

## Supervisor dashboard
The "Pending Change Requests" tile DOES exist on the admin/Supervisor dashboard, with the label `(populated when W3 ships)`. So the dashboard tile is a placeholder waiting for the change-request feature to be implemented.

## OLD parity reference
Per existing parity audit (`docs/parity/wave-1-parity/`), the change-request flow (reschedule + cancellation) is gated on Wave 3 (W3). NEW correctly hides the actions until W3 ships.

## To do
None — this is current expected state, captured for future-session reference.

## Related
- Workstream W3 — change-request feature design + implementation (out of scope for the current testing pass).
