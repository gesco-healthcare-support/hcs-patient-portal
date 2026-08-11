DRAFT - NOT SENT. Reply to Levon's cancel/reschedule email (31 questions, A-J).
Drafted 2026-08-03. Every claim below verified against code, not memory.

NOTE FOR ADRIAN: reply-3 (levon-reply-3-draft.md) was never sent. That is why he re-asks
20/21/22 as "still open" and why the reschedule-link naming reappears. This draft folds in
the still-relevant parts of reply-3 so nothing is lost, and drops the parts he has since
answered himself.

Subject: Re: cancel/reschedule -- answers, and one premise to correct

Hi Levon,

Answering all 31. Taking your point about sending everything we hold: agreed, and it changes a
couple of answers below in your favour.

One correction first, because it saves you work: we have NO 6-business-day rule. We have no
business-day calendar, no holiday list and no notice-period concept anywhere in the portal. The
only date rule we hold is a booking lead time in minutes, which is a different thing entirely. So
questions 6 and 7 have no answer on our side to align with -- the 6-business-day determination is
entirely yours, and there is no risk of us drifting from a calendar we do not have. Do not wait on
us for a holiday list.

A. Blockers

1. Yes, unambiguously, and we will use a separate field: rescheduledFromAppointmentId. Direction is
   in the name -- this appointment was rescheduled FROM that one. previousAppointmentId keeps its
   single existing meaning, the prior evaluation episode. So a rescheduled re-evaluation carries
   both fields, and your Eval / Re-Eval labelling can key on evaluationKind alone without ever
   consulting a link. Your worry about retroactively marking a completed case as Rescheduled is
   exactly why we are not reusing the field.

2. Yes. The original pushes again when it takes its rescheduled outcome. Any change to an
   appointment you already hold is pushed, so that arrives as a normal update on the original's
   appointmentId, independently of the new appointment. You will not have to work backwards.

3. RescheduledNoBill and RescheduledLate. Both already exist in our enum but no code path can set
   them today, which is why they were not in the five. The reschedule work brings them into use, so
   the definitive list goes from five to seven. You will have them before anything can arrive
   carrying one.

4. Partly already done -- your note is out of date, and in your favour. Taking your three asks:

   - The reason text: ALREADY SENT, as `cancellationReason`. Null unless the appointment was
     cancelled. One caution: it is user-authored free text, so treat it as untrusted display data.
     If you print it on a notice that reaches the Appeals Board, it is worth escaping rather than
     trusting.
   - The automatic AME cancel versus a human one: distinguishable TODAY, though not by a boolean.
     The auto-cancel writes a fixed constant into that same reason field rather than free text, so
     you can separate the two on the reason value. If you would rather have an explicit flag than
     match a constant, say so and we will add one.
   - Who cancelled: still NOT sent. This is the one genuine gap of the three. We hold it -- party
     type and, where we have it, the name -- and it is next on our list.

   Both of the first two shipped on our side recently and reach you on our next deployment, which
   has not happened yet. So if you test before then you will still see the old shape.

B. Timing

5. Both timestamps are a fair ask and we will send them. Your reasoning is right that a notice
   period measured from the wrong end changes the answer toward over-billing someone who gave
   timely notice, and our workflow does have a requested state preceding the terminal one.

6 and 7. See the correction above -- no such rule on our side.

8. Better than not-blocking: it is already built, and you get it as an explicit field exactly as you
   wanted. `billingStatus` carries `NO_BILL`, `LATE` or `NONE`. It is always present and
   non-nullable, so you never have to tell "absent" apart from "nothing to bill", and you never have
   to string-match our enum spelling to decide whether to charge.

   To be precise about the relationship, since I muddled this in an earlier email: there are two
   fields and they are not duplicates. `status` remains authoritative for LIFECYCLE -- what happened
   to the appointment. `billingStatus` is the billing INTENT, surfaced separately so a future rename
   of one of our enum members cannot change what your billing team reads. Use `billingStatus` for
   the charge decision and `status` for the case state.

   This is also the cross-check you asked for: when the reschedule work lands, a `RescheduledLate`
   status will arrive alongside `billingStatus: LATE`, so your own 6-business-day calculation has
   something independent to agree or disagree with. Two sources agreeing is the evidence you wanted;
   two disagreeing is a bug worth catching before it reaches an invoice.

   Like the reason field, this reaches you on our next deployment.

