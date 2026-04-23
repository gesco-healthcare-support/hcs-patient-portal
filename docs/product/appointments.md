[Home](../INDEX.md) > [Product Intent](./) > Appointments

# Appointments -- Intended Behavior

**Status:** draft -- Phase 2 of intended-behavior work, interview in progress
**Last updated:** 2026-04-22
**Primary stakeholder:** [UNKNOWN -- queued for Adrian (this doc builds as the interview proceeds)]

> This document captures INTENDED behaviour for the Appointments feature -- what booking is supposed to do in the Patient Portal's MVP. It does NOT describe what the code currently does (that is `src/HealthcareSupport.CaseEvaluation.Domain/Appointments/CLAUDE.md` and `docs/features/appointments/overview.md`). Every claim carries a source tag. Code is never cited as authoritative for intent. Observations from code appear ONLY in the Known Discrepancies section, tagged `[observed, not authoritative]`.

## Purpose

The Appointments feature moves a booking from a booker (applicant attorney, patient, claim examiner, or defense attorney) through a medical-examiner practice's review and -- on approval -- generates a **Packet** of the appointment's data that is delivered to all case parties AND to the other softwares that sit alongside the Patient Portal in the California workers'-compensation pipeline. Without the Packet hand-off to both audiences the application does not fulfil its role in the business pipeline, so Appointments is the core product surface for the MVP, not just a scheduling UI. [Source: Adrian-confirmed 2026-04-23]

## Personas and goals

Persona definitions live in [00-BUSINESS-CONTEXT.md](00-BUSINESS-CONTEXT.md). This section captures feature-specific goals only.

**Booker personas (all four first-class at MVP per 00-BUSINESS-CONTEXT.md):**

- Applicant attorney -- goals for Appointments: [UNKNOWN -- queued for Adrian]
- Patient (injured worker) -- goals for Appointments: [UNKNOWN -- queued for Adrian]
- Claim examiner -- goals for Appointments: [UNKNOWN -- queued for Adrian]
- Defense attorney -- goals for Appointments: [UNKNOWN -- queued for Adrian]

**Non-booker personas:**

- Examiner office staff -- role in Appointments: within the office, a dedicated tenant-level admin role (Adrian's 2026-04-23 handle: **doctor's admin**; other working names raised in this interview: Office Manager, Scheduler, TenantAdmin) holds (a) the authority to approve / reject / send-back-for-info on pending requests and (b) the authority to cancel or reschedule approved appointments. The medical examiner (Doctor role) can also approve / reject / send-back on the review queue. Other staff users inside the tenant can VIEW the queue for scheduling context but cannot take decision or modification actions. [UNKNOWN -- queued for Adrian: is the final role name a single role that owns both the approval authority (this interview 2026-04-22) and the modification authority (this interview 2026-04-23), or two separate roles? Do the current working handles `doctor's admin`, `Office Manager`, `Scheduler` refer to the same role?] [Source: Adrian-confirmed 2026-04-22, extended 2026-04-23]
- Host admin (Gesco-side) -- role in Appointments: always has authority to cancel or reschedule any appointment, across any tenant. The host admin's **reason to exist is precisely this** -- to step in when the doctor's office cannot or will not modify an appointment directly ("that is the point of that admin", Adrian 2026-04-23). No appointment-approval authority in the doctor's review queue (that belongs to the tenant-level admin); modification authority is universal. [Source: Adrian-confirmed 2026-04-23]

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
7. **Packet generation and delivery** -- on approval, the system generates a Packet of the appointment's data and delivers it both to the other parties on the case and to the other softwares in the pipeline.

[Source: Adrian-confirmed 2026-04-23]

Appointments as a feature owns steps 4-7. Steps 1-2 are owned by the Auth-and-Roles cross-cutting concern; step 3 is owned by DoctorAvailabilities.

The full 13-state lifecycle in [APPOINTMENT-LIFECYCLE.md](../business-domain/APPOINTMENT-LIFECYCLE.md) is ratified as long-term intent (see `docs/product/README.md` classification). Whether every state in that lifecycle is MVP-scoped -- in particular the day-of-exam states (`CheckedIn`, `CheckedOut`, `Billed`, `NoShow`) -- is not finalized as of 2026-04-23. [Source: Adrian-confirmed 2026-04-23]

### Booking flow (MVP)

Booking is a **two-step request/review flow** with **notification to all case parties** at both steps. [Source: Adrian-confirmed 2026-04-22]

**Step 1 -- Booker submits an appointment request.**

Any of the four booker personas (applicant attorney, patient, claim examiner, defense attorney) fills out the booking form and submits. The submission creates an appointment **request** (not a confirmed booking) in the database. The system then emails **all parties involved in the case**, which at minimum comprises: [Source: Adrian-confirmed 2026-04-22]

