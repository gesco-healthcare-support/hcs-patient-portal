[Home](../INDEX.md) > [Product Intent](./) > Outstanding Questions

# Patient Portal -- Questions We Need Answered

Below are the questions currently open on the Patient Portal. Each section is addressed to a specific person (your business / product manager, a lawyer or compliance person, whoever owns how our different products fit together). Sections are self-contained, so any one of them can be copied into an email or meeting agenda on its own.

A short glossary at the bottom explains the recurring terms (Packet, doctor's admin, and so on).

---

## Recently resolved (2026-04-24)

The resolutions below have been folded into `docs/product/appointments.md`. Kept here so the history is visible.

- **Q2. Day-of-exam lifecycle** -- RESOLVED. The portal is appointment-booking-only, not case-tracking. Check-in, check-out, no-show, and billing are NOT in MVP; they live in downstream processes or later phases.
- **Q3. Non-booker user goals + traffic estimate** -- RESOLVED. Doctor's office staff handle four things: review decisions (approve / reject / send-back-for-info), phone-and-email intake (book on behalf of callers), cancellations / reschedules, and edits to appointment form data on request. Company-wide admins exist for oversight and break-glass access (no one else holds that access). Rough traffic distribution (patients + applicant attorneys majority, defense attorneys uncommon, claim examiners very rare) accepted as a working estimate.
- **Q4. "Insurance people" vs claim adjustor** -- RESOLVED. Two distinct recipients: the insurance company itself (a carrier or TPA contact) AND the specific adjustor handling this particular case.
- **Q5. Legacy business / contractual questions (contract, end client, handover, prior user research)** -- REMOVED as out of scope for this documentation. "The application has to work as I tell you it should."
- **Q6. HIPAA classification** -- DEFERRED to post-MVP (revisited during limited-user testing).
- **Q8. California medical-privacy and consumer-privacy laws (CMIA, SB 446, CCPA / CPRA)** -- DEFERRED to post-MVP (same timeline as Q6).
- **Q10. Structured Packet delivery to other software systems (formats, mechanisms, auth, retries, SLAs)** -- DEFERRED to post-MVP. At MVP the portal collects the full dataset and delivers via email; specific software-to-software formats and channels are built after MVP. "As long as we collect the data, we can format it as required."
- **Q12. Case-tracking hand-off** -- DEFERRED. MVP delivers via email only; case-tracking integration comes later once the email content is confirmed.
- **Q13. Medical-records AI (MRR AI) data flow** -- DEFERRED. That project is not currently active; integration details will be decided after it resumes.
- **Q14. User research with real bookers** -- REMOVED as not relevant to this build.
- **Q15. Verbal promises to the end client** -- REMOVED as not relevant to this build.

---

## For your business / product manager

### Q1. Should the portal let anyone cancel or reschedule an approved appointment?

If yes: who is allowed to do it -- staff at the doctor's office, a central admin on our side, or both? And what notifications go out when it happens?

---

### Q16. What is a "reevaluation" in our portal, and how does its booking flow differ from a first-time appointment?

Specifically: what triggers a reevaluation (patient request, doctor request, regulatory follow-up, something else?), who is allowed to book one (the original patient and their attorney, the defense, only the doctor's office?), what fields the reevaluation form needs to capture, and whether the approval, notifications, and email data-delivery differ from a first-time booking.

Context: the patient dashboard is planned to have a "Book a reevaluation" button alongside "Book an appointment". The developer's rough working guess is that a reevaluation is somewhat more complex than a first-time booking, but that most of the case data is already available from the first appointment (so the booker fills in fewer fields). Specifics are not yet decided.

---

## For legal or compliance

### Q7. Which California workers'-comp rules apply to how this portal schedules appointments, sends notifications, and hands off data?

Rules that look relevant from our research:

- 8 CCR section 31.3 -- QME exams must be scheduled within 90 days of the request.
- 8 CCR section 34 -- at least 6 business days' notice required before cancelling an exam.
- 8 CCR section 35 -- records must be exchanged 20 days before the evaluation.

Do all of these apply? Are there others we're missing? And do non-QME exams (AMEs and other IMEs) follow a different set of rules?

**Status:** the developer is gathering answers and will bring them back.

---

### Q9. What exact format does each required notification email need to follow so it counts as defensible evidence of communication?

The developer confirmed 2026-04-24 that most of the notifications the portal sends (on booking submit, on approve / reject / send-back-for-info, and likely on modifications) are **legally-required** communications with **strict formats** designed so there is evidence of communication and no impermissible ex-parte communication. The remaining question is: what are those strict formats, per event and per party-type (patient, applicant attorney, defense attorney, insurance company, claim adjustor, doctor's office)? Existing legal-comms templates or examples -- if the firm or client has them -- would unblock the MVP email-template build.

Also: are there audit-trail, retry-on-bounce, delivery-receipt, or logging expectations attached to the legal-evidence standard?

---

## For whoever owns how our products fit together

### Q11. Does the upstream form-capture product send any information INTO the portal when a booking is made?

The developer confirmed 2026-04-24 that the **tenant** a patient books under is **pre-decided upstream** (it is not an open choice on the booking form), so at least the tenant-assignment piece already comes from outside the portal. The remaining question: does form-capture also pre-fill any other fields (patient demographics, claim number, employer information, attorney contact details) into the booking, or is the booking form the sole data origin for everything else?

---

## Glossary

- **Packet.** The bundle of appointment information the portal collects on the booking form and delivers out. At MVP the delivery is by email only, to every party on the case. Structured hand-offs to other software systems (case tracking, medical-records AI, state systems, carriers) are post-MVP.
- **Doctor's admin.** A staff member at the doctor's office who can approve or reject booking requests, send a request back asking for more information, and cancel or reschedule approved appointments. Other possible names for this role: Office Manager, Scheduler.
- **Company-wide admin.** A central administrator on our side who has oversight and break-glass authority across every doctor's practice using the portal. Can cancel or reschedule any appointment at any practice. Meant for when something goes wrong at a practice-level that needs to be corrected from outside the practice.
- **Medical practice.** In this portal, one practice run by a medical examiner. It's not an insurance company, not a third-party claims administrator, not a law firm, not an employer. Each practice is treated as its own account; shared reference data (office locations, US states, lookup lists) is the same across all practices.
- **Send-back-for-info.** A third option the doctor's office has when reviewing a booking request, in addition to approve or reject. The request goes back to whoever submitted it, asking for more information, and sits in an "awaiting more info" state until the booker responds.
- **Ex-parte communication.** California workers'-comp concept: one party taking a case-affecting action without every other party being informed. Impermissible. The portal's notification pattern (all parties emailed on every event) exists specifically to keep the matter out of ex-parte territory.
- **DWC.** California Division of Workers' Compensation. The state agency that administers California's workers'-compensation system.
- **QME / AME / IME.** Three kinds of medical evaluation under California workers'-comp. QME = Qualified Medical Evaluator (a physician certified by the state). AME = Agreed Medical Evaluator (a physician both sides agreed to use in a contested case). IME = Independent Medical Examination (the general umbrella term).
- **TPA.** Third-Party Administrator. A company that handles insurance-claim processing on behalf of an insurance carrier or a self-insured employer.

---

## Change log

- 2026-04-24 -- resolution round. Q2, Q3, Q4, Q5, Q6, Q8, Q10, Q12, Q13, Q14, Q15 moved to Recently resolved (closed or deferred per answers and scope decisions). Q9 narrowed from "are notifications required" to "what is the exact format per event / party". Q11 narrowed (tenant pre-decided; remaining pre-fill question still open). Q16 annotated with the developer's rough working guess. Glossary trimmed.
- 2026-04-23 -- first draft.
