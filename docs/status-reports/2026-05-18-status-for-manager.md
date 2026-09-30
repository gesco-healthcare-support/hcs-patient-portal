# Patient Appointment Portal -- Status Report

**Date:** 2026-05-18
**Prepared by:** Adrian

---

## At a glance

Remaining work to launch the new portal for a single doctor's office is grouped into three stages. The legacy portal we are replacing is 7 years old; the new portal rebuilds the same workflows on a modern foundation, matches the look and feel, and replaces editable Word document outputs with non-editable PDFs.

Realistic calendar, with one developer working full time using Claude Code as a force multiplier:

| Milestone | Target |
|---|---|
| Internal Alpha (leadership demo) | 4-6 weeks from today |
| Closed Beta (one pilot office, staff using test data) | 9-13 weeks from today |
| Production launch (first office, real patients) | 12-17 weeks from today |
| Additional offices and Reports module | Begins after Production |

Numbers include a 30 percent buffer for surprises. No professional QA is assumed; the developer is also the tester.

---

## What is already working

### What patients, attorneys, and adjusters can do today

- Register themselves and accept Terms and Conditions.
- Log in, reset their password, and verify their email address.
- Receive an invitation by email from clinic staff and complete signup from that link.
- Book an appointment through a guided 7-step form. The form recognizes patients who already exist (based on matching basic identifiers) and avoids creating duplicate records.
- Upload required documents through secure one-time links sent by email -- no login needed for the upload itself.
- Upload the specialized "Joint Declaration Form" required for certain exam types, with automatic cancellation if it is not received in time.
- Request a reschedule or a cancellation on an existing appointment.
- View their own appointments and download a PDF packet of all the documents.

### What clinic staff and IT Admins can do today

- IT Admin can create internal users (clinic staff, supervisors). This shipped this week.
- Clinic staff can approve or reject an appointment, leave a reason, and assign a responsible team member.
- Clinic staff can approve or reject reschedule and cancellation requests.
- Clinic staff can accept or reject individual documents.
- The system automatically generates three PDF packets when an appointment is approved: one for the patient, one for the doctor, and one for the attorney or adjuster. The foundation for this shipped this week.
- External users can share read or edit access to their appointment with another person.
- Admin screens for: doctors, doctor availability calendar, office locations, appointment types, WCAB offices, attorneys, and supported languages.
- A dashboard with counters that click through to filtered appointment lists.

### What runs behind the scenes today

- Modern, standards-based login security replacing the legacy custom code.
- Documents stored in cloud storage (replaces the legacy AWS storage; vendor-neutral so the office is not locked in).
- Email sent through SMTP. The previously-reported "emails are disabled" concern is not a concern -- emails go out whenever real SMTP credentials are configured, which they are in the test environment.
- Eighteen distinct email notifications are wired and actively sending: registration confirmation, booking submitted, approval, rejection, status changes, document upload/accept/reject, packet delivery, due-date reminders, daily clinic-staff digest, supervisor digest, and others.
- About ten automated background tasks: due-date reminders, daily digests, auto-cancellation when paperwork is missing, etc.
- Privacy and security safeguards: rate-limited registration and password-reset, generic "we sent you an email" messages so attackers cannot probe whether an account exists, and no patient data echoed back in error messages.

---

## What is still missing

Grouped by impact on launch readiness.

### Features not yet built

| Feature | Description |
|---|---|
| Patient check-in, check-out, no-show, and billed actions | The day-to-day workflow at the front desk. The status values exist; the buttons and actions do not. |
| "Today's appointments" view | A date-driven list with inline check-in and check-out actions. |
| Internal notes on appointments | Staff-only threaded notes with reply and edit history. |
| "Ask us a question" widget | A floating button visible to patients and attorneys to send a simple message to the office. |
| "This patient already exists" prompt during approval | Staff sees a notice and can manually link the new request to an existing patient record. |
| Two-packet split | The legacy app sent the patient one packet and the responsible team member a different internal packet. The new app currently sends one. You decided to preserve the legacy behavior. |
| Appointment Request Report (with Excel and PDF export) | The big tabular report with filters. Deferred to Phase 2 by your decision. |
| Per-appointment "Print" or "Export to Excel" buttons | Deferred to Phase 2 by your decision. |

