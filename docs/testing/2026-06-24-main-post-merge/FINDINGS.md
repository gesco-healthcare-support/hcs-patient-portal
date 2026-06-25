# FINDINGS -- MAIN post-merge QA (2026-06-24)

Senior-QA pass on `main` after merge #322 (frontend-rework epic). Fresh DB seeded then driven.
Synthetic data only. Severity: HIGH (blocks/real-user-impact) / MED / LOW / OBS (observation).

Env: tenant Falkinstein `dfed8778-1dd8-08b4-0c07-3a220ce0b86a` (subdomain falkinstein);
SPA :4200, AuthServer :44368, API :44327, SQL :1434; in-app today 2026-06-24.

---

## Run summary (2026-06-24)
- Built `main` from scratch (fresh DB), seeded + drove the app end-to-end as real users.
- DATA: 17 appointments, ALL complete (injury + active CE + AA link + DA link + employer + insurance);
  0 duplicate masters. Status: 8 Approved, 7 Pending, 1 Rejected, 1 Rescheduled. Bookers: DA ~8
  (defatty3), AA ~4, Patient ~3, CE ~1 (+1 reschedule child). DA-booker share ~50% (target 60%) --
  capped by F-H01 (only 1 defense attorney could register; see below).
- Method: 3 appointments via the reworked UI wizard BEFORE the bulk (A00001 AA-booked, A00002
  patient-booked, A00017 DA-booked post-bulk) + 13 via the faithful complete API recipe; UI-after
  checks confirmed the API data renders correctly and the wizard still works post-bulk.
- FIXES VERIFIED (UI + DB/email evidence): F-006, F-007, F-011, F-013, F-014, F-017, F-018 (details
  below). NEW HIGH bug found: F-H01 (attorney register-after-booking 500).
- FULL LIFECYCLE SET now driven (round 2): reschedule-reject PASS (A00012 stays Approved);
  cancel-request+consent+approve PASS (A00014 -> CancelledNoBill); cancel-reject PASS (A00006 stays
  Approved); DEFENSE-side-initiated change PASS (defatty3 named non-booker cancel -> 200 + consent to
  the APPLICANT attorney = mirror of F-013/14); direct staff cancel on an APPROVED appt PASS (A00008
  -> Cancelled, immediate); PQME approval gate PASS (409 "panel strike list required" -> upload ->
  200, A00018). Re-evaluation: DIAGNOSED + then completed -- it requires loading a prior approved
  appointment via the schedule-step "Load prior appointment" lookup; submitting from Review without
  one is a silent dead-end (F-M05). After loading A00010 it submitted (create-reval) -> A00019.
  Net new appointments: A00018 (PQME approved) + A00019 (re-eval pending). TOTAL = 19 appointments.

## Open findings

### F-H01 (HIGH) -- Attorney "register-after-booking" returns 500 (locked out of sign-up)
- An Applicant/Defense attorney NAMED on an appointment before they have an account CANNOT register:
  `POST /api/public/external-signup/register` -> HTTP 500. Common in the real flow (paralegal books +
  names the opposing attorney, who signs up later). CE + Patient unaffected.
- Root: `ExternalSignupAppService.RegisterAsync` (~line 962) inserts a NEW attorney master instead of
  adopting the booking-created placeholder master (IdentityUserId NULL) -> unique-index violation
  `IX_AppDefenseAttorneys_TenantId_Email` (2601) -> DbUpdateException -> 500.
- This is the F-006/F-019 "deeper layer" reverted on feat/frontend-rework; unfixed on main, now a hard
  500 (was a silent dup before the unique index). Decision pending: fold into multi-tenant work vs
  separate follow-up. Adrian will NOT fix here (avoid drift with the in-progress multi-tenant branch).
- FULL REPORT + fix sketch + repro: `F-H01-attorney-register-after-booking-REPORT.md` (this folder).
- This run: defatty1/defatty2 (named on A00001/A00002 first) -> 500; defatty3 + claimE1/2 -> OK.

### F-M01 (MED) -- Attorney signup hides First/Last name (only Firm name shown)
- Repro: AuthServer `/Account/Register` -> select "Defense Attorney" (or Applicant Attorney).
  The First name + Last name fields are REMOVED and replaced by a single "Firm name" field.
- Expected (per DTO doc `ExternalUserSignUpDto` B17 2026-05-07): "the Razor register form now
  shows both [first/last] fields for all roles -- only FirmName is attorney-only." So first/last
  should remain AND firm be added for attorneys.
