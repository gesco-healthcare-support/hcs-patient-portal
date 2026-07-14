# Patient Portal -- End-to-End Manual QA Plan

- Date: 2026-06-23
- Branch: feat/frontend-rework
- Driver: Playwright MCP against http://falkinstein.localhost:4250 (api 44377, auth 44418, sql 1439)
- Tenant: Falkinstein (09f46f32-6119-0d8f-f552-3a2202649ed3)
- Run mode: INTERACTIVE. Adrian reachable throughout. Per-bug gating (see Bug Protocol).

## 1. Objectives (in priority order)

1. APPOINTMENT LINKING + EMAIL DELIVERY (the spine). Every party whose email is on a
   request (booker, attorney-of-record, opposing attorney, patient, claim examiner) must:
   (a) receive the email (verified via the EMAIL-LINKS dev log), and
   (b) once they hold a registered account with the matching email + correct role, SEE and
   be able to MANAGE the appointment. Parties NOT on the appointment must NOT see it.
2. NO surprise 403/500/console errors on intuitive actions across every role.
3. Correct, role-appropriate, legally/HIPAA-sound data visibility per screen.
4. Business-flow coherence end to end.
5. Modern UI/UX: layout, affordances, empty/error/loading states, basic accessibility.

## 2. The "book on behalf" model (confirmed with Adrian 2026-06-23)

- Login is by unique email. Attorneys register with Firm Name + email; email is unique but
  MANY emails can share one Firm Name => multiple people per firm.
- A paralegal has their own account (own email) and logs in as themselves. On the booking
  form they enter the ATTORNEY's details (not their own) in the attorney section.
- `Appointment.BookedByUserId` = the logged-in account (paralegal when a paralegal books;
  the attorney when they self-book).
- The appointment links to every party by EMAIL: when a person registers an account with an
  email that appears on the appointment AND the correct role, they get linked and can view/
  manage. So linking + email delivery are the make-or-break behaviors.
- 4 external roles are capability-equal (book / re-eval / view / request changes); they
  differ only in stored data. The booker need not be a stakeholder.

## 3. Confirmed environment facts (from Phase 0 investigation)

- Email: outbound SMTP is LIVE (in-house creds in appsettings.secrets.json). Real mail WILL
  send to the @gesco.com inboxes. Link capture: `docker compose logs api | grep EMAIL-LINKS`
  (logging already enabled in docker-compose.override.yml). Emails are NOT silenced.
- Consent gating is ON (AppointmentChangeRequestConsts.ConsentGatingEnabled = true). A
  reschedule/cancel REQUEST issues a consent token and emails the OPPOSING side; a supervisor
  cannot finalize until consent is granted via /public/change-request-consent/{token}.
- Lead time = SystemParameter.AppointmentLeadTime (default 3 days). Slots dated within
  today+lead are permanently non-bookable. MUST generate slots dated >= today+3 (we use
  ~7-28 days out) or the calendar is fully disabled.
- "Doctors" is a vestigial concept: AppDoctors is empty by design; availabilities have no
  DoctorId and the booking path never touches AppDoctors. NOT a blocker. Logged as a
  low-severity finding (misleading naming); no code change this run.
- Booking route /appointments/add renders AppointmentWizardComponent (9 steps): schedule,
  patient, applicant, defense, insurance, examiner, claim, docs, review.
- Re-evaluation has NO UI button on Approved appointments; reachable only via
  /appointments/add?type=2 + manual source-confirmation entry. We test via that route and
  log the missing affordance as a finding.
- Not implemented in UI (state machine only): No-Show, Check-In, Check-Out, Bill. Logged as
  not-implemented; not tested.

## 4. Appointment status model (for reference)

Pending(1) Approved(2) Rejected(3) NoShow(4) CancelledNoBill(5) CancelledLate(6)
RescheduledNoBill(7) RescheduledLate(8) CheckedIn(9) CheckedOut(10) Billed(11)
RescheduleRequested(12) CancellationRequested(13) InfoRequested(14)

Who does what:
- External (Patient/AA/DA/CE): request reschedule, request cancellation, resubmit from
  InfoRequested, re-request from Rejected, request re-eval. (Edit is accessor-scoped.)
- Intake Staff (= "Clinic Staff"): approve, reject, send-back (request info) on Pending.
- Staff Supervisor: all of the above PLUS approve/reject change requests (reschedule/cancel)
  and direct cancel.

