[Home](../INDEX.md) > [Product Intent](./) > Outstanding Questions

# Patient Portal -- Questions We Need Answered

Below are the questions currently open on the Patient Portal. Each section is addressed to a specific person (your business / product manager, a lawyer or compliance person, whoever owns how our different products fit together, and the end client). Sections are self-contained, so any one of them can be copied into an email or meeting agenda on its own.

A short glossary at the bottom explains the recurring terms (Packet, doctor's admin, and so on).

---

## For your business / product manager

### Q1. Should the portal let anyone cancel or reschedule an approved appointment?

If yes: who is allowed to do it -- staff at the doctor's office, a central admin on our side, or both? And what notifications go out when it happens?

---

### Q2. Should the portal cover what happens on the day of the exam itself?

Specifically: check-in when the patient arrives, check-out when the exam is done, marking a no-show when the patient doesn't arrive, and billing after the exam. Or do those steps stay outside the portal (in the office's current processes, or in a different product)?

---

### Q3. For the non-booker users of the portal, what are they trying to get done when they log in?

- Staff at the doctor's office (receptionists, schedulers, office managers)
- Company-wide admins on our side

The four booker users (patient, applicant attorney, defense attorney, claim examiner) were sketched in a 2026-04-23 conversation with Adrian; their flows are captured in the Appointments intent doc. Non-booker flows still need fleshing out.

Also, our rough traffic estimate is that patients + applicant attorneys are the majority, defense-attorney bookings are uncommon, and claim-examiner bookings are very rare (mostly for self-represented patients, who in turn tend to self-book anyway). Does that match your sense of who is really using the portal?

---

### Q4. When we notify "the insurance people" and "the claim examiner / claim adjuster" about a booking, are those the same person, or two different groups?

If they're different: who exactly are "the insurance people" -- a general claims inbox, a supervisor, a team alias, someone in billing, or something else?

---

### Q5. A few older business questions still open

- Is there a signed contract or agreement that says what this portal has to deliver, by when, and what counts as "finished"? If there is, who has a copy and can the developer see it?
- Who is the end client -- the organization that asked for this portal to be built? A single insurance company, a third-party claims administrator, a self-insured employer, a government agency, or an internal group on our side?
- Why did the previous developer leave, and what did they hand over when they did?
- Did anyone talk to real users (lawyers, injured workers, claim examiners, defense attorneys) before the portal was originally designed?

---

### Q16. What is a "reevaluation" in our portal, and how does its booking flow differ from a first-time appointment?

