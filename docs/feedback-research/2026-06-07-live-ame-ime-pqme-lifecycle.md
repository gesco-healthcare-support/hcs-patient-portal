# Live AME / IME / PQME lifecycle simulation -- test report (2026-06-07)

Tenant: Falkinstein (`http://falkinstein.localhost:4200`, TenantId
`7D671799-06FC-A7B3-AB05-3A219B418297`). Stack: `main` after PR #292 (packet HTML pipeline)
+ #296 (exam-intake/parties). Email-link logging (`Notifications:LogLinks`) enabled for this
run via `docker-compose.override.yml` + `SendAppointmentEmailJob` (uncommitted dev edit).

Method: real UI driven via Playwright; evidence from UI, `docker logs main-api-1`, and direct
SQL. Verification pass -- no source fixes applied.

## Pre-flight (PASS)

- All containers healthy; `db-migrator` exited 0; migrations completed; Falkinstein present.
- packet-renderer `/health` lists doctor/patient/attorney-ame/attorney-pqme.
- All 12 listed accounts exist, active, email-confirmed, correct roles (no invites needed).
- Booking prereqs: AME=`...0003`, IME=`...0007`, PQME=`...0002` (DB label "Panel QME"),
  2 locations, 1120 future slots, 22 patients (patient1->Daniel Harper, patient2->Olivia
  Turner linked), 65 appointments pre-existing, 0 ClaimExaminer master records.

## P1 -- email-link logging (PASS)

`EMAIL-LINKS (<context>) to=<email> cc=<emails> subject="..." links=<urls>` lines emit in
`main-api-1` for every outbound email (appointment + account paths converge on
`SendAppointmentEmailJob`). Captured real links incl. AuthServer login URL and
`/appointments/view/<id>` deep-links. Minor cosmetic: the XHTML namespace
`http://www.w3.org/1999/xhtml` is captured as a "link" (regex false-positive).

## AME -- A00065 (booked by Clinic Staff `clistaff1`)

Inputs: AME, Demo Clinic North, 2026-06-10 09:00; patient1 (Daniel Harper); AA appatty1
(Marcus Bennett), DA defatty1 (Gregory Stone), CE claimE1 (Henry Caldwell); Insurance
"Pacific Workers Comp Insurance"; 1 injury (WC-2026-AME-001, ADJ12345678, Lower back).
USPS address-standardization modal accepted ("Continue").

Result: appointment id `42b18aef-7cf9-a3e5-5b90-3a21b64530ab`, confirmation A00065.

PASS items:
- Panel Number field is DISABLED for AME (UI-level rejection of panel number). PASS.
- `POST /api/app/appointments` -> 200; appointment persisted with denormalized party emails
  (patient1/appatty1/defatty1/claimE1). PASS.
- All 3 packets generated (Patient/Doctor/AttyCE Status=Generated) via legacy DOCX->Gotenberg
  (flags off); Patient + AttyCE packets emailed with PDF attachments; Doctor packet not
  emailed. PASS.
- Ex-parte `AppointmentRequested` notice sent as ONE email To=patient1 with
  CC=appatty1,defatty1,claimE1 (single notice + CC, not per-party fan-out). PASS (note To is
  the patient, not the staff booker).

FINDINGS (no fixes applied):

- **F1 (High) -- Clinic Staff booking auto-approves on create.** A00065 is status Approved
  immediately after `POST /appointments` (internal create-as-Approved fast-path), which
  bypasses the Pending->Approved CE gate and triggers packet generation + ~11 notification
  emails synchronously. Repro: book any appointment while logged in as an internal role.

- **F2 (High, data integrity) -- party join rows never written; client gets 409.** For
  A00065 the AA/DA/CE join tables are EMPTY (0 rows each) although 63-64 of 65 pre-existing
  appointments have them. Timeline: `POST /appointments` 200 (2.0s, auto-approve + packets +
  emails fire), then client `POST /appointments/{id}/applicant-attorney` ran 11.6s and
  returned **409 Conflict**; DA/CE attach never succeeded. The appointment view renders the
  Applicant Attorney (from denormalized columns) but NOT the Defense Attorney or Claim
  Examiner. Net: appointment Approved with no CE join (gate bypassed by F1) and no party
  joins.

