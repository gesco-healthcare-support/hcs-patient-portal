DRAFT - NOT SENT. Update to Levon covering everything shipped since reply 4.
Drafted 2026-08-08. Every claim below verified against code and against the live server, not memory.

BEFORE SENDING - DEPLOYMENT GATE. The body opens with "Everything below is deployed to our server".
That is true of sections 1, 2, 6 and 7. It is NOT yet true of section 3 (the new payload fields) or
of the auto-cancel removal in section 8 -- both are merged but undeployed. Deploy first, or edit
those two sections. Sent as-is, Levon would test for fields that are not live and conclude the
integration is broken.

DO NOT PASTE THE INTEGRATION TOKEN INTO THIS EMAIL. It is generated and stored on the portal server
only. Send it to Levon through a separate channel he already trusts (password manager share, or the
channel used for the intake token), and never in the same message as the URLs it unlocks.

---

Subject: Patient Portal -- integration changes ready for you (new inbound endpoint, new payload fields)

Hi Levon,

An update on the portal side. Several things have landed since my last email, and one of them needs
something from you. Everything below is deployed to our server and documented in the contract file
you have.

## 1. Your token is ready (action needed)

The `X-Integration-Token` you need for the reconcile endpoint has been generated. I am sending it
separately rather than in this email.

That token was empty on our side until now, which means any reconcile call you tried previously
would have been rejected with a `401`. That was our gap, not yours -- the endpoint is written to
refuse everything when no token is configured, so that a half-configured deploy can never serve
patient data. Once you have the token, the reconcile endpoint works.

The same token also unlocks the new endpoint in point 2.

## 2. NEW: you can now tell us an appointment produced no evaluation

This is the first thing in this integration that YOU call and WE act on -- everything else is either
us pushing to you or you reading from us.

    POST https://admin.api.<portal-base-domain>/api/integration/offices/{tenantId}/appointments/{appointmentId}/attendance
    Header: X-Integration-Token: <the token>
    Body:   { "outcome": "NO_SHOW" }   or   { "outcome": "NOT_SEEN" }

- NO_SHOW -- the patient did not arrive.
- NOT_SEEN -- the patient arrived but was not evaluated (interpreter missing or wrong, patient left
  before being called, and so on).

Why we need it: your intake staff record these in the Case Tracker, so the portal has no way to find
out. Without this the portal shows the appointment as Approved forever and no re-evaluation can be
booked against it.

Responses: `200` applied (a retry with the SAME outcome is also `200` and changes nothing, so retry
freely); `400` if `outcome` is missing or is not one of those two exact strings; `401` wrong token;
`404` unknown office or appointment; `409` the appointment cannot take that outcome right now.

About the `409` -- it is safe to log and stop, retrying will not help. The most likely cause is that
the appointment is not in Approved state. In particular an appointment with a reschedule in flight
cannot no-show, because there is no agreed date for the patient to miss; finalise the reschedule
first and the NEW appointment is the one that can no-show.

There is NO replacement appointment created. We record the outcome and stop. If the client still
wants an appointment they submit a new request. That applies to re-evaluations too.

Full detail is in section K of the contract file.

## 3. NEW payload fields

Your receiver will start seeing these. All are additive -- nothing was removed or renamed.

Patient postal address, under `data.patient`:

    street, unit, city, state, zipCode

One caution worth reading twice. Our database column names are misleading and the WIRE names are the
truth: `street` is street line 1, and `unit` is the apartment or suite number. If you had mapped a
field called "address" from us before, it would have been the unit number. There is no line-2 field
beyond `unit`. `state` is the state NAME (e.g. `California`), matching how we already send state for
attorneys and examiners.

Change attribution, at the top level:

    changeRequestedBySide    "SIDE_A" or "SIDE_B", or null
    changeRequestType        "CANCEL" or "RESCHEDULE"
    changeRequestedAtUtc     when the change was requested
    changeFinalizedAtUtc     when our staff decided it

Side A is the patient plus applicant attorney; Side B is the defense attorney plus claim examiner.

Two things to note. `changeRequestedBySide` being null means OUR STAFF initiated the change, so no
party requested it -- please do not read null as "unknown". And `changeFinalizedAtUtc` is null while
a request is still pending, which is a real state rather than missing data.

You had asked for requested-versus-finalised timestamps because a notice period measured from the
wrong end over-bills someone who gave timely notice. That is what these two are for. They come from a
dedicated decision column, not from a general "last modified" timestamp, so a later edit to the row
cannot silently change when the decision appears to have been made.

## 4. Written down at last: every push is a full snapshot

Each message we send carries the appointment's COMPLETE current state -- every field, every party,
every document -- never a delta of what changed.

Two consequences for you: you may overwrite your copy wholesale rather than merging field by field,
and a field being absent means it is genuinely absent now, not that we omitted it because it did not
change.

This was always how it worked. It had simply never been written down, so you had no basis to rely on
it. It is now in the contract.

## 5. Written down: a failed reconcile is not a deletion

Also now explicit: any reconcile response other than `200` -- a `401`, a `404`, a timeout, a `5xx` --
carries NO information about documents, and must never cause you to prune your copies.

The `404` stays deliberately ambiguous (unknown appointment, unknown office, and integration switched
off all look identical) so that the endpoint cannot be used to discover what exists. You had already
accepted that. The trade-off is that a `404` cannot distinguish "gone" from "not visible to you right
now", so only a `200` body is evidence about documents. Deletions always reach you explicitly through
the document channel, never by inference from a failed read.

## 6. Reminder: a reschedule is now TWO cases

Already flagged, and now live: when an appointment is rescheduled we close the old one and open a new
one, rather than moving the existing one. The old case carries `supersededByAppointmentId` plus
`supersededReason`, and the new one points back with `rescheduledFromAppointmentId`.

Your receiver keys on `appointmentId` and upserts, so this needs no code change on your side -- but
you will now see two cases where you previously saw one moved date.

## 7. Two statuses you may see but will never receive

`NoShow` and `NotSeen` are now real statuses in the portal. We deliberately do NOT push them to you,
because YOU are the one who told us -- echoing them back would tell you only what you already know.

The consequence, stated plainly so it is not a surprise: once an appointment is NoShow or NotSeen, no
further update about it reaches you at all. Not a demographic correction, not a document. Its case is
closed on both sides.

## 8. Still outstanding on our side

Being honest about what you asked for that is not done:

- Who cancelled, by NAME. You now get the party SIDE, which was the larger half of that ask, but not
  the individual's name.
- The explicit AME auto-cancel flag. Moot rather than built, and the reason matters: we removed the
  auto-cancel itself. An appointment whose Joint Declaration Form never arrives is no longer
  cancelled by the portal with nobody involved -- it is flagged for our staff, who decide. There will
  be no auto-cancellations left for a flag to mark, so please do not build against it.

  Nothing changes in the contract: no field added, none removed, no code change your side. What
  changes in practice is that you will stop receiving these cancellations. Today an overdue form
  eventually pushes you a `CancelledNoBill` carrying the reason "The Joint Declaration Form was not
  uploaded before the required deadline." After this, nothing is pushed unless a person cancels the
  appointment deliberately, and then it carries whatever reason that person wrote.

  Worth saying plainly: this means fewer messages, not more. If you have anything that counts on
  those automatic cancellations arriving, it will go quiet rather than error.

Happy to jump on a call if anything here is easier to work through live.

Adrian