### Correctness gaps that need fixing before production

| Item | Why it matters |
|---|---|
| One doctor per office safeguard | Today an admin could accidentally create a second doctor for one office, or delete the only doctor. The fix prevents this and protects appointment data integrity. |
| Appointment slot scheduling model | The legacy model lets one slot hold one patient of one exam type on contiguous weekdays. Real clinics need: more than one patient per slot (multiple exam rooms), slots that accept several exam types, the ability to pick specific weekdays (Mon + Wed + Fri), and multiple time blocks per day (a lunch break). The new model supports all of this. |

### Screens that need to be built (the feature works, but admins cannot use it from a screen yet)

| Item | What is missing |
|---|---|
| Notification template editor | IT Admin cannot edit email template text from a screen. |
| System parameter editor | IT Admin cannot edit timing rules and cutoffs from a screen. |
| Custom field admin screen | Partial. |
| Audit log view across all appointments | A view per single appointment exists; an across-all-appointments view does not. |
| Document admin views across all appointments | A few legacy admin screens are absent. |
| Reschedule and cancellation submission screens | The action works; the user-facing screens need final confirmation. |

### Email content polish

About 40 email types currently send as plain text because their templates do not yet carry branded HTML content. The emails do reach the recipient; they just look unbranded. Bringing them to the legacy app's branded look is a content-migration task of about a week of focused work.

### Known bugs to close before launch

These are issues already identified during testing rounds. None are crash-level; most are correctness or polish.

| Issue | Impact |
|---|---|
| Insurance adjusters cannot complete a booking | Blocks one role's primary workflow. |
| Date picker rejects some appointment dates that should be allowed | Frustrating for end users; cosmetic on the math, real on the experience. |
| Some signup errors show as a generic "forbidden" instead of a clear "invalid input" message | Login form cannot render a friendly message for these. |
| Direct API calls can reject an appointment without typing a reason | The user-facing form requires it; a malicious or automated caller can bypass. Creates an audit-trail gap. |
| Document uploads have no size limit and no file-type restriction | Risk of someone uploading a huge file or a non-document. |
| Stale "login successful" banner appears in some sequences | Cosmetic confusion. |
| SMTP error messages in developer logs are noisy and misleading | Internal hygiene only; no end-user effect. |

There are also about twenty smaller observations (UX polish items) and three test-data convenience items. None block launch on their own.

---

## Recommended order and timeline

All estimates assume one developer working full time with Claude Code-aided pace and self-testing. Effort is expressed in **working days** (five days per week).

### Stage 1 -- Internal Alpha (target: 4-6 weeks from today)

Goal: a clean demo to leadership, on a single doctor's office, with the day-to-day workflows running end to end.

| # | Item | Effort |
|---|---|---|
| 1 | One-doctor-per-office safeguard | 1-2 days |
| 2 | Appointment slot scheduling rework (multi-room, multi-type, multi-weekday, multi-range) | 7-11 days |
| 3 | Patient check-in, check-out, no-show, and billed actions, plus today's-appointments view | 4-6 days |
| 4 | Close the known bugs above (booking blocked for adjusters; date picker; signup error mapping; rejection without reason) | 2-3 days |
| 5 | Document upload size limit and file-type allowlist | 1 day |
| 6 | Verify the reschedule and cancellation submission screens end to end | 1-2 days |
| 7 | Walk through all eighteen wired email flows on the test environment with real SMTP | 1-2 days |
| | **Stage 1 subtotal** | **17-27 working days (4-6 calendar weeks)** |

### Stage 2 -- Closed Beta with one pilot office (target: 3-5 weeks after Alpha)

Goal: real clinic staff use the system on test data; surface real-world user-experience issues.

