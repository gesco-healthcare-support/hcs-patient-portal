---
id: BUG-008
title: PUT /me concurrency stamp goes stale on submit retry
severity: medium
status: needs-rehydration
found: 2026-05-13
flow: patient-profile-update
component: angular/src/app/patients/me + Application/Patients/PatientAppService.UpdateMeAsync
---

# BUG-008 — PUT /me concurrency stamp on submit retry

## Severity
medium

## Status
**Needs rehydration.** Documented in earlier session compact summary; full repro/evidence to be added when re-encountered.

## What's known from earlier session
- Patient profile PUT to `/api/app/patients/me` returns **409 Conflict** on the second submit when the first submit had already succeeded.
- ConcurrencyStamp from the first response isn't being threaded back into the form's hidden field, so retry payload carries the original (stale) stamp.
- Workaround: reload the page (loses form state).

## To do
- Re-trigger the flow against canonical-port stack.
- Capture network request bodies + ConcurrencyStamp values across both submits.
- Verify whether the bug is client-side (form-state stale) or server-side (response not returning the new stamp).

## Suspected root cause
The `UpdateMeAsync` response DTO may not include the new ConcurrencyStamp, OR the SPA's form-state management doesn't merge the response stamp back into the form before allowing resubmit.

## Workaround
Reload the page after a successful submit before any subsequent edit.
