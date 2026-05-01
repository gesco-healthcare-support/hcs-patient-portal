---
purpose: Round-3 manager email -- T4 Doctors, T5 Patients, T6 Attorney cluster questions
date: 2026-04-24
covers: OUTSTANDING-QUESTIONS.md Q20, Q21, Q22, Q23, Q24, Q25
prior-rounds: round-1 covered Q1, Q7, Q9, Q11, Q16; round-2 covered Q17, Q18, Q19
---

**Subject:** Patient Portal MVP -- 6 more questions from the doctor / patient / attorney work

Hi [Manager],

Six follow-on questions from this week's work on doctor profiles, patient accounts, and attorney records. All sit with you (or a non-technical product person on your side). Separate from the eight questions I've sent before -- these are net new, no overlap with prior batches.

## Doctor practice onboarding

**1. For MVP, is it OK if we create new doctor practices manually through an admin screen on our side, without an invite-email flow or a public self-service signup?**

Our current plan for MVP: the host admin on our side opens an admin UI, types in the new doctor's name / email / locations / exam types, and clicks "create". The system creates the practice account, the doctor profile, and the initial user account in one go. The doctor's team then logs in.

Please confirm this is acceptable. If we need doctors to onboard via an invite email or self-service signup from day one, that's additional build scope.

**2. Who actually logs into the portal day-to-day on a given doctor's practice -- the doctor themselves, their admin / intake staff, or both? And on our side, is "the doctor's manager" a separate role from host admin, or the same thing?**

My working guess is that the medical examiner themselves likely does NOT log in; the day-to-day portal users are the doctor's admin or intake staff at the practice, plus someone on our side you've been calling "the doctor's manager". I need two things confirmed:

- Should the doctor have a portal login at MVP, yes or no?
- On our side, is "doctor's manager" a separate role from host admin (one of us per doctor or per portfolio), or is it just another way of saying "host admin"?

The answer decides whether a new practice is onboarded with one or two user accounts, and whether we need to define a dedicated Gesco-side account-manager role for MVP.

**3. What profile fields does a doctor's record need to carry, from the case-record or regulatory perspective?**

The system today captures the doctor's first name, last name, email, and gender. I don't know whether that's enough. Candidates we might need to add: medical credentials (MD, DO, specialty certifications), QME or state license number, practice / office name, bio, photo, years of experience, languages spoken.

Which of these are required by California workers'-comp paperwork or expected by the case parties (attorneys, insurance, adjusters) seeing the doctor's name on an appointment confirmation or Packet? Anything we're missing?

## Patient onboarding

**4. When an appointment request is submitted for a patient who doesn't yet have a portal account at that practice, when should the invite email fire?**

Three candidate models, please pick one or correct:

- **On request submit.** Invite fires the moment the booker submits the request. The patient can log in right away, see their pending request, and be a full party to it during the office's review. If the office rejects or the request expires, the patient still has an account (but no appointment).
- **On office approval.** Invite fires only after the doctor's office approves the request. Until approval, the patient has no portal access -- they hear about the request via whoever submitted on their behalf or via the other recipients. Rejected or expired requests never produce a portal account.
- **Both.** Request-submit triggers an informational email to the patient ("a request has been submitted for you") without creating a portal account. Approval triggers the actual portal invite ("click to set up your account and see your appointment"). Two email templates on the patient side; portal access gated to approval.

Related: how long should the invite link stay valid, can the office re-send it if the patient misses it, and what happens if the patient's email has a typo and the invite bounces?

## Attorney records

**5. What law-firm information do we actually need to capture on each attorney's saved profile to satisfy the legal case record?**

The system today captures firm name, firm address, phone, fax, web address, street, city, zip, and state for each attorney -- all optional. I need to know which of these are actually required for the legal paperwork or the case record, which are nice-to-have, and which we can drop.

Also: is there anything NOT on that list that IS required (for example, bar number, state bar registration, license number)?

**6. When a single law firm has multiple attorneys using the portal for different cases, does our team treat them as one firm contact with several attorneys, or as several independent attorney contacts who happen to share a firm name?**

Specifically, in normal operations:

- Would we ever want to send a case notification to "the firm" at a single address, or do notifications always go to a specific individual attorney?
- If two attorneys at the same firm update the firm's address or phone, should the change show up on the other attorney's profile too, or is each attorney's record independent?
- When we report on which firms use the portal, do we count the firm once or each attorney separately?

## What each answer unblocks on my side

- **1** -- onboarding UI scope; whether invite-email or self-service signup is MVP work.
- **2** -- how many user accounts are created per new practice; whether we define a dedicated Gesco-side account-manager role at MVP.
- **3** -- doctor profile form fields, what the system stores, and what shows up on appointment confirmations and Packets.
- **4** -- the patient-invite email flow: template count, what happens during the review window, and account handling for expired or rejected requests.
- **5** -- attorney profile form fields and what the system stores.
- **6** -- whether "law firm" is its own record in the system or just duplicated text on each attorney.

Thanks,
Adrian