| # | Item | Effort |
|---|---|---|
| 8 | Migrate the 40+ unbranded email templates to the legacy app's branded look | 5-8 days |
| 9 | Internal notes feature for staff (entity, backend, threaded UI) | 3-5 days |
| 10 | "Ask us a question" widget for external users | 1-2 days |
| 11 | Notification template editor screen for IT Admin | 2-3 days |
| 12 | System parameter editor screen for IT Admin | 1-2 days |
| 13 | Custom field admin screen | 1-2 days |
| 14 | Document admin views across all appointments | 3-5 days |
| 15 | "This patient already exists" prompt during staff approval | 1-2 days |
| 16 | Two-packet split: separate internal packet for the responsible team member | 1.5-2 days |
| 17 | Accessor invitation flow when the invitee is not already registered | 1-3 days (uncertain; needs investigation) |
| 18 | Verification pass: walk every item flagged as "to verify" in the gap analysis | 2-4 days |
| | **Stage 2 subtotal** | **22-38 working days (5-8 calendar weeks)** |

### Stage 3 -- Production launch for one office (target: 3-4 weeks after Beta)

Goal: real patients, real staff, live system at the first office.

| # | Item | Effort |
|---|---|---|
| 19 | Cumulative-trauma injury date-range UX, confirm and adjust | 1 day |
| 20 | Audit log view across all appointments | 1-2 days |
| 21 | Final security pass: HIPAA hygiene review and light load test | 3-5 days |
| 22 | Production hosting setup: cloud environment, domain, certificates, monitoring, backups | 4-6 days |
| 23 | Pilot rollout monitoring and hotfix capacity | 3-5 days |
| | **Stage 3 subtotal** | **12-19 working days (3-4 calendar weeks)** |

### Overall calendar

| Milestone | Best case | Realistic (30 percent buffer) |
|---|---|---|
| Internal Alpha | 4 weeks (mid-June 2026) | 6 weeks (early July 2026) |
| Closed Beta | 9 weeks (late July 2026) | 13 weeks (mid-August 2026) |
| Production launch (first office) | 12 weeks (mid-August 2026) | 17 weeks (mid-September 2026) |

### After Production

- Reports module: the Appointment Request Report and per-appointment Excel and PDF export, about 3-4 weeks.
- Adding more doctor offices: about 4-6 weeks per added office once the multi-office foundation is fully exercised.
- A merged Schedule Report across multiple offices.
- Per-office branding overrides: logo, clinic name, phone, fax.
- Pessimistic record locking decision (the new app currently uses an optimistic approach; the legacy app used pessimistic).

---

## Risks

- **Estimates assume no surprise scope.** Software projects routinely slip 20-40 percent. A 30 percent buffer is already baked into the realistic column above.
- **One developer means a single point of failure.** Illness, time off, or being pulled to another project slips dates 1-for-1.
- **No professional QA assumed.** Adding a third-party security audit or pen-test before production would add 2-3 weeks.
- **About seventy items in the gap analysis are flagged as "to verify."** Historically 5-15 percent of such items turn into rework. Stage 2 budgets 2-4 days for this sweep.
- **The legacy app is 7 years old.** Some of its user-interface behaviors look dated by today's standards. Faithfully reproducing them may surface "can we make it look more modern" requests at demo time. The recommended principle is: reach the legacy app's parity first, modernize visually after parity is signed off.

---

## Bottom line

- **Today, 2026-05-18:** the patient booking journey works end to end. Internal user creation, the Terms and Conditions screen, the consolidated login system, and the PDF document packet foundation all shipped this week.
- **Six weeks out:** demo-ready internal Alpha covering the daily clinic-staff workflow.
- **Three months out:** closed Beta with a pilot office using the system on test data.
- **Four months out:** production launch for the first office.
- **Beyond that:** the Reports module and additional offices.

The three biggest things that move the launch date are:

1. Keeping the developer unblocked and dedicated to this project.
2. Honoring the decision to defer the Reports module to after launch.
3. Honoring the decision to launch one office first, with additional offices following.
