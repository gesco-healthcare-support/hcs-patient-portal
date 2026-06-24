# Final go-live lifecycle test: HCS Patient Portal (post-fix verification) -- 2026-06-08

QA verification of local `main` after F1-F4 + appointment-type reduction. Verifying, not
fixing. Driven against the live Falkinstein compose stack. Synthetic data only.

Base commits (confirmed via `git log --oneline -6`):
`3db294b` F1/F2 | `b600a8f` type reduction | `4a3784f` F3 | `29b6f9d` F4, on top of `a1ecdde`.
Uncommitted diagnostic present: `SendAppointmentEmailJob` email-link logging +
`docker-compose.override.yml` (`Notifications__LogLinks=true`). Confirmed.

## Result table

| Track | Item | Status | Evidence |
|-------|------|--------|----------|
| 0 | Containers healthy; db-migrator exit 0; Falkinstein + data present | PASS | All 10 containers healthy/exited-0; `main-db-migrator-1` exit=0; migrator log "Successfully completed all database migrations"; Falkinstein present, 67 appts + 22 patients preserved in-place |
| 0 | Type reduction: 3 types, 0 legacy refs, dropdown shows 3 | PASS | DB: exactly 3 types (AME ...0003 / IME ...0007 / PQME ...0002), TypeCount=3, LegacyRefs=0. Booking-form Type dropdown shows exactly AME / IME / Panel QME |
| A | AME by internal Clinic Staff (clistaff1) -- F1/F2 core: ends Approved, AA/DA/CE=1/1/1, injuries>=1, NO 409 | PASS (after F-1 fix; also PASS via stafsuper1) | POST-FIX: clistaff1 AME A00075 -> CE 200, injury 200, body-parts 200, auto-approve 200, Approved AA/DA/CE/Injuries/BodyParts=1/1/1/1/1, no 403/409. PRE-FIX detail: | clistaff1: NO 409, but CE attach => 403 (lacks `AppointmentClaimExaminers.Create`); appt A00068 left Pending 1/1/0, injuries=0. stafsuper1 (Staff Supervisor): full sequence POST appt 200 -> AA 204 -> DA 204 -> CE 200 -> injury 200 -> `appointment-approvals/{id}/approve` 200, NO 409. Appt A00069 ends Status=2 (Approved), AA/DA/CE/Injuries = 1/1/1/1 |
| A | IME by Applicant Attorney (appatty1) -- ends Pending, joins 1/1/1, then internal approve | PASS (approve step below) | IME A00072 by appatty1: AA section auto-prefilled w/ appatty1's record. POST appt 200 -> AA 204 -> DA 204 -> CE 200 -> injury 200, NO approve call (external booking stays Pending), NO 409. DB: Status=1 Pending, AA/DA/CE/Injuries=1/1/1/1. IME Panel# field DISABLED. Then stafsuper1 approved A00072 -> `approve` 200, Status=2 Approved, 3 packets generated |
| A | PQME by Patient self (patient1) -- Panel# required+persists, ends Pending, then approved; AME/IME reject Panel# | PASS (approve step below) | PQME makes Panel# label "Panel Number *" + field ENABLED; AME (A00068/69) had Panel# DISABLED. Self-book A00071: POST appt 200 -> AA 204 -> DA 204 -> CE 200 -> injury 200, NO approve call (patient booking stays Pending), NO 409. DB: Status=1 Pending, PanelNumber="PQME-PNL-001" persisted, AA/DA/CE/Injuries=1/1/1/1. (patient1 CAN attach CE -> 403 is Clinic-Staff-specific, see F-1.) Then stafsuper1 approved A00071 -> `approve` 200, Status=2 Approved, 3 packets generated |
| B | Approval gates: Pending->Approved blocked without active CE + >=1 injury | PASS | Positive: A00069 (AME), A00071 (PQME), A00072 (IME) each approved 200 with CE+injury present. Negative: approving A00068 (CE=0, injuries=0) returned an error w/ `CaseEvaluation:Appointment.ApprovalRequiresInjuryDetail` "At least one Claim Information entry is required before the appointment can be approved" -> stayed Pending. (NOTE: gate maps to HTTP 403, not 400/409 -- see F-3) |
| B | Each approved appt -> 3 packets (Patient/Doctor/AttyCE Generated); Patient+AttyCE emailed w/ PDF; Doctor never emailed | PASS | A00069 logs: GenerateAppointmentPacketJob generated kind Patient (529548 B), Doctor (581972 B), AttorneyClaimExaminer (253994 B). Patient emailed to patient2 w/ PDF; AttyCE emailed to appatty2/defatty2/claime2 w/ PDF; Doctor has NO delivered line. NOTE: AttyCE row is hard-pruned after send (by design), so only Patient+Doctor persist in AppAppointmentPackets (Status=2) |
| B | AttyCE notice type-correct: PQME->pqme notice (DWC QME form); AME/IME->ame_ime notice | PASS (after F-4 fix) | POST-FIX: HTML pipeline enabled; renderer logs show PQME->`attorney-pqme` (DWC QME form), AME->`attorney-ame` (no QME form). PRE-FIX detail: | AttyCE PDF is pruned post-send, so content not inspectable without the PACKETS_HTML deeper check (which needs an api container recreate -> engine risk, skipped). Byte-size proxy is INCONCLUSIVE/concerning: AttyCE PDFs are near-identical across types (AME 253994 B, IME 254054 B, PQME 254306 B). A real multi-page DWC QME form would add many KB; the ~300 B spread looks like field-text variance only. Needs the proper content check before demo |
| B | F3: each AttyCE email sent exactly once; no AbpDbConcurrencyException, no Hangfire retry, no dup delivered | PASS | A00069: AbpDbConcurrencyException count=0, Hangfire retry count=0, each AttyCE recipient (appatty2/defatty2/claime2) has exactly ONE "delivered" line. Prune logged twice for same packet id with no exception -> idempotent set-based prune (F3) confirmed |
| C | Upload PDF + PNG; oversize (>10MB) + disallowed types rejected client+server | PASS | On A00072 doc manager (stafsuper1): qa-doc.pdf (243 B) + qa-image.png (70 B) uploaded -> 200, both listed w/ Approved badge. Disallowed qa-bad.txt: client error modal "Only PDF and image formats (JPG, PNG) are accepted" + server 403 at `EnsureValidFileFormat`. Oversize 11 MB: blocked client-side (no POST reached server). (server rejects map to 403 -- see F-3) |
| C | Moderation: external upload lands Uploaded/pending; internal approve one, reject one w/ reason; badges render | PASS | patient1 (external) uploaded 2 docs to A00071 -> both landed status "Uploaded" (vs internal uploads which auto-"Approved"). stafsuper1 approved "Patient Upload Two" -> Approved badge; rejected "Patient Upload One" w/ required reason -> Rejected badge + reason text rendered ("...illegible scan, please re-upload..."). All 3 badge states (Uploaded/Approved/Rejected) render correctly |
| D | F4: reschedule Approved appt -> NO 500, NEW row w/ NEW A##### confirmation, source -> terminal Rescheduled* | PASS | Rescheduled A00069 (stafsuper1): `change-requests/reschedule` 200 -> `change-request-approvals/{id}/approve-reschedule` 200 (NO 500). New row A00070 created (Status 2 Approved) w/ fresh confirmation A00070 != A00069. Source A00069 -> Status 7 (terminal Rescheduled). F4 fresh-confirmation fix holds |
| D | Cancel: raise cancellation CR; internal NoBill auto-approves, external stays Pending on supervisor queue | PASS (internal path) | Cancelled A00070 (stafsuper1): `change-requests/cancel` 200 -> `approve-cancellation` 200 (internal NoBill auto-approved, NO 500). A00070 -> Status 5 (Cancelled). External-booker path (stays Pending on supervisor queue) not exercised this run |
| E | Notifications appear as EMAIL-LINKS lines to right parties; ex-parte "Appointment Requested" is ONE email w/ CC | PASS | A00069: `AppointmentRequested` = ONE email to=patient2 cc=appatty2,defatty2,claime2 (cc=3, not fan-out). Also BookingSubmitted/ApproveReject -> staff queue; StatusChange/Approved/Stakeholders -> all 4 parties; PatientPacket -> patient2; AttyCEPacket -> 3 parties. All have recoverable login/view links (plus the harmless w3.org xhtml string) |