- The patient / client
- The applicant attorney
- The defense attorney
- The insurance people [UNKNOWN -- queued for manager: Adrian uses "insurance people" and "claim adjustors / claim examiners" as separate items in the notification list but is not certain whether they are two labels for the same persona or distinct recipient groups (e.g., a broader carrier / TPA distribution list on top of the individual named claim examiner). Resolving this changes whether the data model needs one or two insurance-side recipient slots per appointment. Confirmed with Adrian 2026-04-22.]
- The claim adjustors / claim examiners
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

**Steps 3+ (post-approval)** -- [UNKNOWN -- queued for Adrian: from Approved through CheckedIn, CheckedOut, Billed, who owns each transition, and what happens if the patient does not show up.]

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
- **All case parties are notified at both steps.** Request submission and doctor's-office decision both trigger emails to the full party list. A decision that sends no notification is a bug. [Source: Adrian-confirmed 2026-04-22]
- **The doctor's office's decision options are exactly three: approve, reject, send-back-for-info.** Not two (no approve/reject-only flow). Not an unbounded set. The send-back action moves the appointment into a distinct "Awaiting more info from booker" status, not an email-only exchange. [Source: Adrian-confirmed 2026-04-22]
- **Recipient emails are captured on the booking form and are required inputs.** Every party that must be notified has a required email field on the form; the form is the canonical distribution list for this appointment. [Source: Adrian-confirmed 2026-04-22]
- **The decision authority inside a practice is role-gated, not tenant-wide.** Only users holding a dedicated decision role (working name Office Manager / Scheduler) and the medical examiner (Doctor) can approve / reject / send-back-for-info. Other tenant-scoped users see the review queue but cannot act. [Source: Adrian-confirmed 2026-04-22]
- **All events are persisted.** Submission, every decision, and the resulting status are stored in the database -- no ephemeral in-memory state. [Source: Adrian-confirmed 2026-04-22]
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

### Packet generation and delivery (MVP -- core product surface)

