---
purpose: Round-5 manager email -- consolidated re-ask of the questions still open from Rounds 3 + 4, plus one net-new question from T13 (tenant URL / access model); each topic reframed into multiple plain-English angles per Adrian's "be creative; multiple questions per real question" guidance
date: 2026-04-27
covers: OUTSTANDING-QUESTIONS.md Q20, Q22, Q23, Q24, Q25, Q26, Q27 + new tenant URL / access question from T13
prior-rounds: round-1 (Q1, Q7, Q9, Q11, Q16); round-2 (Q17, Q18, Q19); round-3 (Q20, Q21, Q22, Q23, Q24, Q25); round-4 (Q26, Q27)
note: Q21 from round-3 is self-resolved (worked through with Adrian on a follow-up call) and does not need an answer. Round-3 / Round-4 questions are repeated here with extra framings so the manager can pick the angle that feels easiest to answer.
---

**Subject:** Patient Portal MVP -- consolidated question round (8 topics)

Hi [Manager],

Following up on the prior question rounds. Below is everything still open on your side, plus one new question that came up this week from work on how new doctor practices get set up. To make answering easier, most topics include multiple ways of asking the same underlying thing -- pick whichever framing is clearest for you, or use them as a starting point. Each section is self-contained so you can hand any of them off to whoever owns that area.

Quick note up front: Q21 from the previous round (about doctor portal logins and the "doctor's manager" role on our side) is no longer waiting on you. Adrian and I worked through it on a follow-up call and you can disregard it.

## 1. Patient onboarding -- when does the patient get their portal account?

(Open since Round 3. Probably the most concrete impact on the build.)

When an attorney or claim examiner books an appointment for a patient who is not yet in our system, when should the patient get the email that lets them log in to the portal?

Three angles -- each one decides the same underlying thing:

- **Timing.** Should the email fire the moment the booking is submitted, or only after the doctor's office has approved the request?
- **Visibility.** Do you want the patient to be able to log in and see their own pending request inside the portal during the office's review window, or is it OK for them to only hear about it through whoever booked on their behalf until approval?
- **Cleanup.** If the office rejects a request, is it OK for the patient to still have a leftover portal account, or should we avoid creating accounts for requests that never got approved?

Sub-questions, smaller but related: how long should the invite link stay valid before it expires? Can the office re-send the invite if the patient missed it? What should happen if the patient's email address has a typo and the invite bounces?

## 2. Doctor profile -- what fields does each doctor's record need to carry?

(Open since Round 3.)

Right now the system captures the doctor's first name, last name, email, and gender. We need to know what additional fields are required for the legal paperwork or the case record.

Three angles to the same answer:

- **Regulatory.** Are there California workers'-comp rules that require specific doctor information on appointment confirmations or in the Packet -- medical credentials, QME / state license number, specialty, practice / office name, anything else?
- **Practical.** When an attorney, claim examiner, or insurance contact reads an appointment confirmation email and sees the doctor's name, what additional information about that doctor would they expect to see for it to feel like a complete, legitimate communication?
- **Helpful but not strictly legal.** What other fields would you want on the profile -- bio, photo, languages spoken, years of experience -- to help the case parties trust the doctor assignment, even if those fields are not strictly required by law?

If anything we have NOT listed is required, please tell us what.

## 3. Doctor onboarding -- is it OK to set up new practices manually?

(Open since Round 3.)

For MVP, our plan is that someone on our side (a host admin) opens an admin screen, types in the new doctor's information (name / email / locations / exam types) plus the assigned account manager, and the system creates the practice account, the doctor profile, and the initial admin login in one go.

Three angles:

- **Acceptance.** Is the manual onboarding flow acceptable for MVP, or do you want a self-service signup or an invite-email flow available from day one?
- **Flexibility.** Would it help if the host admin could also create a "bare-bones" practice by typing only the minimum and then emailing the practice a magic link so they finish their own setup at their own pace?
- **Authority.** Are there scenarios where someone other than the host admin should be allowed to create new practices -- for example, a regional account manager who handles a portfolio of doctors?

## 4. Law-firm fields -- what do we capture on each attorney's profile?

(Open since Round 3.)

Today the system captures firm name, firm address, phone, fax, web address, street, city, zip, and state for each attorney -- all optional. We need you to tell us which of these are actually required.

Three angles:

- **Required vs nice-to-have.** Which fields are mandatory for the legal paperwork or the case record? Which are nice-to-have? Which can we drop?
- **Identifiers.** Is there anything we are missing (bar number, state-bar-registration number, license number) that the case record actually needs?
- **Display.** When an attorney's profile shows up on a notification or in the Packet, which fields should appear by default and which should stay hidden unless asked?

