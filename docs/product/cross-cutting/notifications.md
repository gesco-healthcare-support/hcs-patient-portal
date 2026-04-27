[Home](../../INDEX.md) > [Product Intent](../) > [Cross-cutting](./) > Notifications

# Notifications -- Intended Behavior

**Status:** draft -- Phase 2 T12, cross-cutting cluster
**Last updated:** 2026-04-27
**Primary stakeholders:** All case parties (patient, applicant attorney, defense attorney, insurance carrier / TPA contact, claim examiner / adjuster, doctor's office, employer-when-a-party); the booker; office staff who fire transitions

> Cross-cutting intent for the notification model in the Patient Portal. Consolidates the all-parties notification rule (T2 + T11), the employer-as-recipient flag (T7), the translator-required workflow trigger (T8), the magic-link Patient invite (T10), and the per-transition trigger pattern (T11) into one canonical source. Notifications are legally-required ex-parte communications -- the exact format per event / per party-type remains escalated as `escalations/open-items.md` Item 2. Every claim source-tagged.

## Purpose

Notifications are the portal's outbound communication layer. They tell every party on a workers'-comp case that something happened, in a way that is defensible as evidence under California ex-parte rules. T12 consolidates the rules that govern who receives a notification, when, on what channel, in what language, and with what audit trail. Format specifics (the exact body of each email per event per party) remain queued with the manager via escalations Item 2 / OUTSTANDING-QUESTIONS.md Q9; T12 is the canonical source for everything around the format. [Source: Adrian-confirmed across the T12 interview 2026-04-27]

## Personas and goals

Cross-reference `00-BUSINESS-CONTEXT.md` and `cross-cutting/auth-and-roles.md`.

- **All case parties (recipients).** Each party receives every notification produced by every state transition on the appointment. The recipient list is canonical and identical per event; the email's content is the same for everyone on a given send. [Source: Adrian-confirmed 2026-04-27, T12 Q1]
- **Booker (the actor that submits).** Receives the all-parties email like every other case party, plus an in-portal success-UX confirmation ("A confirmation email has been sent successfully") after every notification event. [Source: Q9 partial answer manager-confirmed 2026-04-24]
- **Patient (auto-created accounts).** Receives a magic-link invite as a SEPARATE notification type, beyond the all-parties transition emails. The magic link is one-time; clicking lands them logged in and lets them set their password. [Source: Adrian-confirmed 2026-04-24, T10 Q-T10-4 / research Q12]
- **Doctor's office (intake / Practice Admin staff).** Receives the same notifications as every other case party AND uses them as a feed for the review queue. The in-portal inbox (per T12-Q2) is the office's own surface to read notifications without leaving the portal.
- **Translator (post-MVP).** Not a notification recipient at MVP -- the office arranges interpreters off-portal based on the translator-required call-out (T8 Q-K). [Source: Adrian-confirmed 2026-04-24]

## Intended workflow

### Trigger model

Every appointment-status transition fires one all-parties notification. The transition-to-event mapping (per T11):

- **Request submit (status -> Pending).** "Request submitted" notification.
- **Send-back-for-info (Pending -> AwaitingMoreInfo).** "Office sent back asking for more information" notification. The email carries the office's structured field flags + the office's free-text note (per T11 Q5). Per the recipient rule, every party sees the same content -- the field flags + note are visible to all parties on the case.
- **Booker response (AwaitingMoreInfo -> Pending; auto).** "Booker responded; appointment back in review" notification with a UI flag on the office's review-queue item identifying the response.
- **Approved (Pending -> Approved).** "Appointment approved" notification, plus the Packet content for downstream hand-off.
- **Rejected (Pending -> Rejected).** "Appointment rejected" notification.
- **Cancelled (admin-initiated; provisional).** "Appointment cancelled" notification.
- **Rescheduled (admin-initiated; provisional).** Two notifications: "original appointment rescheduled" (referencing the now-terminal original record) AND "new appointment request" (referencing the new Pending record on the new slot, per T11 Q4).

[Source: Adrian-confirmed 2026-04-22 via T2; reaffirmed 2026-04-27 via T11 + T12]

### Recipient list (canonical, per event)

For every event above, the canonical recipient list is:

- The patient.
- The applicant attorney (always, when on the case; per T6 one applicant attorney per appointment at MVP).
- The defense attorney (always, when on the case; symmetric per T6).
- The insurance company contact (carrier or TPA -- the company-level contact, not the specific adjuster). [T2 Q4]
- The claim examiner / adjuster (the specific individual on the case). [T2 Q4]
- The doctor's office (Practice Admin / intake staff for the tenant).
- The employer -- only when the appointment's "notify employer" flag is set per T7 (self-insured employers and otherwise directly-involved employers; off-case employers are NOT recipients).

The list is FIXED across all events at MVP; everyone gets the same email content for a given event. Per-event content can vary (different templates per event); per-recipient content does not (no primary-vs-CC variation at MVP). [Source: Adrian-confirmed 2026-04-27, T12 Q1; "we can change that later" -- post-MVP candidate to add primary-vs-CC differentiation]

### Channels

At MVP, every notification is delivered on TWO channels per recipient:

1. **Email.** Required (Packet definition; legal-evidence requirement).
2. **In-portal inbox copy.** Each recipient who has a portal account sees the notification in a per-recipient notification list / inbox inside the portal. The inbox content mirrors what the email said.

Email is the legally-defensible channel; the in-portal inbox is the UX channel. Recipients without a portal account (e.g., an insurance carrier contact who does not log in) get email only. [Source: Adrian-confirmed 2026-04-27, T12 Q2]

### Language

At MVP, all notification content is **English** regardless of the patient's preferred language. When the patient's `AppointmentLanguage` is non-English (and per T8 Q-K, a translator must be arranged), the email body carries a prominent "interpreter required: <language>" line so the office can plan the translator. Per-language email templates land post-MVP. [Source: Adrian-confirmed 2026-04-27, T12 Q3 -- post-MVP option chosen]

### Magic-link Patient invite (separate notification type)

Distinct from the per-transition all-parties notification. Sent to a newly auto-created Patient IdentityUser:

- Triggered when the booker is not the patient AND the patient does not yet have an account at the tenant. The exact fire timing (on-submit vs on-approval vs both) is **Q23 in `OUTSTANDING-QUESTIONS.md`** and remains open with the manager.
- Content: a one-time login link; clicking lands the patient logged in and lets them set a password. No password ever in plain email.
- Fires AT MOST once per Patient account per tenant; the office can re-issue if the link expires (mechanism per T10 / FEAT-05 build details).

[Source: Adrian-confirmed 2026-04-24, T10 Q-T10-4 / resolves research Q12]

### Success-UX confirmation

After every notification event the portal fires successfully, the booker sees a confirmation message in the UI: "A confirmation email has been sent successfully" (or per the actual UX wording the office settles on). [Source: Q9 partial answer -- manager-confirmed 2026-04-24, captured in `appointments.md`]

## Business rules and invariants

- **Trigger:** every appointment-status transition fires exactly one all-parties notification. No silent transitions. [Source: T2 + T11]
- **Recipient list:** canonical and identical per event; same email content for every recipient on a given event at MVP. [Source: Adrian-confirmed 2026-04-27, T12 Q1]
- **Channels at MVP:** email + in-portal inbox copy. [Source: Adrian-confirmed 2026-04-27, T12 Q2]
- **Language at MVP:** all notifications in English; "interpreter required: <language>" call-out when patient language != English. Per-language templates are post-MVP. [Source: Adrian-confirmed 2026-04-27, T12 Q3]
- **Format:** legally-required strict format (defensible-evidence standard). Exact format per event / per party-type is queued with the manager via OUTSTANDING-QUESTIONS.md Q9 + escalations Item 2. T12 is permissive on shape until the manager confirms.
- **Audit logging:** every notification produces an audit record covering trigger event, recipient list, content, dispatch timestamp, and delivery status (or final-failure status after retries). For host / supervisor admin actions that fire transitions, the audit also carries the tenant-context field per T9 Q-T9-5. [Source: T9 + Adrian best-guess 2026-04-27 -- NEEDS CONFIRMATION on the exact audit-record fields once the format is locked]
- **Magic-link patient invite:** separate notification type from all-parties transition emails; one-time link; no password in plain email. [Source: T10 / Q12]
- **No reminder / digest at MVP:** there are no day-before-appointment reminders, weekly digests, or ambient notifications. The portal's notification model is strictly transition-triggered. [Source: Adrian best-guess 2026-04-27 -- NEEDS CONFIRMATION; consistent with T8 Q-M portal-scope-narrowing context (the portal is booking-only, not case-tracking)]
- **Sender identity:** at MVP, notifications send from a generic Gesco-side address (e.g., `noreply@gesco.com` or similar). Per-tenant sender (e.g., from the practice's own email) is post-MVP. [Source: Adrian best-guess 2026-04-27 -- NEEDS CONFIRMATION]

## Integration points

- **T2 Appointments** -- the per-event trigger model lives here originally; T12 lifts it into the cross-cutting layer.
- **T7 AppointmentEmployerDetails** -- the "notify employer" per-booking flag determines whether the employer is on the recipient list.
- **T8 AppointmentLanguages** -- the patient language drives the "interpreter required" call-out on every email.
- **T9 Multi-tenancy** -- audit-logging requirement; tenant-context field on audit entries; per-tenant notification scope (each tenant's appointments fire their own notifications).
- **T10 Auth-and-roles** -- magic-link Patient invite as a separate notification type; the role-to-recipient mapping (Patient role -> patient address; ApplicantAttorney role -> attorney address; etc.).
- **T11 Appointment lifecycle** -- every transition fires the all-parties notification per T12; the trigger list above maps 1:1 to T11's transition list.
- **`escalations/open-items.md` Item 2** -- the format specifics open with the manager (Q9). T12 will be augmented when Item 2 returns.
- **`docs/issues/INCOMPLETE-FEATURES.md` FEAT-05** -- the email-system build item. T12 specifies the intent FEAT-05 must build to.
- **`docs/product/appointments.md`** -- references the all-parties notification at every event; T12 is the canonical source for the notification model.

## Edge cases and error behaviors

- **Recipient missing email address.** A required party (e.g., insurance carrier) has no email on file. The portal cannot send. Intent: surface to the office via the review queue (a "cannot notify <party>" indicator) so the office can collect or correct the email before continuing. Approval should not proceed if a required all-parties recipient lacks an address. [Source: Adrian best-guess 2026-04-27 -- NEEDS CONFIRMATION; consistent with the legal-evidence requirement]
- **Bounce / delivery failure.** Retry per the underlying email-system policy (FEAT-05); surface a final-failure indicator on the appointment / review queue if all retries fail. Office can re-trigger the notification manually after fixing the address. [Source: Adrian best-guess 2026-04-27 -- NEEDS CONFIRMATION]
- **AwaitingMoreInfo notification with field flags + free-text note.** Same content to all parties (per the recipient rule). Every party sees what the office is asking the booker to revise.
- **Reschedule produces two emails on the same event.** One for the original record's terminal status (RescheduledNoBill / RescheduledLate); one for the new record's Pending status. Both go to the same canonical recipient list. [Source: T11 Q4]
- **Booker resubmits AwaitingMoreInfo response with no changes.** Status auto-transitions back to Pending; the all-parties "booker responded" notification still fires; the office's review queue gets the response flag. [Source: T11 Q2]
- **Patient declines magic-link invite (does not click).** The Patient account remains in invite-pending state; the office can re-issue the invite per T10. The all-parties transition emails continue to fire normally; the patient receives them via the email address on file even without a portal account.
- **Translator-required call-out when language not in the seeded list.** The portal still sends the all-parties email in English with the call-out using whatever language string the booker entered. Office arranges the translator off-portal as usual.
- **Notification fires for a tenant in archive-in-place state (T9 decommission).** N/A at MVP -- archived tenants block new bookings + new logins, so no transitions occur. If an in-flight case continues off-portal, no portal notifications fire.

## Success criteria

- Every appointment-status transition produces an all-parties notification email + an in-portal inbox entry per recipient who has a portal account.
- The recipient list is computed from the appointment's case parties (patient, attorneys, insurance / claim examiner, office, employer-when-flagged) consistently per event.
- The booker sees a success-UX confirmation message after every notification event.
- Patient-language non-English appointments carry a visible "interpreter required" line on every email.
- Auto-created Patient accounts receive a working magic-link invite once the timing is confirmed (Q23).
- Notification audit log has one entry per recipient per event with delivery status; failures are visible to the office.
- No silent transitions (every status change fires).

## Known discrepancies with implementation

- `[observed, not authoritative]` **No email sender / template system exists in the Application project.** FEAT-05 captures this gap; T12 is the intent FEAT-05 builds to.
- `[observed, not authoritative]` **No in-portal notification inbox / panel exists in Angular.** New surface required per T12-Q2.
- `[observed, not authoritative]` **No translator-required call-out logic.** The patient's language is stored but no notification-side code reads it (per T8 Known Discrepancies).
- `[observed, not authoritative]` **No magic-link auth flow.** Standard ABP behaviour for auto-created users likely uses random-password-via-email; T10 / T12 intent is magic-link.
- `[observed, not authoritative]` **No all-parties recipient compute logic.** Computing "who is on this case" from the appointment + its links (patient, attorneys, employer) and folding in the per-tenant office staff is unbuilt.
- `[observed, not authoritative]` **No notification audit-log table or per-recipient delivery-status tracking** beyond standard ABP audit logging.
- `[observed, not authoritative]` **No retry / bounce handling.** ABP / SendGrid / underlying provider's defaults will apply once FEAT-05 picks a provider; intent (per T12) requires final-failure surfacing to the office.
- `[observed, not authoritative]` **No per-event notification templates exist.** Each event's email body needs a template informed by the legal-format escalation (Item 2).
- `[observed, not authoritative]` `docs/business-domain/DOMAIN-OVERVIEW.md` does not document an in-portal inbox; that file is OBSERVATION-ONLY per Phase 1 README and does not need updating; T12 is the canonical source for the notification model going forward.

## Outstanding questions

- **Q23 invite-fire timing.** When does the magic-link Patient invite fire (on-submit, on-approval, or both)? Still open in `OUTSTANDING-QUESTIONS.md` -- queued with the manager.
- **Format specifics per event / per party.** OUTSTANDING-QUESTIONS.md Q9 + `escalations/open-items.md` Item 2 -- legal-evidence email format. Open with the manager.
- **Sender identity at MVP.** Generic Gesco address vs per-tenant sender. Inline-seeded as generic; [UNKNOWN -- queued for Adrian on review].
- **Bounce / retry behaviour.** Inline-seeded as "retry per provider, surface final failure to the office"; [UNKNOWN -- queued for Adrian / FEAT-05].
- **Audit-log record fields.** Inline-seeded with a reasonable list; the exact field set settles when Q9 / Item 2 returns. [UNKNOWN -- depends on legal-format answer]
- **Reminder / digest emails.** Inline-seeded as "not at MVP"; [UNKNOWN -- queued for Adrian on review; consistent with portal-is-booking-only ruling].
- **Per-language templates.** Confirmed post-MVP per T12-Q3; the post-MVP languages list is unspecified.
