[Home](../../INDEX.md) > [Product Intent](../) > [Cross-cutting](./) > Appointment Lifecycle

# Appointment Lifecycle -- Intended Behavior

**Status:** draft -- Phase 2 T11, cross-cutting cluster
**Last updated:** 2026-04-27
**Primary stakeholders:** Practice admin (the office's review-queue actor), bookers (patient / applicant attorney / defense attorney / claim examiner), host admin + supervisor admin (for cancel / reschedule actions and T7 corrections)

> Cross-cutting intent for the appointment-status lifecycle in the Patient Portal. Lifts the full 13-state machine from `docs/business-domain/APPOINTMENT-LIFECYCLE.md` as ratified intent for the long-term business domain, adds the **14th state `AwaitingMoreInfo`** that the portal needs at MVP for the send-back-for-info action, and re-scopes the operational subset to the portal's actual responsibility (booking + approval + reschedule / cancel only -- post-approval states are downstream concerns). Documents the **two-enum design** (slot status + appointment status) and the synchronisation rules between them. Resolves research Q1 (status workflow rules). Every claim source-tagged.

## Purpose

The appointment-status lifecycle is the spine of the portal. It tells every actor where their case is, what action is allowed next, who is allowed to take it, and which notifications fire when a transition happens. T11 consolidates the lifecycle decisions that have accumulated across T2 (Appointments), T7 (post-submit lock), T8 (Q-M portal-scope narrowing), T9 (multi-tenancy + audit), and T10 (auth-and-roles), and makes the cross-cutting state machine the canonical source. [Source: Adrian-confirmed across the full T11 interview 2026-04-27]

## Personas and goals

Cross-reference `00-BUSINESS-CONTEXT.md` and `cross-cutting/auth-and-roles.md` for full persona definitions.

- **Practice admin (one role per tenant; T10).** The office-side decision actor. Reviews fresh requests in the queue; takes one of three actions per request (approve, reject, send-back-for-info). Initiates cancel and reschedule on already-approved appointments (provisional -- see escalations Item 1). [Source: Adrian-confirmed via T2 + T10]
- **Booker (patient, applicant attorney, defense attorney, claim examiner).** Submits the original request; responds to send-back-for-info during AwaitingMoreInfo; cannot directly cancel or reschedule at MVP (out-of-portal channels only). [Source: Adrian-confirmed via T2]
- **Host admin + supervisor admin (Gesco-side; T9 + T10).** Run the T7 universal post-submit "proper process" for form-data corrections after the appointment is locked. Have authority to cancel / reschedule when a practice admin cannot or will not. All actions audit-logged with tenant context (T9 Q-T9-5).
- **Downstream systems (Case Tracking, MRR AI -- post-MVP).** Receive the approved appointment via packet handoff; own the day-of-exam states (CheckedIn, CheckedOut, Billed, NoShow). The Patient Portal does NOT operate those transitions. [Source: Adrian-confirmed 2026-04-24 via T8 Q-M]

## Intended workflow

### The two enums (and why we keep them separate)

The portal has two enums whose transitions are correlated but represent different objects:

1. **Slot status** on `DoctorAvailability` -- 3 values:
   - `Available` -- nobody has requested this slot yet.
   - `Reserved` -- a booking request has been submitted on this slot and is awaiting office decision (Pending or AwaitingMoreInfo on the appointment side).
   - `Booked` -- an approved appointment occupies this slot.
2. **Appointment status** on `Appointment` -- 14 values (the existing 13 plus the new `AwaitingMoreInfo`).

The slot is the calendar object: it exists before any booking, drives the calendar UI, and prevents collisions on a single time / location / doctor. The appointment is the case-record object: it exists only when there is a booking request, and its lifecycle covers the request -> review -> approval / rejection -> (optional cancel / reschedule) arc. Collapsing the two would force the appointment enum to model "no appointment exists yet" (an awkward state) and force the slot enum to carry domain semantics like NoShow or Billed that it does not need. [Source: Adrian-confirmed 2026-04-27 in the T11 interview]

### The appointment-side state machine (MVP-scoped)

The portal at MVP transitions appointments through this subset of the 14-value enum:

```
[booker submits]
       |
       v
   Pending  -- (office: send-back) -->  AwaitingMoreInfo
       ^                                       |
       | <-- (booker: response) -- (auto)------+
       |
       +-- (office: approve) --> Approved
       |
       +-- (office: reject) ---> Rejected (terminal)


[admin-initiated cancel on Approved] (provisional, see escalations Item 1)
   Approved --> CancelledNoBill (terminal)
   Approved --> CancelledLate    (terminal)

[admin-initiated reschedule on Approved] (provisional)
   Approved --> RescheduledNoBill (terminal on the original record;
                                   a new Pending appointment is created
                                   on the new slot, parent-FK link to
                                   the original)
   Approved --> RescheduledLate   (same shape, late variant)
```

### Send-back-for-info (AwaitingMoreInfo)

When the office sends a request back asking for more information, the appointment moves Pending -> `AwaitingMoreInfo`. The send-back action carries **two pieces of content**, both supplied by the office: [Source: Adrian-confirmed 2026-04-27, T11]

- **Structured field flags** -- the office picks specific form fields they want the booker to revisit (e.g., "claim number missing", "employer address looks wrong"). Each flagged field appears highlighted on the booker's response screen.
- **Free-text note** -- a single freeform note from the office to the booker, written alongside the field flags. Visible on the booker's response screen.

The booker sees the AwaitingMoreInfo screen, edits the flagged fields (and optionally other fields, subject to the T7-related field-lock policy noted under Business Rules), and re-submits. **On re-submit, the appointment auto-transitions back to `Pending`**, re-entering the office's normal fresh-requests review queue. The office gets a notification AND a UI flag on the queue item indicating "response received from booker on previous send-back" -- so they can distinguish a returning request from a fresh one. [Source: Adrian-confirmed 2026-04-27, T11]

### Approved (terminal for the portal)

Once an appointment is `Approved` -- regardless of which path got there -- the portal hands the case off to downstream systems via the Packet (per `appointments.md` and the forthcoming T12 notifications doc). The portal is "done" with that appointment except for cancel / reschedule actions or T7 corrections.

The day-of-exam states (`CheckedIn`, `CheckedOut`, `Billed`, `NoShow`) exist in the enum for long-term intent (per the ratified `APPOINTMENT-LIFECYCLE.md`) but are NOT operated by the portal at MVP. Downstream products own those transitions. [Source: Adrian-confirmed 2026-04-24 via T8 Q-M; lifted from `docs/business-domain/APPOINTMENT-LIFECYCLE.md` for completeness]

### Reschedule (admin-initiated; provisional)

Per Adrian-confirmed in T11: a reschedule does NOT mutate the original appointment record's slot pointer in place. Instead:

1. The original `Approved` appointment moves to `RescheduledNoBill` or `RescheduledLate` -- a terminal state on that record.
2. The original slot (slot side: `Booked`) moves to `Available`.
3. A **new `Appointment` record is created** at the new slot, starting at `Pending` (or directly at `Approved` if the admin's reschedule action implicitly approves it -- see Outstanding Questions). The new slot moves Available -> `Booked` (or `Reserved` if the new appointment is created in `Pending`).
4. The new appointment carries a **parent-FK link** back to the original. New `RequestConfirmationNumber` is generated globally (per T9 Q-T9-1).

[Source: Adrian-confirmed 2026-04-27, T11]

This shape preserves the audit trail: the original record stays in place with its terminal state, the new record is its own row, and the parent-FK lets reporting traverse the chain when needed.

The "is cancel / reschedule even in MVP?" question itself remains open in `escalations/open-items.md` Item 1; this T11 doc treats reschedule as a working hypothesis if cancel / reschedule is in MVP.

### Booker-initiated cancel / reschedule (NOT in MVP)

The 13-state enum carries `RescheduleRequested` (12) and `CancellationRequested` (13) for booker-initiated request flows. These are **NOT used at MVP** -- bookers do not have a cancel or reschedule action in the portal. They contact the office or Gesco out-of-portal; the admin then performs the modification (admin-initiated path above). [Source: Adrian-confirmed 2026-04-23 via T2; reaffirmed 2026-04-27]

These two states remain in the enum as future-state placeholders for when booker-initiated modifications ship.

## Business rules and invariants

### State machine

- **Appointment-status enum has 14 values at MVP intent**: the 13 in code + `AwaitingMoreInfo`. The 13-value enum in `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/AppointmentStatusType.cs` is incomplete relative to MVP intent. Adding the 14th value is a code build item. [Source: Adrian-confirmed 2026-04-22 via T2; reaffirmed 2026-04-27 via T11]
- **Slot-status enum has 3 values at intent**: `Available`, `Reserved`, `Booked`. The slot side does not gain a new value. [Source: Adrian-confirmed 2026-04-22 via T3 Q17; reaffirmed 2026-04-27 via T11 two-enum confirmation]
- **No `Reserved` on the appointment enum.** Reserved is exclusively a slot-side concept. The appointment side already covers "request submitted, awaiting decision" via Pending and AwaitingMoreInfo. [Source: Adrian-confirmed 2026-04-27 via T11]

### Slot-appointment synchronisation

| Appointment transition | Slot transition |
| --- | --- |
| (none) -> Pending (booker submits) | Available -> Reserved |
| Pending -> AwaitingMoreInfo (office send-back) | (no change -- slot stays Reserved) |
| AwaitingMoreInfo -> Pending (booker responds; auto) | (no change -- slot stays Reserved) |
| Pending -> Approved | Reserved -> Booked |
| Pending -> Rejected | Reserved -> Available |
| AwaitingMoreInfo expires (no booker response within window) | Reserved -> Available + appointment terminal -- exact terminal state pending; see Outstanding Questions |
| Approved -> CancelledNoBill / CancelledLate (admin-initiated) | Booked -> Available |
| Approved -> RescheduledNoBill / RescheduledLate (original record) | Original slot Booked -> Available; new slot Available -> Reserved or Booked depending on whether new appointment starts at Pending or Approved |

[Source: Adrian-confirmed 2026-04-22 via T3 Q17 + 2026-04-27 via T11; subject to the AwaitingMoreInfo-expiry detail in Outstanding Questions]

### Authority per transition

Cross-reference `cross-cutting/auth-and-roles.md` for the role catalogue. At MVP:

| Transition | Authorized actors |
| --- | --- |
| Pending -> Approved | Practice admin (within tenant); host admin (any tenant); supervisor admin (within portfolio) |
| Pending -> Rejected | Practice admin; host admin; supervisor admin |
| Pending -> AwaitingMoreInfo | Practice admin (the send-back action; primary actor); host admin; supervisor admin |
| AwaitingMoreInfo -> Pending | AUTO on booker re-submit -- no human admin action |
| Approved -> Cancelled* | Practice admin OR host admin OR supervisor admin (provisional per escalations Item 1) |
| Approved -> Rescheduled* (original record terminal) | Same actors as Cancelled* |
| Pending -> Cancelled* | Same actors |

[Source: Adrian-confirmed 2026-04-22 via T2 (the three-action review decision is role-gated: only the dedicated decision role + the doctor can take it -- but at MVP "decision role" = Practice Admin per T10 one-role-per-practice ruling); 2026-04-27 via T11]

### Universal post-submit lock (T7) and the AwaitingMoreInfo exception

T7 established that form-captured data on a submitted appointment locks at request-submit and post-submit changes require the Gesco-side proper-process path. AwaitingMoreInfo is the **explicit exception**: by sending the request back, the office is asking the booker to edit specific fields. The booker can edit those fields and re-submit without going through the Gesco-side admin path.

The exact field-lock scope during AwaitingMoreInfo is: **at minimum, the office's structured-flagged fields are unlocked for the booker; whether non-flagged fields are also editable is a follow-up clarification** (see Outstanding Questions). The strictest interpretation is that only flagged fields are unlocked; the broadest is that any field is editable. [Source: T7 + T2 + T11; tension flagged]

After Approved, the T7 lock is unconditional -- no booker-side edit path exists; only the host or supervisor admin via proper-process. [Source: Adrian-confirmed via T7 + T9]

### Confirmation numbers

- Each appointment record gets a **globally unique** `RequestConfirmationNumber` (per T9 Q-T9-1). When a reschedule produces a new appointment record, it gets a NEW confirmation number; the original keeps its original number. [Source: Adrian-confirmed 2026-04-24 via T9]

### All-parties notifications on every transition

- Every state transition fires the all-parties notification (per `appointments.md` and the forthcoming T12 notifications cross-cutting). The recipient list per transition is per-event but always includes the patient, applicant attorney, defense attorney, insurance carrier / TPA contact, claim examiner / adjuster, the doctor's office, and -- case-by-case -- the employer (per T7). [Source: Adrian-confirmed via T2 + T7]

### Audit logging

- Every state transition produces an audit-log entry per the standard ABP audit logging.
- Every **host-admin or supervisor-admin** action that drives a transition (e.g., T7 corrections, cross-tenant cancel) carries the additional `tenant context` audit field per T9 Q-T9-5.

## Integration points

- **T2 Appointments** -- this lifecycle file is the canonical state-machine source; `appointments.md` references the lifecycle for its workflow sections rather than re-defining transitions.
- **T3 DoctorAvailabilities** -- the slot-side enum + the synchronisation rules above. T3 already documented Reserved as the slot-side held-for-pending-appointment state (Q17 resolved 2026-04-24).
- **T7 AppointmentEmployerDetails** -- the universal post-submit lock; this file documents the AwaitingMoreInfo exception to that lock.
- **T9 Multi-tenancy** -- audit-logging requirement for host / supervisor admin actions; tenant scope of all transitions.
- **T10 Auth-and-roles** -- the authorized-actor table per transition (above) is the canonical mapping of role -> transition.
- **T12 Notifications (forthcoming)** -- per-event recipient list and per-event email-format requirement.
- **`docs/business-domain/APPOINTMENT-LIFECYCLE.md`** -- ratified INTENT-BEARING source for the broader 13-state machine. This T11 doc lifts that wholesale and adds (a) the 14th state `AwaitingMoreInfo`, (b) the portal-scope re-scoping, and (c) the slot-appointment synchronisation table.
- **`src/HealthcareSupport.CaseEvaluation.Domain.Shared/Enums/AppointmentStatusType.cs`** -- code source for the 13-value enum (incomplete relative to MVP intent; the 14th value is a build item).
- **`docs/issues/INCOMPLETE-FEATURES.md`** -- FEAT-01 captures the missing state-machine implementation (no enforcement of valid transitions, no role-based permission checks, no transition endpoints, no UI). T11 supplies the intent that FEAT-01 must build to.

## Edge cases and error behaviors

- **Booker abandons an AwaitingMoreInfo request.** Outstanding -- see Outstanding Questions for the timeout / auto-expire behaviour. The slot's `Reserved` status cannot stay forever; somewhere there is a deadline beyond which the slot returns to Available and the appointment moves to a terminal state (likely `Rejected` or a new "expired" terminal -- not yet decided).
- **Booker tries to edit non-flagged fields during AwaitingMoreInfo.** Subject to the field-lock-during-AwaitingMoreInfo follow-up; default-strictest interpretation is "only the flagged fields are editable".
- **Office sends a request back twice.** Pending -> AwaitingMoreInfo is allowed any number of times. Each send-back captures its own structured flags + free-text note (the AwaitingMoreInfo state should retain the office's most recent message; per-send-back history is a Phase 3 detail).
- **Reschedule of an Awaiting / Pending appointment.** Out of MVP shape per Adrian: reschedule is admin-initiated on Approved appointments only. Whether the admin can also reschedule a still-Pending appointment is [UNKNOWN -- queued for Adrian]; the practical workaround is "reject the Pending and ask the booker to re-book".
- **Booker re-submits an AwaitingMoreInfo response with the SAME data (no actual edits).** Allowed; status auto-transitions to Pending; office sees the response flag. Office can immediately send-back-again with sharper flags or escalate.
- **Concurrent send-back + booker submit.** Edge race condition. Last-write-wins; the booker's already-submitted form would conflict with the office's send-back. Implementation detail; resolution is technical.
- **All-parties notification fails on a transition.** Build-item concern, not a state-machine concern at intent level. The appointment's status still transitions; the notification system retries / surfaces a failure to the office. Falls under T12 notifications.

## Success criteria

- Every appointment record carries a status drawn from the 14-value enum (or the 13-value enum until the build adds AwaitingMoreInfo).
- Every state transition is permitted by the role-authority table above; unauthorized transitions are rejected at the application service layer.
- The slot-status enum auto-updates per the synchronisation table on every appointment transition.
- The booker on a send-back-for-info screen sees both the office's flagged fields AND the office's free-text note; can edit the flagged fields; can re-submit; the system auto-transitions back to Pending and shows a "response received" flag on the office's queue item.
- Reschedule produces a new appointment record with a parent-FK link to the original; original moves to RescheduledNoBill or RescheduledLate; new record gets a new global confirmation number.
- Every state transition produces an audit-log entry; host / supervisor admin transitions also carry the tenant-context audit field.
- The 13-state diagram in `docs/business-domain/APPOINTMENT-LIFECYCLE.md` is the long-term intent; the portal at MVP only transitions the subset documented above; downstream products operate the post-Approved states.

## Known discrepancies with implementation

- `[observed, not authoritative]` `AppointmentStatusType` enum has only 13 values; the 14th (`AwaitingMoreInfo`) does not exist in code. Adding it is an MVP-blocking build item (per FEAT-01 + T2 / T11 intent).
- `[observed, not authoritative]` No state-machine enforcement exists. Per FEAT-01, the code accepts any of the 13 status values at creation with no validation, and status is then frozen (BUG-02). Intent: server-side per-role per-transition enforcement, audit-trailed.
- `[observed, not authoritative]` No transition endpoints exist (no `POST /api/app/appointments/{id}/status` or per-action endpoints for approve / reject / send-back / cancel / reschedule). Build item.
- `[observed, not authoritative]` Send-back-for-info has no schema in code. The structured field-flags + free-text-note model from T11 needs new entities / DTOs.
- `[observed, not authoritative]` Reschedule-as-new-record (with parent-FK) is not in code. Current reschedule semantics, if any, would mutate the original record.
- `[observed, not authoritative]` AwaitingMoreInfo timeout / auto-expire is not in code; expects a background job + timer.
- `[observed, not authoritative]` All-parties notification on transition is not in code. T12 covers the notification layer.
- `[observed, not authoritative]` Audit-log review UI is absent (per T10 Known Discrepancies).
- `[observed, not authoritative]` `docs/business-domain/APPOINTMENT-LIFECYCLE.md` was authored before the AwaitingMoreInfo state was confirmed; it shows the 13-state machine. T11 is the canonical source for the 14-state portal-scoped view going forward; the business-domain file remains accurate for the broader system.
- `[observed, not authoritative]` `docs/issues/INCOMPLETE-FEATURES.md` FEAT-01 calls out "no state machine, no role-based permissions on transitions, no UI". T11 provides the intent FEAT-01 should build to.

## Outstanding questions

- **AwaitingMoreInfo timeout / auto-expire.** How long does an AwaitingMoreInfo request live before auto-expiry? What terminal state does it land on (Rejected? a new "expired"? something else)? [UNKNOWN -- queued for Adrian]
- **Field-lock scope during AwaitingMoreInfo.** Are only the office's flagged fields editable by the booker, or any field on the form? Default-strictest in this doc is "only flagged"; confirm. [UNKNOWN -- queued for Adrian]
- **Reschedule of a Pending appointment.** Admin-initiated reschedule is Adrian-confirmed for Approved appointments. Can the admin also reschedule a still-Pending appointment, or must they reject + booker re-books? [UNKNOWN -- queued for Adrian]
- **New record after reschedule -- starts Pending or Approved.** When the admin reschedules an Approved appointment, the new record is created. Does it start at `Pending` (re-enters review queue at the new slot, slot moves to Reserved) or at `Approved` (the admin's reschedule action implicitly approves the new slot, slot moves to Booked immediately)? [UNKNOWN -- queued for Adrian]
- **Send-back history per appointment.** Does the appointment record retain a history of all send-back rounds (each with its own structured flags + free-text), or only the most recent? [UNKNOWN -- queued for Adrian; Phase 3 candidate]
- **Cancel / reschedule MVP existence itself.** Still tracked in `escalations/open-items.md` Item 1; this T11 file is provisional on cancel / reschedule MVP shape until that is resolved.
- **Research Q1 (status workflow rules).** RESOLVED via T11 -- the rule set is documented above (transitions, authorities, slot-sync, audit). Manager confirmation may still narrow the role-authority rows; flagged for review.