## 5. Accounts and firm map (password 1q2w3E*r for all)

Internal (already seeded; verify):
- stafsuper1 Patrick O'Neal (Staff Supervisor), stafsuper2 Denise Fowler (Staff Supervisor)
- clistaff1 Rachel Kim (Intake Staff), clistaff2 Luis Mendoza (Intake Staff)

External (register fresh in Phase 1). Firm groupings (synthetic firm names):
- Defense firm "Stone & Perez Defense LLP": defatty1 Gregory Stone, defatty2 Alicia Perez
- Defense firm "Norris Barret Defense LLP": defatty3 Darla Norris, defatty4 Skye Barret
- Applicant firm "Bennett Lawson Applicants LLP": appatty1 Marcus Bennett, appatty2 Tiffany Lawson
- Applicant firm "Rogers Jones Applicants LLP": appatty3 Jesse Rogers, appatty4 Alexa Jones
- Patients: patient1 Daniel Harper, patient2 Olivia Turner
- Claim Examiners: claimE1 Henry Caldwell, claimE2 Jasmine Reid

Within a firm, "paralegal books for attorney" = one firm account books and enters the OTHER
firm account's email in the attorney section; the second account then logs in to verify it
sees + can manage the appointment.

## 6. Phase 1 setup (do first; nothing is bookable otherwise)

S1. Self-clean DB to a controlled baseline (Adrian may also rebuild; this is idempotent):
    - Delete external + verify.* test users via POST /api/public/external-signup/dev/delete-test-users.
    - Clear residual appointments + change requests + stale availabilities via SQL
      (SET QUOTED_IDENTIFIER ON; run inside sql-server container via stdin heredoc; never
      echo the SA password). Confirm internal users remain.
S2. Confirm prerequisites: AppointmentTypes (PQME/AME/IME) and Locations (Demo Clinic
    North/South) exist. (Verified present.)
S3. Generate bookable availability via the Doctor Availabilities UI: slots dated
    today+7 .. today+28, both locations, all appointment types, business hours, capacity 3.
    Verify slots appear and fall in the bookable window.
S4. Register the 12 external accounts. Prefer the real registration UI for at least one of
    each role-shape (patient, attorney/firm, claim examiner) to assess the registration UX;
    use the dev register endpoint for the remainder to avoid the ~5/min rate limit (429).
    Pace registrations. Then POST .../dev/mark-email-confirmed for each so they can log in.
S5. Smoke test: log in as one external + one internal account; confirm dashboards load with
    no console errors.

## 7. Booking matrix (16 appointments; DA 10 / AA 3 / Patient 2 / CE 1)

Legend: Booker = who logs in to book. AoR = attorney-of-record (email in section).
Each appointment is an injured-worker eval with patient + applicant side + defense side
(+ CE/insurance where noted). ~90% of attorney bookings are paralegal-on-behalf.