9. Previous date/time: not sent today. Under the two-appointment model the original keeps its own
   date on its own case, so you have both dates across the two records, and the link ties them. A
   sequence or count for an appointment moved more than once does not exist today; noted as a
   request.

C. In-flight states

10. Correct, you will receive CancellationRequested and RescheduleRequested before the terminal
    outcome. Not guaranteed to be followed by a terminal outcome, though -- see 11.

11. Yes, effectively. A requestor cannot withdraw, but staff can REJECT a change request, and that
    returns the appointment to plain Approved. Our request status has only Pending, Accepted and
    Rejected -- there is no withdrawn state. So your instinct is right and worth acting on: treat
    the *Requested states as informational and act only on the terminal outcome, because a request
    can simply evaporate back to Approved.

D. Documents

12. The new appointment gets its OWN document records with their own ids, but uploaded documents
    reference the SAME object key as the original. So two of your cases will point at the same blob,
    deliberately. Packets are different: they are regenerated for the new appointment with their own
    object keys, because packet content embeds the appointment date. Since your upsert keys on our
    document id, a new id means you create a second document rather than moving the original off the
    first case -- which is the behaviour you want. Thank you for raising it in time.

13. Yes, the retention guarantee holds for a cancelled appointment's documents. No purging, and
    deletion is soft-delete only, blob retained. That is unchanged by cancellation, and it is
    reinforced by the shared-object-key point above: a shared blob cannot be hard-deleted without
    breaking the other case.

14. Good catch, and your margin IS thinner than before. The mechanics: an intake payload is rebuilt
    from current state every time it is enqueued, so ANY later intake push carries the complete
    current document set including packets. A superseding push therefore has them. If a push
    dead-letters, recovery is a manual re-push on our side, which rebuilds the payload fresh. So
    nothing is permanently lost, but you are right that "the first push is the only carrier" is
    closer to true than it was. Two things reduce the exposure: we alert on failures, so a
    dead-lettered push is visible to us rather than silent; and if packets settle again afterwards
    they go out on the document feed rather than as another intake. If you would rather we
    unconditionally re-send the document set on the feed after any intake, say so and we will look at
    it.

E. Notices and parties

15. Yes, the portal already notifies. We have cancellation notification handlers, including a
    dedicated one for the AME auto-cancel that goes to all stakeholders, plus a general
    status-change notifier. So you may well be duplicating a notice. Worth a short call to compare
    recipient lists before your paperwork restates something our email already said.

16. Not today, and this is the one real gap for your proof-of-service. We HOLD the injured worker's
    postal address -- street, unit, city, state, zip -- we simply do not put it on the payload. Small
    addition and we will make it.

17. Both, and my earlier answer was incomplete rather than wrong. Checked properly: the attorney
    blocks carry first name, last name, firm, email, phone, fax, web address AND a full address
    (street, city, state, zip). Claim examiner entries carry name, suite, email, phone, fax AND a
    full address. Insurance carriers likewise. So every party you need to serve already has an
    address on the payload EXCEPT the injured worker, which 16 fixes.

18. No. We hold no records-available or records-reviewed flag on the appointment, so there is
    nothing to prefill. Your staff will have to keep entering those.

F. Type change after approval

19. Your argument is right and I accept the distinction: a general rebooking link is not
    reconstructable, but a type change IS, because our own workflow performs the cancel-and-rebook
    and therefore knows the two are the same exam. Noted as a request. It is not in a plan yet, so
    I will not promise timing, but it is a legitimate ask rather than one I want to talk you out of.