## Fixes applied (2026-06-08, round 2) -- verified

Source changed: `InternalUserRoleDataSeedContributor.cs`, `CaseEvaluationDomainErrorCodes.cs`,
`AppointmentDocumentsAppService.cs`, `CaseEvaluationHttpApiHostModule.cs`, `docker-compose.yml`.
Deployed via sequenced backend rebuild (db-migrator + api, built one at a time to respect the
12 GB WSL cap); F-4 was config-only. Re-verified live:

- **F-1 (FIXED, HIGH):** added the missing booking-child Create grants to the Clinic Staff role
  seed -- `AppointmentClaimExaminers`, `AppointmentPrimaryInsurances`, `AppointmentBodyParts`,
  `AppointmentAccessors` (the existing `AppointmentInjuryDetails` grant was the W2-8 precedent).
  Re-test: clistaff1 booked AME **A00075** -> CE 200, injury 200, body-parts 200, auto-approve
  200, ended **Approved, AA/DA/CE/Injuries/BodyParts = 1/1/1/1/1, no 403, no 409**. (Surfaced +
  fixed two sequential child-POST 403s: CE then body-parts.)
- **F-4 (FIXED, MEDIUM):** enabled the HTML->WeasyPrint packet pipeline for all three kinds
  (`Packets__HtmlPipeline__{Doctor,Patient,Attorney}` default flipped false->true). Re-test:
  packets now render `via HTML` (Patient 1.87 MB, Doctor 1.03 MB fillable); the AttyCE notice
  branches correctly -- PQME -> `attorney-pqme` (DWC QME form), AME -> `attorney-ame` (no QME
  form), confirmed in the packet-renderer logs.
