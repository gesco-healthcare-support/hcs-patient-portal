# AppointmentChangeRequests -- cancel / reschedule request lifecycle

## What lives here

| File | Purpose |
|---|---|
| `AppointmentChangeRequest.cs` | Aggregate root: `ChangeRequestType`, `RequestStatusType`, slot FKs, supervisor fields |
| `AppointmentChangeRequestManager.cs` | Domain service: submit + (Phase 17) approve / reject flows |
| `AppointmentChangeRequestDocument.cs` | Supporting document attached to a change request |
| `CancellationRequestValidators.cs` | Static guards: status + cancel-time window |
| `RescheduleRequestValidators.cs` | Static guards: status + slot availability |
| `IAppointmentChangeRequestRepository.cs` | Repository contract; the same file also declares `IAppointmentChangeRequestDocumentRepository` for supporting documents |
| `ChangeRequestConsentManager.cs` | Two-sided consent: issues each side's single-use token (only its SHA-256 hash is stored), validates it, records the decision. Also declares `ChangeRequestConsentMatch`: a raw token resolved to its change request, the round that owns it (null for a cancellation) and the side |
| `ChangeRequestConsentRound.cs` | One consent round per staff-proposed date: per-side token hash, expiry and decision. Rounds are rows so the record of who declined which date survives |
| `IChangeRequestConsentRoundRepository.cs` | Repository contract for consent rounds |
| `RescheduleSplitPolicy.cs` | Pure policy for approving a reschedule: the NEW appointment's status (inherits the source's) and the trigger that closes the OLD one |
| `RescheduleInPlacePolicy.cs` | Superseded by `RescheduleSplitPolicy` (the July in-place design); no production caller, only its own unit tests |
| `Jobs/ChangeRequestConsentExpirySweepJob.cs` | Hourly sweep (phase 4c) that expires consent tokens nobody clicked; before it, expiry was only evaluated when a party followed the link |

## Request status lifecycle

`RequestStatusType` has three values: `Pending` (set on submit, the only state a
user ever sees), `Accepted`, `Rejected` (both set by the supervisor approval flow,
Phase 17).

On **cancel submit**: parent appointment STAYS `Approved` while the request is Pending.
The supervisor's approve sets a `CancellationOutcome` (`CancelledNoBill` or `CancelledLate`)
onto the parent via the state machine.

On **reschedule submit**: a slot is OPTIONAL (phase 4b, 2026-08-04) -- an external requester
may send only a reason. A proposed slot transitions `Available -> Reserved` immediately
(interim hold); with no proposal nothing is held. The parent appointment transitions
`Approved -> RescheduleRequested` via `AppointmentManager.RequestRescheduleAsync` (state
machine -- do NOT set `AppointmentStatus` directly); a staff-filed request on a Pending
appointment skips that step and stays Pending. On supervisor reject, a held slot is released.

**Consent (phase 4c).** Staff commit to a date, which opens a `ChangeRequestConsentRound`
that both sides must accept through their emailed tokens. **Approval** uses the split model
(`RescheduleSplitPolicy`): a NEW appointment is created in the new slot with a new
confirmation number, inheriting the source's status, and the old appointment is closed
through `AppointmentManager.CloseForRescheduleAsync`.

## Conventions

### Dual-ctor manager -- use full ctor for submit paths

`AppointmentChangeRequestManager` exposes a slim ctor (repository only, for `GetAsync`)
and a full ctor (all collaborators, required for submit flows). Calling
`SubmitCancellationAsync` or `SubmitRescheduleAsync` with the slim ctor throws
`InvalidOperationException`. Resolve via DI to guarantee the full ctor is injected.

### SystemParameterNotSeeded is a seed gap, not a validation error

`SubmitCancellationAsync` reads `SystemParameter.AppointmentCancelTime` per tenant
(via `ISystemParameterRepository.GetCurrentTenantAsync`). If the row is missing, it throws
`BusinessException(SystemParameterNotSeeded)`. This is a deployment seed gap -- the
tenant's SystemParameters row was never seeded -- not a user-input problem. Do NOT catch
and rethrow as a validation error; surface it to the operator to seed the row.

### Lead-time + max-time gates belong in the Application layer

`SubmitRescheduleAsync` does NOT re-run the booking policy gates (lead-time, per-type
max-time). Those run upstream via `BookingPolicyValidator` in the Application layer,
matching OLD parity. The domain only guards slot availability and source-appointment status.

### Three AppServices, single feature folder

- `IAppointmentChangeRequestsAppService` -- submit (cancel, reschedule).
- `IAppointmentChangeRequestsApprovalAppService` -- supervisor decisions, date confirmation
  and consent resends.
- `PublicChangeRequestConsentAppService` -- the emailed consent link, `[AllowAnonymous]`;
  the token is the credential.

All three carry `[RemoteService(IsEnabled = false)]` and have paired manual controllers.

### Entity ctor enforces type-specific required fields

`AppointmentChangeRequest(...)` calls `Check.NotNullOrWhiteSpace` on `CancellationReason`
when type is Cancel, and on `ReScheduleReason` when type is Reschedule.
`NewDoctorAvailabilityId` is optional (`Guid?`) since phase 4b. Pass the wrong combination
and the ctor throws before the row is inserted.

## Gotchas

- `AdminOverrideSlotId` is set only when the supervisor picks a different slot than the
  user during reschedule approval. When it equals `NewDoctorAvailabilityId`, it is redundant;
  only use `AdminOverrideSlotId` as the authoritative slot source in the approve path.
- `IsBeyondLimit` is always `false` on external-user submits. The field exists so a future
  admin-side path can set it to lift the per-type max-time gate on approval.
- Both submit methods publish `AppointmentChangeRequestSubmittedEto` for email fan-out AFTER
  the repository insert. Do not reorder; the handler expects the row to already exist.

## Related

- docs/business-domain/APPOINTMENT-LIFECYCLE.md
- docs/parity/_parity-flags.md
