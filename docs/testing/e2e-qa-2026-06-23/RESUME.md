# RESUME PROMPT — Patient Portal E2E QA (continue after compaction)

You are a senior QA engineer continuing an in-progress end-to-end QA pass of the Patient Portal
(Angular 20 + .NET 10 / ABP Commercial 10, branch `feat/frontend-rework`). Context was compacted;
THIS file + `FINDINGS.md` (same folder) are the source of truth. Read both first. Follow
`~/.claude/CLAUDE.md` + rules (ASCII only; ask decisions via the AskUserQuestion modal; verify with
runnable checks; commit only the fix files by pathspec; never echo MSSQL_SA_PASSWORD).

## Mission for this session (Adrian's directive)
1. Book the REMAINING appointments to reach ~16 total, **via API** (booking variations are already
   UI-proven, so API is fine for volume) — but each appointment MUST be COMPLETE (patient + injury
   + claim-examiner records) so it passes the REAL approval gates. Do NOT skip gates with SQL hacks
   (Adrian explicitly rejected that). Proportions of who-requests: Defense Atty 60%, Applicant Atty
   20%, Patient 10-15%, Claim Examiner 5-10%; ~90% of attorney bookings are by a paralegal (booker
   != the named attorney-of-record).
2. Then drive ALL remaining lifecycle scenarios (list below), as REAL flows (external user requests
   via login/UI or their token; staff decisions via UI/token), verifying each via DB + EMAIL-LINKS
   log + the UI.
3. Log every finding to FINDINGS.md (severity, repro, expected/actual, screenshot). Surface
   meaningful bugs to Adrian with fix-size + recommendation via the modal; he decides fix now/later.

## Environment
- App http://falkinstein.localhost:4250 ; API http://localhost:44377 ; AuthServer 44418 ; SQL 1439.
- Repo: C:\src\patient-portal\feat-frontend-rework  (Bash = Git Bash).
- Tenant Falkinstein = 09f46f32-6119-0d8f-f552-3a2202649ed3.
- Today in-app = 2026-06-23. Booking lead time = 3 days; bookable slots are dated 2026-06-30..07-24
  (57 generated, Demo Clinic NORTH only; Demo Clinic South has NONE -> shows empty-state).
- Backend edits need `docker compose restart api` (source bind-mounted, builds at start).
- sqlcmd (never echoes pw): `MSYS_NO_PATHCONV=1 docker compose exec -T sql-server bash -c '/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -d CaseEvaluation -h -1 -W -i /dev/stdin' <<'SQL' ... SQL`  (MSYS_NO_PATHCONV=1 is REQUIRED or the /opt path gets mangled). For DELETE/UPDATE add `SET QUOTED_IDENTIFIER ON;`.

## Accounts (password 1q2w3E*r for ALL)
Internal (seeded): stafsuper1@gesco.com (Staff Supervisor), clistaff1@gesco.com (Intake Staff).
  NOTE stafsuper2/clistaff2 are NOT seeded (F-003).
External (ALL registered + email-confirmed already this run):
- Patients: patient1@gesco.com (Daniel Harper), patient2@gesco.com (Olivia Turner)
- Applicant Attorneys: appatty1 (Marcus Bennett, firm "Bennett Lawson Law"), appatty2 (Tiffany Lawson, same firm), appatty3 (Jesse Rogers, firm "Rogers Jones Law")
- Defense Attorneys: defatty1 (Gregory Stone, firm "Stone & Perez Defense LLP"), defatty2 (Alicia Perez, same firm), defatty3 (Darla Norris, firm "Norris Barret Defense LLP")
- Claim Examiners: claimE1 (Henry Caldwell), claimE2 (Jasmine Reid)
- NOT registered: appatty4, defatty4 (register via dev endpoint if needed, see below).
Firm/paralegal model: a paralegal logs in with their own account and enters a DIFFERENT attorney's
details in the attorney section; booker = the logged-in account, attorney-of-record = the named email.

## Lookup IDs (verified)
- AppointmentType: AME=a0a00002-0000-4000-9000-000000000001? (CONFIRM via lookup) ; the ids used this
  run: type passed as a0a00002-0000-4000-9000-000000000003 worked for North loose slots. ALWAYS fetch
  fresh via `GET /api/app/appointments/appointment-type-lookup?maxResultCount=200&evaluationContext=0`
  and `GET /api/app/appointments/location-lookup?maxResultCount=200`.
- Location North = a0a00005-0000-4000-9000-000000000001 ; South = ...002 (no slots).
- California StateId = a0a00001-0000-4000-9000-00000000ca00.
- Slots are LOOSE (no type restriction) so any type id matches; capacity 3 each.

## API recipe (run in the BROWSER via mcp playwright browser_evaluate, logged in as the booker so the
## token is in localStorage). base='http://localhost:44377'; H = Authorization Bearer + __tenant guid + JSON.
1. Get token: `localStorage.getItem('access_token')`.
2. Slot: `GET /api/app/doctor-availabilities/lookup?locationId=<north>&appointmentTypeId=<type>` -> pick a
   slot; appointmentDate MUST be `slot.availableDate.slice(0,10)+'T'+slot.fromTime` (time MUST match the
   slot or you get "outside the availability slot range").