- **F-3 (FIXED, LOW):** mapped the business-rule codes off ABP's default 403. Approve-without-
  injury / -CE -> **409 Conflict** (+ the SPA now shows the gate message instead of a silent
  403); disallowed file type -> **400 Bad Request**. Re-test: approving A00068 returned 409 with
  message; `.txt` upload returned 400.
- **F-2 (DEFERRED, LOW):** the blank-page-after-login is an ABP bootstrap/config-readiness timing
  artifact (self-heals on reload; the pending-count 401 is already guarded + swallowed and is not
  the cause). The only real fix touches app-wide routing/bootstrap (high blast radius) and cannot
  be deterministically reproduced/verified -- recommended as its own focused task rather than a
  speculative change in this batch.
- **Email (DEFERRED per request):** the notification emails are HTML-templated and sent over real
  SMTP; the earlier "SMTP delivery failed -- Configure ACS" log is a mislabeled provider
  rate-limit. No change made this round.

## Findings

### F-1 (HIGH): Clinic Staff (clistaff1) gets 403 attaching Claim Examiner during booking -> partial appointment, no auto-approve
- Repro: log in as `clistaff1` (Clinic Staff), book an AME with Patient + AA + DA + CE + 1 injury,
  Continue past the USPS modal.
- Observed network sequence: `POST /appointments` 200 -> `.../applicant-attorney` 204 ->
  `.../defense-attorney` 204 -> `POST /appointment-claim-examiners` **403** -> flow aborts
  (no injury-detail POST, no approve call).