On appointment approval, the system generates a **Packet** (Adrian's working name; final term TBD) of the appointment's data and delivers it to **two audiences**: [Source: Adrian-confirmed 2026-04-23]

1. **The other parties on the case** -- the same notification-recipient list captured on the booking form (patient, applicant attorney, defense attorney, insurance people, claim adjustors).
2. **The other softwares** that operate alongside the Patient Portal in the California workers'-compensation pipeline.

The Packet hand-off is the reason the Patient Portal exists in its business pipeline; it is not a post-MVP addition. Without it the application cannot function inside the California system. [Source: Adrian-confirmed 2026-04-23]

**Packet design is an active work item, not finalized.** Adrian has confirmed the Packet exists as a concept, that it is the reason the application works in the business pipeline, and that it's not yet designed in full. Specifically known 2026-04-23: [Source: Adrian-confirmed 2026-04-23]

- **Confirmed content piece:** some of the information entered about the patient on the booking form is sent to the doctor's office as part of the Packet. This is one concrete example, not the full Packet definition.
- **Form factor direction:** the Packet will use **templates** of some kind (exact template technology and format -- PDF, Word mail-merge, email body, structured data, or a mix -- is not yet decided).
- **Everything else** -- what data goes to which audience, in what format, via what mechanism -- is **[UNKNOWN -- queued for Adrian]** as an explicit follow-up interview pass. The Packet surface gets its own design interview before it can be specced.

**Specific follow-up interview threads (parked until the Packet design pass):**

- What is in a Packet, per audience? Per-audience tailoring vs one bundle? Which Appointment and related-record fields?
- Delivery mechanism per audience -- email attachment vs downloadable link for parties, vs API / file drop / webhook for other softwares.
- Specific "other softwares" in MVP delivery scope. Candidates: downstream Gesco products (Case Tracking, MRR AI), upstream (Digital Forms), California-state systems (DWC / EAMS), insurance-carrier platforms, TPA systems.
- Delivery timing -- immediately on approval, on a schedule, queued-and-retryable.
- Failure behavior -- if a Packet fails to deliver to one of the other softwares, what happens?
- Packet evolution on modifications -- is a new Packet generated and redelivered? Does it supersede the earlier one?

### Upstream pipeline (Digital Forms hand-in)

[UNKNOWN -- queued for manager / pipeline architect: does any Digital Forms artifact flow INTO an appointment at booking time, or is the booking form the sole data origin? This is the upstream mirror of the Packet hand-off and remains open from Phase 0.]

## Edge cases and error behaviors

[UNKNOWN -- queued for Adrian. Candidate edge cases to cover:]

- Double-submit: booker clicks Submit twice. Intended behaviour? [UNKNOWN]
- Concurrent booking of the same slot by two bookers. Which one wins? [UNKNOWN]
- Booker tries to book a past date. Hard block or warn-and-allow? [UNKNOWN]
- Booker tries to book beyond the advance-booking window. Hard block or warn? [UNKNOWN]
- Slot becomes unavailable between form open and submit. Error behaviour? [UNKNOWN]
- Patient doesn't show up. What's the intended next step and who owns it? [UNKNOWN]

## Success criteria

[UNKNOWN -- queued for Adrian: testable conditions that indicate Appointments works as intended for MVP demo. At minimum: which personas must be able to book end-to-end, which confirmation moments the booker sees, and what the examiner office sees.]

## Known discrepancies with implementation

Pending Phase 3 cross-reference pass. Candidate entries surfaced during evidence load + confirmed intent (to be reconciled against `docs/issues/` IDs in Phase 3):

- `[observed, not authoritative]` Code accepts any of the 13 status values at creation with no validation; status is then frozen (no update path). No state-machine enforcement. **Intent divergence:** confirmed two-step flow implies Pending -> (Approved | Rejected | NeedsMoreInfo) transitions must be server-side-enforced, per-role, with an audit trail.
- `[observed, not authoritative]` The 13-state enum has no `NeedsMoreInfo` / `InfoRequested` value, but the confirmed flow requires a "send back requesting more information" action. **Intent divergence:** either the enum is missing a state, or the action is an email-only exchange that does not mutate the appointment's status. Resolution pending Q2 of the interview.
- `[observed, not authoritative]` No email-notification code path exists for appointment-request submission or for doctor's-office decisions; FEAT-05 tracks the fact that `NullEmailSender` is wired in `#if DEBUG` and no templates / trigger points exist. **Intent divergence:** confirmed intent requires two notification events to the full party list at MVP; FEAT-05 is therefore MVP-blocking, not "nice to have".
- `[observed, not authoritative]` No doctor's-office review queue UI exists in the Angular project. The list page is a generic all-appointments grid, not a "pending review" bucket. **Intent divergence:** confirmed flow requires the office to see and act on pending requests; the list UI should surface the review queue explicitly.
- `[observed, not authoritative]` The current booking form has email-like fields on related records (e.g., `Patient.Email`, `ApplicantAttorney.Email`, `AppointmentEmployerDetail`), but not all are required and there is no single "notification recipient list" concept on the Appointment itself. No dedicated email fields exist on the Appointment for defense-attorney, claim-examiner, or insurance-contact recipients. **Intent divergence:** the confirmed form UX requires a required-email field for every party-to-notify on the Appointment, independent of whether the party is linked via an existing entity. Several of those fields don't exist today; Adrian intends to add them when he next touches the form.
- `[observed, not authoritative]` The only tenant-level role the code defines today is `Doctor` (assigned to the first user of a new tenant via `DoctorTenantAppService.CreateAsync`). There is no `Office Manager`, `Scheduler`, or equivalent role. `FEAT-12` already tracks the Doctor-vs-TenantAdmin conflation as separate tech debt. **Intent divergence:** confirmed intent is that decision authority lives in a dedicated decision role (Office Manager / Scheduler) plus Doctor. That role needs to be introduced, seeded during tenant provisioning, and bound to the approve / reject / send-back permissions -- net new MVP-blocking work that sits on top of FEAT-12.
- `[observed, not authoritative]` No Packet-generation code path exists anywhere in the project. There is no data model for what a Packet contains, no generator, no delivery mechanism (email attachment / API call / file drop / webhook), and no downstream-software integration. **Intent divergence:** Packet generation + delivery on approval is THE product purpose per Adrian-confirmed intent (2026-04-23), making it the single largest MVP-blocking gap between implementation and intent. Volume of work depends on how many downstream softwares the MVP delivers to and how tailored each Packet form is; currently unscoped pending follow-up interview.
- `[observed, not authoritative]` The 13-state enum defines `CancellationRequested` and `RescheduleRequested` states (booker-initiated request flows per `APPOINTMENT-LIFECYCLE.md`). **Intent divergence with MVP scope:** confirmed MVP intent (Adrian 2026-04-23) is that ONLY admins (doctor's admin, host admin) can initiate modifications; bookers have no cancel/reschedule action in MVP. Therefore `CancellationRequested` and `RescheduleRequested` states are **unused in MVP** and remain as future-state placeholders. Only the admin-initiated outcome states (`CancelledNoBill`, `CancelledLate`, `RescheduledNoBill`, `RescheduledLate`) are in MVP scope for the cancel/reschedule branches.
- `[observed, not authoritative]` `HostAdmin` is not a formally-defined role in the codebase (per `FEAT-11`). **Intent divergence:** confirmed MVP intent (Adrian 2026-04-23) is that host admin has a specific operational authority in Appointments -- unconditional cancel/reschedule across all tenants. The role must be defined, seeded on the host database, and bound to that specific authority. `FEAT-11` therefore becomes MVP-blocking for Appointments, not just architectural debt.
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
