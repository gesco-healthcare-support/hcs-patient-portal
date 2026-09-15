# Change summary: a reschedule now produces two cases

For the Case Tracker team. Portal side, 2026-08-06. Full detail in
`case-tracker-api-contract.md` (revision block at the top, plus §A, §C, §E2 and §H).

## The short version

**You do not need to change anything for this to work.** The case key is `appointmentId` and you
upsert on it, so when the portal sends a new appointment id you open a new case -- which is exactly
the intended behaviour. Everything below is additive, and a receiver that ignores all of it stays
as correct as it is today.

What changes is what your staff will SEE, and there are four new fields worth reading if you want
the two cases joined up in your UI.

## What changed

Until now, finalizing a reschedule moved a single appointment: same case, new date. That left the
old date's billing outcome with nowhere to live, and our staff need to bill or close it separately
from the new one.

From this release, finalizing a reschedule does two things:

1. **The original appointment closes.** You receive it with `status` = `RescheduledNoBill` or
   `RescheduledLate`, and the matching `billingStatus` (`NO_BILL` / `LATE`). That is your signal to
   close or bill the old date.
2. **A replacement appointment is created** on the agreed date and pushed under a NEW
   `appointmentId` -- a second case. Its status is `Approved`; both parties had already consented to
   that date before it was booked, so there is nothing further to wait for.

## The four new fields

On the REPLACEMENT (the new case):

| Field                               | Meaning                                       |
| ----------------------------------- | --------------------------------------------- |
| `rescheduledFromAppointmentId`      | the appointment it replaced -- match on this  |
| `rescheduledFromConfirmationNumber` | that appointment's `A000xx`, for display only |

On the ORIGINAL (the closed case):

| Field                       | Meaning                                 |
| --------------------------- | --------------------------------------- |
| `supersededByAppointmentId` | the appointment that replaced it        |
| `supersededReason`          | why -- currently always `"RESCHEDULED"` |

Two things worth knowing about these:

- **They are NOT the same as `previousAppointmentId`.** That field means RE-EVALUATION and is
  unchanged. We kept them separate on purpose: a re-evaluated appointment HAPPENED and is being
  followed up, whereas a rescheduled one did NOT happen and was replaced. Reusing one field for both
  would make it impossible to tell a follow-up from a replacement.
- **`supersededReason` is an open value set.** Today it is only `RESCHEDULED`. A no-show flow we
  have planned will add `NO_SHOW` without another contract change, so please store it verbatim
  rather than treating it as a boolean.

## Two smaller consequences

- **A reschedule now costs two messages instead of one.** Both still wait for their packet sets to
  settle, so timing is unchanged; the volume cap (100/office/hour) is nowhere near threatened.
- **The same file can appear under two document ids.** The replacement's document rows are copies
  that share the original's `objectKey` -- the blob is shared, not duplicated. Your existing rule
  ("upsert by `id`; `objectKey` is never an identity") already handles this correctly. We mention it
  only so it does not look like a fault if anyone audits storage. Our retention guarantee covers the
  shared object, so neither case can end up pointing at a deleted blob.

## Two statements in the old contract are now false

Both are struck through in place rather than deleted, so anyone who read the old version can see
what moved:

1. §E2 said the portal "moves the SAME appointment in place ... it never creates a second one."
2. §E2's RESCHEDULE TRAP said a reschedule is signalled by a CHANGED DATE, not a status change.

The second one matters most: **if you key "case moved" off a changed appointment date, you will now
watch a case that never moves and miss the close entirely.** A reschedule is a status change now.

## What we would like from you

Nothing blocking. Read the four fields when convenient. If anything here conflicts with how your
intake handles a repeated patient or claim, tell us before we enable it for an office and we will
work it through.