- api log: `PermissionRequirement: CaseEvaluation.AppointmentClaimExaminers.Create` /
  `Volo.Authorization:010001 "Given policy has not granted."`.
- DB (appt A00068, id f74eca86-3634-c1d4-3987-3a21b73f12a5): Status=1 (Pending), AA=1, DA=1,
  CE=0, Injuries=0.
- Suspected area: the Clinic Staff role is missing `AppointmentClaimExaminers.Create` (permission
  definition / role grant), OR the booking client should attach the CE through an endpoint the
  role is allowed to call. The booking submit also does not tolerate the 403 -- it stops before
  persisting injuries and before the internal auto-approve, leaving a half-built appointment.
- NOTE: this is NOT the 409 race that F1/F2 fixed; no 409 occurred. It is a separate permission gap
  that blocks the F1/F2 flow for the clistaff1 role specifically. Patient and Staff Supervisor roles
  CAN attach the CE (verified: patient1 and stafsuper1 both got 200 on the same endpoint).

### F-4 (MEDIUM): PQME AttyCE notice may not include the DWC QME form (unverified, weak signal)
- Repro/observation: approved one AME (A00069), one IME (A00072), one PQME (A00071). The emailed
  AttorneyClaimExaminer PDF byte sizes are near-identical: AME 253994 B, IME 254054 B, PQME 254306 B.
- Expectation (prompt): PQME AttyCE notice contains the DWC QME form (a multi-page government form);
  AME/IME use the ame_ime notice (no QME form). A real extra form would add many KB / several pages.
- Suspected area: the PQME-vs-AME/IME notice branch in the AttyCE packet builder may not be inserting
  the QME form (or it is a same-length template swap). UNVERIFIED -- byte size is only a proxy; the
  AttyCE PDF is pruned after send so content could not be inspected directly. Confirm with the
  PACKETS_HTML deeper check (regenerate with the HTML flag, inspect the AttyCE HTML for the QME form)
  before relying on this for the demo.

### F-2 (LOW): blank booking page immediately after login (transient, self-heals on reload)
- Repro: log in as an external user (seen with appatty1) and land directly on `/appointments/add` via
  the post-login returnUrl. The page renders fully blank.
- Console: `401 Unauthorized` on `GET /api/app/appointments/pending-count` (token not yet attached when
  the SPA fired the call right after the OIDC redirect).
- A manual reload renders the form correctly. Suspected area: a race between the OIDC token exchange
  and the first authenticated API call on the deep-linked route. Low severity but looks bad in a demo
  if a booker deep-links in.

### F-3 (LOW): approval-gate business-rule violation returns HTTP 403
- Repro: approve a Pending appointment lacking an injury (A00068). Server returns HTTP 403 carrying
  `CaseEvaluation:Appointment.ApprovalRequiresInjuryDetail` (a `UserFriendlyException`).
- 403 normally means "not authorized"; a domain validation failure is conventionally 400 or 409.
  The 403 mapping can confuse clients/log triage (it looks like a permission problem). Behaviour is
  otherwise correct (blocks + clear message, appt stays Pending). Cosmetic/contract nit.

### F-5 (LOW, env): reschedule/cancel notification emails log SMTP delivery failure in dev
- Observation: change-request emails log `SendAppointmentEmailJob: SMTP delivery failed
  (ChangeRequestApproved/Reschedule|Cancel/...) Configure ACS credentials to deliver. Job will not
  retry...`, whereas booking/approval/packet emails log `delivered (...)`. EMAIL-LINKS lines are
  present for all types, so links are recoverable.
- Expected in dev (no ACS/SMTP creds); flagged only because the "delivered" vs "SMTP delivery failed"
  split across notification types is inconsistent and could mask a real delivery problem in a higher
  env. Not a fix regression.

## Verdict

