# AppointmentChangeRequests -- cancel / reschedule request lifecycle

## What lives here

| File | Purpose |
|---|---|
| `AppointmentChangeRequest.cs` | Aggregate root: `ChangeRequestType`, `RequestStatusType`, slot FKs, supervisor fields |
| `AppointmentChangeRequestManager.cs` | Domain service: submit + (Phase 17) approve / reject flows |
| `AppointmentChangeRequestDocument.cs` | Supporting document attached to a change request |
| `CancellationRequestValidators.cs` | Static guards: status + cancel-time window |
| `RescheduleRequestValidators.cs` | Static guards: status + slot availability |
| `IAppointmentChangeRequestRepository.cs` | Repository contract |

## Request status lifecycle

`RequestStatusType` has three values: `Pending` (set on submit, the only state a
user ever sees), `Accepted`, `Rejected` (both set by the supervisor approval flow,
Phase 17).

On **cancel submit**: parent appointment STAYS `Approved` while the request is Pending.
The supervisor's approve sets a `CancellationOutcome` (`CancelledNoBill` or `CancelledLate`)
onto the parent via the state machine.

On **reschedule submit**: the user-picked slot transitions `Available -> Reserved`
immediately (interim hold). The parent appointment transitions `Approved -> RescheduleRequested`
via `AppointmentManager.RequestRescheduleAsync` (state machine -- do NOT set
`AppointmentStatus` directly). On supervisor reject, the held slot is released.

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

### Two AppServices, single feature folder

The Application.Contracts layer splits this into two interfaces:
- `IAppointmentChangeRequestsAppService` -- external-user submit (cancel, reschedule).
- `IAppointmentChangeRequestsApprovalAppService` -- supervisor approve / reject.

Both must carry `[RemoteService(IsEnabled = false)]` and have paired manual controllers.

### Entity ctor enforces type-specific required fields

`AppointmentChangeRequest(...)` calls `Check.NotNullOrWhiteSpace` on `CancellationReason`
when type is Cancel, and on `ReScheduleReason` + non-null `NewDoctorAvailabilityId` when
type is Reschedule. Pass the wrong combination and the ctor throws before the row is inserted.

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