Specifically: what triggers a reevaluation (patient request, doctor request, regulatory follow-up, something else?), who is allowed to book one (the original patient and their attorney, the defense, only the doctor's office?), what fields the reevaluation form needs to capture, and whether the approval, notifications, and Packet delivery differ from a first-time booking.

Context: the patient dashboard is planned to have a "Book a reevaluation" button alongside "Book an appointment", but the reevaluation form and rules have not been designed yet.

---

## For legal or compliance

### Q6. Under HIPAA, is this portal formally a "covered entity" or a "business associate"?

If it's a business associate: who is the covered entity we're operating under, and can the developer see the formal agreement (the BAA) so its terms can be built into the system?

---

### Q7. Which California workers'-comp rules apply to how this portal schedules appointments, sends notifications, and hands off data?

Rules that look relevant from our research:

- 8 CCR section 31.3 -- QME exams must be scheduled within 90 days of the request.
- 8 CCR section 34 -- at least 6 business days' notice required before cancelling an exam.
- 8 CCR section 35 -- records must be exchanged 20 days before the evaluation.

Do all of these apply? Are there others we're missing? And do non-QME exams (AMEs and other IMEs) follow a different set of rules?

---

### Q8. Beyond HIPAA, do any of these California laws apply to how we handle data?

- The Confidentiality of Medical Information Act (CMIA)
- SB 446 (notification duties)
- The CCPA / CPRA (consumer privacy rights)

---

### Q9. Are the emails the portal sends at booking time legally required notifications, or are they just helpful reminders?

Specifically: the emails that go out to everyone involved (injured worker, their lawyer, defense lawyer, insurance people, claim handler) when a booking is submitted, and again when the doctor's office approves or rejects it.

The developer's current working understanding is that these notifications are required by California's **ex-parte communication rule** -- any action one party takes on a workers'-comp matter has to be visible to every other party, or it counts as impermissible ex-parte communication. Does that cover the full compliance picture, or are there additional regulatory (DWC, HIPAA), contractual, or business-promise obligations on top of it? And if ex-parte is the only driver, are there specific delivery, logging, retry-on-bounce, or audit-trail expectations that have to be met for the notifications to be defensibly compliant?

---

## For whoever owns how our products fit together

### Q10. Which specific other software systems need to receive a Packet from the portal?

For each system on the list, we'll need:

- How to deliver the Packet (system-to-system connection, shared folder, encrypted email, etc.)
- How to authenticate (username and password, access token, certificate, SFTP credentials)
- What format the Packet has to be in (a specific PDF template, a structured data file, a spreadsheet)
- What to do if a delivery fails
- How quickly delivery has to happen after an appointment is approved

Possible recipients that came up in earlier conversations (please confirm or rule out each):

- Our own case-tracking product
- Our own medical-records AI product
- Our upstream form-capture product (inbound -- see Q11)
- California state systems: DWC, EAMS
- Insurance-company internal systems (many, each different)
- Third-party claims-administrator systems

---

### Q11. Does the upstream form-capture product send any information INTO the portal when a booking is made?

For example: patient demographics, claim number, employer information, attorney contact details. Does any of that already exist in form-capture and flow into the booking automatically, or does the person booking enter everything from scratch on the portal's booking form?

---

### Q12. What does the downstream case-tracking product expect FROM the portal?

Specifically: when an appointment is approved, what data should flow to case-tracking, in what format, and on what trigger? Or does case-tracking just read from a shared database on its own?

---

### Q13. Does the medical-records AI product get data directly from the portal, or only through case-tracking?

---

## For the end client or product owner

### Q14. Has anyone actually talked to the real users of this portal -- and if so, what did we learn?

If there's existing research, we'd like to read it. If there isn't, we'd like to propose a light round of user interviews before we finalize screen designs.

---

### Q15. Has anyone made verbal promises to the end client about what this portal will do that the developer hasn't been told about?

For example: "we'll also send SMS reminders", "we'll support booking multiple appointments at once", "we'll connect to [a specific insurance-company system]". Anything promised in a sales or kickoff conversation that isn't written down.

---

## Glossary

- **Packet.** The bundle of information the portal puts together when an appointment is approved, and delivers both to the people involved in the case AND to the other software systems that work alongside the portal. The exact contents, formats, and recipients are still being designed; the name itself might change.
- **Doctor's admin.** A staff member at the doctor's office who can approve or reject booking requests, send a request back asking for more information, and cancel or reschedule approved appointments. Other possible names for this role: Office Manager, Scheduler.
- **Company-wide admin.** A central administrator on our side who has authority across every doctor's office using the portal. Can cancel or reschedule any appointment at any practice.
- **Medical practice.** In this portal, one practice run by a medical examiner. It's not an insurance company, not a third-party claims administrator, not a law firm, not an employer. Each practice is treated as its own account; shared reference data (office locations, US states, lookup lists) is the same across all practices.
- **Send-back-for-info.** A third option the doctor's office has when reviewing a booking request, in addition to approve or reject. The request goes back to whoever submitted it, asking for more information, and sits in an "awaiting more info" state until the booker responds.
- **DWC.** California Division of Workers' Compensation. The state agency that administers California's workers'-compensation system.
- **QME / AME / IME.** Three kinds of medical evaluation under California workers'-comp. QME = Qualified Medical Evaluator (a physician certified by the state). AME = Agreed Medical Evaluator (a physician both sides agreed to use in a contested case). IME = Independent Medical Examination (the general umbrella term).
- **PHI.** Protected Health Information. Under HIPAA, any health information linked to an identifiable person.
- **HIPAA.** The US federal law that protects medical information.
- **TPA.** Third-Party Administrator. A company that handles insurance-claim processing on behalf of an insurance carrier or a self-insured employer.
- **BAA.** Business Associate Agreement. A formal contract required by HIPAA between a covered entity and any outside party that handles PHI on its behalf.

---

## Change log

- 2026-04-23 -- first draft.