**The four go-live fixes hold and the build is demo-ready -- with one gating caveat (F-1).**

All four fixes were validated end to end on live Falkinstein data, running together in one stack:

- **F1/F2 (book Pending -> attach -> auto-approve):** the crown-jewel scenario works. An internal
  Staff Supervisor booking (A00069) created Pending, attached AA/DA/CE + injury, then auto-approved
  AFTER the attach sequence, ending Approved with all join rows = 1/1/1/1 and **zero 409s**. The
  exact failure that prompted the fix did not recur in any of the 5 bookings driven this session.
- **F3 (idempotent AttyCE prune):** packet emails sent exactly once per recipient, 0
  `AbpDbConcurrencyException`, 0 Hangfire retries, no duplicate deliveries.
- **F4 (fresh reschedule confirmation):** reschedule approved with no 500, created a new row with a
  new `A#####` distinct from the source, source moved to a terminal Rescheduled status.
- **Type reduction:** exactly AME/IME/PQME everywhere (DB + dropdown), 0 legacy-type references,
  migration applied in place with all 67 existing appointments + 22 patients preserved.

Supporting lifecycle (approval gates both directions, 3-packet generation, doctor-not-emailed,
documents upload + validation + moderation, ex-parte one-email-with-CC) all passed.

**Gate before demo:** **F-1 (HIGH)** -- a Clinic Staff user (`clistaff1`, the exact actor the test
plan named for the F1/F2 case) cannot complete any booking that attaches a Claim Examiner: the
`appointment-claim-examiners` POST returns 403 (`AppointmentClaimExaminers.Create` not granted to
that role), which aborts the rest of the submit and leaves a half-built Pending appointment. If
Clinic Staff are expected to book in the demo, this is a blocker and must be triaged first.
Patient and Staff Supervisor roles are unaffected. This is a permission/role gap, **not** a
regression of the F1/F2 logic (no 409 occurred).

**Verify before relying on PQME packets:** **F-4 (MEDIUM)** -- could not confirm the PQME AttyCE
notice includes the DWC QME form; byte sizes suggest it may not. Needs the PACKETS_HTML content
check.

Remaining findings (F-2 blank-page-after-login, F-3 403-for-business-rule, F-5 dev SMTP delivery
logging) are low severity and do not block the demo.

### Not tested / could not verify
- **AttyCE notice content (PQME QME form vs AME/IME)** -- F-4; the optional PACKETS_HTML deeper
  check was skipped to avoid an api-container recreate on the memory-constrained engine. Byte-size
  proxy only (inconclusive).
- **External-booker cancellation queue path** (Track D) -- only the internal NoBill auto-approve
  path was exercised; the "external stays Pending on the supervisor's queue" branch was not driven.
- **Approval-gate "missing active CE" branch in isolation** -- the negative gate fired on the
  injury requirement first (A00068 had neither CE nor injury), so the CE-specific message was not
  observed separately.

## Environment notes

- Engine state at start: `main-*` containers had all exited 255 simultaneously (a crash event) and
  the first `docker info` returned 500. The engine recovered on its own; a single
  `docker compose up -d` (no `--build`) brought the stack up and **it stayed healthy for the full
  ~1-hour session** (no recurrence of the prior session's repeated crashes). `db-migrator` exited 0.
- Test data created this session (all synthetic): appointments A00068 (Pending, partial -- F-1
  artifact), A00069 (Approved->Rescheduled), A00070 (Approved->Cancelled, reschedule clone),
  A00071 (PQME Approved), A00072 (IME Approved); plus documents on A00071/A00072. No volumes were
  wiped; no source was modified.
- Method: Playwright UI for booking/approval/reschedule/cancel/documents; `docker logs main-api-1`
  for EMAIL-LINKS + packet/job evidence; sqlcmd against `CaseEvaluation` for status/join/packet
  counts. Browser network panel for HTTP status codes (409/500/403 checks).