## 5. Law firms with multiple attorneys -- one record or many?

(Open since Round 3.)

If a single law firm has multiple attorneys using our portal for different cases, do we treat them as one firm contact with several attorneys, or as several independent attorney contacts who happen to share a firm name?

Three angles:

- **Notifications.** Would we ever send a case notification to "the firm" at a single firm-wide email, or does every notification always go to a specific individual attorney?
- **Synchronization.** If two attorneys at the same firm update the firm's address or phone, should that change appear on the other attorney's profile too, or are the records independent?
- **Reporting.** When we report on which firms are using the portal, do we count the firm once or count each attorney separately?

## 6. WCAB Office catalogue -- what is it actually for?

(Asked in Round 4.)

The portal has a complete catalogue of California Workers' Compensation Appeals Board district offices. But nothing else in the portal currently links to it -- no appointment, case record, or notification reads it.

Three angles, any one of which gives us the answer:

- **Use case.** Is this list there because staff need it as reference data (e.g., looking up which WCAB office handles a given case), or is there a future feature that will link appointments or case records to a WCAB office?
- **Downstream.** Does Case Tracking, MRR AI, or another Gesco product use this list, with the Patient Portal as the source of truth?
- **Not needed.** Was the WCAB Office feature over-built? Should we retire it or deprioritize until a concrete need surfaces?

(Side note: the export of this list is currently reachable without logging in. No patient data is at risk because WCAB office addresses are public state info, but it is inconsistent with every other export in the portal. We can fix that as part of answering the main question.)

## 7. Appointment Type pre-fill -- which fields does the type drive?

(Asked in Round 4.)

When a booker picks an appointment type on the booking form (e.g., QME Orthopedic, AME Psychiatric), Adrian confirmed it should automatically pre-fill several other fields on the appointment. We need you to tell us which fields.

Three angles:

- **Time and money.** Does the type set a default slot duration (45 minutes vs 60 minutes vs something else) and a default billing rate?
- **Practical preparation.** Does each type carry default prep instructions for the patient (fasting, clothing, imaging to bring) and a list of medical records the case parties must hand over in advance?
- **Schedule constraints.** Are there types that only happen at specific times of day, or types that require a specific kind of room or equipment?

For each field you confirm: is the type's default editable by the booker on a specific appointment, or locked?

## 8. NEW -- how do users find the right doctor's portal?

(New question from this week's work on practice onboarding.)

When a patient, attorney, or insurance staffer needs to interact with the portal for a specific doctor, how do you want them to get there?

Several angles, all landing on the same answer:

- **Branding.** When practices hand out their portal link to clients (on a business card, a follow-up email, or a printed handout), do you want each practice to have its own web address such as "drsmith.gesco-portal.com" or "smith-orthopedic.gesco-portal.com" -- effectively their own branded URL -- or is one shared Gesco-wide web address fine?
- **Navigation.** If the address is shared, would the user pick their doctor from a list (or have it selected for them automatically) after they sign in, or would they always know in advance which doctor they are headed to?
- **Multiple-doctor users.** If an attorney works with several doctors over time, do you want them to have a separate login (and a separate URL) for each doctor, or one login that lists all the doctors they work with?
- **Marketing.** Do practices currently hand out a unique URL to their clients today? If yes, would it be a problem to ask them to switch to a Gesco-wide URL? If no, what URL do they currently use?

The real question underneath is whether the portal lives at one Gesco-wide web address or splits into per-practice web addresses. Pick the framing that is easiest to answer; whichever you settle on, we will design accordingly.

---

## What each answer unblocks on my side

- **1** -- the patient-invite email flow (template count, account-creation timing, expired-request handling).
- **2** -- doctor profile form fields and what shows up on appointment confirmations and Packets.
- **3** -- onboarding UI scope; whether self-service or invite-email signup is also MVP work; whether a regional supervisor / account-manager role can also onboard tenants.
- **4** -- attorney profile form fields and the case-record schema.
- **5** -- whether "law firm" is its own record in the system or just text duplicated across each attorney.
- **6** -- WCAB Office admin UI scope; whether to plan a future linkage now or defer; closes the unauthenticated-export gap.
- **7** -- Appointment Type form schema and the booking form's pre-fill behavior.
- **8** -- the URL structure of the portal at MVP and what marketing materials practices hand out.

Thanks,
Adrian
