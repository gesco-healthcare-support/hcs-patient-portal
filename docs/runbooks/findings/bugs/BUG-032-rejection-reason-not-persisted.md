---
id: BUG-032
title: Rejection reason accepted by /api/app/appointments/{id}/reject (200) but never persisted to AppAppointments.RejectionNotes
severity: medium
status: fixed
fixed: 2026-05-22 (live-verified both /reject routes; symmetric companion-field write added in AppointmentManager)
found: 2026-05-21 hardening HRD-P5.3
flow: appointment-rejection
component: src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs (ApplyTransitionAsync)
---

# BUG-032 - Rejection reason not persisted

## Symptom

HRD-P5.3 called:
```
POST http://falkinstein.localhost:44327/api/app/appointments/22FA225C-D14E-3EA8-11AE-3A215DE39F48/reject
Body: { "reason": "Invalid claim number" }
Auth: Bearer <clistaff1's token>
```

Server returned 200 OK with the AppointmentDto body. AppointmentStatus flipped to 3 (Rejected). But the SQL row:

```sql
SELECT RequestConfirmationNumber, AppointmentStatus, AppointmentApproveDate, RejectionNotes
FROM AppAppointments
WHERE Id = '22FA225C-D14E-3EA8-11AE-3A215DE39F48'
-- A00003 | 3 | NULL | NULL
```

