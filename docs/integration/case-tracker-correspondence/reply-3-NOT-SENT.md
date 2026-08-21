DRAFT - NOT SENT. Reply to Levon's third email (the one proposing an inbound
outcome endpoint). Drafted 2026-07-31.

Decisions this reply reflects, all Adrian's, taken 2026-07-31:
  - Build the inbound endpoint, taking outcomes GENERALLY (not cancellation-only)
  - A no-show sets NoShow (making that value reachable) rather than a cancellation status
  - Conflict: domains are disjoint by design; if they ever collide, CASE TRACKER WINS
  - Suppress the echo, so an inbound outcome is not pushed straight back to them
  - Reply now; our implementation planned in a later session

Subject: Re: inbound outcomes -- yes, and the state machine already expects you

Hi Levon,

Yes to the endpoint, and your reasoning changed how I read our own code.

You asked why NoShow, CheckedIn, CheckedOut and Billed exist with no code path. I had written them off
as dead values. You are right that the day-of-exam flow is yours -- and when I went back to the state
machine, it already models the whole chain:

  Approved --MarkNoShow--> NoShow
  Approved --CheckIn-----> CheckedIn --CheckOut--> CheckedOut --Bill--> Billed

Every transition is permitted, with named triggers. Nothing is missing from our domain model; only the
entry point was never built. Someone designed for exactly what you are proposing and stopped short.

So: not cancellation-only. One endpoint taking an outcome, as you suggested. A cancellation-only
endpoint would leave three of those four permanently unreachable and need widening later.

The shape

  POST api/integration/offices/{tenantId}/appointments/{appointmentId}/outcome
  X-Integration-Token: <the token we issue you>

  {
    "outcome": "NoShow",
    "reason": "free text, yours",
    "decidedAtUtc": "2026-08-05T22:10:00Z",
    "decidedBy": "optional label for who decided"
  }

That mirrors the reconcile GET you already call -- same route prefix, same header -- so nothing new to
learn on auth.

  - Outcomes accepted: NoShow, CheckedIn, CheckedOut, Billed. An explicit allowlist, so an unknown
    value gets a 400 rather than being stored and puzzled over later. Since your list is still being
    settled, tell me what you add and we will widen it deliberately.
  - Idempotent on (appointmentId, outcome), as you asked: if the appointment is already in that state
    we return success and change nothing. Retry freely.
  - Ordering is validated, not assumed: the state machine rejects CheckedOut arriving before CheckedIn
    rather than silently applying it. You will get a clear error, not a wrong record.
  - Reason and decidedBy are persisted. Billing needs them and a status alone will not do.

Auth, and one thing you need from us

The token is the gate: X-Integration-Token, compared in constant time. Worth flagging that the
configuration slot exists but is currently EMPTY in production, which fails closed -- so this endpoint
AND the reconcile GET will both reject everything until we issue you a token. That is on us, and it is
the same token for both directions of inbound traffic. Ask me for it when you are ready to test.

On a source-IP allowlist: we do not have one today, the token is the only gate. Happy to add one if you
want the belt and braces -- send me the addresses and I will tell you honestly whether it buys much
over a shared secret on a LAN.

Conflict, and why I think it is narrower than you fear

Our answer is that the domains do not overlap by design. A no-show can only ever come from you; a
requested cancellation can only ever come from a party in the portal. Neither system can produce the
other's outcome.

If they ever do meet, YOU win. You are physically at the exam, so for anything on the day your report
is the better fact.

One thing you would otherwise notice

Setting a status is a change to an appointment we already hold, so our normal behaviour would be to push
it straight back to you -- you would receive your own no-show as an inbound intake seconds later. We
will suppress that. An outcome that originated with you will not be echoed. If you ever DO see one come
back, treat it as a bug on our side and tell me.

Cancellation reasons, outbound

Agreed: free text plus who acted. Coming with the attribution work, including the auto-cancel case, so
you can separate a document deadline from a person.

And yes, your reading is right, confirmed against our own documentation: CancelledNoBill versus
CancelledLate, with CancelledLate being the billable one. Nothing else needed there. The reschedule
equivalent is RescheduledNoBill and RescheduledLate, which closes the gap you identified.

The link -- you are right that it matters more than folder labelling

This is the argument that landed hardest. Because we deliberately do not send a patient identifier, your
patient id is entered by your staff -- so following the link back to the original case is the only way
you can read that id and file the new case in the same folder. Without the link, two dates for one
person file apart. That makes the link load-bearing, not cosmetic, and I have recorded it that way.

Your four follow-ups