- **F3 (Medium, race) -- AbpDbConcurrencyException on AppointmentPacket; duplicate emails.**
  During the auto-approve packet/email burst, `PacketAttachmentProvider.NotifySendCompletedAsync`
  and Hangfire jobs 11049/11051 threw "expected to affect 1 row, affected 0" optimistic-
  concurrency errors and retried, RE-SENDING the AttyCE packet email to appatty1 and claimE1
  multiple times (21:58:53, 21:59:21, 21:59:36).

- **Root-cause hypothesis (F2+F3):** the internal auto-approve fast-path (F1) mutates the
  appointment aggregate + AppointmentPacket rows (approval + packet jobs) concurrently with
  the Angular client's follow-up party-attach POSTs, producing optimistic-concurrency
  conflicts surfaced as the 409 (F2) and the packet job retries/duplicate sends (F3).
  Prediction: external bookers (IME/PQME, stay Pending, no packets on create) should NOT
  collide and should populate join rows -- tested next.

## IME -- A00066 (booked by Applicant Attorney `appatty1`) -- PASS (clean)

Inputs: IME, Demo Clinic South, 2026-06-11 10:00; patient2 (Olivia Turner); AA appatty1
(Marcus Bennett, booker -- email auto-filled + READ-ONLY); DA defatty2 (Alicia Perez); CE
claimE2 (Jasmine Reid); Insurance "Golden State Insurance Fund"; 1 injury (WC-2026-IME-002,
ADJ87654321, Right shoulder). Result: id `86ead7cd-0879-70a2-13bc-3a21b6504993`, A00066.

PASS items:
- Panel Number DISABLED for IME (UI rejects panel number). PASS.
- External-attorney booker: AA section auto-filled with booker identity, AA email locked
  (read-only). PASS.
- Status = **Pending (1)** (external booker cannot self-approve). PASS.
- **Parties persisted: AA join=1, DA join=1, CE join=1 (active), injuries=1.** PASS -- the
  exact opposite of the AME outcome.
- Ex-parte `AppointmentRequested` = ONE email To=patient2 CC=appatty1,defatty2,claimE2; staff
  `BookingSubmitted/ApproveReject` to 5 internal mailboxes. No 409, no concurrency errors.
- Packets = 0 (correct; not generated until approval).

**Comparison (root-cause confirmation):** the only behavioral difference between the failed
AME and the clean IME is the booker's role -> internal create-as-Approved fast-path (AME) vs
external Pending (IME). The fast-path's synchronous approval side-effects (packet jobs +
emails + aggregate writes) race the client's post-create attach calls, producing the 409 +
concurrency errors and the empty join/injury tables on A00065. External Pending bookings have
no on-create side-effects, so attaches complete cleanly.

OBSERVATION (data): seeded patient2 (Olivia Turner) has DateOfBirth `0001-01-01` (unset), so
booking on her behalf left the required DOB blank and blocked submit until manually set
(used 1990-03-22). patient1 (Daniel Harper) has a real DOB and auto-filled fine.

## PQME -- A00067 (booked by Patient `patient1`, self-book) -- PASS (clean)

Inputs: Panel QME, Demo Clinic North, 2026-06-12 11:00; patient1 self (demographics
pre-filled, email locked); Panel Number PNL-2026-0042; AA appatty2 (Tiffany Lawson); DA
defatty2 (Alicia Perez); CE claimE2 (Jasmine Reid); Insurance "Sequoia Casualty"; 1 injury
(WC-2026-PQME-003, ADJ55667788, Cervical spine). Result: id
`e7629001-3e82-3606-3b74-3a21b6549307`, A00067.

PASS items:
- **Panel Number ENABLED + required ("Panel Number *") for PQME** and persisted
  (PanelNumber=PNL-2026-0042) -- the inverse of the AME/IME disabled state. Confirms the
  per-type panel-number rule at the UI + storage.
- Patient self-book: own demographics pre-filled, email read-only.
- Status = Pending (1); AA/DA/CE joins = 1/1/1, injuries = 1 (clean, same as IME).
- Ex-parte + staff approve/reject emails sent; no 409, no concurrency errors.

## Booking matrix summary

| Appt | Conf | Booker role | Status on create | AA/DA/CE joins | Injuries | Anomaly |
|------|------|-------------|------------------|----------------|----------|---------|
| AME  | A00065 | Clinic Staff (internal) | Approved (auto) | 0/0/0 | 0 | 409 + concurrency, broken |
| IME  | A00066 | Applicant Attorney (ext) | Pending | 1/1/1 | 1 | none |
| PQME | A00067 | Patient (self)           | Pending | 1/1/1 | 1 | none |

## P4 -- approve / documents / packets / change-requests