`RejectionNotes` is **NULL**. The 21-char reason "Invalid claim number" -- accepted by the API validator (>= 5 chars per [[BUG-024]]'s fix) -- was silently dropped before persistence.

Cross-check: the response DTO contains `"internalUserComments": null` and there is no field in the returned DTO that surfaces the reason back to the caller, so the loss is not even observable to the client.

## Hypothesis

1. **DTO -> entity mapper hole.** `RejectAppointmentInput.Reason` is not mapped to `AppAppointment.RejectionNotes` (Riok.Mapperly mapper omission). The `AppointmentManager.Reject(input)` method receives the input but the `Reason` value flows into no setter. Fix: add the mapping OR a direct assignment in `AppointmentManager.Reject`.

2. **Wrong column being written.** The handler writes to a different column (e.g., `InternalUserComments`). The DTO response also shows `internalUserComments: null` so this is unlikely UNLESS the write was inside a transaction that didn't commit. But the status transition DID commit (status=3), so the same UoW should have flushed the comment.

3. **Validation strips the value.** A pre-process step (e.g., Sanitizer / HtmlScrubber) returns `null` for the input and the entity gets that null. Defensive coding meant to protect against XSS over-sanitizes a plain ASCII reason.

Most likely (1): pure mapping hole. The [[BUG-024]] fix added the validation attribute to the DTO but the entity assignment step is in a separate place that the fix missed.

## Reproduction

1. Pick any Pending appointment id (e.g., from HRD-P3.3).
2. Get a clinic-staff or supervisor bearer token from localStorage after login.
3. ```
   curl -X POST http://falkinstein.localhost:44327/api/app/appointments/<id>/reject \
     -H "Content-Type: application/json" \
     -H "Authorization: Bearer <token>" \
     -d '{"reason": "Invalid claim number"}'
   ```
   -> 200 OK.
4. SQL:
   ```sql
   SELECT AppointmentStatus, RejectionNotes
   FROM AppAppointments WHERE Id = '<id>'
   ```
   -> status=3, RejectionNotes=NULL.

## Recommended fix

Step 1: Locate the rejection handler:
```bash
grep -rn "RejectAsync\|RejectAppointment\|RejectInput" src/HealthcareSupport.CaseEvaluation.Application/
grep -rn "RejectionNotes" src/
```

Step 2: In the handler, confirm assignment exists:
```csharp
appointment.RejectionNotes = input.Reason;
appointment.RejectedById = currentUser.Id;
```

Step 3: If the assignment is missing -- add it. If present but a Mapperly partial method is overriding it, fix the Mapperly definition.

Step 4: Add an integration test under `test/HealthcareSupport.CaseEvaluation.Application.Tests/` that:
1. POSTs reject with a known reason.
2. Asserts `appointment.RejectionNotes == reason` in DB.

## Functional impact

- Audit trail is broken. When a manager later asks "Why was A00003 rejected?", no answer exists in the DB. Email templates that include the rejection reason will render with an empty placeholder.
- HIPAA/compliance: rejection reasons are part of the workflow audit trail; missing data is a documentation gap that may flag in an audit.
- Future packet-generation for rejected appointments (if any) cannot include the reason.

## Related

- [[BUG-024]] (fixed 2026-05-19) -- added `[Required, StringLength(min 5, max 500)]` to `Reason`. That fix is intact (this run's 21-char reason was accepted). The gap is downstream: validation passes but persistence does not.
- [[BUG-030]] -- another "field that should be set is NULL" pattern (auto-approve missing date). Both findings suggest the entity-assignment layer has multiple holes.

## Corrected root cause (2026-05-22)

The hypothesis above ("DTO -> entity mapper hole") was directionally
right but misnamed the layer. There is no Mapperly mapper between
`RejectAppointmentInput.Reason` and `Appointment.RejectionNotes`; the
two AppService surfaces both pass the reason through as a plain
parameter to `AppointmentManager.RejectAsync`. The actual cause is
**asymmetric domain-service behavior**:

`AppointmentManager.ApplyTransitionAsync` (the shared transition
core) DID write the Approve trigger's companion field
(`AppointmentApproveDate = DateTime.UtcNow`) but did NOT write the
Reject trigger's companion fields (`RejectionNotes` and
`RejectedById`). The `reason` parameter survived only on the emitted
`AppointmentStatusChangedEto`; nothing in the persistence path picked
it up. The Approve branch existed because of an explicit
`if (trigger == AppointmentTransitionTrigger.Approve)` block; the
Reject branch was missing.

The bug was route-specific under the prior code:
- `/api/app/appointments/{id}/reject` (Session A surface) -- BROKEN.
  AppService only calls the manager; manager drops the reason.
- `/api/app/appointment-approvals/{id}/reject` (Phase 12 surface) --
  WORKED. AppService loads the entity, mutates
  `RejectionNotes`/`RejectedById` directly, calls `UpdateAsync`
  with `autoSave: true`, then calls the manager. EF Core's identity
  map propagated the mutation through the manager's `GetAsync`
  re-fetch.

The Angular SPA migrated to the Phase 12 route on 2026-05-05, so end
users via the UI were unaffected. Only direct API callers of the
Session A route lost data -- which is how the hardening test (HRD-P5.3)
surfaced it.

## Fix verified (2026-05-22)

Fix lives at
`src/HealthcareSupport.CaseEvaluation.Domain/Appointments/AppointmentManager.cs`,
inside `ApplyTransitionAsync`, immediately after the existing Approve
branch:

```csharp
if (trigger == AppointmentTransitionTrigger.Approve)
{
    appointment.AppointmentApproveDate = DateTime.UtcNow;
}
else if (trigger == AppointmentTransitionTrigger.Reject)
{
    appointment.RejectionNotes = reason;
    appointment.RejectedById = actingUserId;
}
```

Two assignments. The Phase 12 surface's pre-call mutation in
`AppointmentsAppService.Approval.cs:156-158` is now redundant but
left in place as defense in depth.

### Live verification matrix

Two appointments seeded directly via SQL (TenantId = Falkinstein,
status = Pending), then rejected over the two distinct routes from a
single `clistaff1` bearer token obtained via the full OIDC PKCE flow.

| Test | Route | Body | Expected | Actual |
|---|---|---|---|---|
| A | `POST /api/app/appointments/{id}/reject` (Session A -- previously broken) | `{"reason":"Invalid claim number BUG032 Session A"}` | 200 + row has `RejectionNotes` set + `RejectedById` set | **200**; SQL shows `RejectionNotes = "Invalid claim number BUG032 Session A"`, `RejectedById = clistaff1's user id` |
| B | `POST /api/app/appointment-approvals/{id}/reject` (Phase 12 -- previously working) | `{"reason":"Invalid claim number BUG032 Phase 12"}` | 200 + row has `RejectionNotes` set (no regression) | **200**; SQL shows `RejectionNotes = "Invalid claim number BUG032 Phase 12"`, `RejectedById = clistaff1's user id` |

### Unit-test suite

Application.Tests: 538/538 pass. Domain.Tests: 13/13 pass (+ 4
pre-existing Skipped). No regressions from the existing test surface.

### Out of scope

- Removing the redundant pre-call mutation in
  `AppointmentsAppService.Approval.cs:156-158`. It's defense in depth
  and the planned "Sync 3 cleanup PR" (per the IAppointmentApprovalAppService
  XML doc) is the right venue.
- Tightening the public setters on `Appointment.AppointmentStatus`
  (the feature CLAUDE.md notes "no domain-level state-machine
  guard"). Broader scope.
- Symmetric write for `RequestReschedule` -- reschedule reason lives
  on the separate `AppointmentChangeRequest` aggregate, not on
  `Appointment`. No companion-field needed on `Appointment` for that
  trigger.