G. Carried over

20. Done and merged -- data.doctor.id, our stable row identifier for that doctor. It reaches you on
    our next deployment, which has not happened yet. Match on the id, not the name. One caveat: it
    is our own row key, stable for the life of the record, not a licence number. And because the
    portal is database-per-office, the same human at two offices is two rows with two different ids,
    exactly as with patients -- so I would key your mapping on (tenantId, doctorId) for symmetry with
    how you already handle patients.

21. This one you solved yourself in your last email and I do not think you saw my answer: your queue
    column is labelled Received but renders our submittedAtUtc in the viewer's local timezone, so
    09:42 was 16:42Z, about two hours before the 18:45:09Z push. Both clocks are fine. For the
    record, I did verify ours: the API container runs UTC with TZ unset, and every timestamp we send
    is ISO-8601 with an explicit Z. Your ordering guard is comparing our UTC values against each
    other, so it is on solid ground.

22. Still empty. It is data entry on our locations screen and it is ours to do before go-live, not a
    code change. I will confirm when both production clinics carry it.

23. Yes, always authoritative. Take the incoming objectKey as the truth for that document id. The key
    is mutable for a stable id and the superseded blob is deleted, so holding a previous key risks
    pointing at nothing.

H. Reconcile

24. Your risk is real and I want to be careful how we fix it, because the ambiguity is deliberate:
    unknown appointment, unknown office and switched-off office all return the same 404 so that a
    holder of a leaked token cannot enumerate which offices or appointments exist. I would rather not
    weaken that casually.

    But your actual exposure has a fix that costs nothing: a non-200 carries NO information about
    documents and must never drive pruning. Prune only on a 200. We will state that explicitly in the
    contract rather than leave it implied, so the safe behaviour is written down instead of being
    something you inferred. If that is not enough for you to prune with confidence, tell me and we
    will weigh a distinguishable marker for the switched-off case specifically -- I just want the
    decision made deliberately rather than by convenience.

25. Yes, that still holds. We will warn you before disabling a clinic's integration.

I. Infrastructure

I checked the server rather than guess, and I would rather give you the honest position than a
reassuring one: none of 26 to 28 is done yet.

26. Not done, and slightly further back than "only a reverse-proxy rule remains". Two things are
    missing, not one. MinIO currently has no published port at all -- it is reachable only on the
    internal container network, so nothing outside the host can see it. And there is no MinIO route
    in the reverse proxy; the only host blocks configured are for auth, api and the app itself.

    Before anyone writes that rule, one thing to settle: you wrote "exposed to 192.168.101.35 over
    TLS". If you mean reachable FROM your server at that address, a hostname route works and our
    wildcard covers it. If you mean reachable AT that IP, a wildcard DNS certificate will not
    validate against a bare IP address and we would need a hostname anyway. Which did you mean?

27. Not created. The only bucket that exists is case-evaluation-documents.

28. Not created. There are no non-root users and no custom policies -- only MinIO's five built-in
    ones. This one is also blocked by 27, since the policy has to name a bucket that exists.

29. Nothing on the server tells me where this stands, because it is a conversation between our IT
    and yours rather than a configuration. I will chase it and come back with a real answer.
    Understood that it is what gates your deploy, so I am treating it as the most urgent of the
    four.

All four are now on our implementation list with the rest of the work from this email, rather than
sitting as loose ends.

J. Coordination

30. Any notice is enough. Our pushes queue and retry rather than failing outright, so a restart on
    your side is not the problem it would be with fire-and-forget delivery. A heads-up so we are not
    investigating a spike of failures is all we need.

31. Please delete A00005. It was a synthetic test appointment and it has served its purpose --
    confirming the end-to-end path. Leaving it in your live queue is a standing invitation for
    someone to confirm it by accident and create a junk case and patient folder. We have the record
    on our side if we ever need to refer back to it.

And noted on the day-of failures being yours -- that matches what we said, and we are not building
anything that expects them from you.

