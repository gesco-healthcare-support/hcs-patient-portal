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

> **Verification 2026-05-22: LIKELY FIXED by construction (confidence 75%). Needs live repro to close.**
>
> The broken pattern this bug described appears to be **structurally gone** in current code, though no targeted commit since 2026-05-13 cites BUG-008 by name. Evidence:
>
> - Server returns a fresh stamp: `PatientsAppService.UpdateMyProfileAsync` (`src/HealthcareSupport.CaseEvaluation.Application/Patients/PatientsAppService.cs:474-507`) calls `PatientManager.UpdateAsync`, which invokes `patient.SetConcurrencyStampIfNotNull(concurrencyStamp)` then `_patientRepository.UpdateAsync(patient)` (`src/.../Domain/Patients/PatientManager.cs:103-104`). The returned entity is `FullAuditedAggregateRoot<Guid>` (`Patient.cs:26`), and ABP's UoW regenerates `ConcurrencyStamp` on `SaveChanges`. Result is mapped to `PatientDto` (which itself implements `IHasConcurrencyStamp`, `PatientDto.cs:9,52`) and returned over the wire.
> - SPA rehydrates on success: `patient-profile.component.ts:162-168` merges the response via `this.selected.patient = { ...this.selected.patient, ...updated }`, so the next submit at line 156 (`concurrencyStamp: this.selected.patient.concurrencyStamp`) reads the new stamp.
> - No `If-Match`/ETag, but the pattern is the standard ABP "stamp in body" flow.
>
> **Residual concerns:**
> - Original repro (page reload required) was never re-tested against the canonical-port stack.
> - If the first submit fails with 409 *before* any successful response, `selected.patient` is never refreshed -- that subpath would still require a page reload. Worth confirming whether the SPA error handler rehydrates from the 409 response body.
> - No automated regression test exists for the two-submit case.
>
> **Action: live-verify, then close.** Repro: login as Patient, edit profile, submit, edit again without reloading, submit. If both 200, close BUG-008 with `status: fixed-by-redesign` and add an Application-tier test asserting the response `concurrencyStamp` differs from the input stamp. If the second submit still 409s, treat as OPEN -- the suspect would be the spread-merge ordering or proxy-DTO key casing rather than missing server-side stamp regeneration.

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