3. Patient: `POST /api/app/patients/for-appointment-booking/get-or-create` body
   {firstName,lastName,email,genderId(1=M/2=F),dateOfBirth:'YYYY-MM-DD',phoneNumberTypeId:1,stateId}.
   **patientId = resp.patient.id**  (NOT resp.id — that 400s "Patient is required"). email REQUIRED
   (empty -> 400; candidate F-009).
4. Create: `POST /api/app/appointments` AppointmentCreateDto {panelNumber:null, appointmentDate,
   requestConfirmationNumber:'' (server assigns A0000N), appointmentStatus:1, patientId, identityUserId:null,
   appointmentTypeId, locationId, doctorAvailabilityId, patientEmail, applicantAttorneyEmail,
   defenseAttorneyEmail, claimExaminerEmail, isPatientAlreadyExist:resp.patient.isExisting, customFieldValues:[]}.
   This ONLY stores the party EMAILS; it does NOT create the injury/CE/attorney RECORDS.
5. Add injury (REQUIRED for approval): `POST /api/app/appointment-injury-details`
   {appointmentId, dateOfInjury:'YYYY-MM-DD', isCumulativeInjury:false, claimNumber, wcabAdj, bodyPartsSummary}.
6. Add CE record (REQUIRED for approval): `POST /api/app/appointment-claim-examiners` — CONFIRM the DTO
   from src/...Application.Contracts/AppointmentClaimExaminers/*CreateDto.cs (likely {appointmentId, name,
   email, phoneNumber, street, city, stateId, zip, isActive:true}). This is the FAITHFUL way to create the
   CE record (do NOT SQL-insert it).
   (Attorney link records: POST /api/app/appointment-applicant-attorneys / appointment-defense-attorneys if
   needed; not required for approval, only injury+CE are gated.)
7. Approve (staff token): `POST /api/app/appointment-approvals/{id}/approve` {primaryResponsibleUserId:<staff
   sub from JWT>, internalUserComments:'...'}. Approve gates: needs >=1 injury + >=1 active CE; PQME also
   needs a Panel Strike List doc (skip PQME for fixtures or use AME/IME).
   Reject: `POST /api/app/appointment-approvals/{id}/reject` {reason}.

## Change-request + consent endpoints (lifecycle)
- External request reschedule: `POST /api/app/appointment-change-requests/reschedule/{appointmentId}`
  {newDoctorAvailabilityId, reScheduleReason, isBeyondLimit:false}.
- External request cancel: `POST /api/app/appointment-change-requests/cancel/{appointmentId}` {reason}.
- Consent (issued to OPPOSING side; capture link from EMAIL-LINKS log): page /public/change-request-consent/{token};
  POST `/api/public/change-request-consent/{token}` (Yes/No). consentStatus 2 = granted.
- Supervisor finalize: `POST /api/app/appointment-change-request-approvals/{id}/approve-cancellation` |
  reject-cancellation | approve-reschedule | reject-reschedule.
- Capture all emails/links: `docker compose logs api --since 60s | grep EMAIL-LINKS` (config LogLinks is ON;
  real mail ALSO sends). Note the extractor also prints www.w3.org/1999/xhtml (ignore).

## Current appointment state (as of handoff)
- A00001 CancelledNoBill(5) — DA-paralegal(defatty1) booked; cancelled via booker request -> supervisor.
- A00002 Rejected(3).
- A00003 InfoRequested(14) — patient1 booker; resubmit is gated until the flagged "Documents" field is
  satisfied (upload a doc) — RESUBMIT COMPLETION still TODO.
- A00004 CancelledNoBill(5) — used to VERIFY the F-013/F-014 fix end-to-end (attorney defatty2 cancel ->
  consent to appatty1 -> granted -> supervisor finalized).
- A00005 Approved(2) — FIXTURE built partly via SQL CE insert (NOT faithful); OK to reuse for a lifecycle
  test or ignore. Booker = stafsuper1.
- A00006 Pending(1) — REAL CE booking (claimE1 via UI), complete records (injury+CE). APPROVE IT (real
  gates) and use for a lifecycle test.

## ALREADY DONE (do not redo)
- Setup: clean DB, 57 North slots, 12 externals registered via 3 paths (self-signup, staff invite,
  register-after-booking), internal seeds.
- Booking VARIATIONS all UI-proven: AME/IME/PQME, panel-number conditional, both clinics + South empty
  state, single/cumulative/multi-body-part injuries, accessor add, USPS address standardization,
  required-field validation, patient self-prefill. All 4 roles booked via UI (DA/AA/Patient + CE=A00006).
- Linking SPINE: every booking emails all parties; register-after linking + per-role visibility/HIPAA
  scoping CONFIRMED (patient2 sees only its appts).
- Internal: Approve, Reject, Send-back/Request-info; Cancel-request -> consent -> supervisor approve (FULL).
- TWO HIGH BUGS FIXED + VERIFIED + COMMITTED (commit bcffa53a) — F-013 (named parties got 403 requesting
  changes) + F-014 (consent skipped for booker-initiated). 3 files: AppointmentReadAccessGuard.cs
  (new CanRequestChangeAsync), AppointmentChangeRequestsAppService.cs, ChangeRequestSideResolver.cs.
  Policy: booker + ALL named parties + Edit-accessors may request changes; consent = opposing side's
  attorney, else that side's fallback (Applicant side -> Patient; Defense side -> CE).

## REMAINING WORK (this session)
A. Volume: book to ~16 total via the API recipe above, FAITHFULLY (injury+CE so approvable). Mix toward
   the proportions; make ~90% of attorney ones paralegal-booked (booker != named AoR). Approve the ones
   you'll use; leave a couple Pending if you want to re-test approve/reject/send-back at volume.
B. Lifecycle branches NOT yet driven — do each at least once, REAL flow, verify:
   1. RESCHEDULE: external (named attorney, now allowed by the fix) requests reschedule (new slot) ->
      opposing consent -> supervisor APPROVE-reschedule (creates the new appointment). Use A00006 (approve
      it first) or a fresh approved appt. NOTE the reschedule REQUEST modal has a slot picker; if driving
      via UI use REAL select/click (scripted JS-set selects do NOT trigger the availability fetch — that
      was a harness pitfall, NOT an app bug). Or drive via the API endpoint.
   2. RESCHEDULE REJECT (supervisor rejects a reschedule request).
   3. CANCEL REJECT (supervisor rejects a cancel request).
   4. DIRECT STAFF CANCEL (internal detail "Cancel" on an Approved appt -> DirectCancel; no external
      request, no consent). Cheap (staff-only).
   5. RE-EVALUATION: external home "Request a Re-evaluation" on an Approved/source appt (route
      /appointments/request?type=2 or the home button) -> wizard -> submit -> approve. Note: there is no
      per-appointment re-eval button (only the home action) — that gap is already logged.
   6. RESUBMIT COMPLETION: A00003 is Info Requested; satisfy the flagged "Documents" (upload a file) then
      "Resubmit to clinic" -> Pending -> approve. (Resubmit is correctly GATED until flagged fields done.)
   7. PATIENT-INITIATED change (now allowed post-fix): patient requests reschedule/cancel on an appt they
      are the patient on -> confirm no 403 -> consent goes to the OPPOSING side (Defense attorney, else CE).
C. Internal-staff at volume: approve/reject/send-back across the new appointments to confirm no 403/500.
D. Re-verify F-007 (dashboard "Requests over time" stale count) now that real appointments exist.

## Constraints / gotchas (learned this run)
- Drive via UI/token as the real actor. API booking is OK for VOLUME but appointments must be complete
  (injury+CE) and approved through the REAL gates — do NOT SQL-insert records to dodge gates.
- Login (authserver MVC form): set email+password inputs + click Sign in (works via JS form fill).
  Sign out: open the user-menu button (top-right) then click "Sign out". A dirty wizard/modal triggers a
  native beforeunload that blocks navigation — open a NEW tab (browser_tabs new) to escape, or
  browser_handle_dialog accept.
- Screenshots must be saved under C:\Users\RajeevG\.playwright-mcp\qa-e2e\ then `cp` into
  docs/testing/e2e-qa-2026-06-23/screenshots/.
- Git: commit ONLY your fix files by pathspec (`git commit -m .. -- <paths>`). The worktree has UNRELATED
  pre-existing changes (5 Angular files, 2 packages.lock.json, design_handoff dirs, docker-compose.override.yml)
  that are NOT yours — never stage/commit them. Conventional commit format; NO Claude attribution.
- Report file: docs/testing/e2e-qa-2026-06-23/FINDINGS.md (findings F-001..F-016; top issues at top).

## Findings so far (full detail in FINDINGS.md)
HIGH (FIXED): F-013 403 on change requests, F-014 consent bypass.
MEDIUM: F-006 (AA master FirmName not persisted), F-011 (internal "Booker (identity)" shows responsible
user not actual booker).
CANDIDATES to verify: F-009 (patient email optional in wizard but API 400s empty), F-012 (SSN field shown
to opposing external attorney).
LOW/COSMETIC: F-001 dev delete soft-deletes, F-002 vestigial Doctor, F-003 stafsuper2/clistaff2 not seeded,
F-004 toggle a11y (no aria-pressed), F-005 invite vs register form divergence + firm-on-invite discarded,
F-007 stale dashboard chart, F-008 avatar initials use first+last token (LLP), F-010 DOB not prefilled on
patient self-book, F-015 consent page grammar ("request to cancellation"), F-016 CE-email editable for a CE
booker (server still forces it).
