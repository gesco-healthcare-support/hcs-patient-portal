# Case Tracker integration -- verified findings

Facts established by reading the code, not by inference, during the 2026-07-30 to 2026-08-05
correspondence with Levon (Case Tracker). Recorded because several were re-derived more than once,
and twice a wrong answer nearly reached him.

RE-VERIFY BEFORE ASSERTING. Code moves. Each claim carries where to check it.

## Status model

**Four writers to `Appointment.AppointmentStatus`, and only four.** Verified with
`grep -rE "\.AppointmentStatus\s*=[^=]" src/`:

| Writer                                                | Sets                                             |
| ----------------------------------------------------- | ------------------------------------------------ |
| `AppointmentChangeRequestsAppService.Approval.cs:95`  | the chosen cancellation outcome                  |
| `AppointmentChangeRequestsAppService.Approval.cs:327` | `Approved` (reschedule finalize / reject revert) |
| `AppointmentManager.cs:384`                           | the state machine's single sink                  |
| `JointDeclarationAutoCancelJob.cs:159`                | `CancelledNoBill` (AME JDF auto-cancel)          |

WARNING: an earlier pass reported only THREE writers. That was a `head -30` truncation artefact, not
the data. Do not trust a truncated grep when the claim is "all X".

**Reachable statuses.** The manager exposes only `Approve`, `Reject`, `RequestReschedule`,
`ResubmitInfo`, `SendBack`. Combined with the two direct writers, an appointment can only ever hold:
`Pending`, `Approved`, `Rejected`, `InfoRequested`, `RescheduleRequested`, `CancellationRequested`,
`CancelledNoBill`, `CancelledLate`.

**Unreachable enum values:** `NoShow`, `CheckedIn`, `CheckedOut`, `Billed`, `RescheduledNoBill`,
`RescheduledLate`.

These are NOT dead code to delete. `BuildMachine` (`AppointmentManager.cs:398-417`) already models the
entire day-of-exam chain with named triggers:

    Approved --MarkNoShow--> NoShow
    Approved --CheckIn-----> CheckedIn --CheckOut--> CheckedOut --Bill--> Billed

Every transition is permitted; only the entry point was never built. The day-of-exam flow is the Case
Tracker's side, which is why nothing calls them. `RescheduledNoBill` / `RescheduledLate` become
reachable when epic Phase 4d/4e lands.

**Statuses that reach the Case Tracker: five today.** `CaseTrackerPublishPolicy.IsPublished` excludes
only `Pending`, `Rejected`, `InfoRequested`. So `Approved`, `RescheduleRequested`,
`CancellationRequested`, `CancelledNoBill`, `CancelledLate`. Seven once the reschedule work lands.

Note the in-flight ones DO reach them -- a move from `Approved` to `RescheduleRequested` is a change
to a published appointment and is pushed.

**Cancellation outcomes are hard-validated** to exactly `CancelledNoBill` or `CancelledLate`
(`ChangeRequestApprovalValidator.cs:53-61`); anything else throws.

**Reschedule today is in-place** (`Approval.cs:229-234`): same row, only slot and date change, status
returns to `Approved`. The NoBill/Late outcome is recorded on the CHANGE-REQUEST row, not the
appointment status -- which is why the `Rescheduled*` values are never set.

**A rejected change request reverts to `Approved` and saves** (`RejectRescheduleAsync`, comment reads
"Revert parent appointment to Approved"). There is no withdrawn state -- `RequestStatusType` is
`Pending` / `Accepted` / `Rejected` only. So a request can evaporate and a terminal outcome is NOT
guaranteed to follow one.

**The AME auto-cancel is distinguishable** but not by a boolean: the job writes a fixed reason
constant and a null acting user. Levon has asked for an explicit flag instead.

## Payload

**There is exactly ONE payload builder and no slim path.** `IIntakePayloadBuilder.BuildAsync` has two
callers: `CaseTrackerIntakeQueue` (push) and `CaseTrackerReconcileService` (reconcile GET). Both
produce the complete envelope. Every push is a full snapshot, so an absent field means genuinely
absent, never "unchanged".

**`updatedAt` = `LastModificationTime ?? CreationTime`** (`IntakePayloadBuilder.cs:97-98`). A status
change writes the appointment, so it bumps.

This is guaranteed by construction, not convention: the outbox idempotency key is versioned by
`updatedAt`. If a change failed to bump the stamp, the enqueue would collapse onto the existing row
and NO push would be sent. Observed live -- falkinstein A00004 produced two rows precisely because
their `updatedAt` differed by one second.

