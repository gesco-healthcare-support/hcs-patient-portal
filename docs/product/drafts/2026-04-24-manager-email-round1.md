---
purpose: Round-1 manager email -- first five questions after T2 Appointments resolution round
date: 2026-04-24
covers: OUTSTANDING-QUESTIONS.md Q1, Q16, Q7, Q9, Q11 (renumbered 1-5 for the email; internal cross-refs preserved in the spec)
note: reconstructed 2026-04-24 from conversation history; Adrian sent before round-2 and round-3 were produced
---

**Subject:** Patient Portal MVP -- 5 questions to unblock next steps

Hi [Manager],

I've narrowed the Patient Portal MVP scope down to five open questions. I need answers (from you, or from the right people on your side) before I can move to the next feature specs without guesswork. Each question below is self-contained and can be forwarded to the right person as-is.

## For business / product decisions

**1. Should the portal let anyone cancel or reschedule an approved appointment in MVP?**

If yes: who is allowed to do it -- staff at the doctor's office, a central admin on our side, or both? And what notifications go out when it happens?

This is the biggest remaining lever on MVP scope. If cancel/reschedule is not in MVP, we ship book-review-approve-only and handle cancellations off-portal (phone/email) for now.

**2. What is a "reevaluation" supposed to do, and how does its booking flow differ from a first-time appointment?**

The patient dashboard will have "Book a reevaluation" alongside "Book an appointment". I need to know: what triggers a reevaluation (patient request, doctor request, regulatory follow-up, something else?), who can book one (the original patient and attorney, defense, only the doctor's office?), what fields the reevaluation form captures, and whether the approval / notifications / email delivery differ from a first-time booking.

My rough guess is that reevaluation is somewhat more complex than a first-time booking but most of the data is already available from the first appointment. Can you confirm or correct that?

## For legal or compliance

**3. Which California workers'-comp rules apply to how this portal schedules appointments, sends notifications, and hands off data?**

Rules that looked relevant from our research:

- 8 CCR section 31.3 -- QME exams scheduled within 90 days of the request.
- 8 CCR section 34 -- at least 6 business days' notice required to cancel an exam.
- 8 CCR section 35 -- records exchanged 20 days before the evaluation.

Do all of these apply? Are there others we're missing? Do non-QME exams (AMEs and other IMEs) follow a different set of rules?

**4. What exact format does each required notification email need to follow so it counts as defensible evidence of communication?**

Most of the notifications the portal sends (on booking submit, approve, reject, send-back-for-info, and modifications) are legally-required communications with strict formats, meant to create evidence of communication and avoid ex-parte issues. What I need: the actual format per event and per party-type (patient, applicant attorney, defense attorney, insurance company, claim adjustor, doctor's office). If the firm or client already has legal-comms templates, those would unblock the build right away.

Also: any audit-trail, retry-on-bounce, delivery-receipt, or logging expectations attached to the evidence standard?

## For whoever owns how our products fit together

**5. Does the upstream form-capture product send any information INTO the portal when a booking is made?**

The tenant a patient books under is already pre-decided upstream -- that part is clear. Remaining question: does form-capture also pre-fill any other fields (patient demographics, claim information, employer information, attorney contact details) into the booking, or does the booking form collect the rest from scratch?

---

## What each answer unblocks on my side

- **1** -- modification flow spec and sizing.
- **2** -- reevaluation form design and the second booking type.
- **3** -- scheduling validation rules (advance-booking windows, cancellation-notice floors, record-exchange warnings).
- **4** -- MVP email templates. Currently the single biggest MVP-blocking build item on my end.
- **5** -- finalizing the booking form's data model (which fields get user entry vs. pre-fill).

Once these land I can move on to specs for the other features (doctor availabilities, patients, attorneys, etc.) without blocking on guesses.

Thanks,
Adrian