Exercised primarily on the clean IME (A00066) as Staff Supervisor `stafsuper1`.

- **Approve appointment (IME) -- PASS.** Approve modal requires a Responsible User + optional
  comment. After selecting `stafsuper1`, A00066 -> Approved (ApproveDate stamped 22:19:10).
  All 3 packets generated (Status=Generated). Approval emails delivered once each to all 4
  parties (patient2/appatty1/defatty2/claimE2) + the responsible user; Patient packet (529 KB
  PDF) + AttyCE packet (253 KB PDF) emailed with attachments. **No 409, no concurrency
  errors, no duplicates** -- the explicit-approval path is clean (contrast with F1/F3 on the
  fast-path). CE gate satisfied because the IME has an active CE join.
- **Packet generation + retrieval -- PASS.** 3 packets per approved appointment; the email
  job fetched packet bytes from blob and delivered them as attachments (proves retrievability/
  download). (Legacy DOCX->Gotenberg path; `PACKETS_HTML_*` left default false this run.)
- **Document upload -- PASS (partial).** Uploaded synthetic PDF (`%PDF` magic bytes) via the
  view; row created (DocumentName/FileName persisted, FileSize 476). Internal-staff upload
  landed Status=**Accepted** automatically (per the internal-upload-auto-accept model), so the
  pending->approve/reject **moderation path was NOT exercised live** (would need an
  external-user upload to produce a `Uploaded`=pending doc). Approve/Reject controls +
  `AppointmentDocuments.Approve` permission exist per code.
- **Change-request (reschedule) -- creation PASS, auto-approve FAIL (see F4).** A00066 Actions
  menu offers **Review / Reschedule / Cancel / Delete** (no "Edit" -- matches #296). The
  "Request reschedule" modal (new-slot dropdown + required reason) created the change request
  (ChangeRequestType=Reschedule, new slot + reason recorded) and moved the appointment to
  RescheduleRequested -- but the internal-staff auto-approve POST 500'd (F4).

FINDING:

- **F4 (High) -- internal reschedule auto-approve 500s (duplicate confirmation number).**
  `POST /api/app/appointment-change-request-approvals/{id}/approve-reschedule` -> 500. Root
  cause from logs: the approval inserts a NEW `AppAppointments` row reusing the SAME
  `RequestConfirmationNumber` as the original, violating unique index
  `IX_AppEntity_Appointments_TenantId_RequestConfirmationNumber` (duplicate key
  `(7d671799..., A00066)`). The reschedule cannot complete; the appointment is left in
  RescheduleRequested with an unprocessed change request. Repro: as internal staff, Approve an
  appointment, then Actions -> Reschedule -> pick a slot + reason -> Reschedule.

## Not exercised (transparency)

- Document approve/reject moderation of an external (pending) doc -- internal uploads
  auto-accept; not run end-to-end.
- Cancellation change-request; reminder Hangfire jobs; PQME panel-strike-list opt-in gate at
  booking; interpreter "Yes" token path (seeded patients are English/No).
- PQME (A00067) left Pending (not approved); AME (A00065) downstream skipped (booking broken).
- UI packet Download button click (retrievability proven via the emailed attachments instead).

## Email-link logging (left ON for your continued testing)

The `Notifications:LogLinks` flag is LEFT ENABLED (via `docker-compose.override.yml`) so you
can keep pulling invite/confirm/approval/packet links from `docker logs main-api-1` (grep
`EMAIL-LINKS`). It logs single-use tokens, so it must NOT ship to prod. To disable: delete
`docker-compose.override.yml` and `git checkout -- ` the `SendAppointmentEmailJob.cs` edit,
then `docker compose up -d --build api`.

## Verdict

The integrated #292 + #296 state is **safe for the external-booking + explicit-staff-approval
happy path** (IME and PQME booked cleanly with all parties/injuries; IME approval generated +
delivered all packets with no errors; email-link logging works end to end). It is **NOT safe
for internal-staff on-create approval or internal reschedule approval**: a defect cluster
(F1-F4) means internal-staff bookings silently auto-approve while losing all party + injury
join rows (409) and emit duplicate packet emails (concurrency race), and internal reschedule
auto-approval hard-fails with a 500 (duplicate confirmation number). Recommend triaging F1/F2/
F4 (High) before relying on internal-staff booking/reschedule. All findings are additive
synthetic test data on the preserved Falkinstein volume; no source was changed except the
opt-in email-link diagnostic.