**Addresses.** `IntakePatientSection` has NO address fields at all -- name, email, DOB, phones, and
the hashed `samePersonGroupKey`. `IntakeAttorneySection`, `IntakeClaimExaminerSection` and
`IntakeInsuranceSection` DO each carry a full street/city/state/zip. So the patient is the only
address gap, and it blocks the Case Tracker's proof-of-service document.

**Shipped in Phase 2 (PR #414, main `baa1fee6`):**

- `cancellationReason` -- nullable, user-authored free text or the auto-cancel constant. Untrusted
  display data; never log it.
- `billingStatus` -- `NO_BILL` / `LATE` / `NONE` via `BillingStatusWire`, always present,
  non-nullable.

IMPORTANT: `status` and `billingStatus` are COMPLEMENTARY, not duplicates. `status` is authoritative
for lifecycle; `billingStatus` is the billing intent, surfaced separately so a rename of an enum
member cannot change what their billing team reads. An earlier draft "corrected" this into saying
there was only the status -- that correction was itself the error and was caught before sending.

**`evaluationKind`** is `"EVAL"` / `"RE_EVAL"`, explicit wire constants rather than enum names
(`EvaluationKindWire.cs`), so an internal rename cannot change the wire.

**`data.doctor.id`** added 2026-07-31 (PR #413, main `d658382b`). Nullable GUID, the portal's own row
key. One Doctor row per office (`IX_AppEntity_Doctors_TenantId_Unique`), so the id alone is
sufficient, but the same human at two offices is two rows with two ids -- same caveat as patients.

**`documents[]` is uploads UNION generated packets**, filtered to fetchable
(`DocumentListResolver.cs:49`): not `Pending`, real blob present. Rejected documents ARE included
with their status, deliberately.

## Things the portal does NOT have

Checked because they were assumed to exist:

- **No business-day, notice-period or holiday concept anywhere.** The only date rule is
  `LeadTimeMinutes` (booking lead time), which is unrelated. The Case Tracker's 6-business-day
  late/timely determination is entirely theirs; there is nothing on our side to align with and no
  holiday calendar to copy.
- **No records-available / records-reviewed flag** on `Appointment`.
- **No reschedule sequence or count.**
- **No previous-date field** on the payload.
- **No actor on a cancellation** -- who cancelled is held internally but not transmitted.

## Delivery mechanics

**`DrainBatchSize = 50` bounds ONE invocation, not throughput.** Every enqueue schedules its own
drain, so N rows become N drains on parallel workers. The volume cap (PR #413) is the actual ceiling:
100 per office per rolling hour, counted from `SentAt` in the ledger so there is no new state and no
trip flag -- it releases itself as the window slides.

**The intake waits for packets to settle** (`IntakeSettlePolicy`, PR #404). Settled = every kind
rendered, OR nothing moved for 30 minutes (`PacketSetPolicy.SettleAfterMinutes`, ONE shared
constant). This reversed the original push-immediately design after the first live approval produced
two intakes ten seconds apart.

Gating is applied at every AUTOMATIC enqueue site, not one trigger. `AppointmentChangedHandler` fires
on the approval itself, so removing only the approval trigger would NOT have worked -- the two
enqueues had merely collapsed onto one row via a shared `updatedAt`.

**The reconcile 404 is deliberately ambiguous** between unknown appointment, unknown office and
switched-off office (`CaseTrackerReconcileService.cs:48-51`) so a holder of a leaked token cannot
enumerate. Do not weaken it. The Case Tracker's document-pruning risk is solved by the rule "prune
only on a 200", which they have accepted.

## Environment

- API container runs **UTC** (`TZ` unset, container local == UTC), so `IClock.Now` returning
  `Unspecified` genuinely is UTC and `IntegrationTimestamp.ToIsoUtc` is correct to treat it so.
- `CaseTracker:IntegrationToken` is **EMPTY in production**, failing closed. The reconcile GET
  therefore rejects everything today.
- MinIO has **no published port** and there is **no MinIO route** in
  `docker/nginx-proxy/default.conf.template`. Only `case-evaluation-documents` exists; zero non-root
  users; only MinIO's five built-in policies.
- `secrets/env.prod` is docker env-file format and is **NOT shell-sourceable** -- the SMTP password
  contains shell metacharacters. Read individual keys with `grep`/`cut`.
- ABP caches permission grants in **Redis**, including negative results, and that survives an API
  restart. Clearing specific keys is needed after a seeder grant change. Busybox `xargs` has no `-d`.