A call would probably be faster than another round of this for the notice duplication in 15 and the
reconcile question in 24. Happy to set one up.

Thanks Levon -- the reschedule-versus-re-evaluation point and the packet-carrier point in 14 were
both things worth catching before they cost either of us real work.

Adrian

---

IMPLEMENTATION BACKLOG -- everything this email puts on us, in one list

Grouped by who does it and what blocks what. Nothing here is scheduled yet; the epic roadmap is at
docs/plans/2026-07-31-reschedule-cancel-calendar-integration-epic.md and another session is
currently on Phase 4c.

CODE -- already built, needs shipping
C1. Doctor id (data.doctor.id). MERGED in d658382b. NOT cascaded to development, NOT deployed, so
it has not reached Levon. Deploy unblocks his Q20 with no further work.
C2. Volume cap (100 per office per rolling hour). MERGED in the same commit, same deploy.

CODE -- new, small, not in any phase
C3. Patient postal address on the intake payload (Q16). We already hold street, unit, city, state,
zip on Patient; IntakePatientSection simply has no address fields. This is the ONLY address
gap -- attorney, examiner and insurance blocks already carry full addresses. Blocks his
proof-of-service document.
C4. Requested-vs-finalized timestamps, UTC plus local zone (Q5). Needed because a notice period
measured from the wrong end over-bills someone who gave timely notice.

CODE -- epic Phase 2: SHIPPED in PR #414 -> main baa1fee6. NOT deployed, so Levon has none of it yet.
C5. Cancellation reason text (Q4). DONE -- `cancellationReason`, nullable, user-authored free text.
C6. Billing status as an EXPLICIT field (Q8). DONE -- `billingStatus` = NO_BILL / LATE / NONE via
BillingStatusWire, always present, non-nullable. IMPORTANT: this means the earlier "explicit field"
promise was CORRECT and my later retraction was the error. Two complementary fields: `status`
authoritative for lifecycle, `billingStatus` for billing intent.
C7. Auto-cancel distinguishable (Q4). DONE, but by CONSTANT not boolean -- JointDeclarationAutoCancelJob
writes a fixed reason constant instead of free text, so it separates on the reason value. Offer an
explicit flag only if he asks.
C7b. Who cancelled -- party type and name (Q4). STILL NOT DONE. The only remaining gap of the three,
and the one his proof-of-service and billing attribution actually need.

CODE -- belongs to epic Phase 4d / 4e (two-appointment reschedule)
C8. rescheduledFromAppointmentId as a SEPARATE field (Q1). previousAppointmentId keeps its single
existing meaning. This is his top blocker.
C9. RescheduledNoBill / RescheduledLate become reachable, taking the definitive status list from
five to seven (Q3).
C10. Confirm the original re-pushes when it takes its rescheduled outcome (Q2). Believed to be
existing behaviour -- any change to a held appointment is pushed -- but verify inside 4e
rather than assume.
C11. Carried-over uploads get NEW document ids while sharing the object key (Q12). Already the
locked decision; call it out in 4e so it is not lost.

CODE -- new, requested but NOT promised
C12. Reschedule sequence / count for an appointment moved more than once (Q9).
C13. Type-change link or marker for the cancel-and-rebook case (Q19). His argument is sound: the
workflow knows the new booking replaces the old one, unlike a general rebooking.
C14. Inbound outcome endpoint. CORRECTION: this IS an epic phase -- Phase 5, "No-show round trip
(INBOUND from CT)", branch `feat/no-show-round-trip`, TODO after 4d, plan not written. I previously
said it was in no phase; that was wrong. POST an outcome, allowlisted to NoShow / CheckedIn /
CheckedOut / Billed, idempotent on (appointmentId, outcome), ordering validated by the state
machine, echo suppression so an inbound outcome is not pushed back at them.