- Actual: attorney form collects Firm name + email only; no personal name.
- Impact (to verify once a DA registers): attorney master `Name`/`Surname` may be blank, so the
  appointment detail attorney section shows firm but no contact person name.
- Status: OPEN, impact verification pending (blocked by F-M02 this session).

### F-M02 (MED, UX) -- Registration rate-limit (5/hr/IP) surfaces only as "Registration failed."
- The signup UI (browser) POSTs to the rate-limited API endpoint
  `http://falkinstein.localhost:44327/api/public/external-signup/register` (FixedWindow, PermitLimit=5,
  Window=1h, partitioned by client IP -- CaseEvaluationHttpApiHostModule). UI + API share the window.
- On the 6th attempt in the window the API returns 429 with a `Retry-After` header (good), but the
  SPA overlay shows a generic red "Registration failed." with NO indication it is rate-limiting and
  no "try again in N minutes." A real user cannot tell a transient throttle from a hard failure.
- Real-world friction: a law-firm office (shared/NAT IP) onboarding 6+ paralegals in an hour is
  blocked. Compare: password-reset has a per-IP secondary limit of 50/h; register is 5/h/IP.
- Status: OPEN (the 5/h limit itself is intentional security; the finding is the opaque UX + the
  low per-IP cap for shared-office onboarding). Working-as-designed on the security control.
- Severity rationale: not a security hole; a UX + onboarding-friction issue.

### F-M03 (LOW) -- Staff "New appointment" links to legacy /appointments/add
- The internal header + Appointments-list "New appointment" button routes to `/appointments/add` (the
  legacy single-page form), not the reworked wizard. Adrian wants legacy pages removed; this is a live
  link to one. Scope: legacy-removal sweep.

### F-M05 (MED, UX) -- Re-evaluation submit is a silent dead-end UNLESS a prior appointment is loaded
- ROOT CAUSE (diagnosed in code): re-eval (`bookingMode='reval'`, `?type=2`) requires loading a prior
  APPROVED appointment via the "Load prior appointment" lookup on the SCHEDULE step (enter its
  confirmation #, e.g. A00010 -> `loadRevalSource` -> sets `sourceConfirmationNumber`). `onSubmit()`
  (`appointment-add.component.ts:1810`) gates on it:
  `if (bookingMode !== 'new' && !sourceConfirmationNumber) { this.sourceLoadMessage = 'Look up the
  prior approved appointment by confirmation number before submitting.'; return; }`
  The create routing (`createAppointmentForCurrentMode`, ~1733) only calls `createReval(source,...)`
  when `sourceConfirmationNumber` is set.
- THE UX BUG (two parts):
  1. The wizard lets you complete ALL 9 steps and reach Review WITHOUT loading a source (no step-1
     gating / no stepper error), so you can fill everything then dead-end.
  2. The guard's only feedback, `sourceLoadMessage`, renders on the SCHEDULE step (template
     `appointment-wizard.component.html` ~96). When you click Submit from the REVIEW step, the message
     is set off-screen -> from the user's vantage Submit does nothing (no POST, no toast, no error).
  My initial run used the "Find Existing Patient" lookup (step 2, reuses patient details only) instead
  of "Load prior appointment" (step 1, sets the reval anchor) -> hit the guard.
- VALIDATED: loading prior appointment A00010 then submitting -> `POST /api/app/appointments/
  create-reval/A00010` 200 -> A00019 created complete (full child cascade + emails). So re-eval WORKS;
  the defect is the silent dead-end + no Review-step feedback + no step gating.
- Recommend: (a) surface the guard message on the Review step (or disable Submit with a tooltip), and
  (b) gate progression / show a stepper error when reval has no source loaded.
- SECONDARY: the reval child (A00019) has `OriginalAppointmentId = NULL` -- it is NOT linked back to
  its source (A00010) via that field (reschedule children DO set it). The follow-up may be untraceable
  to the prior appointment unless the link is stored elsewhere. Worth a dev check.
- FULL REPORT (for hand-off to the multi-tenant session): `F-M05-reevaluation-submit-deadend-REPORT.md`
  (this folder) -- frontend-only UX fix + the backend OriginalAppointmentId question + conflict notes.

