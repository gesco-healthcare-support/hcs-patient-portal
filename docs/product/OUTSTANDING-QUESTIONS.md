[Home](../INDEX.md) > [Product Intent](./) > Outstanding Questions

# Patient Portal -- Questions We Need Answered

Below are the questions currently open on the Patient Portal. Each section is addressed to a specific person (your business / product manager, a lawyer or compliance person, whoever owns how our different products fit together). Sections are self-contained, so any one of them can be copied into an email or meeting agenda on its own.

Items that have been escalated for further consultation live separately in [escalations/open-items.md](escalations/open-items.md). They are kept out of this file so the live-questions list here stays focused on what the direct manager can still answer.

A short glossary at the bottom explains the recurring terms (Packet, doctor's admin, and so on).

---

## Recently resolved

### 2026-04-24 (later -- from the first round of email responses)

- **Q16. Reevaluation flow** -- RESOLVED. Manager described the core dedup flow (match on Name + Date of Birth + SSN; confirmation prompt "We found an existing record..."); fine details added 2026-04-24 in a follow-up with Adrian. Full behaviour -- including dedup scope (reevaluation only), which fields pre-fill (patient PII only), the case-number / confirmation-number field on the form, and the claim-level "same case" semantics -- is captured in the `appointments.md` Reevaluation-flow subsection.
- **Q17. Reserved slot status** -- RESOLVED. Manager validated the pending-review interpretation ("you might be correct") Adrian proposed during T3. Reserved = slot with a pending appointment request awaiting office review; state-transition model confirmed (Available -> Reserved on submit; Reserved -> Booked on approve; Reserved -> Available on reject or send-back-expires). Captured in `doctor-availabilities.md`.
- **Q18. Slot duration model** -- RESOLVED. 15 min is the default; the office can change duration in the new-slot publishing form before publishing. Once published and booked, duration is locked per the T7 universal post-submit lock rule. Matches the "independently set per slot" candidate model from the original question. Captured in `doctor-availabilities.md`.

### 2026-04-24 (earlier -- from the T2 resolution round)

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

### Q23. When an appointment request is submitted for a patient who doesn't yet have an account at that tenant, when should the portal invite email fire?

Three candidate models to pick from (please pick one or correct):

- **On request submit.** The invite fires the moment the booker submits the request. The patient can log in right away, see their pending request, and be a full party to it during the office's review window. If the office rejects or the request expires, the patient still has an account (but no appointment).
- **On office approval.** The invite fires only after the doctor's office approves the request. Until approval, the patient has no portal access -- they hear about the request via whoever submitted on their behalf or via the other recipients. Rejected or expired requests never produce a portal account.
- **Both.** Request-submit triggers a plain informational email to the patient ("a request has been submitted for you") without creating a portal account. Approval triggers the actual portal invite ("click to set up your account and see your appointment"). Two email templates on the patient side; portal access gated to approval.

This decides whether the patient is reachable through the portal during the review window, whether rejected requests produce orphan accounts, and how many email templates we need on the patient side.

Related but separate: (a) how long is the invite link valid before it expires, (b) can the office re-send the invite if the patient misses it, (c) what if the patient's email address has a typo and the invite bounces?

---

### Q22. What profile fields does a doctor need to carry, from the case-record and regulatory-compliance perspective?

The code today captures first name, last name, email, and gender. The developer doesn't know whether that's enough for the MVP case record / regulatory paperwork. Candidates we'd consider adding if required: medical credentials (MD, DO, specialty certifications), QME / state license number, practice / office name, bio, photo, years of experience, languages spoken.

Please advise which of these are required by California workers'-comp paperwork or expected by the case parties (attorneys, insurance, adjustors) seeing the doctor's name on an appointment confirmation or Packet.

---

### Q21. Who actually logs into the portal day-to-day -- the doctor themselves, the doctor's admin or intake staff, and/or Gesco-side account managers?

**Status 2026-04-24 (RESOLVED via T10 interview):**

- (a) **Doctors do not get a special role at MVP.** A doctor who wants their own login receives an additional Practice Admin account at their tenant -- same authority and same data access as the practice's other admin staff. There is no separate "Doctor" role.
- (b) **"Doctor's manager at our side" is the Supervisor Admin role -- a distinct Gesco-side mid-tier role,** sitting between host admin and Practice Admin in the authority hierarchy. Same authority as host admin but scoped to an explicitly assigned portfolio of practices; cannot create new tenants; assigned by host admin.

Captured fully in `docs/product/cross-cutting/auth-and-roles.md` (T10).

The developer's leaning (2026-04-24) is that the medical examiner themselves probably does NOT log in; the portal's day-to-day users are the doctor's admin or intake staff (at the practice) and "the doctor's manager at our side" (on Gesco's side). That last phrase needs clarification: is it the same as the already-defined host admin (one Gesco superuser across all tenants), or a more specific role (a Gesco account manager who handles a portfolio of specific doctors)?

Please confirm:

- Should we plan for the doctor to have a portal login at MVP, yes or no?
- On the Gesco side, is "doctor's manager" a separate role from host admin, or the same thing?

The answer decides whether each practice is onboarded with one user (just the admin) or two (admin + doctor), and whether a separate Gesco-side "account manager" role needs to be defined for MVP.

---

### Q20. For MVP, is it OK for the host admin to create new doctor practices manually through an admin screen (no invite flow, no self-service signup)?

The developer's MVP call (2026-04-24) is yes -- host admin creates the tenant, the doctor's profile, and the initial user account by hand; invite-email or self-service signup flows for doctors are deferred until after MVP.

Please confirm this is acceptable for MVP. If we need an invite or self-service flow for doctors at MVP, that's a meaningful additional build item.

---

### Q25. When a single law firm has multiple attorneys using the portal for different cases, does our team treat them as one firm contact with several attorneys, or as several independent attorney contacts who happen to share a firm name?

Specifically, in normal operations:

- Would we ever want to send a case notification to "the firm" at a single address, or do notifications always go to a specific individual attorney?
- If two attorneys at the same firm update the firm's address or phone, should the change show up on the other attorney's profile too, or is each attorney's record independent?
- When we report on which firms are using the portal, do we count the firm once or count each attorney separately?

The answer decides how the portal stores firm info: either shared across attorneys at the same firm (one firm record, many attorney records) or duplicated per attorney (each attorney carries their own copy of the firm's details).

---

### Q24. What law-firm information do we actually need to capture on each attorney's saved profile to satisfy the legal case record?

The code today captures firm name, firm address, phone, fax, web address, street, city, zip, and state for each attorney -- all optional. We need to know which of these are actually required for the legal paperwork or the case record, which are nice-to-have, and which we can drop.

Also: is there anything NOT on that list that is required (for example, bar number, state bar registration, license number)?

---

### Q26. What is the intended MVP role of the WCAB Office catalogue in the portal?

The portal has a complete `WcabOffice` entity (California Workers' Comp Appeals Board district-office records -- name, abbreviation, address, active / inactive flag) with admin CRUD, an Angular admin screen, and an Excel export. But no other feature references it -- no appointment, no case record, no notification reads the list. Adrian does not personally know what WCAB offices are meant to do inside the Patient Portal.

Candidates we would consider:

- **Data directory only.** Staff reference the list for case context (e.g., to know which WCAB handles a given case). No live business linkage; no FK from Appointment.
- **Planned future linkage.** The catalogue is seeded in advance because an upcoming feature will link an appointment or case record to a WCAB office.
- **Downstream consumption.** Case Tracking, MRR AI, or another Gesco product consumes this list and the Patient Portal is the source of truth.
- **Not needed at MVP.** The feature was over-built; retire it or deprioritise until a concrete need arises.

Please tell us what the catalogue is actually for -- or whether we should drop it -- and we will scope the admin UI and any downstream linkage accordingly.

Related but smaller: the Excel export on this list is currently reachable without logging in (marked `[AllowAnonymous]` in code). No PHI is at risk (WCAB offices are public DWC data), but the pattern is inconsistent with every other export in the portal. We can close that gap as part of answering the main question.

---

### Q27. When a booker picks an Appointment Type on the booking form, which specific fields on the appointment does the type pre-fill?

Adrian confirmed 2026-04-24 that picking an Appointment Type drives multiple other fields on the appointment (it is not just a classification label). He did not know which specific fields; this one is for you.

Candidate fields we would consider:

- **Default slot duration** -- e.g., QME Orthopedic defaults to 45 minutes, AME Psychiatric to 60 minutes.
- **Default billing rate / pricing** -- each type carries its own rate.
- **Required prep documents** -- certain types require specific medical records handed over in advance.
- **Default prep instructions for the patient** -- e.g., fasting, wearing loose clothing, bringing current imaging.
- **Default time-of-day alignment** -- some types only happen at specific times.

Please tell us which of these the type should actually drive, plus anything we have missed. For each field you confirm, we also need to know whether the type's default is editable by the booker on a specific appointment or locked.

---

## For legal or compliance

### Q7. Which California workers'-comp rules apply to how this portal schedules appointments, sends notifications, and hands off data?

Rules that look relevant from our research:

- 8 CCR section 31.3 -- QME exams must be scheduled within 90 days of the request.
- 8 CCR section 34 -- at least 6 business days' notice required before cancelling an exam.
- 8 CCR section 35 -- records must be exchanged 20 days before the evaluation.

Do all of these apply? Are there others we're missing? And do non-QME exams (AMEs and other IMEs) follow a different set of rules?

**Status 2026-04-24:** the manager flagged this needs further research because some rules have already changed. Actively in progress on the manager's side; awaiting the updated summary.

---

### Q9. What exact format does each required notification email need to follow so it counts as defensible evidence of communication?

The developer confirmed 2026-04-24 that most of the notifications the portal sends (on booking submit, on approve / reject / send-back-for-info, and likely on modifications) are **legally-required** communications with **strict formats** designed so there is evidence of communication and no impermissible ex-parte communication. The remaining question is: what are those strict formats, per event and per party-type (patient, applicant attorney, defense attorney, insurance company, claim adjustor, doctor's office)? Existing legal-comms templates or examples -- if the firm or client has them -- would unblock the MVP email-template build.

Also: are there audit-trail, retry-on-bounce, delivery-receipt, or logging expectations attached to the legal-evidence standard?

**Status 2026-04-24 (partial answer):** the direct manager confirmed a related UX expectation -- after each notification event the system should show a clear success confirmation to the booker ("A confirmation email has been sent successfully"). That success-UX intent is folded into `appointments.md`. The exact per-event / per-party-type legal-compliant format remains open and has been moved to [escalations/open-items.md](escalations/open-items.md) (Item 2) for further consultation.

---

## For whoever owns how our products fit together

### Q11. Does the upstream form-capture product send any information INTO the portal when a booking is made?

The developer confirmed 2026-04-24 that the **tenant** a patient books under is **pre-decided upstream** (it is not an open choice on the booking form), so at least the tenant-assignment piece already comes from outside the portal. The remaining question: does form-capture also pre-fill any other fields (patient demographics, claim number, employer information, attorney contact details) into the booking, or is the booking form the sole data origin for everything else?

**Status 2026-04-24:** the manager flagged that an in-person conversation is needed to get through this one -- the phrasing and scope of the question doesn't translate well in writing. Pending Adrian's in-person discussion with the manager.

---

## Escalated items (tracked separately)

The following items have been moved to [escalations/open-items.md](escalations/open-items.md) for further consultation. They remain open; they are tracked out-of-band from this file.

| Item | Topic | Original question |
| ---- | ----- | ----------------- |
| 1 | Cancel / reschedule MVP scope | formerly Q1 |
| 2 | Exact format of each required notification email | Q9 format specifics (success-UX intent already answered; format remains open) |
| 3 | Direct edits on booked slots | formerly Q19 |

---

## Glossary

- **Packet.** The bundle of appointment information the portal collects on the booking form and delivers out. At MVP the delivery is by email only, to every party on the case. Structured hand-offs to other software systems (case tracking, medical-records AI, state systems, carriers) are post-MVP.
- **Doctor's admin.** A staff member at the doctor's office who can approve or reject booking requests, send a request back asking for more information, and cancel or reschedule approved appointments. Other possible names for this role: Office Manager, Scheduler.
- **Company-wide admin.** A central administrator on our side who has oversight and break-glass authority across every doctor's practice using the portal. Can cancel or reschedule any appointment at any practice. Meant for when something goes wrong at a practice-level that needs to be corrected from outside the practice.
- **Medical practice.** In this portal, one practice run by a medical examiner. It's not an insurance company, not a third-party claims administrator, not a law firm, not an employer. Each practice is treated as its own account. Office locations are tenant-specific -- each practice has its own list of office locations, and users see only their own practice's locations (this is the intent; the code currently has Locations as host-scoped, and the tenant-scoping change is a follow-up build item). The other shared reference data (US states, appointment types, appointment languages, appointment statuses) is a common base list maintained by Gesco; each practice can hide entries it does not use from its own users but cannot add its own entries. The WCAB Office catalogue's role is pending manager input (see Q26 above).
- **Send-back-for-info.** A third option the doctor's office has when reviewing a booking request, in addition to approve or reject. The request goes back to whoever submitted it, asking for more information, and sits in an "awaiting more info" state until the booker responds.
- **Ex-parte communication.** California workers'-comp concept: one party taking a case-affecting action without every other party being informed. Impermissible. The portal's notification pattern (all parties emailed on every event) exists specifically to keep the matter out of ex-parte territory.
- **DWC.** California Division of Workers' Compensation. The state agency that administers California's workers'-compensation system.
- **QME / AME / IME.** Three kinds of medical evaluation under California workers'-comp. QME = Qualified Medical Evaluator (a physician certified by the state). AME = Agreed Medical Evaluator (a physician both sides agreed to use in a contested case). IME = Independent Medical Examination (the general umbrella term).
- **TPA.** Third-Party Administrator. A company that handles insurance-claim processing on behalf of an insurance carrier or a self-insured employer.

---

## Change log

- 2026-04-24 (T10 Auth-and-roles cross-cutting) -- new file `docs/product/cross-cutting/auth-and-roles.md`. Resolutions in session via Adrian interview: (a) **Practice roles** (Q-T10-1) -- one role per practice ("Practice Admin" / "Doctor's Admin" / "Staff Admin"); all practice-side staff share full within-tenant authority. (b) **Doctor portal login** (Q-T10-2; resolves **Q21 part 1**) -- doctors do not get a special role at MVP; a doctor who wants their own login gets an additional Practice Admin account with the same authority and data access. (c) **Practice Admin account creation post-onboarding** (Q-T10-3) -- host admin OR the assigned supervisor admin can add Practice Admin accounts; Practice Admin staff cannot self-add other admins. (d) **Patient auth flow for auto-created accounts** (Q-T10-4; resolves **research Q12**) -- magic-link invite when booker is not the patient; patient sets their own password during the first session. No password ever in plain email. (e) **Supervisor Admin role** (Q-T10-clarification + Q-T10-5 + Q-T10-6; resolves **Q21 part 2**) -- a Gesco-side mid-tier role between host admin and Practice Admin. Same authority as host admin but scoped to an explicitly assigned portfolio of practices; cannot create new tenants; assigned by host admin. The portal now has a **7-role catalogue**: 3 admin tiers (host > supervisor > practice) + 4 external user roles (patient, applicant attorney, defense attorney, claim examiner). (f) **Authentication method** (Q-T10-7; resolves **research Q9**) -- password-only at MVP; no social / OAuth / SSO providers wired in. Q21 (full question) is now RESOLVED and annotated in place. Q12 and Q9 from the original research list are also resolved in this round. T9 (`multi-tenancy.md`) amended with a Supervisor Admin bullet in the Personas list pointing to T10 for the full hierarchy. New code gaps captured in Known Discrepancies on the cross-cutting auth-and-roles file: Supervisor Admin role does not exist in code; portfolio assignment table is missing; permission checks do not account for the supervisor tier; magic-link auth flow is unbuilt; AppointmentAccessor admin UI exists but intent at MVP per T6 is no ad-hoc grants; defense attorney symmetry gap (T6); one-applicant-attorney-per-appointment is not enforced; audit-log review UI is absent. Outstanding items (queued for Adrian or post-MVP): 2FA / MFA at MVP for admin tiers; audit log review surface; Practice Admin removal flow; Q23 invite-fire timing (still queued for manager).
- 2026-04-24 (T9 Multi-tenancy cross-cutting) -- new file `docs/product/cross-cutting/multi-tenancy.md`. No new manager-bound questions; no new escalations. Resolutions in session via Adrian interview: (a) **Confirmation number scope** (Q3 from the original research list, never previously answered) -- globally unique across the portal; counter increments across all tenants; `A` + 5-digit format; `A00042` names exactly one appointment system-wide. (b) **External user accounts across tenants** (Q-T9-2) -- one login per practice; same human at multiple practices gets multiple separate IdentityUsers; no cross-tenant identity, no auto-link by email; email uniqueness enforced per-tenant. (c) **Host admin authority** (Q-T9-3) -- full operational authority across all tenants; can perform any action any tenant admin can perform, in any tenant, at any time; no read-only restriction or per-action approval gating. (d) **Tenant decommission** (Q-T9-4) -- archive in place; tenant marked inactive on doctor retirement / departure; data retained read-only; new bookings and new logins blocked; in-flight cases continue off-portal. (e) **Host-admin audit** (Q-T9-5) -- every host-admin action inside any tenant audit-logged with timestamp, user, action verb, target, and tenant context. The cross-cutting file consolidates these decisions plus the prior tenant-vs-host scoping rulings (T8 lookup partitioning, T7 universal post-submit lock, T5 Patient one-record-per-tenant, T4 one-Doctor-per-tenant, T0 tenant unit definition). Code gaps captured in Known Discrepancies on the cross-cutting file: Locations tenant-scoping refactor pending; per-tenant visibility flag for the four common entities is intent-only; Patient IMultiTenant gap (FEAT-09); confirmation-number generator is per-tenant in code (needs host-scope re-anchoring); AppointmentStatus drop pending; full-audit-on-host-admin instrumentation pending; tenant-archive flow has no code mechanism. Also flagged: `docs/business-domain/DOMAIN-OVERVIEW.md`'s Global-vs-Tenant diagram is now out of date (out of T9 scope to modify; the cross-cutting file is the canonical source going forward).
- 2026-04-24 (T8 Lookup cluster) -- six files drafted: `locations.md`, `states.md`, `wcab-offices.md`, `appointment-types.md`, `appointment-languages.md`, `appointment-statuses.md`. Major resolutions via Adrian interview (Q-A, Q-A2, Q-A3, Q-I, Q-K, Q-M and their follow-ups): (a) **Locations are TENANT-SPECIFIC**, not host-scoped. Host admin creates the initial list at tenant onboarding; doctor's admin edits ongoing. Users see only their tenant's locations. Substantial intent-vs-code gap -- code has `Location` host-scoped -- needs `IMultiTenant` + data migration in follow-up work. (b) **States / Appointment Types / Appointment Languages are common global + per-tenant HIDE/SHOW**. Host admin owns the base list; tenants can hide entries they do not use but cannot add their own. Per-tenant visibility flag is intent-only, not in code. (c) **Appointment Language is a workflow trigger, not storage-only**. Non-English on the Patient record legally requires Gesco to arrange a translator and notify all case parties so scheduling can happen; English is a hard exclusion (no English-to-English translation). Entirely unbuilt in code. (d) **Appointment Type drives multiple fields** on the appointment; specific pre-fill fields queued via Q27 (manager). (e) **`AppointmentStatus` entity is DROPPED at MVP** -- enum + existing `en.json` localization already covers display labels. Resolves the original Q2 seed (enum vs lookup table). Follow-up build item removes the entity, AppService, DTOs, permissions, Angular UI, migration. (f) **Portal's operational lifecycle scope narrowed to pre-approval + reschedule / cancel only**; post-approval states (CheckedIn, CheckedOut, Billed, NoShow, CancelledLate) are downstream concerns. `appointments.md` (T2) carries the full 13-state lifecycle; T11 cross-cutting doc needs to re-scope to match this portal-scope boundary. (g) **WCAB offices' MVP role is unknown to Adrian**; queued via Q26 (manager). New active manager questions added: Q26 (WCAB purpose + the incidental `[AllowAnonymous]` Excel-export gap) and Q27 (Appointment Type pre-fill fields). Tensions flagged (NOT silently reconciled): the OUTSTANDING-QUESTIONS.md glossary line "shared reference data ... the same across all practices" predates the Q-A / Q-A2 answers and should read "common base with per-tenant hide/show" for States / Types / Languages / Statuses and "tenant-specific" for Locations; the root CLAUDE.md's "intentionally shared across all tenants" wording for Locations also diverges from intent. Glossary and root CLAUDE.md corrections are OUT of T8 scope (T8 only appends this change-log entry); those updates will need a separate turn. Methodology note: T8 initially drafted from code research + inline-seeds without asking Adrian; he corrected mid-session. New memory at `feedback_docs_product_interview_driven.md` captures the rule -- intent docs are ALWAYS interview-driven; the `feedback_research_decide_dont_ask.md` rule applies only to code-build flows.
- 2026-04-24 (email-responses round) -- processed manager responses to round-1 and round-2 emails. Resolved: Q16 (reevaluation flow -- manager described dedup, Adrian added fine details on case-number field + pre-fill scope), Q17 (Reserved = pending-review state, manager-validated), Q18 (15 min default at publishing, no type-derived duration). Escalated to `escalations/open-items.md`: Q1 (cancel/reschedule scope), Q9 format specifics (success-UX partial answer stays here), Q19 (direct edits on booked slots). Status updates: Q7 manager is actively researching (some rules changed), Q11 needs in-person conversation with manager. Introduced the escalation folder so PN-bound items are tracked separately from the regular list.
- 2026-04-24 (T7 AppointmentEmployerDetails) -- no new manager questions added directly. Resolved in session: (a) employer info is required on every booking, no exceptions; retired / self-employed / unemployed patients still name the employer relevant to the claim; (b) whether the employer is a notification recipient depends on the case -- self-insured or directly-involved employers are parties, off-case employers are just data on file; entity needs an Email field and the appointment needs a "notify employer" flag; (c) post-submit changes to ANY form-captured data (patient, attorneys, employer, insurance, etc.) require Gesco-side admins running the proper process. This (c) answer RESOLVES the T4/T5 tension previously flagged: T4's "practice-side doctor's admin can make form-data edits" authority applies pre-submit only; after request-submit the form data is locked and all changes are Gesco-side. appointments.md, patients.md, and appointment-employer-details.md updated accordingly.
- 2026-04-24 (later) -- Attorney cluster session (T6) added Q24 (required attorney firm-profile fields for the legal record) and Q25 (one-firm-with-many-attorneys vs each-attorney-is-its-own-contact). Resolved directly in session: (a) defense attorneys get symmetric treatment to applicant attorneys, with saved firm profiles and the same booking flow -- the current code's asymmetry is an intent gap, not a design choice; (b) no ad-hoc access grants at MVP -- portal access is strictly tied to legal-party membership, and staff use the party's registered credentials; (c) one applicant attorney per appointment at MVP, no co-counsel. Claim Examiner role (Q4 from the original research list) remains minimally wired at MVP: CE can book rarely for self-represented patients (per T2), receives all-parties notifications, and sees appointments where they are a legal party -- no dedicated dashboard or CE-specific actions at MVP. Internal methodology note: architecture-framed questions must be reshaped into business-behaviour questions before they go to managers; see `feedback_business_questions_not_architecture_questions` memory.
- 2026-04-24 (later) -- Patients session added Q23 (when the patient invite email fires -- on request submit, on approval, or both). Flagged a T4/T5 tension for T10 resolution: T4 said practice-side doctor's admin can edit form data on booker request; T5 says patient-data changes post-submit require Gesco-side admins, not practice-side. Boundary to be pinned down in the Auth-and-Roles cross-cutting session. (This tension was resolved 2026-04-24 during T7; see above.)
- 2026-04-24 (later) -- Doctors session added Q20 (host-admin-only onboarding MVP OK?), Q21 (doctor login + Gesco-side "manager" role), Q22 (required doctor profile fields for case record / regulatory).
- 2026-04-24 (later) -- DoctorAvailabilities session added Q17 (Reserved slot status), Q18 (slot duration model), Q19 (direct edits on booked slots). Q17, Q18 now resolved; Q19 escalated.
- 2026-04-24 -- resolution round. Q2, Q3, Q4, Q5, Q6, Q8, Q10, Q12, Q13, Q14, Q15 moved to Recently resolved (closed or deferred per answers and scope decisions). Q9 narrowed from "are notifications required" to "what is the exact format per event / party". Q11 narrowed (tenant pre-decided; remaining pre-fill question still open). Q16 annotated with the developer's rough working guess. Glossary trimmed.
- 2026-04-23 -- first draft.