DOCS
D1. Contract: a non-200 reconcile response carries NO document information and must never drive
pruning (Q24). Documentation only -- it protects him fully without weakening the deliberate
404 ambiguity, which exists so a leaked token cannot enumerate offices or appointments.
D2. Contract: status list five to seven when C9 lands.
D3. Epic roadmap: record that the reschedule link is load-bearing for patient filing, not
cosmetic, and that the billing outcome is carried by STATUS only.

DATA -- Adrian, no code
A1. Populate Location.FacilityId on both production clinics (Q22). Until then his staff type it
manually on every intake.

INFRA -- Adrian / IT, verified NOT done on the server 2026-08-03
I1. Publish MinIO outside the container network. It currently has no ports mapping at all.
I2. Add a MinIO route to the reverse proxy. docker/nginx-proxy/default.conf.template has no
minio, 9000 or 9001 reference; only auth, api and app host blocks exist. BLOCKED on deciding
hostname versus bare IP -- a wildcard certificate will not validate against an IP.
I3. Create the case-tracker-documents bucket. Only case-evaluation-documents exists.
I4. Author the scoped MinIO policy: read-only on case-evaluation-documents, read/write + delete on
case-tracker-documents. No custom policies exist, only MinIO's five built-ins. BLOCKED BY I3.
I5. Create the MinIO user / key for that policy and send the secret out of band. Zero non-root
users exist today. BLOCKED BY I4.
I6. Joint DNS request with their IT (Rod). Not visible on the server. THIS GATES THEIR DEPLOY, so
it is the highest-leverage item of the four.
I7. Issue CaseTracker:IntegrationToken. Still EMPTY in production, which fails closed -- so the
reconcile GET rejects everything today and his whole section H concerns an endpoint he cannot
currently call.

COORDINATION
X1. Delete the synthetic A00005 from their live Pending Intakes queue (Q31).
X2. Compare notice recipient lists with him (Q15) -- the portal already notifies on cancellation
via three handlers, so his paperwork may duplicate ours.

Dependency order that matters: I3 -> I4 -> I5. I2 is pointless before I1. C1/C2 need only a deploy.
Everything else is independent.

---

WHAT THIS REPLY COMMITS US TO, and what is genuinely new

Already committed / built:

- doctor id: BUILT and merged (d658382b), awaiting deployment
- rescheduledFromAppointmentId: committed to previously, epic Phase 4e
- cancellation reason + actor + auto-cancel flag: epic Phase 2
- billing outcome carried by STATUS only, no separate field (correction, restated)

NEW, not in any plan:

- patient postal address on the payload (Q16) -- SMALL, we already hold the data
- requested-vs-finalized timestamps with local zone (Q5) -- SMALL
- contract note: a non-200 reconcile response must never drive document pruning (Q24) -- DOC ONLY
- reschedule sequence / count (Q9) -- noted as a request, not promised
- type-change link marker (Q19) -- noted as a legitimate request, no timing given

Facts established by this research (verified, worth keeping):

- NO business-day / notice-period / holiday concept exists anywhere in the portal
- IntakePatientSection has NO address fields; attorney, examiner and insurance blocks DO carry
  full street/city/state/zip
- No records-available / records-reviewed flag on Appointment
- RequestStatusType is Pending/Accepted/Rejected only -- no Withdrawn; staff rejection returns
  the appointment to Approved
- The portal DOES notify parties on cancellation (ClinicalStaffCancellationEmailHandler,
  JdfAutoCancelledEmailHandler, StatusChangeEmailHandler) -- possible duplicate notices
- The reconcile 404 ambiguity is deliberate anti-enumeration, documented in
  CaseTrackerReconcileService; do not weaken it without a decision

Actions on Adrian, not code:

- populate Facility IDs on both production clinics (Q22)
- issue CaseTracker:IntegrationToken -- still EMPTY in production, so reconcile rejects
  everything today
- infrastructure answers for 26-29 (reverse proxy, bucket, scoped key, DNS with Rod)
- deploy the doctor id so Q20 actually lands