1. evaluationKind literals: exactly "EVAL" and "RE_EVAL". A first evaluation carries "EVAL". They are
   explicit wire constants, deliberately not our enum names, so renaming anything internally cannot
   change what you receive.

2. The reschedule-link field: rescheduledFromAppointmentId. Direction is in the name -- this appointment
   was rescheduled FROM that one. previousAppointmentId keeps its single existing meaning, the prior
   evaluation episode. So a rescheduled re-evaluation carries both, unambiguously.

3. Carried-over uploads get a NEW document id. The rows are copied, so the new appointment has its own
   record pointing at the same object key. Your upsert therefore creates a second document rather than
   moving the original -- which is the behaviour you want. Thank you for raising it; a reused id would
   have quietly moved a document off the original case, and that is much easier to decide now than to
   discover later.

4. The doctor identifier is a GUID, and there is exactly one doctor row per office (we enforce it with a
   unique index). So the id alone is sufficient to key on. But be aware of the same caveat that applies
   to patients: the same human practising at two offices is two separate rows with two different ids,
   because each office has its own database. Given your patient handling is already tenant-scoped, I
   would key on (tenantId, doctorId) for symmetry -- it costs you nothing and it will not surprise you
   later.

The doctor id is built and merged on our side; it reaches you once we deploy, which has not happened
yet.

The inconsistency you spotted -- my error, not two things

You are right to press on this. I described the billing outcome as an explicit field AND as
RescheduledNoBill / RescheduledLate on the status. That was sloppy: they are the same thing, and I
should not have implied two.

The STATUS is the billing outcome and it is authoritative. There will be no separate field. That makes
reschedule symmetric with cancellation, where CancelledNoBill and CancelledLate already carry it the
same way. Two sources of truth for a billing decision is the last thing either of us wants.

The two small ones

Thank you for chasing the timestamp down, and no apology needed -- checking our container's timezone was
worth doing regardless, since it confirmed something we had been assuming rather than verifying.

On precision: nothing on our side depends on distinguishing sub-microsecond edits. Each change is its
own HTTP request and its own transaction, so two edits genuinely cannot land that close together
through the application. Your truncate-plus-equal-timestamp approach preserves the intent, which was
only ever to stop two rapid edits comparing equal and losing the newer one. Good to know about the
rounding bug, and good that nothing of ours was affected.

One thing new since we last wrote

We have added a delivery cap: at most 100 messages per office per rolling hour. Beyond that we hold and
resume automatically as the window slides. Normal traffic is nowhere near it -- an office runs about a
dozen slots a day. It exists so that a backlog release or a bad backfill on our side cannot fill your
queue with cases your staff would unpick by hand. Practically: a large burst arrives spread over hours,
and a gap in delivery is not necessarily a fault.

I will come back to you with a build plan and timing for the outcome endpoint rather than guess at dates
now. Nothing in it should change the shape above, so you can build your outbox against it.

Thanks Levon -- the point about the patient id travelling only via the link was the most useful thing in
your email.

Adrian

---

OPEN ITEMS THIS REPLY CREATES OR CONFIRMS, for tracking:

Commitments made:
  - inbound POST outcome endpoint, outcomes generally, allowlisted to the four day-of-exam values
  - idempotent on (appointmentId, outcome); state machine validates ordering
  - reason + decidedBy persisted
  - echo suppression for inbound-originated changes
  - Case Tracker wins any conflict
  - rescheduledFromAppointmentId as the reschedule link field name
  - carried-over uploads get a NEW document id
  - cancellation attribution: free text reason + actor, covering auto-cancel
  - billing outcome carried by STATUS only, no separate field (corrects the earlier promise)
  - doctor id: recommend they key on (tenantId, doctorId)

Actions on us, not yet done:
  - ISSUE THE INTEGRATION TOKEN. CaseTracker:IntegrationToken is empty in production, so BOTH the new
    endpoint and the existing reconcile GET reject everything. Levon has been told to ask for it.
  - Deploy the doctor id (merged in d658382b, not cascaded or deployed).
  - Populate Facility IDs on the locations screen before go-live.
  - Record in the epic doc: the reschedule link is load-bearing for patient filing, not cosmetic;
    and that the billing outcome is status-only.
  - Plan + build the outcome endpoint (deferred by Adrian to a later session).

Epic phases affected:
  - Phase 2 (cancellation reason + billing status): now also owes actor + auto-cancel attribution.
  - Phase 4e (CT two-case semantics): now also owes rescheduledFromAppointmentId, and must document
    that the status carries the billing outcome.
  - NEW, unscheduled: the inbound outcome endpoint. Not currently any phase in the epic roadmap.