| # | Req role | Booker (login) | DA AoR | AA AoR | Patient | CE | Lifecycle to exercise |
|---|----------|----------------|--------|--------|---------|----|-----------------------|
| 1 | DA | defatty1 (paralegal) | defatty2 | appatty1 | patient1 | claimE1 | Happy path -> Intake approve. Then patient1 + defatty2 + appatty1 + claimE1 each log in: verify they SEE it; verify a non-party (defatty3) does NOT. |
| 2 | DA | defatty2 (paralegal) | defatty1 | appatty2 | patient2 | - | Approve -> booker requests RESCHEDULE -> opposing AA appatty2 grants CONSENT via link -> supervisor APPROVES reschedule |
| 3 | DA | defatty3 (paralegal) | defatty4 | appatty3 | (record-only, unregistered) | - | Approve -> attorney defatty4 logs in + requests CANCEL -> appatty3 CONSENT -> supervisor APPROVES cancel |
| 4 | DA | defatty4 (paralegal) | defatty3 | appatty4 | patient1 | - | Submit -> Intake SEND-BACK (info request) -> booker RESUBMITS -> approve |
| 5 | DA | defatty1 (paralegal) | defatty2 | appatty1 | patient2 | - | Submit -> Intake REJECT -> booker RE-REQUESTS from rejected -> approve |
| 6 | DA | defatty2 (paralegal) | defatty1 | appatty2 | patient1 | - | Approve -> reschedule request -> supervisor REJECTS reschedule |
| 7 | DA | defatty3 (paralegal) | defatty4 | appatty3 | patient2 | - | Approve -> cancel request -> supervisor REJECTS cancel |
| 8 | DA | defatty4 (paralegal) | defatty3 | appatty4 | patient1 | - | Approve -> supervisor DIRECT CANCEL (no external request) |
| 9 | DA | defatty1 (paralegal) | defatty2 | appatty1 | patient1 | - | Approve -> RE-EVAL via ?type=2 from this source -> new appt approve |
| 10| DA | defatty2 (SELF-book) | defatty2 | appatty2 | patient2 | - | Self-book (booker==attorney) -> approve |
| 11| AA | appatty1 (paralegal) | defatty1 | appatty2 | patient1 | claimE2 | Approve -> PATIENT patient1 requests RESCHEDULE -> opposing DA defatty1 CONSENT -> supervisor approves |
| 12| AA | appatty3 (paralegal) | defatty3 | appatty4 | patient2 | - | Approve -> AA requests CANCEL -> DA defatty3 CONSENT -> supervisor approves |
| 13| AA | appatty4 (SELF-book) | defatty4 | appatty4 | patient1 | - | Self-book -> approve |
| 14| Patient | patient1 (SELF) | defatty1 | appatty1 | patient1 | - | Self-book -> approve; verify patient sees own booking + can request a change |
| 15| Patient | patient2 (SELF) | defatty2 | appatty2 | patient2 | - | Submit -> SEND-BACK -> patient RESUBMITS -> approve (patient experiences info-request) |
| 16| CE | claimE1 (SELF) | defatty2 | appatty2 | patient2 | claimE1 | CE self-books -> approve; verify CE sees + can request change |

Internal action coverage check: approve (most, via clistaff + stafsuper), reject (#5),
send-back+resubmit (#4,#15), reschedule approve (#2,#11), reschedule reject (#6), cancel
approve (#3,#12), cancel reject (#7), direct cancel (#8), re-eval approve (#9). Both Intake
Staff and Staff Supervisor exercised. Patient-initiated and attorney-initiated change
requests both exercised. Opposing-side consent exercised on #2,#3,#11,#12.

## 8. Per-screen QA lens (apply at every screen, screenshot evidence)

- Works: no 403/500, no console error, network calls succeed.
- Data correctness: right role sees exactly the right data; negative test that it CANNOT see
  others' data (HIPAA/legal). Linking reflects every party email.
- Business sense: the action and its result match the medical-legal workflow.
- UI/UX: layout/responsive, clear affordances + labels, empty/loading/error states,
  keyboard focus + contrast (basic a11y), validation messages.

## 9. Linking + email verification protocol (per appointment)

1. After submit, read EMAIL-LINKS log: confirm an email went to EACH party email on the
   request. Record recipients + link types.
2. For appointments #1, #11, #16 (representative full checks) and lighter checks elsewhere:
   log in as each registered party and confirm visibility + available actions; confirm a
   non-party cannot see it. Log where checks are lighter and why.
3. For consent flows, follow the /public/change-request-consent/{token} link from the log.
4. For email-confirmation, follow /Account/EmailConfirmation link OR use dev/mark-email-confirmed.

## 10. Bug protocol (interactive)

- Log every issue immediately to FINDINGS.md with: title, severity
  (blocker/high/medium/low/cosmetic), exact repro, expected vs actual, screenshot path,
  role/flow.
- For each meaningful bug: surface to Adrian with severity + fix-size estimate (S/M/L +
  reasoning) + recommendation (fix now / later), then ask via the question modal. Batch
  low/cosmetic ones. If Adrian says "fix now": patch, restart the affected container
  (docker compose restart api|angular), regen the Angular proxy if a backend API changed,
  then continue. Otherwise keep driving.
- Blockers that halt a flow: log, attempt a reasonable workaround to keep coverage, and
  surface immediately.

## 11. Outputs

- docs/testing/e2e-qa-2026-06-23/FINDINGS.md (running report, blockers first).
- docs/testing/e2e-qa-2026-06-23/screenshots/ (evidence; named <step>-<role>-<desc>.png).
- Coverage section at the end: tested vs skipped (+ why) and top issues.

## 12. Out of scope (logged, not driven)

- No-Show / Check-In / Check-Out / Bill (UI not implemented).
- Doctor entity removal (vestigial; Adrian chose note-and-proceed).
- Load/perf/security pen-testing (functional + UX QA only this pass).
