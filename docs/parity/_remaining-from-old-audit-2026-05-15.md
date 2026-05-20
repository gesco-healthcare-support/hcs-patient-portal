---
title: Remaining work to port from OLD Patient Portal — 2026-05-15 snapshot
date: 2026-05-15
last-triage: 2026-05-20 (targeted refresh of sections touched by PRs #201-#204, #207, BUG-023 / BUG-024 fixes, brand rename)
author: Claude / Adrian
scope: Cross-cutting gap analysis comparing every OLD feature against current NEW implementation
status: snapshot (point-in-time)
sources:
  - P:\PatientPortalOld\PatientAppointment.* (legacy code, ~31,720 LOC C#)
  - P:\PatientPortalOld\patientappointment-portal\ (legacy Angular ~80+ components)
  - P:\PatientPortalOld\Documents_and_Diagrams\ (8 OLD reference docs + Postman collection of 220 requests)
  - W:\patient-portal\main\src\ (NEW backend, ~59,000 LOC C#)
  - W:\patient-portal\main\angular\src\app\ (NEW frontend, 66 components / ~9,900 LOC)
  - W:\patient-portal\main\docs\parity\wave-1-parity\ (43 existing audit docs)
refresh-log:
  - 2026-05-20: sections 2.A (AME role gate -> OBS-23; 6 booking
    validators -> OBS-24; 1 injury-duplicate verified Implemented;
    accessor-add at booking flipped to intentional deviation),
    2.B (external-user field masking -> verified Implemented),
    2.C, 2.D (BUG-027 filed + fixed), 2.E, 2.EE (RoleAppointmentType
    -> OBS-23), 2.G (attachmentLink not implemented; magic-link
    still TO VERIFY -- needs docker), 2.H (Cascade JDF docs verified
    Implemented with intentional parity for non-ad-hoc JDF), 2.I
    (ResponsibleUserId on ad-hoc -> intentional deviation), 2.K
    (auto-create accessor + role-consistency both flipped: one to
    deviation, one to OBS-24 V6), 2.U, 2.V, 2.BB, 10.B, 10.D refreshed
    against current code on feat/replicate-old-app. Section 3 access
    matrix rows for AME / AME-REVAL also flipped to "No -- see
    OBS-23". Sections not listed here are still at the 2026-05-15
    baseline.
---

# Remaining work to port from OLD Patient Portal

This is a cross-cutting audit of every feature, workflow, business rule,
entity, validation, email/SMS, job, integration, role/permission, UI
behavior, and data-model element that EXISTS in the OLD Patient Portal
(`P:\PatientPortalOld`) and is **NOT YET FULLY** reproduced in the
NEW ABP 10.0.2 / .NET 10 / Angular 20 stack (this repo).

Where wave-1-parity audits already document a feature, this doc links to
the audit rather than duplicating it. Where no audit exists for a gap,
this doc flags it as **unaudited** so a parity doc gets written before
implementation starts (per CLAUDE.md "Unaudited features protocol").

Status legend:
- **Implemented**: AppService + DTOs + controller + tests + UI all present and matching OLD.
- **Mostly implemented**: backend done, UI gap; or AppService present but a documented OLD rule missing.
- **Partial**: scaffolding present (entity, permission constant), no working endpoint or UI.
- **Not started**: nothing in NEW source matches.
- **Stub**: placeholder file or comment with no behavior.

---

## 0. Methodology

Six parallel investigation tracks ran prior to writing this doc:

1. OLD backend inventory — every controller (54), domain service (30),
   entity (100+), email template (38), background job (9 reminder
   types), V-View (76), stored procedure (~24), and integration (AWS S3,
   AWS SES, Twilio, OpenXml, iTextSharp, ClosedXML, NodaTime, JWT).
2. OLD frontend inventory — every page component (~35), service (~25),
   modal, guard, route data block, enum, hardcoded label.
3. NEW backend inventory — 38 AppServices, 42 controllers, 18 email
   handlers, 3 scheduled jobs, 25 core entities, 44 permission constants.
4. NEW frontend inventory — 66 components, ~23 routes, 13 CRUD modules,
   3 custom guards, branding-token file.
5. Existing parity audits — all 43 docs in `wave-1-parity/` read.
6. OLD reference docs — Readme, 10pp Technical Architecture, 25pp
   Project Overview ("business bible"), 25pp Data Dictionary (45
   tables), 37pp Data Dictionary Views (76 views), 124pp API Doc, 220
   Postman requests, ER diagram, Process Flow diagram, Project
   Solution diagram.

OLD's `Documents_and_Diagrams\Architecture\SoCal Project Overview
Document.pdf` is the canonical business contract — every "must / shall"
quote captured below is verbatim from that doc unless noted.

---

## 1. Executive summary

| Category | OLD count | NEW count | Gap |
|---|---|---|---|
| HTTP endpoints (controllers) | ~220 (Postman) / 54 (controllers) | 42 controllers | ~30 endpoint surfaces missing |
| AppServices / domain services | 30 domain services | 38 AppServices | NEW has more granular split; gaps below by feature |
| Entities / tables | 45 tables + 100+ join/reference | 25 entities | ~20 entities absent or partial |
| V-Views (read model) | 76 SQL views | 0 | Replaced by EF queries (no migration needed); some join shapes may need recreating in DTOs |
| Stored procedures | ~24 SPs | 0 | Replaced by LINQ; verify each SP's logic is replicated |
| Email templates | 59 (16 DB + 43 disk HTML) | 18 handlers wired, 41 templates seeded but unwired | 41 templates need handler wiring |
| Background jobs (recurring) | 9 reminder types | 8 jobs (varying completeness) | 1+ unwired; SMS path not integrated |
| External user roles | 4 (Patient, Adjuster, AA, DA) | 4 | OK — Adjuster renamed to ClaimExaminer |
| Internal user roles | 3 (IT Admin, Staff Supervisor, Clinic Staff) | 4 (+ Doctor, retained) | OK — Doctor role removed in cleanup |
| Major UI features | ~80 OLD components | 66 NEW components | many list/admin pages missing |
| 3rd-party integrations | AWS S3, SES, Twilio, OpenXml, iTextSharp, ClosedXML, NodaTime | MinIO/Azure Blob, Azure Comm, Gotenberg, ABP audit | SMS integration + Excel export not done |

Bottom-line: the NEW app reproduces ~55-65% of OLD behavior depending
on how you count UI vs backend. Backend booking flow is well advanced;
backend approval/reschedule/cancellation flow is implemented but
incompletely tested; UI is missing the admin/staff-facing screens for
about half of OLD's surface; reporting, CSV/Excel export, SMS, Notes,
Submit Query, internal-user creation, and check-in/check-out are not
yet started.

---

## 2. Backend gap by feature area

### 2.A Appointment booking workflow

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| 7-step wizard (type → location → intake → docs → slot → accessor → submit) | Project Overview §3.4 | **Mostly implemented** (Phase 11a-11k) | Verify each of 7 steps is gated server-side, not just UI-coupled |
| 3-of-6 patient dedup (LastName, DOB, Phone, Email, SSN, ClaimNumber) | Project Overview §3.5 verbatim | **Implemented** (Phase 11k) per audit | Spot-check: grep returned no IsPatientAlreadyExist / PatientDeduplication constants — confirm it's coded by some other name |
| `IsPatientAlreadyExist` flag flows through booking + approval | Postman body shows the field; Project Overview §3.12 | **Not visible in NEW** | Manual link option on approval ("link to existing patient or create new") is unimplemented per audit |
| Patient match notice on staff approval review | Project Overview §3.12 | **Not implemented** | "While viewing the appointment request, the Clinic Staff will be notified if the patient already exists" — no UI flag found in NEW |
| Confirmation # generation (A##### format) | Postman body `requestConfirmationNumber: A00021` | **Implemented** | NEW uses `ConfirmationNumberRetryPolicy` |
| Lead-time gate (External user only; AppointmentLeadTime days) | vSystemParameter + AppointmentDomain.AddValidation | **Implemented** (BookingPolicyValidator) | |
| Max-time gate per AppointmentType (PQME=90 / AME=90 / OTHER=90) | vSystemParameter + AppointmentDomain.AddValidation | **Implemented** but verify substring routing matches OLD (NEW uses `Contains("AME")` per memory; OLD uses explicit type IDs) | See `AppointmentBookingValidators.cs:49-88` |
| Slot transitions: External → Reserved, Internal → Booked | AppointmentDomain.Add | **Mostly implemented** | NEW marks slot Booked directly (no Reserved-then-Booked two-step per OLD); confirm parity intent |
| Re-Request form (after rejection, same confirmation #) | AppointmentDomain.AddValidation `IsReRequestForm` | **Implemented** (ResubmitAsync in NEW backend inventory) | |
| Revolution form / REVAL pre-load (load original via confirmation #) | Project Overview §3.7 + Postman flag `isRevolutionForm` | **Implemented** per audit (CreateRevalAsync) | Verify behavior matches OLD's TempAppointmentInjuryDetails staging buffer pattern |
| AME / AME-REVAL booking restricted to Attorneys (PARole+DARole) | Project Overview §3.3 matrix | **Not implemented** — see OBS-23 (filed 2026-05-20). Patient / Claim Examiner can POST AME bookings via direct API. Recommend A: small AppService gate. | |
| Accessor add at booking time (auto-create account if email not in User) | AppointmentAccessorDomain.Add | **Not implemented (intentional deviation)** — NEW uses tokenized invite (PR #202) instead of random-password auto-create. See `OBS-24` discussion + audit row in section 2.K. | |
| Accessor account: random password = `{4chars}@{4chars}` | OLD code | **Documented as security debt** | OLD-pattern security weakness; carry-over decision pending |
| AccessTypeId (View=23 / Edit=24) controls accessor permissions | AppointmentAccessor.AccessTypeId | **Implemented** (AppointmentAccessRules.CanRead per audit) | |
| Booking date < DOB validation | AppointmentDomain.CommonValidation | **Not implemented** — see OBS-24 V1 (2026-05-20) | |
| Injury date validation (≤ today; ≥ DOB) | AppointmentInjuryDetailDomain.AddValidation | **Not implemented** — see OBS-24 V2 + V3 (2026-05-20) | |
| Per-injury duplicate check (AppointmentId, ClaimNumber, DateOfInjury) | AppointmentInjuryDetailDomain.AddValidation | **Implemented** — verified 2026-05-20. `AppointmentInjuryDetailManager.CreateAsync` lines 41-48 + UpdateAsync lines 82-91 enforce the tuple uniqueness. | |
| Cumulative trauma injury date-range (From + To) | AppointmentInjuryDetailDomain | **Partial** — entity has `ToDateOfInjury`; no enforcement that ToDate > FromDate or that it's set when `IsCumulativeInjury=true`. See OBS-24 V4 (2026-05-20). | |
| Stakeholder email uniqueness (no duplicates patient/AA/DA/CE) | AppointmentDomain.CommonValidation | **Not implemented** — see OBS-24 V5 (2026-05-20) | |
| Accessor role consistency (email in User must match accessor.RoleId) | AppointmentAccessorDomain.AddValidation | **Not implemented** — see OBS-24 V6 (2026-05-20) | |
| Same-day reschedule time gate (today ≥ slot.AvailableDate ⇒ now-hour must be < FromTime) | AppointmentDomain.UpdateValidation | **Not implemented** — see OBS-24 V7 (2026-05-20) | |

### 2.B Appointment view / detail

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| Confirmation-number lookup endpoint | OLD `Appointments.search` + Home component | **Implemented** (GetByConfirmationNumberAsync) | |
| Eager-load full graph (Patient + Attorneys + Injuries + BodyParts + Insurance + Examiners + Employer + Custom Fields + Accessors) | AppointmentDomain.Get(id) | **Implemented** per audit | Verify all child collections returned |
| External user access rules: read access to creator OR accessor OR matching-email-on-AA/DA/CE/Patient | AppointmentDomain implicit by query joins | **Implemented** (AppointmentAccessRules.CanRead per audit; verified in R1 hardening 2026-05-14) | |
| External users masked from internal fields (InternalUserComments, RejectionNotes when not own rejected, ResponsibleUser) | OLD by view-projection | **Implemented** — verified 2026-05-20. `InternalUserComments` masked via `ExternalUserDtoFilter.MaskInternalFields` in `AppointmentsAppService.GetAsync` + `GetWithNavigationPropertiesAsync`. `RejectionNotes` and `PrimaryResponsibleUserId` are NOT exposed on `AppointmentDto` at all (entity-only fields; Mapperly auto-mapper skips them). | |
| Patient self-view of own appointments by Patient.IdentityUserId | OLD | **Implemented** | |

### 2.C Appointment approval / rejection

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| ApproveAsync endpoint | AppointmentDomain.Update with status=Approved + IsInternalUserUpdateStatus | **Implemented** (Phase 12) | |
| RejectAsync endpoint with rejectionNotes (required server-side) | Same | **Implemented** (BUG-024 fixed 2026-05-19 via commit `5da5c31`) | Empty/whitespace rejection notes now throw 400 with `AppointmentRejectionReasonRequired` |
| Responsible Team Member dropdown + persistence | Project Overview §3.13 verbatim | **Implemented** (PrimaryResponsibleUserId persisted via approve endpoint per current code) | UI may not yet pick from full clinic-staff list |
| Different doc-packet emails: patient receives package; responsible internal user receives "internal" packet | Project Overview §3.13 verbatim | **Partially implemented** | Patient packet, attorney/CE packet implemented; internal-user "responsible team member" filled packet separately TBD |
| Patient-match notice on approve (manual link override) | Project Overview §3.12 | **Not implemented** | Backend + UI absent |
| Re-approving already-approved → 400/state-machine error | OLD allows; NEW throws AppointmentInvalidTransition | **Implemented** (BUG-024 second half fixed 2026-05-19 via `5da5c31`) | Status code now 400 (was 403 per legacy BUG-023 pattern) |
| Audit log entry on approve (FieldName=AppointmentStatusId, Old=Pending, New=Approved) | AppointmentChangeLogDomain.ChangeLogs | **TO VERIFY** — ABP `[Audited]` may or may not capture this in a shape the UI expects | Audit doc says deferred; mapping to OLD DTO not done |

### 2.D Reschedule (request + approval)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| RequestRescheduleAsync (external user submits) | AppointmentChangeRequestDomain.Add | **Implemented** (Phase 16) | |
| `IsBeyodLimit` flag (sic — canonical typo in OLD) | Postman body + AppointmentChangeRequest entity | **Partially implemented** — DTO carries flag, but admin-side inversion of validation logic is DESCOPED per audit | Phase 17 follow-up |
| Approve reschedule creates NEW Appointment row with same RequestConfirmationNumber + cascade-copies all child entities | Project Overview §3.10 verbatim | **Mostly implemented** (Phase 17 ApproveRescheduleAsync + AppointmentRescheduleCloner) | Cascade-copy of CustomFieldValues, Accessors, JointDeclaration history per OLD — confirm coverage |
| Reschedule outcome bucket (Late vs NoBill manually selected by supervisor) | Project Overview §3.8 | **Implemented** | |
| Slot transitions: new slot Available → Reserved on submit; old slot Booked stays until approve; old → Available + new → Booked on approve; new → Available on reject | AppointmentChangeRequestDomain.Update | **Mostly implemented** | Spot-test reject path releases the new slot |
| ReScheduleReason required (server-side) | Domain validation | **Implemented** (BUG-027 fixed 2026-05-20). Whitespace-only payloads now caught at AppService boundary with HTTP 400, matching cancellation path. | |
| Admin reschedule (supervisor modifies date before approval) with `AdminReScheduleReason` field | AppointmentChangeRequestDomain.Update isAdminReschedule | **TO VERIFY** | OLD column exists; NEW DTO has `RescheduleOutcome` but admin-modify-date flow not obviously wired |
| Reschedule lead-time + max-time enforcement (with `IsBeyodLimit=true` admin override) | AppointmentChangeRequestDomain.AddValidation | **Partial** | |
| Document upload on reschedule (AppointmentChangeRequestDocuments) | OLD has dedicated child entity | **Not implemented** in NEW — change requests have no doc-upload surface | File audit if needed |

### 2.E Cancellation (request + approval)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| RequestCancellationAsync (external user submits) | AppointmentChangeRequestDomain.Add | **Implemented** (Phase 15) | |
| `AppointmentCancelTime` cutoff check (days before slot) | AppointmentChangeRequestDomain.AddValidation | **Implemented** — verify against vSystemParameter default of 1 day | |
| CancellationReason required (server-side) | Domain validation | **Implemented** — verified 2026-05-20. `AppointmentChangeRequestsAppService.cs:60-63` has the correct `input == null OR IsNullOrWhiteSpace(input.Reason)` gate. | |
| Approve cancellation: status → CancelledNoBill OR CancelledLate (manual selection) | Project Overview §3.8 verbatim | **Implemented** (Phase 17) | |
| Slot release on approval (Booked → Available) | OLD implicit | **Implemented** per audit | |
| Reject cancellation: revert appointment to Approved | OLD | **Implemented** | |
| CancellationRejectionReason field on reject | AppointmentChangeRequest entity | **TO VERIFY** | |
| Clinical Staff Cancellation email (CC'd) on approval | OLD ClinicalStaffCancellation template | **Template seeded; handler unwired** | Phase 18 follow-up |

### 2.F Check-in / Check-out / No-show / Billed

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| CheckInAsync method | OLD AppointmentDomain status transitions | **NOT STARTED** — no AppService method | grep confirmed; only enum constant exists |
| CheckOutAsync method | Same | **NOT STARTED** | |
| MarkNoShowAsync method | Same | **NOT STARTED** | |
| MarkBilledAsync method | Same | **NOT STARTED** | |
| Today's appointments list view (date-driven) | OLD `/appointment-approve-request` route | **NOT STARTED** | Audit `clinic-staff-check-in-check-out.md` documents the gap |
| Idempotency check (re-checking-in does nothing) | OLD by status guard | **n/a until implemented** | |
| State-machine validation (CheckIn only when Approved, CheckOut only when CheckedIn) | OLD validation flow | **Audit-only** | New audit doc exists |
| Emails per status: CheckedIn / CheckedOut / NoShow | OLD templates exist | **Templates seeded; handlers unwired** | |
| Document acceptance state required before CheckIn? | OLD spec unclear | **Open question** | |

### 2.G Document upload — package documents (auth + verified-link)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| Auto-queue PackageDocuments on Approve | OLD AppointmentDomain.AddAppointmentDocumentsAndSendDocumentToEmail | **Implemented** (Phase 14, PackageDocumentQueueHandler subscribes AppointmentApprovedEto) | |
| Per-document VerificationCode (uniqueidentifier) | AppointmentDocuments.VerificationCode column | **Implemented** | |
| Unauthenticated upload via VerificationCode | OLD anonymous endpoint | **Implemented** + rate-limited (PublicDocumentUploadController) | |
| Magic-link → login → deep-link to upload page | Project Overview §3.9 verbatim | **TO VERIFY** end-to-end on NEW Angular | |
| Per-document accept/reject (granular) | Project Overview §3.9 verbatim | **Implemented** (DocumentReviewAppService Approve/Reject) | |
| Accepted documents immutable for external users | OLD | **Implemented** | |
| Re-upload clears RejectionNotes + status | OLD AppointmentDocumentDomain.Update | **Implemented** | |
| Reminder cadence (T-7 / T-3 / T-1 or single cutoff) | OLD scheduler job | **Mostly implemented** — PackageDocumentReminderJob exists | MVP fires once; T-7/T-3/T-1 cadence is post-MVP |
| Document upload size cap | OLD client-side 10 MB only | **BUG-025 open: no server-side cap, only Kestrel default ~30 MB** | See bug file; recommend 10 MB cap to match OLD |
| Content-type allowlist (PDF, DOCX, PNG, JPG) | OLD client-side "word only" | **Not implemented** | File new audit OR BUG-025b |
| `attachmentLink` (HTML hyperlink for emails) | OLD AppointmentDocuments.AttachmentLink | **Not implemented** — verified 2026-05-20. `grep AttachmentLink` returns no matches anywhere in NEW src. NEW emails use signed-URL pattern instead. | Likely accepted deviation; document in `_parity-flags.md` |

### 2.H Document upload — Joint Declaration Form (JDF, AME-only)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| EnsureAme gate (JDF only for AME / AME-REVAL) | OLD AppointmentJointDeclarationDomain.UpdateValidation | **Implemented** per audit | |
| Approved-only + before-DueDate gate | Same | **Implemented** | |
| Booking-attorney-only upload | Same | **Implemented** | |
| Auto-cancel job (Hangfire) when JDF missing N days before DueDate | OLD scheduler job + Project Overview §3.9 verbatim | **Implemented** (JointDeclarationAutoCancelJob) | |
| Per-document accept/reject | OLD AppointmentJointDeclarationDomain.Update | **Implemented** per audit | |
| Cascade JDF documents into next-appointment on reschedule | OLD AppointmentRescheduleCloner | **Implemented (parity preserved)** — verified 2026-05-20. `AppointmentRescheduleCloner.CloneAdHocDocumentFor` line 436 carries forward `IsJointDeclaration` flag on ad-hoc-flagged docs. Per OLD line 526, JDF non-ad-hoc docs intentionally stay on source row. NEW matches. | |
| JDF rejection email (with remaining count) | OLD PatientDocumentRejected (+RemainingDocs) template | **Template seeded; handler unwired** | |

### 2.I Document upload — ad-hoc / new documents (AppointmentNewDocuments)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| Ad-hoc upload endpoint (no VerificationCode, no DocumentPackageId) | OLD AppointmentNewDocumentDomain.Add | **Implemented** (IsAdHoc flag) | |
| ResponsibleUserId set on add | OLD | **Mostly implemented (intentional deviation)** — verified 2026-05-20. NEW only sets `ResponsibleUserId` when an internal-staff member is the uploader (initialStatus=Accepted) or on accept/reject review. External uploads stay null until review. OLD set it on every add; NEW's semantic ("responsible = who reviewed it") is arguably more correct. | Document in `_parity-flags.md` if explicit deviation log is preferred |
| Internal-user fast-path (auto-accept) | OLD | **Implemented** | |
| Email PatientNewDocumentUploaded / Accepted / Rejected | OLD templates | **Mostly implemented**; subjects unified | |
| OLD's parallel `AppointmentNewDocuments` table — keep or unify with AppointmentDocuments? | Schema | **NEW unified into single AppointmentDocument entity with IsAdHoc flag** | Accepted deviation; document in `_parity-flags.md` |

### 2.J Document download / packet generation

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| Patient Packet PDF generation | OLD GenerateDocumentBluePrint + iTextSharp / OpenXml | **Implemented** (Phase 1A: 44 tokens) | |
| Doctor Packet PDF generation | OLD GenerateDocumentBluePrint | **Implemented** (Phase 1A: 15 tokens) | |
| Attorney/Claim Examiner Packet PDF | OLD attorneyClaimExaminer/ S3 path | **Implemented** (AttyCEPacketEmailHandler) | |
| Joint Agreement Letter DOCX generation | OLD FileOperations.GetJointAgreementLetter | **Not implemented — JDF upload is by external user, not generated** | Confirm direction: in OLD this was a downloadable letter for attorneys to sign |
| Patient + Doctor + 5 other packet templates (multi-tenant) | Project Overview §3 implicit | **Phase 1A ships 3 packets; remaining 5 are Phase 2 multi-tenant work** | |
| Doctor signature stamp on Patient Packet only | OLD UserDomain SignatureAWSFilePath | **Implemented** (UserSignatureAppService) | |
| Token: hardcoded clinic name + doctor name | OLD ("West Coast Spine Institute", "Yuri Falkinstein, M.D.") | **Hardcoded in templates Phase 1A; Phase 2 work to parameterize per tenant** | |
| Email-attached vs link-only delivery | OLD attaches package docs to email | **NEW link-only with signed URL** | Audit `email-packet-parity/` documents the deviation |
| PDF replaces DOCX deliverable | CLAUDE.md "Primary Mission" | **Implemented** (Gotenberg + QuestPDF) | Recipients cannot edit; intentional |
| Page-count metadata on packet | OLD implicit | **Implemented** (AppointmentPacket entity) | |

### 2.K Accessor sharing (AppointmentAccessor)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| CRUD endpoints | OLD AppointmentAccessorsController | **Implemented** | |
| Auto-create accessor user account on first add | OLD AppointmentAccessorDomain.Add | **Not implemented (intentional deviation)** — NEW uses tokenized invite flow (PR #202 + Invitation entity). Auto-creating accounts with random `{4chars}@{4chars}` passwords was an OLD security weakness; invite-token replacement is a deliberate improvement. | Document in `_parity-flags.md` if explicit deviation log is preferred |
| Send AccessorAppointmentBooked email | OLD template | **Implemented** | |
| AccessTypeId = 23 (View) vs 24 (Edit) | OLD AccessType lookup | **Implemented** as `AccessTypeId` field | Verify NEW honors Edit-only restriction |
| Accessor email role-consistency check | OLD validation | **Not implemented** — see OBS-24 V6 (2026-05-20) | |

### 2.L Internal appointment notes (Notes entity, OLD)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| Notes table with `ParentNoteId` (threading) and `EditNoteId` (edit chain) | OLD Notes table | **NOT STARTED** | grep confirmed; no Notes entity in NEW |
| CRUD endpoints for notes | OLD NotesController | **NOT STARTED** | |
| `IsLatest` flag for edit-chain filtering | OLD | **NOT STARTED** | |
| Internal-user-only view gate | OLD | **NOT STARTED** | |
| Rich-text editor in Angular | OLD AppointmentListComponent (NoteRequest modal) | **NOT STARTED** | |
| Cross-reference: `appointment-notes.md` audit | wave-1-parity | **Audit exists, not implemented** | |

### 2.M Appointment change log

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| Per-field audit table (FieldName, Old, New, ChangedBy, ChangedDate, IsMailSent, IsInternalUserUpdate) | OLD AppointmentChangeLog table | **NOT implemented as separate table; relies on ABP `[Audited]`** per audit | Accepted deviation per `appointment-change-log.md` |
| List endpoint with filters (FieldName, date range, user) | OLD AppointmentChangeLogsController | **TO VERIFY** ABP audit-log endpoint provides this | |
| Internal-user-only filter on view | OLD | **TO VERIFY** | |
| Angular `AppointmentChangeLogsComponent` route exists in NEW | OLD module + NEW route `/appointments/view/:id/change-log` | **Route + component exist** | Verify the data loads in expected shape |
| `IsMailSent` per-field flag on change log row | OLD | **Not implemented** | NEW does not differentiate per-field email firing |
| SendEmailForIntakeFormChanges (staff email summary on update) | OLD AppointmentChangeLogDomain | **Not implemented** | |

### 2.N Dashboard

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| 12 KPI counters on internal-user dashboard | OLD `DashboardComponent.html` | **Partially implemented** (NEW shows 13 counter cards per inventory) | Confirm field mapping matches OLD: 8 appt counters + 4 user counters |
| Click-through to filtered list pages | OLD | **Implemented** (query params route to `/appointments` with `appointmentStatus=N`) | |
| Counts: Pending JDF (AME), Approaching Legal Deadline, Pending Change Requests, Today's check-ins | OLD via stored procs | **Implemented for some; remaining "stay at 0 until day-of-exam states ship"** per inventory | |
| External user home (patient/AA/DA/CE view of own appointments) | OLD HomeComponent | **Implemented** | |
| Confirmation # search box on home | OLD HomeComponent | **TO VERIFY** | |
| Dashboard service `post()` vs ABP GET convention | OLD POST `/api/Dashboard/post` body `{UserTypeId}` | **Implemented as GET** (accepted deviation; UserTypeId derived from auth) | |
| `isAccessor` flag flow on home (different UI for accessor users) | OLD user.data.isAccessor | **TO VERIFY** | NEW external-user inventory does not mention accessor-specific UI variant |

### 2.O Reports (Schedule, Appointment Request, Excel ODBC)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| Schedule Report (merged across 3 doctor sites; non-sensitive read-only DB) | Project Overview §3.10 verbatim | **NOT STARTED** (Phase 2 multi-tenant) | |
| Appointment Request Report (HTML tabular + Excel + PDF export) | Project Overview §3.15 | **NOT STARTED** | grep confirmed; no ReportAppService |
| Filters: Date range, Location, Type, Status, Patient name, Doctor name | OLD | **NOT STARTED** | |
| Excel ODBC link (3-4 views exposed for Excel pivot) | Project Overview §3.15 verbatim | **NOT STARTED** (Phase 2) | |
| AppointmentRequestReportSearchController in OLD | OLD | **NOT STARTED** | |
| Audit `internal-user-reports.md` | wave-1-parity | **Audit exists, deferred** | |

### 2.P CSV / Excel / PDF export

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| `/api/CsvExport/{appointmentId}/{downloadType}` | OLD CsvExportController | **NOT STARTED** | grep confirmed |
| Async export pattern (POST start + key + GET retrieve) | OLD | **NOT STARTED** | |
| ClosedXML.Excel integration | OLD | **NOT STARTED** | |
| iTextSharp PDF export | OLD | **Replaced by Gotenberg + QuestPDF for packets, but not for report export** | |
| Per-appointment "Print to PDF" or "Export to Excel" button on appointment view | OLD | **NOT STARTED** | |

### 2.Q Master data CRUD

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| AppointmentType CRUD | OLD | **Implemented** | |
| AppointmentType.ReEvalId (links base to REVAL variant) | OLD | **TO VERIFY** | NEW may not have this linkage field |
| AppointmentStatus CRUD (and seeded statuses) | OLD | **Implemented** | NEW has 11 statuses matching OLD |
| AppointmentLanguage CRUD (interpreter languages, separate from system Languages) | OLD | **Implemented** | |
| Location CRUD with ParkingFee column | OLD | **Implemented** but verify ParkingFee column | NEW entity inventory does not list ParkingFee — flag if missing |
| Location.AppointmentTypeId (which appt types are available at this location) | OLD | **TO VERIFY** | |
| WcabOffice CRUD | OLD | **Implemented** | |
| DocumentType (Master library of doc-types like X-ray, EMG, etc.) | OLD AppointmentDocumentType entity | **TO VERIFY** | Verify table exists; current NEW inventory does not list it |
| Document master CRUD (template files for package assembly) | OLD | **Implemented** (DocumentsAppService) | |
| PackageDetails CRUD (one active per AppointmentType) | OLD | **Implemented** (PackageDetailsAppService) | |
| Country / State / City master data | OLD + Project Overview | **Partial** — State implemented; Country, City absent in NEW | File audit |
| Application time zones | OLD ApplicationTimeZones table | **Partial** — NEW uses ABP setting; doesn't replicate the OLD lookup table |
| Doctor CRUD | OLD | **Implemented** | |
| DoctorPreferredLocation (Doctor × Location M:M with active flag) | OLD | **Implemented** (DoctorPreferredLocationsAppService) | |
| DoctorAppointmentType (Doctor × AppointmentType M:M) | OLD | **Mostly implemented; UI toggle deferred Phase 7b** | |
| Doctor availability bulk generation with overlap checks | OLD `DoctorsAvailabilityDomain.GenerateDoctorsAvailability` | **Implemented** (Phase 7 GeneratePreviewAsync) | 4 conflict checks: contained-in, exact-duplicate, same-location, booked/reserved |
| Slot delete: bulk by date+location with booked/reserved guard | OLD | **Implemented** (Phase 7b) | |
| Slot picker filter (LocationId required, AppointmentTypeId optional, >= lead-time) | OLD | **Implemented** | |

### 2.R Custom fields (IT-Admin configurable per-appointment-type)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| `CustomFields` table (OLD `CustomFieldss` sic) with FieldLabel, DisplayOrder, FieldTypeId, IsMandatory, AppointmentTypeId | OLD | **Implemented** (Phase 6 CustomField entity) | |
| Max-10 per AppointmentTypeId | OLD == 10 (bug-fix) → NEW >= 10 | **Implemented** | |
| FieldType enum: Date / Text / Number | OLD | **Implemented** | |
| `MultipleValues` field for dropdown options | OLD | **TO VERIFY** | NEW entity does not list this column in inventory |
| `CustomFieldsValues` per-appointment storage | OLD | **Implemented** (CustomFieldValue entity) | |
| `IsCustomField` system flag (toggle entire custom fields section) | OLD vSystemParameter | **TO VERIFY** | |
| Render custom fields in booking form by AppointmentType | OLD | **Implemented** (appointment-add-custom-fields.component.ts) | |
| Render custom fields read-only on appointment view | OLD | **TO VERIFY** | |
| Render custom fields in report exports | OLD | **NOT STARTED** (since report itself is not started) | |

### 2.S Notification templates (IT-Admin editable)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| 16 DB-managed TemplateCode rows | OLD Templates table | **Implemented** — 59 unified templates seeded per tenant | |
| 43 disk HTML template files | OLD wwwroot/EmailTemplates/ | **Migrated to DB** (NotificationTemplate entity) | |
| Both BodySms + BodyEmail in single row | OLD Templates table | **TO VERIFY** — NEW entity may only have Email body | If NEW has separate SMS field, confirm; otherwise add |
| `##placeholder##` variable substitution | OLD ApplicationConstants placeholders | **Implemented** (TemplateVariableSubstitutor per audit) | Faithful to OLD syntax (not Razor) |
| Edit endpoint for IT Admin | OLD TemplatesController | **Implemented** | |
| Edit UI in Angular | OLD `/templates` route | **NOT STARTED — UI deferred** | Backend complete; Angular has no template-management screen yet |
| Subject line per template | OLD Templates.Subject | **Implemented** | |
| Re-seed on tenant creation | OLD per-doctor DB seed | **Implemented** (NotificationTemplateDataSeedContributor) | |

### 2.T System parameters (IT-Admin configurable)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| `SystemParameter` entity with 12 OLD fields | OLD Data Dictionary §5.10 | **Implemented** (NEW adds CcEmailIds, IsCustomField — verify all 12 OLD fields present) | |
| `AppointmentLeadTime` | OLD default 3 days | **Implemented** | |
| `AppointmentMaxTimePQME` / `MaxTimeAME` / `MaxTimeOTHER` (90/90/90 days default) | OLD | **Implemented** | |
| `AutoCancelCutoffTime` (5 days) | OLD | **Implemented** | |
| `ReminderCutoffTime` | OLD | **Implemented** | |
| `AppointmentDurationTime` (15 min default) | OLD | **TO VERIFY** | |
| `AppointmentDueDays` (2 days) | OLD | **TO VERIFY** | |
| `AppointmentCancelTime` (1 hour/day) | OLD | **Implemented** | |
| `JointDeclarationUploadCutoffDays` (4 days) | OLD | **Implemented** | |
| `PendingAppointmentOverDueNotificationDays` (15) | OLD | **TO VERIFY** | |
| `CcEmailIds` (semicolon-separated CC list) | OLD vSystemParameter (not in main SystemParameters table) | **Implemented** in NEW | |
| `IsCustomField` (global toggle) | OLD vSystemParameter | **TO VERIFY** | |
| `RecordLock` / lock duration | OLD GlobalSettings table | **Not implemented** (Phase 2 deferred per `record-locking.md` audit) | |
| Edit endpoint for IT Admin | OLD | **Implemented** (SystemParametersAppService) | |
| Validate-positive-integers on update | NEW OLD-BUG-FIX | **Implemented** | |
| 2 fields hidden in OLD UI surfaced in NEW | NEW OLD-BUG-FIX | **Implemented** | |

### 2.U Internal user management (IT-Admin CRUD)

(Section refreshed 2026-05-20 after PR #203 + commit `a2efb9c`.)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| Add internal user with random password emailed | OLD UserDomain.AddInternalUser | **Implemented** (PR #203, `InternalUsersAppService.CreateAsync`) | Both IT Admin (host scope, can pick tenant) and tenant admin (tenant scope, auto-filled) |
| Auto-verify internal users (skip email-confirmation step) | OLD | **Implemented** | EmailConfirmed=true set on create |
| Edit internal user (role, contact, active) | OLD | **TO VERIFY** — Create + List paths shipped; edit/deactivate UI may still be ABP-default | |
| Soft-delete via StatusId | OLD | **TO VERIFY** (ABP ISoftDelete) | |
| List filtered by IsExternalUser=false | OLD | **Implemented** (List uses IsExternalUser extension prop) | |
| Welcome email with credentials (plaintext OLD pattern; security debt) | OLD AddInternalUser template | **Implemented as invite-token email** (no plaintext password) | NEW deviates from OLD's plaintext-credential pattern by intent (security improvement) |
| UI: `/users` admin route with list + add + edit modal | OLD | **Mostly implemented** — list + create modal shipped; edit UI uses ABP Identity admin | |

### 2.V External user management

(Section refreshed 2026-05-20 after PR #202 + `Invitation` entity ship.)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| External user self-registration | OLD UserDomain.AddExternalUser | **Implemented** (Phase 8 ExternalSignupAppService) | |
| `/users/invite` flow (IT-Admin invites external users) | NEW addition (no OLD equiv per inventory) | **Implemented via tokenized invite** (PR #202) | Invitation entity + migration `20260515183211_Added_Invitations` + InvitationManager. Token-based email link; no plaintext password emitted |
| Block external user (flip IsActive) | OLD UserDomain.Update | **TO VERIFY** — ABP Identity admin can deactivate; verify Patient Portal UI surfaces this | |
| `IsAccessor` flag on external user | OLD Users.IsAccessor | **Implemented** as extension prop per audit | |
| `IsVerified` flag (OLD) ↔ `EmailConfirmed` (NEW) | OLD Users.IsVerified ↔ ABP IdentityUser | **Implemented** with mapping | |
| Edit own profile | OLD `/users/:id` self-edit | **Implemented** (`/doctor-management/patients/my-profile`) | |
| Patient demographics: SSN-have-checkbox + conditional SSN input | OLD intake form Annexure A | **TO VERIFY** | |
| Patient `isInterpreter` toggle + Interpreter Vendor Name field | OLD HomeComponent + appointment-add | **TO VERIFY** | NEW custom fields may or may not include this |
| Patient `othersLanguageName` when languageId=7 ("Other") | OLD | **TO VERIFY** | |
| Patient phone number type radio (Home/Work/Mobile) with custom validator | OLD | **TO VERIFY** | |

### 2.W Submit Query (UserQueries)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| `UserQuery` entity (Message, 500 char max, UserId, audit fields) | OLD Data Dict §5.10 | **NOT STARTED** | grep confirmed; only template constants reference UserQuery |
| `SubmitAsync` endpoint | OLD UserQueriesController | **NOT STARTED** | |
| Email trigger on submit (to staff inbox) | OLD UserQueryDomain | **NOT STARTED** | |
| Configured staff inbox setting | OLD config | **NOT STARTED** | |
| Admin list view of submitted queries | OLD | **NOT STARTED** | |
| Modal: UserQueryAddComponent from app root | OLD | **NOT STARTED** | |
| Audit `external-user-submit-query.md` | wave-1-parity | **Audit exists, not implemented** | |

### 2.X Scheduler / background jobs (9 reminder types in OLD)

| OLD Job | Trigger | NEW status |
|---|---|---|
| 1. AppointmentApproveRejectInternalUserNotification | spm.spAppointmentApproveRejectInternalUserNotification | **Partial** — InternalStaffQueueDigestJob exists (06:30 PT); SMS path absent |
| 2. AppointmentPackageDocumentPendingNotification | sp...PendingNotification | **Partial** — PackageDocumentReminderJob exists; single cadence (T-1 only per MVP); T-7/T-3 deferred |
| 3. AppointmentDueDateApproachingNotification | sp...DueDateApproaching | **Implemented** — DueDateApproachingJob |
| 4. AppointmentDueDateDocumentApproachingNotification | sp...DueDateDocumentApproaching | **Implemented** — DueDateDocumentIncompleteJob |
| 5. AppointmentJointDeclarationDocumentUploadNotification | sp...JointDeclaration | **Not separately implemented** — covered by JDF reminders within PackageDocumentReminderJob? Confirm |
| 6. AppointmentAutoCancelledNotification | sp...AutoCancelled | **Implemented** — JointDeclarationAutoCancelJob publishes AppointmentAutoCancelledEto |
| 7. AppointmentPendingReminderStaffUsersNotification (next-day staff list) | sp...PendingReminder | **Partial** — covered by InternalStaffQueueDigestJob; verify "next-day pending" semantics |
| 8. AppointmentPendingDocumentSendToResponsibleUser | sp...PendingDocumentSend | **Not implemented** — separate reminder to per-appointment ResponsibleUserId not surfaced |
| 9. PendingAppointmentDailyNotification (daily clinic-staff digest) | sp...PendingAppointment | **Implemented** — PendingDailyDigestJob (06:00 PT) |

Plus NEW-specific jobs:
- AppointmentDayReminderJob (T-7 + T-1 reminders) — **Implemented**
- CancellationRescheduleReminderJob — **Implemented**
- RequestSchedulingReminderJob — **Implemented**

### 2.Y SMS integration (Twilio in OLD)

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| Twilio SMS for: status change, doc reminder, package reminder, auto-cancel | OLD TwilioSmsService | **NOT STARTED** — grep `SmsSender|TwilioClient` returned nothing | |
| `isSMSEnable` config flag | OLD ServerSetting | **NOT STARTED** | |
| SMS templates (BodySms field per Template row in OLD) | OLD | **TO VERIFY** — NEW NotificationTemplate may or may not have SMS body | |
| Phone number normalization (country code prefix; dashes stripped) | OLD | **NOT STARTED** | |
| ABP SMS module integration | NEW | **NOT STARTED** (per `email-coverage-audit.md`) | |

### 2.Z Email integration

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| 59 templates universe (16 DB + 43 disk → unified 59 in NEW DB) | OLD | **Templates seeded; 41 handlers unwired** per `email-coverage-audit.md` | |
| AWS SES delivery in OLD | OLD AmazonSimpleEmailServiceClient | **Replaced by Azure Communication Services SMTP passthrough** | Accepted deviation |
| SMTP fallback | OLD SendSMTPMail | **Replaced by ABP IEmailSender (ACS)** | |
| CC recipients auto-appended from `SystemParameter.CcEmailIds` | OLD SendMail | **TO VERIFY** | NEW SystemParameter has CcEmailIds column; confirm pipeline appends them |
| BCC support | OLD SendMail | **TO VERIFY** | |
| File attachment via MimeKit | OLD | **NEW uses ABP IEmailSender; attachments via Stream** | |
| `NullEmailSender` gate in dev (blocking issue per audit) | NEW design | **Blocking — Phase 18** | Dev cannot send real emails until fixed |
| Email subject builder with patient + claim + ADJ suffix | OLD ad-hoc | **Implemented** (EmailSubjectBuilder.BuildIdentitySuffix) | |
| HTML body with `##placeholder##` substitution | OLD ApplicationUtility.GetEmailTemplateFromHTML | **Implemented** | |
| Inline signature image on Patient Packet PDF | OLD | **Implemented** (UserSignatureAppService) | |

### 2.AA Authentication — Login

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| Email lookup (lowercased, normalized) | OLD UserDomain.PostLogin | **Implemented** (ABP LookupNormalizer) | |
| Verified-only gate | OLD | **Implemented** | |
| Active-only gate | OLD | **Implemented** | |
| FailedCount tracking (OLD unused server-side bug) | OLD | **Not replicated** (OLD bug; intentional) | |
| Single-session enforcement (one JWT per user; logout invalidates) | OLD ApplicationUserToken | **NOT replicated** — NEW uses ABP default multi-session per Adrian Q1 | Accepted deviation |
| Auto-resend verification email on login (if unverified) | OLD | **Replaced with explicit "Resend Verification" button** per Adrian Q2 | Accepted deviation |
| Post-login redirect by user type (external → home, internal → dashboard) | OLD | **Implemented** | |
| Modules/permissions response on login | OLD `authorize` endpoint | **Replaced by ABP permission service** | |
| `IsAccessor`, `IsFirstTime` flags returned on login | OLD | **Implemented** (extension props) | |
| `isAccessor`-specific home UI variant | OLD HomeComponent.isAccessor branch | **TO VERIFY** | |
| Login failure rate-limiting | OLD has none | **NEW adds** (per memory; verify on /api/account/login) | |
| OLD bug: duplicate `PatientAttorney` check in login flow | OLD | **Fixed in NEW** | Audit `external-user-login.md` |

### 2.BB Authentication — Registration

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| Self-registration for Patient / AA / DA / CE | OLD UserDomain.AddExternalUser | **Implemented** (ExternalSignupAppService) | |
| ConfirmPassword DTO field + match validator | OLD intake form | **Implemented** | |
| FirmName required for Attorney roles; FirmEmail | OLD | **Implemented** (per `external-user-registration.md` G3-G6) | |
| ClaimExaminer = OLD Adjuster (id=5) | OLD | **Implemented** as `ClaimExaminer` role | |
| `IsExternalUser` extension prop set on signup | NEW | **Implemented** | |
| Email-confirmation sent | OLD | **Implemented** | |
| Generic duplicate-email error (no echo of email; HIPAA-safe) | NEW security hardening | **Implemented** (PR #197) | |
| Rate-limit on /external-signup/register (5/hr/IP → 429) | NEW security hardening | **Implemented** (PR #197) | |
| HTTP 400 (not 403) on validation errors | OLD returns mixed; NEW | **Implemented** (BUG-023 fixed 2026-05-19 via commit `25a50c6` + `d5d95d1`) | Const name corrected to `ConfirmPasswordMismatch`; HTTP 400 mapping added in HttpApi.Host module |
| Terms & Conditions checkbox + modal | OLD term-and-condition.component | **Implemented** on AuthServer Razor (PR #204, commit `39477af`) | Modal appears during registration; SPA path retired |
| Patient defaults on signup: Gender=Male, DOB=Today, PhoneNumberType=Home (hardcoded) | NEW ExternalSignupAppService | **Implemented as workaround** | Decide: prompt for real values or keep hardcoded |
| OLD bug: duplicate `PatientAttorney` check | OLD | **Fixed in NEW** | |
| OLD bug: dead `FirmName` assignment | OLD | **Fixed in NEW** | |
| OLD bug: email typo + file typo | OLD | **Fixed in NEW** | |

### 2.CC Authentication — Forgot Password

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| 2-step flow: postforgotpassword → putforgotpassword | OLD UserDomain | **Implemented** (Phase 10) | |
| VerificationCode (GUID) token | OLD Users.VerificationCode | **Replaced by ABP DataProtectionTokenProvider** | Accepted deviation |
| Verified-only + active-only gates | OLD | **Implemented** | |
| Rate limit 5/hr/email | NEW hardening | **Implemented** | |
| Generic message (no enumeration leak) | NEW HIPAA hardening | **Implemented** | |
| OLD bug: dead branch on token mismatch | OLD | **Fixed in NEW** | |
| OLD bug: pre-check email logging | OLD | **Fixed in NEW** | |

### 2.DD Authentication — Email verification

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| `/verify-email/:userId` route | OLD VerifyEmailComponent | **Implemented** (VerifyEmailRedirectComponent transforms OLD-style URL → ABP `/account/email-confirmation`) | |
| `POST /api/account/verify-email-confirmation-token` endpoint | NEW correction | **Implemented** (BUG-006 was wrong path; fixed) | |
| Resend Verification button | NEW Q2 decision | **Implemented** | |

### 2.EE Authorization / RBAC

| Item | OLD source | NEW status | Notes |
|---|---|---|---|
| 7 OLD roles: ITAdmin / StaffSupervisor / ClinicStaff / Patient / Adjuster / PatientAttorney / DefenseAttorney | OLD Roles table | **All 7 in NEW** (Adjuster→ClaimExaminer; Doctor role removed) | |
| `RoleUserType` (Role × Internal/External) | OLD | **Replaced by ABP role properties / IsExternalUser extension prop** | Accepted deviation |
| `RoleAppointmentType` (which roles can request which appt types — only attorneys can request AME) | OLD | **Not implemented** — see OBS-23 (filed 2026-05-20). NEW has no backend gate. | OLD permission table; NEW would need a small AppService check or a per-tenant policy table |
| `RolePermission` (Role × ApplicationModule with CanView/Add/Edit/Delete) | OLD | **Replaced by ABP PermissionDefinitionProvider** | |
| `ApplicationModule` hierarchy (parent/child modules) | OLD | **Mapped to NEW permission tree** (44 constants) | Verify all OLD modules covered |
| `ApplicationObject` permissions | OLD | **Mapped to NEW permission Default/Create/Edit/Delete children** | |
| Per-user authorization endpoint (`/UserAuthorization/access`) | OLD | **Replaced by ABP `currentUser.permissions`** | Accepted deviation |
| 30-minute permission cache | OLD UserPermissionCache | **Replaced by ABP permission service (session-scoped)** | Accepted deviation |
| External user appointment-view scope rules (creator OR matching AA/DA/CE email OR Patient.IdentityUserId OR Accessor) | OLD implicit; NEW verified | **Implemented** | |

---

## 3. Frontend (Angular) gap by feature area

### 3.A Booking wizard (`/appointments/add`)

| Item | OLD | NEW | Notes |
|---|---|---|---|
| 7 section sub-components | OLD `appointment-add` (single 1500-line component) | **Decomposed into 7 sections** (PR #121) | Architectural improvement |
| Demographics: Name / DOB / SSN / Phone / Email / Address / Gender / Language / Interpreter / Phone-type radio | OLD | **Mostly implemented** | Verify SSN-have checkbox + conditional input + phone-type custom validator + interpreter-vendor field |
| Claim information modal (injury detail + insurance + claim examiner per injury) | OLD nested modal | **Implemented** as in-form section + modal | |
| Cumulative trauma injury (date range) | OLD radio toggle | **TO VERIFY** | Memory says we considered this for OBS-15 |
| Multiple injuries per appointment | OLD | **TO VERIFY** | |
| Per-injury body parts (FormArray) | OLD | **TO VERIFY** | |
| Per-injury claim examiner | OLD | **TO VERIFY** | |
| Per-injury primary insurance | OLD | **TO VERIFY** | |
| Schedule section: doctor → availability calendar → time picker | OLD | **Implemented** | Memory: BUG-021 (datepicker mass-disable) closed |
| Attorney sections (AA + DA with fieldset disabled toggle) | OLD `isActiveApplicantAttorney`, `isActiveDefenceAttorney` | **Implemented per AA/DA decompose work** | Verify fieldset toggle works on edit |
| Employer details section | OLD | **Implemented** | |
| Authorized users (Accessor) add modal | OLD | **TO VERIFY** | NEW may invite accessors elsewhere |
| Custom fields rendering by AppointmentType | OLD | **Implemented** (appointment-add-custom-fields.component.ts) | |
| Date/time custom validators (`AppointmentDate < today + LeadTime` etc) | OLD bindFormGroup validators | **TO VERIFY** | NEW reactive forms + RxWeb validators |
| Adjuster-specific readonly fields (claim examiner email/name auto-fill from logged-in adjuster) | OLD `isReadonlyAdjuster` | **TO VERIFY** | Memory: addressed in DA-booker question; verify for Adjuster |
| Attorney-specific readonly fields (similar) | OLD `isReadonlyPatientAttorney`, `isReadonlyDefenseAttorney` | **TO VERIFY** | |
| Patient email readonly for patient role | OLD `isPatient && userRoleId != ITAdmin` | **TO VERIFY** | |
| Panel number readonly for AME / AME-REVAL types | OLD `appointmentTypeId == AME` | **TO VERIFY** | |
| Revolution form search-by-confirmation-# UI | OLD `isRevolutionForm` flag | **TO VERIFY** | NEW likely covered in REVAL flow |
| Inline validation summary (component) | OLD AppointmentValidationComponent | **TO VERIFY** | |
| AutoJump (focus next field after entry) | OLD | **NOT STARTED** (low priority UX polish) | |
| Tooltip help on appointment date with availability hint | OLD `rxTooltip` | **NOT STARTED** | |

### 3.B View appointment page (`/appointments/view/:id`)

| Item | OLD | NEW | Notes |
|---|---|---|---|
| Status-driven button visibility (Approve/Reject/Cancel buttons by current status) | OLD `pendingState`, `approveState` flags | **Implemented** | |
| Approve confirmation modal (with Responsible User dropdown + Comments) | OLD inline | **Implemented** (ApproveConfirmationModalComponent) | |
| Reject modal (with reason; max 500 chars) | OLD | **Implemented** (RejectAppointmentModalComponent) | BUG-024: server allows empty reason |
| "Re-Apply" button when status=Rejected and `createdById == loginUserId` | OLD | **TO VERIFY** | |
| Reschedule modal (calendar + new date + reason + IsBeyodLimit) | OLD | **TO VERIFY** | NEW has approval-side but request-submission modal unconfirmed |
| Cancellation modal (with reason) | OLD | **TO VERIFY** | |
| Document upload section (with rejection re-upload) | OLD | **Implemented** (AppointmentDocumentsComponent) | |
| Packet generation status section + download links | NEW | **Implemented** (AppointmentPacketComponent; 5s polling) | |
| Joint Declaration upload (AME only, attorney only) | OLD | **TO VERIFY** | |
| Pending change requests details (reason, requested date, doc status) | OLD when `isCancelRescheduleRequestPending` | **TO VERIFY** | |
| Audit log link `/change-log` | NEW route exists | **Implemented** but data-load shape TBD | |
| Print to PDF / Export to Excel button | OLD | **NOT STARTED** | |
| "Original Appointment" link (for rescheduled-from chain) | OLD OriginalAppointmentId | **TO VERIFY** | |

### 3.C Appointment list pages

| Item | OLD | NEW | Notes |
|---|---|---|---|
| `/appointments` list with quick + advanced search | OLD | **Implemented** | |
| `/appointment-pending-request` (pending requests view) | OLD | **TO VERIFY** — NEW dashboard counter clicks into filtered `/appointments?status=1` | Equivalent functionality, different route |
| `/appointment-approve-request` (date-driven approval queue with inline check-in/out actions) | OLD | **NOT STARTED** | Today-view list (clinic-staff-check-in-check-out.md audit) |
| `/appointment-rescheduled-requests` (admin reschedule queue) | OLD | **Partial** — backend exists; UI possibly inline in `/appointments` | |
| `/appointment-cancel-requests` (admin cancellation queue) | OLD | **Partial** | |
| `/appointment-search` (advanced filter page) | OLD | **Implemented as advanced search on `/appointments`** | |
| `/appointment-documents` (document admin view across appointments) | OLD | **NOT STARTED** | |
| `/appointment-new-documents` (ad-hoc doc admin view) | OLD | **NOT STARTED** | |
| `/appointment-documents-search` (cross-system search) | OLD | **NOT STARTED** | |
| `/appointment-joint-declarations-search` (JDF admin view) | OLD | **NOT STARTED** | |
| `/appointment-change-logs` (audit log admin view) | OLD | **Partial** — `/appointments/view/:id/change-log` for single appointment; cross-appointment view absent | |
| `/report` (Appointment Request Report) | OLD | **NOT STARTED** | |
| Patient home: confirmation # search + appointments list (mode-aware) | OLD HomeComponent | **Implemented** | |
| Pagination 10 default + sizes [5,10,20,30,40,50] | OLD | **TO VERIFY** | NEW uses ABP ListService |

### 3.D Master data admin pages

| Item | OLD | NEW | Notes |
|---|---|---|---|
| `/doctors/:id` (Doctor profile edit) | OLD | **Implemented** | |
| `/doctors-availabilities` (calendar slot generator) | OLD | **Implemented** (with bulk generate route) | |
| `/locations` (clinic locations CRUD) | OLD | **Implemented** | |
| `/system-parameters/:id` (System parameter edit) | OLD | **NOT STARTED — UI deferred per audit** | Backend implemented |
| `/templates` (Notification template editor) | OLD | **NOT STARTED — UI deferred** | Backend implemented |
| `/custom-fields` (Custom field admin) | OLD | **Implemented** (CustomFieldsAppService + UI assumed) | Verify CRUD UI |
| `/documents` (Master document catalog) | OLD | **Implemented** | |
| `/package-details` (Package definitions) | OLD | **Implemented** | |
| `/appointment-document-types` (Document type CRUD) | OLD | **NOT STARTED** | grep confirmed no entity |
| `/users` (User admin) | OLD | **Partial** — ABP Identity UI usable for internal users; NEW `/users/invite` is external-user only | |
| `/notes` (Notes admin) | OLD | **NOT STARTED** | |
| `/applicant-attorneys`, `/defense-attorneys` (Attorney CRUD admin) | NEW | **Implemented** | NEW addition |
| WcabOffice CRUD UI | OLD | **Implemented** | |
| AppointmentType CRUD UI | OLD | **Implemented** | |
| AppointmentStatus CRUD UI | OLD | **Implemented** | |
| AppointmentLanguage CRUD UI | OLD | **Implemented** | |
| State CRUD UI | OLD via cascading | **Implemented** | |

### 3.E Login / auth UI

| Item | OLD | NEW | Notes |
|---|---|---|---|
| `/login` form (email + password + failed-count tracking) | OLD | **Replaced** by ABP `/account/login` Razor page | |
| `/forgot-password` page | OLD | **Replaced** by ABP `/account/forgot-password` | |
| `/reset-password/:code` page | OLD | **Replaced** by ABP equivalent | |
| `/verify-email/:userId` redirect to ABP | OLD | **Implemented** | |
| `/Account/Register` AuthServer page (NEW custom) | NEW | **Implemented** | |
| Terms & Conditions modal during register | OLD term-and-condition component | **NOT STARTED** | grep confirmed; file new audit before implementation |
| Self-represented modal | NEW Phase 19 (per `2026-05-07-bug-batch-2.md`) | **NOT STARTED** | |
| Floating "Submit Query" widget on app root | OLD | **NOT STARTED** | |
| `/unauthorized` page | OLD | **Implemented** as ABP HttpErrorComponent for 403 | |
| `/404` page | OLD | **Implemented** (RouteNotFoundComponent) | |
| Failed login counter (localStorage tracking) | OLD | **NOT replicated** (OLD bug; unused) | |
| CallBackUrl post-login redirect | OLD | **TO VERIFY** | NEW guards may handle this |
| Sidebar/topbar/footer | OLD | **Implemented** (LeptonX) | |
| Sidebar hidden for external users | NEW `.externaluser-sidebar-hidden` | **Implemented** | |
| External-user-only home (no dashboard navigation) | OLD | **Implemented** | |

### 3.F Profile / Settings

| Item | OLD | NEW | Notes |
|---|---|---|---|
| Own profile edit (`/users/:id` for self) | OLD | **Implemented** (`/doctor-management/patients/my-profile` for patient role; ABP `/account/manage` for general identity) | |
| Change password | OLD | **Implemented** by ABP | |
| Upload signature (internal user) | OLD UserDomain.SignatureAWSFilePath | **Implemented** (UserSignatureAppService + Angular UI exists?) | Verify Angular UI; if missing, file audit |

### 3.G Notes UI (OLD)

| Item | OLD | NEW | Notes |
|---|---|---|---|
| Note thread per appointment (with reply, edit history) | OLD AppointmentListComponent → NoteRequest modal | **NOT STARTED** | |
| Note add modal (rich text) | OLD | **NOT STARTED** | |
| Notes list view across appointments | OLD `/notes` | **NOT STARTED** | |
| "Who edited this note" display via `vNoteLookUps` | OLD | **NOT STARTED** | |

---

## 4. Database / schema gaps

Entities and columns present in OLD but absent or partial in NEW. Verify each against `CaseEvaluationDbContext` and `CaseEvaluationTenantDbContext` configurations.

### Architectural invariant — one doctor per tenant (confirmed 2026-05-15)

OLD is single-doctor-per-deploy. NEW maps that to **exactly one `Doctor`
row per tenant, for the lifetime of the tenant.** This is the permanent
product invariant — there will never be a tenant with two doctors. A
group-practice product would be a separate application, not an
extension of this one.

This is now recorded as `PARITY-FLAG-NEW-006` in
`docs/parity/wave-1-parity/_parity-flags.md`.

Consequences (and corrections to earlier hedges in this audit):

1. `DoctorAvailability` has **no FK to `Doctor`** and never will — tenant
   scope IS the doctor identifier. The earlier note "if multi-doctor-per-tenant
   ever lands, this becomes a problem" no longer applies and is withdrawn.
2. `DoctorPreferredLocation` and `DoctorAppointmentType` keep `DoctorId`
   in their composite keys for EF compatibility only — the column is
   functionally derivable from `TenantId`.
3. `DoctorTenantAppService.CreateAsync` (override of `SaasTenantAppService`)
   is the canonical Doctor-creation path. It runs atomically with tenant
   provisioning.
4. **New gap surfaced by the invariant:** `DoctorsAppService.CreateAsync`
   and `DeleteAsync` are still exposed and do NOT enforce the
   invariant. See section 3.C in `staff-supervisor-doctor-management.md`
   (row "One-doctor-per-tenant invariant enforcement"). A second Doctor
   row in the same tenant can be created today, and the sole Doctor can
   be deleted while DoctorAvailabilities / Appointments still reference
   the tenant.

### Missing entities (no NEW equivalent):

| OLD entity | Purpose | Recommendation |
|---|---|---|
| `Notes` (with ParentNoteId, EditNoteId, IsLatest) | Internal threaded notes | New entity + AppService when audit `appointment-notes.md` is built |
| `UserQuery` | Submit Query log | New entity + AppService per `external-user-submit-query.md` audit |
| `AppointmentDocumentType` (lookup master) | Document type catalog | Verify NEW has equivalent (Document master may serve dual purpose) |
| `Country`, `City` lookups | Address master data | Add if/when address fields require validation |
| `ApplicationTimeZones` (tenant default timezone) | Per-tenant TZ | NEW relies on ABP setting; verify per-tenant override works |
| `RoleAppointmentTypes` (Role × AppointmentType M:M) | "Only attorneys can request AME" rule | Verify NEW enforces this in code (not data) |
| `TempAppointmentInjuryDetails` (REVAL staging) | Holds injury rows during REVAL edit before commit | Likely unnecessary in NEW (transactional create); verify REVAL flow doesn't lose data |
| `LockRecord` + `GlobalSetting.RecordLock` | Pessimistic record locking | Phase 2 per `record-locking.md` |
| `ConfigurationContents` / `LanguageContents` / `ModuleContents` (En/Fr i18n) | Bilingual support | Phase 2 per `application-configurations.md`; ABP localization replaces |
| `CacheCollections` / `CacheKeys` (SQL-backed cache) | Redis fallback layer | Skip (NEW uses ABP distributed cache) |
| `AuditRequests` / `AuditRecords` / `AuditRecordDetails` (Custom audit trail) | Custom audit log | Replaced by ABP `[Audited]` |
| `RequestLogs` (per-request log) | API audit trail | Replaced by ABP request logging middleware |
| `ApplicationExceptionLogs` | Exception tracking | Replaced by ABP exception logging |
| `SmtpConfigurations` | Per-tenant SMTP config | Replaced by appsettings + ABP Settings |

### Missing fields on existing entities:

| Entity | OLD column | NEW status | Notes |
|---|---|---|---|
| Location | `ParkingFee` decimal(5,2) | **Not in NEW inventory** | Surfaces on dashboard / packet output |
| Location | `AppointmentTypeId` | **Not in NEW inventory** | "Which appt types are bookable at this location" |
| AppointmentType | `ReEvalId` (linkage to REVAL variant) | **Not in NEW inventory** | OLD reference for REVAL pre-load flow |
| AppointmentInjuryDetail | `ToDateOfInjury` (cumulative trauma end-date) | **TO VERIFY** | |
| AppointmentInjuryDetail | `IsCumulativeInjury` flag | **TO VERIFY** | |
| AppointmentInjuryDetail | `WcabAdj` (text field) | **TO VERIFY** | |
| AppointmentInjuryDetail | `BodyParts` freetext summary | **TO VERIFY** — NEW has BodyPart join table; OLD has both | |
| AppointmentClaimExaminer | `ClaimExaminerNumber` (varchar 255) | **TO VERIFY** | |
| AppointmentPrimaryInsurance | `Attention` nvarchar(Max) | **TO VERIFY** | |
| AppointmentPrimaryInsurance | `InsuranceNumber` varchar(255) | **TO VERIFY** | |
| AppointmentDocument | `VerificationCode` uniqueidentifier | **Implemented** in NEW | |
| AppointmentDocument | `AttachmentLink` nvarchar(255) | **TO VERIFY** | |
| AppointmentDocument | `IsJoinDeclaration` (sic) bit | **Replaced by separate AppointmentDocument types** | |
| AppointmentChangeRequest | `IsBeyodLimit` (sic) bit | **Implemented as IsBeyondLimit (typo corrected; verify acceptable)** | |
| Appointment | `InternalUserComments` varchar(250) | **TO VERIFY** | OLD has it; NEW may or may not |
| Appointment | `AppointmentApproveDate` date | **TO VERIFY** | Should be present per memory |
| User | `SignatureAWSFilePath` | **Implemented via UserSignature blob path** | |
| User | `IsAccessor`, `IsVerified`, `IsActive` | **Implemented as ABP extension props** | |
| SystemParameter | `AppointmentDurationTime` (15 min default) | **TO VERIFY** | |
| SystemParameter | `AppointmentDueDays` | **TO VERIFY** | |
| SystemParameter | `PendingAppointmentOverDueNotificationDays` | **TO VERIFY** | |

### Missing V-Views (read-model layer)

OLD has 76 V-Views. NEW uses EF Core projection. Most won't need 1:1 ports — but some join shapes are non-trivial and need replicating in DTOs:

- `vAppointmentStakeholders` — used to drive notification recipient list. NEW NotificationDispatcher should produce equivalent shape; verify.
- `vEmailSender` — denormalized email-template context (PatientName, AppointmentDateTime, ConfirmationNumber, etc.). NEW `NotificationTemplateRenderer` context — verify field coverage.
- `vAppointmentsForChangeLog` (~70 columns) — wide snapshot for change-log emails. NEW likely doesn't replicate this snapshot; flag for `appointment-change-log.md` follow-up.
- `vPackageDetailLookUps` (~30 columns) — package-doc email context. NEW PackageDocumentReminderEmailHandler — verify field coverage.

---

## 5. Business rules / verbatim binding quotes from OLD docs that need replicating

The following are direct, binding quotes from `SoCal Project Overview
Document.pdf` (the OLD business bible). Each is annotated with NEW
implementation status.

| Rule | Source (verbatim) | NEW status |
|---|---|---|
| **Patient dedup (3-of-6)** | "This will be done if any 3 of below fields are matching past records: Last name, Date of Birth, Phone, Email, SSN, Claim Number. If a match is found, the system will not create a new patient record, and map new appointment with existing record." | **Mostly implemented** (Phase 11k); verify exact 3-of-6 logic |
| **Manual link override on approval** | "While viewing the appointment request, the Clinic Staff will be notified if the patient already exists in the system. ... the user will have an option to link the appointment request to the same patient record or create a new one." | **NOT IMPLEMENTED** — file audit |
| **Responsible team member email convention** | "On accepting, the Clinic Staff will select the 'responsible team member' from the list of other internal users. This user will be primary responsible user for the respective appointment request and will receive all the email notifications." | **Partially implemented** — PrimaryResponsibleUserId persists; verify all notifications route to them |
| **Internal vs external doc packet split** | "Email sent to the patient will have the package documents in form of attachments. ... Email sent to Clinical staff marked as 'responsible team member' will have a separate package of documents in form of attachments." | **NOT IMPLEMENTED** — NEW sends one packet, signed-URL link only |
| **Patient-email fallback** | "In case the appointment request does not have patient email id, the package documents will be sent to the user who created the appointment request." | **TO VERIFY** |
| **Reschedule creates new appointment row, same confirmation #** | "will create a new appointment record with same confirmation number and new appointment date, in Approved status" | **Implemented** (AppointmentRescheduleCloner; cascade-copy in Phase 17) |
| **JDF auto-cancel for AME** | "the appointment will be auto-cancelled and a notification email will be sent to all the stakeholders related to the appointment" | **Implemented** (JointDeclarationAutoCancelJob) |
| **Per-document accept/reject granularity** | "The clinic staff can accept few documents and reject others." | **Implemented** |
| **Accepted documents immutable** | "Accepted documents cannot be modified by the external users." | **Implemented** |
| **"Reach 30 minutes early" reminder copy** | "the notifications will prompt the user to reach before 30 minutes to submit the pending information" | **TO VERIFY** in NEW template body |
| **Change log filters** | "The change log should be filterable on field name, date range and user" | **TO VERIFY** — ABP audit-logging UI provides this; verify on NEW change-log page |
| **Custom fields ≤ 10 per AppointmentType** | "The user can add up to 10 additional fields" | **Implemented** (NEW fixes OLD `== 10` bug to `>= 10`) |
| **Block (not delete) external users** | "IT Admin should also be able to block external user's access to the system." | **TO VERIFY** — likely works via ABP IsActive |
| **Submit query is fire-and-forget** | "The system will not track any further correspondence related to this feature." | **NOT IMPLEMENTED** — Submit Query feature absent |
| **Reports: HTML + Excel + PDF only (no Crystal Reports / report builder)** | "All the reports will be standard HTML based reports" | **NOT STARTED** |
| **3 doctor sites → merged Schedule Report via replicated read-only DB** | "show the merged data of all the three doctor's website" | **NOT STARTED** (Phase 2) |
| **ODBC views (3-4) for Excel pivot** | "expose 3-4 views via ODBC connection. These views will be linked into MS Excel as an external data source" | **NOT STARTED** (Phase 2) |
| **Notification template trigger events (9 documented)** | "When a new appointment has been booked. / On Approval or rejection..." | **Template content exists; 41 handlers unwired** |

---

## 6. Email / SMS templates — coverage matrix

OLD has 59 templates (unified from 16 DB + 43 disk). NEW has 59 seeded
+ 18 handlers wired. Audit `email-coverage-audit.md` lists the gap;
restated here briefly.

**Wired handlers (NEW):**

1. BookingSubmissionEmailHandler — booking created
2. ChangeRequestSubmittedEmailHandler — cancel/reschedule request submitted
3. ChangeRequestApprovedEmailHandler — cancel/reschedule approved
4. ChangeRequestRejectedEmailHandler — cancel/reschedule rejected
5. AccessorInvitedEmailHandler — accessor granted
6. DocumentUploadedEmailHandler — document uploaded
7. DocumentAcceptedEmailHandler — document approved
8. DocumentRejectedEmailHandler — document rejected
9. DueDateApproachingEmailHandler — due date reminder
10. DueDateDocumentIncompleteEmailHandler — doc-incomplete reminder
11. PatientPacketEmailHandler — packet ready to patient
12. AttyCEPacketEmailHandler — packet ready to AA/DA/CE
13. StatusChangeEmailHandler — status mutation (CheckedIn/CheckedOut/NoShow/Cancelled etc.)
14. ClinicalStaffCancellationEmailHandler — admin-side cancellation
15. PendingDailyDigestEmailHandler — daily clinic-staff digest
16. InternalStaffQueueDigestEmailHandler — staff supervisor digest
17. JdfAutoCancelledEmailHandler — JDF auto-cancel notification
18. PackageDocumentReminderEmailHandler — reminder for missing package docs
19. PackageDocumentQueueHandler — enqueue packet generation on approval

**OLD templates seeded in NEW DB but NOT wired:** ~41 of 59 — see
`email-coverage-audit.md` for the exhaustive list. Notable absentees:

- `UserRegistered` / welcome email — **demo-critical, unwired**
- `AppointmentBooked` — **demo-critical, unwired** (BookingSubmissionEmailHandler should fire it; verify)
- `AppointmentApproved` (external) — verify Patient/AttyCEPacket cover this
- `PasswordChange` confirmation — unwired
- `AppointmentRescheduleReqAdmin` (admin-initiated reschedule) — unwired
- `Appointment-Cancelled-With-DueDate` (auto-cancel after JDF missed) — handler exists but verify template wiring
- `Appointment-Change-Logs` (staff email summary on appointment edits) — unwired
- `Joint-Agreement-Letter-Accepted/Uploaded/Rejected` — verify JDF-specific templates fire (vs generic Document* templates)

**SMS path:** entirely unimplemented. Twilio replacement TBD. Audit `email-coverage-audit.md` flags this as a blocking gap.

---

## 7. Background jobs — complete matrix (already shown in §2.X)

Plus add-ons in NEW:
- `AppointmentDayReminderJob` (T-7 + T-1)
- `CancellationRescheduleReminderJob`
- `RequestSchedulingReminderJob`
- `GenerateAppointmentPacketJob` (on-demand packet regeneration)
- `SendAppointmentEmailJob` (generic email enqueue)

---

## 8. Permissions / role matrix gap

OLD's role × use-case matrix from §3.3 of Project Overview. Mapping
status against NEW permissions:

| Use Case | OLD assigned to | NEW permission(s) | Implemented? |
|---|---|---|---|
| Registration / Login / Forgot / Manage Profile | All external roles | ABP Identity built-in | Yes |
| Appointment Request (PQME) | Patient + Adjuster + AA + DA | `Appointments.Create` | Yes (no per-type role gate) |
| Appointment Request (AME) | AA + DA only | `Appointments.Create` + AME-type-role gate | **No -- see OBS-23 (2026-05-20)** |
| Appointment Request (PQME-REVAL) | All external | `Appointments.Create` + REVAL flow | Yes |
| Appointment Request (AME-REVAL) | AA + DA only | Same | **No -- see OBS-23 (2026-05-20)** |
| View Appointment Request | Appointment Owner | Access rules | Yes |
| Upload Package Documents | Patient + Owner | `AppointmentDocuments.Default` | Yes |
| Upload Joint Declaration | Owner (attorney) | JDF rules | Yes |
| Re-schedule Request | Owner | `AppointmentChangeRequests.Create` | Yes (via creator gate) |
| Cancellation Request | Owner | Same | Yes |
| Submit Query | All external | **NOT IMPLEMENTED** | No |
| Internal: Dashboard | All internal | `Dashboard.Tenant` | Yes |
| View All Appointments | All internal | `Appointments.Default` | Yes |
| View Change Log | All internal | `AppointmentChangeLogs.Default` | Yes |
| Approve/Reject Appointment | All internal | `Appointments.Approve` / `.Reject` | Yes |
| Approve/Reject Package Docs | All internal | `AppointmentDocuments.Approve` | Yes |
| Approve/Reject JDF | All internal | Same | Yes |
| Approve/Reject Reschedule | Supervisor + IT Admin | `AppointmentChangeRequests.Approve` / `.Reject` | Yes (verify Clinic Staff excluded) |
| Approve/Reject Cancellation | Supervisor + IT Admin | Same | Yes |
| Check-In / Check-Out | All internal | **NOT IMPLEMENTED** (no AppService) | No |
| Manage Doctor location preference | Supervisor + IT Admin | `DoctorPreferredLocations.Toggle` | Yes |
| Manage Doctor appt-type preference | Supervisor + IT Admin | (deferred Phase 7b) | Partial |
| Manage Doctor Availability + Timeslots | Supervisor + IT Admin | `DoctorAvailabilities.*` | Yes |
| Manage Appointment request fields (Custom fields) | IT Admin only | `CustomFields.*` | Yes |
| Manage Users (internal create/edit) | IT Admin | **NOT IMPLEMENTED** | No |
| Manage System Parameters | IT Admin | `SystemParameters.Edit` | Yes (backend; no UI) |
| Manage Notification Templates | IT Admin | `NotificationTemplates.Edit` | Yes (backend; no UI) |
| View Reports | Supervisor + IT Admin | **NOT IMPLEMENTED** | No |

---

## 9. Integration & infrastructure gaps

| OLD integration | OLD library / config | NEW replacement | Status |
|---|---|---|---|
| AWS S3 (`mainsocalpelton` bucket, us-east-2) | AWSSDK.S3 | MinIO (dev) / Azure Blob (prod) | Implemented |
| AWS SES email | AWSSDK.SimpleEmail | Azure Communication Services SMTP | Implemented |
| Twilio SMS | Twilio library | **ABP SMS module — not yet integrated** | **NOT STARTED** |
| OpenXml DOCX generation | DocumentFormat.OpenXml | QuestPDF (via Gotenberg) | Implemented (PDF replacement) |
| iTextSharp PDF generation | iTextSharp | QuestPDF + Gotenberg | Implemented |
| ClosedXML Excel export | ClosedXML.Excel | **NOT YET — no Excel export feature** | NOT STARTED |
| NodaTime time-zone arithmetic | NodaTime 2.3 | NodaTime or System.TimeZoneInfo TBD | Partial |
| Redis cache | StackExchange.Redis | ABP DistributedCache (Redis-compatible) | Implemented |
| JWT custom | Rx.Core.Security.Jwt | OpenIddict OAuth2 | Implemented (different model) |
| MimeKit email | MimeKit | ABP IEmailSender | Implemented |
| Quartz / Hangfire scheduler | OLD has no formal scheduler — external cron + `/api/Scheduler/postscheduler` endpoint | ABP Hangfire integration | Implemented |
| Windows Task Scheduler trigger | OLD external | Hangfire RecurringJob | Implemented |
| Postman collection (220 requests) | OLD curated | OpenAPI / Swagger (NEW auto-generated) | OK |
| Reports via Excel ODBC views (3-4 views exposed) | OLD ODBC | **NOT STARTED** (Phase 2) | NOT STARTED |

---

## 10. Cross-cutting concerns

### 10.A Multi-tenancy

- OLD: per-doctor isolated DBs (3 sites; merged read-only DB for Schedule Report)
- NEW: ABP multi-tenant (Phase 1A targets one demo tenant; Phase 2 multi-tenant rollout)
- Gap: Per-tenant logo / colors / clinic name (currently hardcoded in packets per `packet-generation-audit.md`); Phase 2 owns

### 10.B Branding (32 hardcoded items in OLD)

(Section refreshed 2026-05-20: brand rename complete.)

- Audit `_branding.md` documents the migration plan (8-step Phase 1)
- NEW has `_brand.scss` with 26 brand tokens — pixel-perfect color/font match (per CLAUDE.md Primary Mission)
- "Appointment Portal" brand rename shipped (commit `f9d73de feat(brand): rename to "Appointment Portal" across SPA + AuthServer + emails`)
- Gap: Tenant override layer for logo / clinic name / phone / email / fax (Phase 1 ongoing per `_branding.md`)

### 10.C i18n / Localization

- OLD: bilingual (En/Fr) columns across ModuleContents / LanguageContents / ConfigurationContents (likely unused but in schema)
- NEW: ABP localization (JSON files now; DB-managed in Phase 2 per `application-configurations.md`)
- Gap: French support never rolled out in OLD; safe to skip until needed

### 10.D HIPAA / PHI hygiene

(Section refreshed 2026-05-20: BUG-023 closed.)

- All test data must be synthetic (per CLAUDE.md hipaa.md rule)
- NEW additions: rate limiting on registration, generic duplicate-email message, no echo of input
- ~~Gap: BUG-023 (validation errors return 403 instead of 400)~~ — FIXED 2026-05-19 (commits `25a50c6` + `d5d95d1`)
- Gap: BUG-025 (no document upload size cap) — DoS risk, still open
- Open: content-type allowlist on document uploads (recommend PDF/DOCX/PNG/JPG)

### 10.E Test data conventions

- Default test password (per memory): `1q2w3E*r`
- Standard test patient: synthetic SSN / DOB / MRN
- Verified in R1+R2 hardening rounds (2026-05-14)

---

## 11. Demo-critical blockers (per email-coverage-audit + R2 hardening findings)

| Blocker | Severity | Owner | Status |
|---|---|---|---|
| NullEmailSender gate disables dev emails | Blocking | Phase 18 | Open |
| SMS not integrated (Twilio replacement) | High | Phase 18 / TBD | Open |
| Stale Angular proxy (`getList` sends 18 params; DTO defines 7) | High | Dev | Open per `internal-user-view-all-appointments.md` |
| Master-data seed incomplete (Languages, WCAB offices, States/Cities) | High | Phase 1 | Partial |
| Internal user creation AppService missing | High | Phase 3 | Open |
| Submit Query feature missing | Medium | TBD | Open |
| Terms & Conditions modal missing | Medium | TBD | Open |
| Notes feature missing | Medium | TBD | Open |
| Check-in / Check-out / NoShow / Billed transitions missing | High | TBD | Open |
| Appointment Request Report missing | High | TBD | Open |
| CSV / Excel / PDF export missing | Medium | TBD | Open |
| Document upload size cap missing | Medium | Backend dev | Open per BUG-025 |
| Document content-type allowlist missing | Medium | Backend dev | Open per BUG-025 |
| Reject endpoint accepts empty notes | Medium | Backend dev | Open per BUG-024 |
| ConfirmPasswordMismatch / FirmNameRequiredForAttorney return 403 | Medium | Backend dev | Open per BUG-023 |

---

## 12. Accepted parity deviations (per `_parity-flags.md` + this audit)

Items intentionally diverging from OLD; these are NOT gaps:

- **Custom JWT → OpenIddict OAuth2** (login flow)
- **Single-session → multi-session** by default (login)
- **Stored procedures → LINQ** (every read)
- **V-Views → EF Core projections** (read model)
- **AppointmentDocuments + AppointmentNewDocuments unified** into single entity with `IsAdHoc` flag
- **DOCX deliverables → PDF** (CLAUDE.md Primary Mission)
- **Email-attached packets → signed-URL link** (audit `email-packet-parity/`)
- **Custom audit tables → ABP `[Audited]`** (audit `appointment-change-log.md`)
- **Custom permission cache → ABP permission service**
- **Per-doctor DB isolation → ABP multi-tenant** (Phase 1A: one tenant; Phase 2: many)
- **AWS S3 → MinIO/Azure Blob** (storage abstraction)
- **AWS SES / SMTP → Azure Communication Services**
- **`mainsocalpelton` bucket layout → flat `{tenant}/{appointmentId}/{Guid:N}`** (audit `blob-storage-layout.md`)
- **Custom Rx* Angular framework → standard Angular 20 reactive forms + RxWeb validators**
- **In-house Bootstrap theme → LeptonX with OLD-color overrides** (per CLAUDE.md)
- **Pessimistic record locks → optimistic concurrency** (Phase 2 decision per `record-locking.md`)
- **Bilingual En/Fr support → single-language English** (Phase 2 work)
- **`isBeyodLimit` (canonical typo) → `IsBeyondLimit`** in NEW — verify Adrian accepts the typo fix; OLD typo is canonical in OLD schema + Postman

---

## 13. Recommended next-phase ordering

By gap severity and blast radius:

### Wave 1 — close demo-critical gaps

1. Wire 41 unwired email templates (Phase 18 follow-through after NullEmailSender fix).
2. Internal user creation AppService + UI (no parity for IT Admin tasks today).
3. Check-in / Check-out / NoShow / Billed transitions (Clinic Staff core workflow).
4. Submit Query feature (small but visible to all external users).
5. Terms & Conditions modal on registration.
6. SMS integration (decide on Twilio vs ACS SMS + wire to all reminder jobs).
7. Document size cap + content-type allowlist (BUG-025 series).
8. Rejection-notes server-side validation (BUG-024).
9. ConfirmPasswordMismatch / FirmNameRequiredForAttorney → 400 mapping (BUG-023).
10. Notification template editor UI (admin-facing).
11. System parameter editor UI (admin-facing).

### Wave 2 — fill UI for backend-complete features

12. Reschedule / cancellation request submission modals (UI side, backend ready).
13. Custom field admin CRUD UI.
14. Appointment Document Type CRUD UI (and confirm backend support).
15. Audit log cross-appointment view (or accept single-appointment view + ABP audit UI).
16. Document admin views (`/appointment-documents`, `/appointment-joint-declarations`, etc.).
17. Notes feature (entity + AppService + thread UI).

### Wave 3 — net-new features absent in NEW

18. Appointment Request Report (HTML + Excel + PDF).
19. CSV / Excel / PDF per-appointment export.
20. Patient-match notice + manual link on staff approval.
21. Internal vs external doc packet split (Project Overview §3.13 verbatim).
22. Today's-appointments date-driven check-in view.
23. Cumulative trauma date-range support (verify).

### Phase 2 (post-parity)

24. Multi-tenant rollout (3-doctor sites, Schedule Report, branding overrides).
25. Excel ODBC views.
26. Record locking decision (pessimistic vs optimistic).
27. SMS templates separated from email (if not unified).
28. French (Fr) localization rollout (if business demands).

---

## 14. Open questions for Adrian

1. **Patient-match notice on approval** (Project Overview §3.12) — implement the manual "link to existing patient or create new" UI?
2. **Internal vs external doc packet split** (§3.13) — preserve OLD's two-packet pattern, or accept current single-packet behavior?
3. **Accessor account auto-creation** — keep OLD's auto-create-account with random password, or move to invite-based flow?
4. **`isBeyodLimit` typo** — keep typo (OLD canonical) or fix to `IsBeyondLimit` (NEW already uses corrected form)?
5. **SMS provider** — Twilio (OLD parity) or Azure Comm SMS (NEW ecosystem)?
6. **Welcome email plaintext password** (`it-admin-user-management.md`) — preserve OLD security debt or change to invite-with-reset link?
7. **Cumulative trauma injury date range** — confirm UX (single date vs From/To radio) on NEW booking form.
8. **Today's appointment view route** (`/appointment-approve-request` in OLD) — same path or a different surface in NEW?
9. **Appointment Document Type catalog** — needed in NEW (entity + admin UI) or can Document master serve as both catalog and type?
10. **Excel export** — needed for Phase 1 demo or defer to Phase 2?
11. **Report module** — defer entirely to Phase 2 (multi-tenant), or stub up Appointment Request Report (single-tenant) now?
12. **AME role gate** (only Attorneys can book AME) — confirm where enforced in NEW; if not server-side, add validation.

---

## 15. Verification approach

Every "TO VERIFY" item above should be confirmed by:

1. Running the specific UI flow on the local Docker stack (`http://falkinstein.localhost:4200` + AuthServer at `:44368`).
2. Direct API probe via Playwright MCP or `curl` against `:44327`.
3. SQL inspection in `main-sql-server-1` via `docker exec`.

Use `HARDENING-TEST-SUITE.md` (Round 1 + Round 2) as the smoke-test
scaffold; extend with verification scenarios for each gap above as
they get closed.

---

## Appendices

### A. Source files for this audit

- `_old-docs-index.md` — OLD module structure index
- `_appointment-form-validation-deep-dive.md` — booking form validators
- `_slot-generation-deep-dive.md` — slot expansion + overlap logic
- `_branding.md` — branding migration plan
- `_cleanup-tasks.md` — Phase 0 cleanup items
- `_parity-flags.md` — accepted deviations register
- All 37 per-feature audits in `wave-1-parity/`

### B. Persisted agent reports (full inventories)

The four parallel inventory agents and two deep-dive agents produced
~250KB of detailed output. Key persisted dumps:

- OLD audit digest (60KB): `C:\Users\RajeevG\.claude\projects\W--patient-portal-main\923b8059-754a-44cd-8d83-1c27a38ad85d\tool-results\toolu_0149VAsisQc6A3wXKcH49p6q.json`
- OLD docs digest (62KB): `C:\Users\RajeevG\.claude\projects\W--patient-portal-main\923b8059-754a-44cd-8d83-1c27a38ad85d\tool-results\toolu_01AX37TJSMqW67tNCPWVuchS.json`
- Postman full dump (40KB): `C:\Users\RajeevG\.claude\projects\W--patient-portal-main\923b8059-754a-44cd-8d83-1c27a38ad85d\tool-results\bmx3rof3y.txt`

### C. Cross-references to hardening findings

- BUG-001..006 — fixed via PR #197
- BUG-021, BUG-022 — datepicker / booking horizon (low UX / intent-vs-math)
- BUG-023 — status mapping 403→400 for two errors (open)
- BUG-024 — reject endpoint validation gap (open)
- BUG-025 — document upload size + content-type (open)
- OBS-15..20 — observation-level issues from R2 hardening

End of audit.