### F-M04 (MED, UX) -- Staff "Cancel" on a PENDING appointment fails with a generic error
- Staff detail action bar offers "Cancel" on a Pending appointment. Clicking it + entering a reason
  POSTs `/api/app/appointment-change-requests/cancel/{id}` -> 403 with generic "An internal error
  occurred during your request!" (domain `BusinessException` at AppointmentChangeRequestManager
  SubmitCancellationAsync:122). A Pending appointment should be Rejected, not cancelled; the Cancel
  action should be hidden/disabled there or give a clear message. Worse: the failed dialog stays
  stuck open (Close/Escape do not dismiss it; required a page reload). Reject on the same Pending
  appointment works (A00009 -> Rejected).
- Not verified this run: staff cancel on an APPROVED appointment (the valid case) -- recommend
  checking it routes correctly (direct cancel vs opposing-consent).

### F-010 (LOW) -- Patient self-book prefill is identity-only (DOB/gender/address blank)
- When the patient self-books, name/email/cell prefill from the identity, but DOB, gender, and address
  are blank even though the patient already has a patient record (from a prior booking). Confirms prior
  F-010 on main; prefill does not pull the patient master.

---

## Merged-fix verifications (all via real UI flow + DB/email evidence)
- **F-013 PASS**: patient1 (named patient, NOT the booker -- booker is appatty1) requested a reschedule on
  approved A00001 -> HTTP 200 (no 403). Named non-booker parties can request changes.
- **F-014 PASS**: that request emailed a consent link to the OPPOSING side -- defatty1 (defense attorney)
  via `ChangeRequestConsent ... A00001`. Applicant-side requester -> defense attorney consent. Consent
  granted via the public link; staff queue showed "Applicant side filed / Consent received".
- **F-017 PASS**: supervisor approved the reschedule -> NEW child appointment A00003 created with
  AppointmentDate = 2026-07-15 13:30:00 (slot time PRESERVED, not midnight); source A00001 -> status 7
  (Rescheduled), child OriginalAppointmentId = A00001.
- **F-018 PASS**: staff "Request info" on A00002 flagging Documents -> Info Requested. Booker (patient1)
  resubmit via the endpoint while Documents unsatisfied -> HTTP 403 "Please complete all the requested
  corrections before resubmitting to the clinic." After uploading a valid PDF (200, magic-byte check) ->
  resubmit -> HTTP 204. Server-side gate enforced (not just the disabled button).
- **F-006 PASS**: AA registration persists email to master; booking reuses existing AA master by email
  (appatty2 reused with IdentityUserId set, no dup). DA/AA/Patient masters 1-per-email after 2 bookings.
- **F-011 PASS (apparent)**: staff detail "Booker (identity)" shows appatty1@gesco.com (the ACTUAL booker),
  not the responsible user.
- **F-007 PASS (apparent)**: dashboard "Requests over time" reflects real counts (Received 2), not stale 0.

## Verified-good (positive QA results)
- Booking wizard (9 steps) end-to-end x2 COMPLETE appointments (A00001 AA-booked AME, A00002 patient-booked
  IME cumulative w/ 2 body parts). DB audit: every child record present (injury, active CE, AA link, DA
  link, employer, insurance). Slot time stored correctly (10:00, 14:00).
- Smarty address autocomplete + USPS standardization dialog (suggested vs keep-mine) -> standardized ZIPs
  persisted (90012-4801, 92101-3311).
- South empty-state message + 3-day lead-time enforced in date picker.
- Staff approval through REAL gate (injury + active CE) -> Approved; fired stakeholder + responsible
  emails AND generated WeasyPrint packets (Patient Packet 1.87 MB; Attorney/CE Packet) emailed w/ deep
  links + PDF attachments. Packet renderer sidecar works.
- Patient self-booking visibility + packet download surfaced to patient.
- Self-signup (Patient) via UI: account created + email-verification link delivered + clicking the
  link verifies and redirects to login (`flash=email-verified`). End-to-end OK.
- Applicant Attorney registration persists email to the AA master (F-006 from prior pass holds on
  main: AppApplicantAttorneys rows for appatty1-3 have Email populated).
- Fresh-DB seed clean: tenant Falkinstein + internal users (stafsuper1, clistaff1, admin) seeded;
  no Patient-role/OpenIddict seed gaps.

## Accounts registered this session
- Via UI signup: patient1@gesco.com (Daniel Harper, Patient) -- email-verified via link.
- Via API (register + dev confirm): patient2, appatty1 (Marcus Bennett/Bennett Lawson Law),
  appatty2 (Tiffany Lawson/same), appatty3 (Jesse Rogers/Rogers Jones Law).
- BLOCKED by F-M02 this hour: defatty1-3, claimE1-2 (to register after window reset ~18:55 or via
  a different path).
