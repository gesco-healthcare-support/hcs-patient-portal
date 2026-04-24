[Home](../INDEX.md) > [Product Intent](./) > Appointments

# Appointments -- Intended Behavior

**Status:** draft -- Phase 2 of intended-behavior work, interview in progress
**Last updated:** 2026-04-22
**Primary stakeholder:** [UNKNOWN -- queued for Adrian (this doc builds as the interview proceeds)]

> This document captures INTENDED behaviour for the Appointments feature -- what booking is supposed to do in the Patient Portal's MVP. It does NOT describe what the code currently does (that is `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md` and `docs/features/appointments/overview.md`). Every claim carries a source tag. Code is never cited as authoritative for intent. Observations from code appear ONLY in the Known Discrepancies section, tagged `[observed, not authoritative]`.

## Purpose

The Appointments feature moves a booking from a booker (applicant attorney, patient, claim examiner, or defense attorney) through a medical-examiner practice's review and -- on approval -- delivers the appointment's data to every case party via email in strict, legally-compliant formats that serve as evidence of communication (per California workers'-comp ex-parte rules). At MVP the portal is **appointment-booking-only, not case-tracking** (Adrian 2026-04-24): the day-of-exam lifecycle, structured hand-offs to downstream softwares, and specific software-to-software integration formats are all post-MVP. Appointments is nonetheless the core product surface because data capture on the booking form AND legally-required notifications to parties are the two things the portal MUST do for it to play its role in Gesco's workers'-compensation pipeline. [Source: Adrian-confirmed 2026-04-23 on product purpose, 2026-04-24 on MVP scope boundary]

## Personas and goals

Persona definitions live in [00-BUSINESS-CONTEXT.md](00-BUSINESS-CONTEXT.md). This section captures feature-specific goals, portal experience, and role authority per persona, per Adrian 2026-04-22 / 2026-04-23.

### Booker personas

All four booker personas are first-class at MVP per [00-BUSINESS-CONTEXT.md](00-BUSINESS-CONTEXT.md), but their goals, portal experience, and expected traffic share differ meaningfully. [Source: Adrian-confirmed 2026-04-23]

#### Patient (injured worker)

Together with the applicant attorney, the patient drives the majority of portal traffic at MVP.

- **Onboarding.** The patient is invited to the portal by email with their tenant already selected. Tenant discovery is deliberately controlled: the patient does not browse a list of tenants or type one in, because (a) they may not know the exact name of their examiner's practice as stored in the system, and (b) exposing the full tenant list would reveal medical-practice relationships protected by legal / medical-privacy norms. [Source: Adrian-confirmed 2026-04-23]
- **Registration / login.** The patient completes account creation from the invite; subsequent visits use standard login. [Source: Adrian-confirmed 2026-04-23]
- **Dashboard view (limited scope, patient role).** Two action buttons -- **Book an appointment** and **Book a reevaluation** -- plus a list of this patient's own appointments at this specific tenant (active and completed). The patient sees nothing else; no menus, pages, or data for any other area of the portal. [Source: Adrian-confirmed 2026-04-23]
- **Booking-form fill.** Patient fills their own personal information, their insurance and adjustor information, and their applicant-attorney and defense-attorney information. The self-represented exception path is specified in "Business rules" and "Edge cases". [Source: Adrian-confirmed 2026-04-23]
- **Reevaluation flow.** Reached from the "Book a reevaluation" button. This is a distinct booking type, not a second first-time appointment. Form design and rules are [UNKNOWN -- queued for manager: what counts as a reevaluation in our product's sense, how does it differ from a first-time booking, who is eligible, what fields does it capture, what notifications does it trigger?].

#### Applicant attorney

Represents the injured worker in the workers'-compensation matter. Together with the patient, drives the majority of portal traffic.

- **Registration, login, and per-booking form flow** mirror the patient's, with the difference that an attorney manages many cases rather than one. [Source: Adrian-confirmed 2026-04-23]
- **Multi-patient management.** The attorney can book appointments for multiple patients and see all of them in a list. The list is **tenant-scoped**: an attorney who works with several tenants (several medical-examiner practices) sees a separate per-tenant list in each tenant context, NOT a unified cross-tenant inbox. [Source: Adrian-confirmed 2026-04-23]

#### Defense attorney

Represents the employer or carrier in the contested matter. Can book appointments, but does so **uncommonly**; most bookings originate from the patient / applicant-attorney side. [Source: Adrian-confirmed 2026-04-23]

- **Registration, login, and form flow** mirror the applicant-attorney flow. [Source: Adrian-confirmed 2026-04-23]
- **When defense attorney books:** the defense attorney must still enter the applicant attorney's information as part of the booking. Any action one party takes must be visible to every other party on the case so the matter stays on the right side of California's **ex-parte-communication rule** (see Business rules). Failing to inform all parties is a legal risk, not just a UX miss. [Source: Adrian-confirmed 2026-04-23]

#### Claim examiner

Insurance-side claim-file manager. Can book appointments but does so **very rarely** -- generally only on behalf of patients who are self-represented (no attorney). Even in that case, most self-represented patients book their own appointments, so claim-examiner bookings are edge-case traffic at MVP. [Source: Adrian-confirmed 2026-04-23]

#### Rough traffic distribution at MVP

Adrian's 2026-04-23 rough estimate, pending real usage data or firmer business / client estimate:

1. Patients + applicant attorneys -- majority of portal traffic.
2. Defense-attorney bookings -- uncommon.
3. Claim-examiner bookings -- very rare; mostly for self-represented patients, who in turn usually self-book anyway.

[Source: Adrian best-guess 2026-04-23 -- NEEDS CONFIRMATION against real usage data or business estimate]

### Non-booker personas

#### Examiner office staff

Within the office, a dedicated tenant-level admin role (Adrian's working handle: **doctor's admin**; other working names raised: Office Manager, Scheduler, TenantAdmin) handles four appointment-related responsibilities at MVP: [Source: Adrian-confirmed 2026-04-22, extended 2026-04-23 and 2026-04-24]

1. **Review decisions.** Approve, reject, or send-back-for-info on pending appointment requests by reading the information the booker provided on the form.
2. **Phone / email intake.** When someone calls or emails the office wanting to book an appointment rather than using the portal, office staff book it themselves on the caller's behalf.
3. **Modifications.** Cancel or reschedule approved appointments (subject to the MVP-scope caveat in the Modifications subsection below).
4. **Form-data edits.** Make changes to the appointment form data when a booker requests a change (e.g., updated contact info, corrected attorney info).

The medical examiner (Doctor role) can also approve / reject / send-back on the review queue. Other staff users inside the tenant can VIEW the queue for scheduling context but cannot take any of the four actions above. [UNKNOWN -- queued for Adrian: is the final role name one role that owns all four actions, or split across two or more? Do the working handles `doctor's admin` / `Office Manager` / `Scheduler` refer to the same role?]

#### Host admin (Gesco-side)

Gesco-internal superuser for the portal, with unconditional cross-tenant authority. The host admin exists for two related reasons: [Source: Adrian-confirmed 2026-04-23, extended 2026-04-24]

1. **Oversight.** General administrative visibility across all tenants that no other portal user has.
2. **Break-glass access.** When something goes wrong with an appointment at a tenant -- whether the doctor's office cannot or will not act, or a higher-level correction is needed -- the host admin can step in and modify the appointment directly. Cancel / reschedule authority is unconditional across every tenant.

No appointment-approval authority in the doctor's review queue (that belongs to the tenant-level admin). Modification authority is universal.

## Intended workflow

### The MVP pipeline (what the app is for)

The Patient Portal's reason to exist inside Gesco's business and the California workers'-compensation system is to move a booking request through a medical-examiner practice AND deliver the resulting appointment's data -- the **"Packet"** (Adrian's working name; final term TBD) -- to the other parties on the case and to the other softwares that operate alongside the portal in the pipeline. Without the Packet hand-off to both audiences, the portal does not function in its business role. [Source: Adrian-confirmed 2026-04-23]

The MVP pipeline, end to end:

1. **Registration** -- external users (patient, applicant attorney, claim examiner, defense attorney) sign up.
2. **Login** -- users authenticate.
3. **Appointment slot creation** -- the doctor's office publishes availability.
4. **Appointment booking** -- a booker submits a request against a slot.
5. **Booking confirmation and modification notifications** -- email notifications to all case parties at submit, approval, and modifications.
6. **Appointment approvals** -- the doctor's office reviews the request (approve / reject / send-back-for-info).
7. **Data delivery to all case parties via email** -- on approval, the appointment's data is delivered to every case party by email in a strict, legally-compliant format. The email is the MVP realization of what Adrian calls the "Packet" concept -- it is the portal's hand-off of data to the parties. Structured hand-offs to other softwares in the pipeline (Case Tracking, MRR AI, DWC/EAMS, insurance-carrier systems) are deferred; see Post-MVP deferrals below.

[Source: Adrian-confirmed 2026-04-23 on purpose; narrowed 2026-04-24 to email-only for MVP]

Appointments as a feature owns steps 4-7. Steps 1-2 are owned by the Auth-and-Roles cross-cutting concern; step 3 is owned by DoctorAvailabilities.

### Post-MVP deferrals (confirmed 2026-04-24)

These are explicitly out of MVP scope per Adrian 2026-04-24 and are listed here so the MVP boundary is unambiguous:

- **Day-of-exam lifecycle** -- `CheckedIn`, `CheckedOut`, `Billed`, `NoShow` states and their transitions. "This is an appointment portal, it handles appointment booking, not case tracking." The enum retains these values for long-term intent (per `APPOINTMENT-LIFECYCLE.md`) but no MVP work implements them.
- **Structured Packet delivery to other softwares** -- API integrations, file drops, webhooks, or any non-email delivery channel to Case Tracking, MRR AI, DWC/EAMS, carrier systems, or TPA systems. "As long as we collect the data, we can format it as required" -- Adrian 2026-04-24. MVP collects data; post-MVP builds the software-specific format translators and delivery channels.
- **HIPAA formal classification + broader California medical-privacy review** (CMIA, SB 446, CCPA/CPRA) -- revisited during the limited-user-testing phase after MVP.
- **MRR AI integration** -- that project is not active; integration details will be defined after the MRR AI project resumes.

[Source: Adrian-confirmed 2026-04-24]

### Booking flow (MVP)

Booking is a **two-step request/review flow** with **notification to all case parties** at both steps. [Source: Adrian-confirmed 2026-04-22]

**Step 1 -- Booker submits an appointment request.**

Any of the four booker personas (applicant attorney, patient, claim examiner, defense attorney) fills out the booking form and submits. The submission creates an appointment **request** (not a confirmed booking) in the database. The system then emails **all parties involved in the case**, which at minimum comprises: [Source: Adrian-confirmed 2026-04-22]

- The patient / client
- The applicant attorney
- The defense attorney
- The insurance company (a contact for the carrier or TPA itself, distinct from the specific adjustor on the case) [Source: Adrian-confirmed 2026-04-24]
- The claim adjustor / claim examiner (the specific individual from the insurance company handling this case) [Source: Adrian-confirmed 2026-04-24]
- The doctor's office (examiner + their office staff)

**Step 2 -- Doctor's office reviews the request.**

The doctor's office receives the request in their notification email and also sees it in the portal (review queue). They take one of three actions: [Source: Adrian-confirmed 2026-04-22]

1. **Approve** -- request becomes a confirmed appointment.
2. **Reject** -- request is declined.
3. **Send back requesting more information** -- request moves to a distinct "Awaiting more info from booker" status, visible to the booker in the portal. The office's review queue shows these in a separate bucket from fresh requests. The booker sees a "your request needs more information" screen, can edit their submission and respond. On response, the request re-enters the office's active review queue. [Source: Adrian-confirmed 2026-04-22]

The status enum today has 13 values; this confirmed intent introduces a 14th concept (working name: `AwaitingMoreInfo`). The 13-value enum is therefore incomplete relative to MVP intent; the state machine / enum expansion is MVP-blocking work. [Source: Adrian-confirmed 2026-04-22]

**Open:** [UNKNOWN -- queued for Adrian: what specifically can the office ask for? Is there a free-text note the office writes ("please provide injury date" etc.), a structured list of fields they can flag as missing, or both? Affects whether we need a message/notes entity per request.]

On each decision, the system emails **all the same parties** (patient, applicant attorney, defense attorney, insurance people, claim adjustors) notifying them of the decision. [Source: Adrian-confirmed 2026-04-22]

All events (submission, decision, resulting status) are persisted to the database. [Source: Adrian-confirmed 2026-04-22]

**Steps 3+ (post-approval)** -- **NOT IN MVP.** The portal is appointment-booking-only; the day-of-exam states (`CheckedIn`, `CheckedOut`, `Billed`, `NoShow`) and their transitions are deferred. No-show handling, check-in, check-out, and billing workflows are post-MVP concerns; they will live in downstream products or in the office's own processes at MVP time. [Source: Adrian-confirmed 2026-04-24]

### Reevaluation flow

A distinct second booking type, reached from the patient's "Book a reevaluation" dashboard button (see Personas > Patient). Reevaluation is not treated as a repeat first-time appointment; it is a separate flow with its own form and rules. [Source: Adrian-confirmed 2026-04-23 on existence]

Design details are [UNKNOWN -- queued for manager: what counts as a reevaluation in the product's sense, who is eligible to book one, which fields the form captures, and whether the approval / notification / Packet flow diverges from a first-time booking]. See `OUTSTANDING-QUESTIONS.md` Q16.

### Modifications (cancel / reschedule of an approved appointment)

**Provisional framing caveat.** Adrian flagged 2026-04-23 that **whether cancellation is an MVP feature at all** has not been confirmed with his manager. The flow below describes his current intent if cancellation lands in MVP; it is working-hypothesis content, not settled scope. [Source: Adrian best-guess 2026-04-23 -- NEEDS CONFIRMATION on "is cancel/reschedule in the MVP feature list?"]

Modifications on an already-approved appointment are a **direct admin action, not a request-and-review flow**: [Source: Adrian-confirmed 2026-04-23 as stated intent; the surrounding "does this feature exist in MVP?" is pending manager confirmation per caveat above]

1. The tenant-level admin (doctor's admin) or the host admin locates the appointment (from the review queue or an appointment search).
2. They take a direct action -- cancel or reschedule. For reschedule, a new slot is picked at modification time.
3. The modification is applied immediately; the all-parties notification fires (same recipient list as the booking confirmation). [Source: Adrian-confirmed 2026-04-23]
4. **Slot lifecycle on modification -- Adrian's preference, pending confirmation.** Adrian's ideal is that cancellation returns the original slot to Available (symmetric with booking), and that a reschedule releases the old slot and marks the new slot Booked. [Source: Adrian best-guess 2026-04-23 -- NEEDS CONFIRMATION]. The current code's `DeleteAsync` does NOT release the slot (see Known Discrepancies), so implementing the preferred behaviour is net-new work contingent on cancel/reschedule being confirmed in the MVP feature list.
5. [UNKNOWN -- queued for Adrian: is a new Packet generated and redelivered on modification, or is the earlier Packet superseded in some other way? Parked under the Packet design follow-up.]

Bookers do **not** have a cancel or reschedule action in the portal at MVP. A booker who needs a modification contacts the doctor's office or Gesco (host admin) through out-of-portal channels; the admin then performs the modification in the portal. [Source: Adrian-confirmed 2026-04-23]

This MVP intent means the 13-state enum's `CancellationRequested` and `RescheduleRequested` states (which the ratified `APPOINTMENT-LIFECYCLE.md` uses for booker-initiated request flows) are **not used in the MVP flow**; they remain as future-state placeholders for when booker-initiated modifications ship. The admin-initiated outcome states (`CancelledNoBill`, `CancelledLate`, `RescheduledNoBill`, `RescheduledLate`) are the only cancel/reschedule states the MVP is expected to land on. [Source: Adrian-confirmed 2026-04-23]

## Business rules and invariants

### Confirmed rules

- **Two-step request/review flow is mandatory for MVP.** A booker cannot unilaterally confirm an appointment; only the doctor's office can approve, reject, or return-for-more-info. [Source: Adrian-confirmed 2026-04-22]
- **All case parties are notified at both steps -- a legal, not UX, requirement.** Request submission and doctor's-office decision both trigger emails to the full party list. The rationale is California's **ex-parte-communication rule**: any action one party takes on a workers'-compensation matter must be visible to every other party, or the communication is considered ex-parte and impermissible. A decision (or any other appointment-affecting action) that sends no notification is therefore not just a UX bug but a legal-compliance bug. [Source: Adrian-confirmed 2026-04-22, rationale clarified 2026-04-23]
- **The doctor's office's decision options are exactly three: approve, reject, send-back-for-info.** Not two (no approve/reject-only flow). Not an unbounded set. The send-back action moves the appointment into a distinct "Awaiting more info from booker" status, not an email-only exchange. [Source: Adrian-confirmed 2026-04-22]
- **Recipient emails are captured on the booking form and are required inputs.** Every party that must be notified has a required email field on the form; the form is the canonical distribution list for this appointment. [Source: Adrian-confirmed 2026-04-22]
- **The decision authority inside a practice is role-gated, not tenant-wide.** Only users holding a dedicated decision role (working name Office Manager / Scheduler) and the medical examiner (Doctor) can approve / reject / send-back-for-info. Other tenant-scoped users see the review queue but cannot act. [Source: Adrian-confirmed 2026-04-22]
- **All events are persisted.** Submission, every decision, and the resulting status are stored in the database -- no ephemeral in-memory state. [Source: Adrian-confirmed 2026-04-22]
- **Ex-parte communication foundation rule.** California workers'-compensation practice forbids one party taking a case-affecting action without every other party's visibility. This foundational rule is the "why" behind several specific rules in this feature: all-parties notification on every event, required-email fields per party on the form, and the defense-attorney requirement to enter applicant-attorney info at booking time. Any future Appointments feature that involves party communication must preserve the same foundation. [Source: Adrian-confirmed 2026-04-23]
- **Patient registration is email-invite-based with the tenant pre-selected.** Patients do not choose or browse tenants; they arrive via an invitation email scoped to a specific examiner's practice. Deliberate constraint: patients may not know the exact tenant name as stored, and exposing the tenant list would reveal medical-practice relationships protected by legal / medical-privacy norms. [Source: Adrian-confirmed 2026-04-23]
- **Attorney information is mandatory on patient-initiated bookings, with a controlled self-represented exception.** The booking form treats attorney information as required. Attempting to skip the attorney section triggers a popup that asks whether the patient is self-represented or is missing the attorney's info. Self-represented -> continue with an active warning banner persistently visible. Missing info -> hard-block, with instructions to contact the attorney (either for the info or to have the attorney book through their own account). [Source: Adrian-confirmed 2026-04-23]
- **Attorneys can book for multiple patients; lists are tenant-scoped.** An attorney managing many cases sees a per-tenant list inside each tenant context. An attorney who works with several tenants does NOT get a unified cross-tenant inbox at MVP; they see separate per-tenant lists. [Source: Adrian-confirmed 2026-04-23]
- **Defense-attorney bookings must identify the applicant attorney.** When a defense attorney books an appointment, entering the applicant attorney's contact information is mandatory so the all-parties notification reaches them. This is the ex-parte rule applied to the defense-booking path. [Source: Adrian-confirmed 2026-04-23]
- **Reevaluation is a distinct booking type, not a repeat of a first-time appointment.** The patient dashboard surfaces it as its own action button. Adrian's rough guess 2026-04-24: reevaluation is somewhat more complex than a first-time appointment, but most of the necessary data is already available from the first appointment (so the booker fills fewer fields). Reevaluation form design, eligibility rules, and the exact extra-complexity fields are [UNKNOWN -- queued for manager]. [Source: Adrian-confirmed 2026-04-23 on existence; best-guess 2026-04-24 on shape; exact design pending]
- **MVP scope limit: Appointments is a booking portal, not case tracking.** The day-of-exam lifecycle states (`CheckedIn`, `CheckedOut`, `Billed`, `NoShow`) are defined in the enum for long-term intent (see `APPOINTMENT-LIFECYCLE.md`) but are NOT used in MVP. MVP responsibility ends when the appointment is approved and data / notifications are delivered to the case parties. Day-of-exam handling lives post-MVP, likely in downstream products or in office-side manual processes. [Source: Adrian-confirmed 2026-04-24]
- **Tenant is pre-decided for a patient booking, not an open choice.** When a patient logs in, their tenant context is already fixed (via the invitation email). Patients do not pick their tenant during booking. Non-patient bookers work within the tenant context of the patient they are booking for. Upstream of the portal, some system or human decides which tenant a patient is booking under; that upstream decision is not open to the booker at portal time. [Source: Adrian-confirmed 2026-04-24]
- **Notification emails use strict, legally-compliant formats.** Because most Appointments notifications are legally-required communications (per the ex-parte rule), each email template must follow a specific format that serves as defensible evidence of communication. Specific per-template content / structure is [UNKNOWN -- queued for Adrian to confirm with legal and the client]. [Source: Adrian-confirmed 2026-04-24]
- **MVP data delivery beyond email is deferred.** Post-MVP: structured Packet delivery to downstream softwares (Case Tracking, MRR AI, DWC/EAMS, carrier systems, TPA systems). MVP collects the full dataset on the booking form and stores every event; post-MVP formats and delivers it to each software that needs it. "As long as we collect the data, we can format it as required." [Source: Adrian-confirmed 2026-04-24]
- **On approval, a Packet is generated and delivered to two audiences: all case parties AND the other softwares in the pipeline.** The Packet hand-off is mandatory for the Patient Portal to function in its California workers'-comp business pipeline; an approval that does not produce a Packet is a bug. Specific Packet contents, formats, and recipient-software list are being clarified in follow-up interview turns. [Source: Adrian-confirmed 2026-04-23]
- **Modifications (cancel / reschedule) on an approved appointment can be initiated only by a tenant-level admin (doctor's admin) or the host admin.** Bookers cannot initiate modifications through the portal; modification requests reach an admin via out-of-portal channels (phone, email, support). The host admin exists specifically to step in when the doctor's admin cannot or will not act. **Caveat:** whether cancel/reschedule is an MVP feature at all is pending manager confirmation per Adrian 2026-04-23 -- this rule describes the intended flow IF the feature is in MVP. [Source: Adrian-confirmed 2026-04-23 as stated intent; NEEDS CONFIRMATION on MVP inclusion]

### Open rules (to resolve during interview)

- Advance-booking window: how far in the future can a booking be placed? Is there a minimum lead time? [UNKNOWN -- queued for Adrian]
- Confirmation number scope and format: the code today generates `A#####` per-tenant; is that intent, and is the format user-facing? [UNKNOWN -- queued for Adrian]
- Slot lifecycle: when a booking is cancelled or rescheduled, is the original slot returned to the available pool? [UNKNOWN -- queued for Adrian]
- Who (which role) can edit a booking after it's created, and which fields are editable? [UNKNOWN -- queued for Adrian]
- Who (which role) can delete a booking, and what happens to the slot? [UNKNOWN -- queued for Adrian]
- Who at the doctor's office (any tenant-scoped staff user? specific roles? the doctor only?) holds the approve / reject / request-more-info authority? [UNKNOWN -- queued for Adrian]
- Can the booker edit or withdraw a request while it is still awaiting the doctor's office review? [UNKNOWN -- queued for Adrian]

## Integration points

### Email notifications (MVP -- confirmed)

Email is a first-class integration for MVP. Two notification events confirmed: [Source: Adrian-confirmed 2026-04-22]

- **Appointment request submitted** -- notify all case parties (patient, applicant attorney, defense attorney, insurance people, claim adjustors, doctor's office).
- **Doctor's office decision** (approve / reject / send-back-for-info) -- notify the same all-parties list.

**Recipient resolution.** The booking form itself captures the email address for every party that must be notified, and every such email field is a required form input. The form is the source of truth for the per-appointment distribution list; the system does not derive recipients from case-link entities alone. [Source: Adrian-confirmed 2026-04-22]

The form does not rely on every party pre-existing as a user or record in the system -- if a party (e.g., a defense attorney with no `ApplicantAttorneys`-style entity yet, or an insurance contact with no equivalent link) is ad-hoc, the booker still types their email and the appointment's distribution list is complete. [Source: Adrian-confirmed 2026-04-22]

**Open implementation intent:**

- [UNKNOWN -- queued for Adrian: what if the booker genuinely does not know a required party's email (e.g., a claim examiner has not yet been assigned to the claim)? Does the form hard-block submission until every email is provided, or is there a "not yet known" fallback?]
- [UNKNOWN -- queued for manager: is there a regulatory notification requirement (e.g., DWC section 35 records-exchange, QME panel notifications) tied to these emails, or are they purely operational?]
- [UNKNOWN -- queued for Adrian: SMS or any other channel beyond email for MVP?]

### Data delivery on approval (MVP -- email-only)

On each appointment event (submission, decision, modification), the appointment's data is stored in the database AND delivered to every case party by email in a strict, legally-compliant format.

The "Packet" concept (Adrian's working term, introduced 2026-04-23) denotes the bundle of appointment data the portal is responsible for capturing and handing off. **At MVP the hand-off is email-only** to the party list: patient, applicant attorney, defense attorney, insurance company, claim adjustor, doctor's office. Structured hand-offs to other softwares in the California workers'-compensation pipeline (Case Tracking, MRR AI, DWC/EAMS, insurance-carrier systems, TPA systems) are **post-MVP** per Adrian 2026-04-24. MVP collects the data; post-MVP builds the software-specific formatters and delivery channels. [Source: Adrian-confirmed 2026-04-23 on the Packet concept; narrowed 2026-04-24 to email-only for MVP]

The portal's MVP responsibility with respect to data hand-off is therefore:

1. **Collect** every appointment's data on the booking form, including patient, insurance + adjustor, and applicant + defense attorney info.
2. **Store** every event (submission, decision, modification) in the database for audit.
3. **Send** the data to every case party via email on each event, in a legally-compliant strict format (per the ex-parte-communication rule).

That is the full MVP scope for data hand-off. Translation to structured formats for downstream softwares, non-email delivery channels, schemas per recipient, and retry / failure / replay semantics are all post-MVP.

**Open follow-ups (still parked, narrower now):**

- Exact content / structure of each email template (per-event, per-party-type?). Queued for Adrian to confirm with legal + client; this blocks email-template implementation in MVP. [UNKNOWN]
- Whether some parties receive a distinct email variant vs. all parties receiving the same email (e.g., the doctor's office may need review-context info the patient does not). Adrian 2026-04-23 said some patient info goes to the doctor's office; whether that is a distinct email or the same all-parties email is [UNKNOWN -- queued for Adrian].

### Upstream pipeline (Digital Forms hand-in)

Adrian 2026-04-24: Digital Forms specifics remain unknown; however, the tenant under which a patient books IS pre-decided (not an open choice at booking time), meaning at least the tenant-assignment piece is resolved upstream of the portal. [Source: Adrian-confirmed 2026-04-24]

Remaining upstream items -- whether Digital Forms pre-fills any other fields (patient demographics, claim number, employer info, attorney contacts) into the booking form, or whether the booker re-enters everything from scratch -- are [UNKNOWN -- Adrian doesn't personally hold the answer; queued for manager / pipeline architect].

## Edge cases and error behaviors

### Confirmed

- **Self-represented patient flow.** The booking form treats attorney info as required. A patient attempting to skip the attorney section sees a popup with two choices: "I'm self-represented" or "I don't have the attorney's info". [Source: Adrian-confirmed 2026-04-23]
  - **Self-represented** -> patient continues filling the form; an active warning banner is persistently visible during the remaining flow, clearly communicating that the patient has elected to proceed without attorney representation.
  - **Missing info** -> patient is hard-blocked from submitting. The UI instructs them to contact their attorney to obtain the attorney's info, or to have the attorney book the appointment through their own account.
- **Attorney working with multiple tenants.** The attorney sees a separate per-tenant list in each tenant context, not a unified cross-tenant inbox. Attempting to cross-reference appointments between tenants is not a supported operation at MVP. [Source: Adrian-confirmed 2026-04-23]
- **Defense attorney without applicant-attorney info.** The defense attorney cannot skip entering applicant-attorney info (no self-represented equivalent for defense-initiated bookings). The ex-parte rule requires the applicant attorney be notified; missing that contact is a hard-block. [Source: Adrian-confirmed 2026-04-23, implied by the ex-parte foundation rule and defense-attorney booking description]

### Candidate edge cases still open

- Double-submit: booker clicks Submit twice. Intended behaviour? [UNKNOWN -- queued for Adrian]
- Concurrent booking of the same slot by two bookers. Which one wins? [UNKNOWN -- queued for Adrian]
- Booker tries to book a past date. Hard block or warn-and-allow? [UNKNOWN -- queued for Adrian] (Note: `docs/issues/research/BUG-09.md` documents that the server currently accepts past dates; this is an observed bug, not intent.)
- Booker tries to book beyond the advance-booking window. Hard block or warn? [UNKNOWN -- queued for Adrian] (Refines `docs/issues/research/Q-06.md`.)
- Slot becomes unavailable between form open and submit. Error behaviour? [UNKNOWN -- queued for Adrian]
- Patient-invite email never arrives or is lost in spam. Retry mechanism, expiration, manual resend path? [UNKNOWN -- queued for Adrian]
- Patient doesn't show up (NoShow). Next step and who owns it? [UNKNOWN -- depends on day-of-exam MVP scope decision (see `OUTSTANDING-QUESTIONS.md` Q2)]
- Attorney account used by a non-attorney (assistant, paralegal). Intended behaviour? [UNKNOWN -- queued for Adrian: does the attorney persona support delegated sub-users or is one-login-per-person assumed at MVP?]

## Success criteria

[UNKNOWN -- queued for Adrian: testable conditions that indicate Appointments works as intended for MVP demo. At minimum: which personas must be able to book end-to-end, which confirmation moments the booker sees, and what the examiner office sees.]

## Known discrepancies with implementation

Pending Phase 3 cross-reference pass. Candidate entries surfaced during evidence load + confirmed intent (to be reconciled against `docs/issues/` IDs in Phase 3):

- `[observed, not authoritative]` Code accepts any of the 13 status values at creation with no validation; status is then frozen (no update path). No state-machine enforcement. **Intent divergence (MVP-scoped per Adrian 2026-04-24):** MVP needs server-side-enforced, per-role, audit-trailed transitions for the request / review arc only: Pending -> (Approved | Rejected | AwaitingMoreInfo). Admin-initiated cancel / reschedule arcs are MVP-provisional (see Modifications subsection caveat). Day-of-exam transitions (`CheckedIn`, `CheckedOut`, `Billed`, `NoShow`) are explicitly NOT MVP.
- `[observed, not authoritative]` The 13-state enum has no `NeedsMoreInfo` / `InfoRequested` value, but the confirmed flow requires a "send back requesting more information" action. **Intent divergence:** either the enum is missing a state, or the action is an email-only exchange that does not mutate the appointment's status. Resolution pending Q2 of the interview.
- `[observed, not authoritative]` No email-notification code path exists for appointment-request submission or for doctor's-office decisions; FEAT-05 tracks the fact that `NullEmailSender` is wired in `#if DEBUG` and no templates / trigger points exist. **Intent divergence:** confirmed intent requires two notification events to the full party list at MVP; FEAT-05 is therefore MVP-blocking, not "nice to have".
- `[observed, not authoritative]` No doctor's-office review queue UI exists in the Angular project. The list page is a generic all-appointments grid, not a "pending review" bucket. **Intent divergence:** confirmed flow requires the office to see and act on pending requests; the list UI should surface the review queue explicitly.
- `[observed, not authoritative]` The current booking form has email-like fields on related records (e.g., `Patient.Email`, `ApplicantAttorney.Email`, `AppointmentEmployerDetail`), but not all are required and there is no single "notification recipient list" concept on the Appointment itself. No dedicated email fields exist on the Appointment for defense-attorney, claim-examiner, or insurance-contact recipients. **Intent divergence:** the confirmed form UX requires a required-email field for every party-to-notify on the Appointment, independent of whether the party is linked via an existing entity. Several of those fields don't exist today; Adrian intends to add them when he next touches the form.
- `[observed, not authoritative]` The only tenant-level role the code defines today is `Doctor` (assigned to the first user of a new tenant via `DoctorTenantAppService.CreateAsync`). There is no `Office Manager`, `Scheduler`, or equivalent role. `FEAT-12` already tracks the Doctor-vs-TenantAdmin conflation as separate tech debt. **Intent divergence:** confirmed intent is that decision authority lives in a dedicated decision role (Office Manager / Scheduler) plus Doctor. That role needs to be introduced, seeded during tenant provisioning, and bound to the approve / reject / send-back permissions -- net new MVP-blocking work that sits on top of FEAT-12.
- `[observed, not authoritative]` No email-based data-delivery code path exists for appointment events (submit, decision, modification). `FEAT-05` tracks the fact that `NullEmailSender` is wired in `#if DEBUG` and no templates or trigger points exist. **Intent divergence (narrowed 2026-04-24):** MVP intent is that each event sends legally-compliant, strict-format emails to every case party (ex-parte rule). This IS the MVP realization of the Packet hand-off. Structured format delivery to downstream softwares (Case Tracking, MRR AI, DWC/EAMS, carrier systems, TPA systems) is POST-MVP per Adrian 2026-04-24 and is NOT part of the current MVP gap. The MVP-blocking work is: real email sender (FEAT-05), strict-format templates per event + party-type, and per-event trigger wiring. The broader software-hand-off scope I previously flagged 2026-04-23 as the single biggest MVP gap is now explicitly post-MVP.

- **Scope boundary (not a code/intent gap):** The enum values `CheckedIn`, `CheckedOut`, `Billed`, `NoShow` exist for long-term intent per `APPOINTMENT-LIFECYCLE.md`. They are NOT in MVP scope per Adrian 2026-04-24. No MVP implementation work targets these states; they remain defined for future phases. Flagging here so Phase 3 does not treat their absence as an implementation gap.
- `[observed, not authoritative]` The 13-state enum defines `CancellationRequested` and `RescheduleRequested` states (booker-initiated request flows per `APPOINTMENT-LIFECYCLE.md`). **Intent divergence with MVP scope:** confirmed MVP intent (Adrian 2026-04-23) is that ONLY admins (doctor's admin, host admin) can initiate modifications; bookers have no cancel/reschedule action in MVP. Therefore `CancellationRequested` and `RescheduleRequested` states are **unused in MVP** and remain as future-state placeholders. Only the admin-initiated outcome states (`CancelledNoBill`, `CancelledLate`, `RescheduledNoBill`, `RescheduledLate`) are in MVP scope for the cancel/reschedule branches.
- `[observed, not authoritative]` `HostAdmin` is not a formally-defined role in the codebase (per `FEAT-11`). **Intent divergence:** confirmed MVP intent (Adrian 2026-04-23) is that host admin has a specific operational authority in Appointments -- unconditional cancel/reschedule across all tenants. The role must be defined, seeded on the host database, and bound to that specific authority. `FEAT-11` therefore becomes MVP-blocking for Appointments, not just architectural debt.
- `[observed, not authoritative]` The current booking form does not capture the patient's insurance or claim-adjustor information as structured fields on the Appointment / related entities. **Intent divergence (Adrian-confirmed 2026-04-23):** insurance and adjustor info is a required part of the patient booking flow. Fields need to be added to the form and to the underlying entity / DTO set.
- `[observed, not authoritative]` Patient registration in the current codebase uses the generic ABP external-signup flow, where an external user chooses their user type (`Patient`, `ClaimExaminer`, `ApplicantAttorney`, `DefenseAttorney`). No invitation-based flow exists where the patient is emailed a tenant-scoped link that pre-selects the examiner's practice. **Intent divergence (Adrian-confirmed 2026-04-23):** patient registration is invite-based with tenant pre-selected; tenant discovery is deliberately controlled. The invite-email + tenant-pre-selection UX needs to be designed and built; the existing signup flow may remain for non-patient personas.
- `[observed, not authoritative]` No self-represented patient popup / warning-banner / hard-block UX exists. **Intent divergence (Adrian-confirmed 2026-04-23):** self-represented path with a popup and persistent warning banner, and a hard-block on missing attorney info, are required MVP components of the booking form.
- `[observed, not authoritative]` No reevaluation booking type exists. The Appointment entity treats all bookings uniformly; there is no second-button flow, no reevaluation-specific fields, and no second workflow. **Intent divergence (Adrian-confirmed 2026-04-23 on existence, design pending manager):** reevaluation is a distinct MVP booking type.
- `[observed, not authoritative]` The attorney-side booking list is not implemented as a multi-patient, per-tenant view. The code has `AppointmentApplicantAttorney` as a join entity but no dedicated attorney dashboard listing the attorney's patients' appointments within a tenant. **Intent divergence (Adrian-confirmed 2026-04-23):** attorneys need a per-tenant multi-patient list view in MVP.
- `[observed, not authoritative]` `DeleteAsync` does not release the `DoctorAvailability` slot back to `Available`. Slot stays `Booked` after an appointment is deleted.
- `[observed, not authoritative]` Server does not reject past-date bookings. The 3-day minimum lead time exists only in the Angular datepicker.
- `[observed, not authoritative]` `Appointments.Edit` and `Appointments.Create` permissions are checked in the Angular UI but are NOT enforced in `AppointmentsAppService.UpdateAsync` / `CreateAsync` -- any authenticated user can bypass via direct API call.
- `[observed, not authoritative]` `/appointments/view/:id` route has only `authGuard`; any authenticated user can view any appointment by id. No `permissionGuard`.
- `[observed, not authoritative]` Three fields on `Appointment` (`IsPatientAlreadyExist`, `InternalUserComments`, `AppointmentApproveDate`) exist in the schema but are never written by any code path.
- `[observed, not authoritative]` Confirmation number generation uses a `MAX + 1` pattern under Read Committed with no unique constraint on `(TenantId, RequestConfirmationNumber)` -- a race can produce duplicates.

## Outstanding questions

Each bare `[UNKNOWN]` above corresponds to a concrete question that will roll up into [OUTSTANDING-QUESTIONS.md](OUTSTANDING-QUESTIONS.md) in Phase 4. Per-question phrasing is maintained in the section where it first appears.

<!-- DRAFT:MANUAL:START -->
<!-- DRAFT:MANUAL:END -->
