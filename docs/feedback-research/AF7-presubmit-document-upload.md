---
id: AF7
title: Pre-submit document upload on the booking form (two-phase create-then-upload)
type: enhancement
components: [angular/src/app/appointments/appointment-add.component.ts, angular/src/app/appointments/sections/, angular/src/app/appointment-documents/appointment-documents.component.ts, src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs, src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/AppointmentDocuments/AppointmentDocumentController.cs]
related_known_bugs: [BUG-025, BUG-037, OBS-20, AF5, AF6]
status: researched
decision: approved-2026-06-03
---

## Issue / desired change

Let a booker attach documents WHILE filling the booking form (pre-submit), in a new section
placed between Claim Information and the Additional Authorized User block (directly below the
AF6 PQME panel-strike-list checkbox). Today upload is structurally post-submit only -- the
documents UI exists only on the appointment-view page after the appointment row exists. This
item is the linchpin for AF6: the PQME mandatory-upload gate cannot work without a pre-submit
upload mechanism.

## Current behavior (from investigation)

- Upload is post-create only by design. `AppointmentDocument.AppointmentId` is a NON-nullable
  `Guid` (AppointmentDocument.cs:31, verified), and the manager hard-rejects `Guid.Empty` with
  "AppointmentId is required." (AppointmentDocumentManager.cs:33-37). No row can exist before
  its parent appointment.
- The upload AppService requires a REAL, persisted appointment: `UploadStreamAsync` calls
  `_readAccessGuard.EnsureCanReadAsync(appointmentId)` before any blob save
  (AppointmentDocumentsAppService.cs:177); the guard does `_appointmentRepository.GetAsync(id)`
  which throws `EntityNotFoundException` if the row does not exist
  (AppointmentReadAccessGuard.cs:79-83). So an upload cannot target a not-yet-created
  appointment.
- The booker auto-passes the read-access gate as appointment Creator:
  `AppointmentAccessRules.CanRead` allows `appointment.CreatorId`
  (AppointmentReadAccessGuard.cs:152). This is exactly what makes create-then-upload work with
  ZERO backend authorization change.
- BUG-025 validation lives INSIDE the upload AppService: `EnsureFileSizeWithinLimit`
  (10 MB cap, AppointmentDocumentsAppService.cs:44 + 692-714) and `EnsureValidFileFormat`
  (extension + magic-byte PDF/JPG/PNG, 716-747), both called from `UploadStreamAsync`
  (173, 183). Reusing the existing endpoint reuses these for free.
- Confirmed client/server cap mismatch: `appointment-documents.component.ts:114` declares
  `maxBytes = 25 * 1024 * 1024` (25 MB, verified) while the backend app cap is 10 MB and the
  Kestrel/multipart framework cap is 12 MB (CaseEvaluationHttpApiHostModule.cs:296-305). A
  large file 413s at the framework before the friendly 10 MB message can fire. Fix this as
  part of this work.
- The booking submit already uses a create-then-POST-children pattern with NO transactional
  rollback: `onSubmit` POSTs `/api/app/appointments`, then sequentially awaits
  createEmployerDetailsIfProvided / upsertApplicantAttorney / upsertDefenseAttorney /
  persistInjuryDrafts / createAppointmentAccessors against the returned `createdAppointment.id`
  (appointment-add.component.ts:1158-1173), then navigates to '/' (1175). A deferred document
  upload inherits this exact partial-failure shape.
- The HTTP surface for ad-hoc uploads already fits unchanged:
  `POST /api/app/appointments/{appointmentId}/documents`
  (AppointmentDocumentController.cs:32-50) -> `UploadStreamAsync`, which sets `IsAdHoc=true`
  and `Status` based on internal-vs-external actor (AppointmentDocumentsAppService.cs:192-211).
  The Angular doc component already calls this via raw `RestService` FormData
  (appointment-documents.component.ts:159-189) because the generated multipart proxy wrapper
  does not produce valid browser FormData.
- The existing upload component cannot be reused verbatim: it self-POSTs immediately and
  refuses to act without an `appointmentId` (appointment-documents.component.ts:128, 152). The
  booker needs a form-integrated picker that DEFERS the POST until after create.

## Relevant code locations

Frontend (the work is here):
- `angular/src/app/appointments/appointment-add.component.ts` -- onSubmit (1158-1175), new file
  staging state, post-create upload loop, on-page progress + retry, conditional PQME validator.
- `angular/src/app/appointments/sections/` -- the section template that renders the upload
  control + the AF6 PQME checkbox, sited as a sibling block between Claim Information and
  Authorized Users (Authorized Users is hidden for Claim Examiner bookers, so the new content
  must be its own sibling block, not nested in that conditional).
- `angular/src/app/appointment-documents/appointment-documents.component.ts:114` -- 25 MB ->
  10 MB cap fix (centralize the 10 MB constant so both the pre-submit picker and the
  appointment-view uploader agree).

Backend (untouched by Option A; cited only as the consumed contract):
- `src/...HttpApi/Controllers/AppointmentDocuments/AppointmentDocumentController.cs:32-50`
- `src/...Application/AppointmentDocuments/AppointmentDocumentsAppService.cs:173,183,692-747`
- `src/...Application/Appointments/AppointmentReadAccessGuard.cs:152`

## Phase 3 cross-reference

- BUG-025 (two-layer upload size cap): the 25 MB client vs 10 MB server vs 12 MB framework
  mismatch (appointment-documents.component.ts:114) must be fixed in this work or the pre-submit
  picker regresses BUG-025; reuse `EnsureFileSizeWithinLimit` / `EnsureValidFileFormat`
  (AppointmentDocumentsAppService.cs:692-747) by routing through the existing endpoint.
- AF5 (panel strike list as first-class doc) + AF6 (PQME checkbox makes strike-list mandatory):
  AF6 has a HARD dependency on AF7 -- bundle them. AF6's mandatory gate is a client-side
  conditional reactive validator that blocks submit until a strike-list file is staged.
  Per the locked Documents decision, AF5 adds a new boolean `IsPanelStrikeList` flag on
  `AppointmentDocument` (mirrors `IsAdHoc` / `IsJointDeclaration` at AppointmentDocument.cs:65-78).
- BUG-037 (staff 403 on upload): verify internal-staff bookers reach the same upload path
  without the 403 while touching this code; one-line confirm, not a re-investigation.
- OBS-20 (Playwright file-upload driver limit): manual/automated test plans for the staging
  picker must use the `browser_file_upload` MCP tool, not the raw Playwright driver.

## Research findings

Internal patterns / prior art:
- The create-then-POST-children pattern in `onSubmit` (appointment-add.component.ts:1158-1173)
  is the direct precedent: documents become one more child POST against the returned
  `createdAppointment.id`. This makes Option A consistent, not novel.
- Per-type required-field behavior is already config-driven via
  `applyFieldConfigsForAppointmentType` (appointment-add.component.ts:750-792), but the PQME
  mandatory-strike-list gate is a NEW checkbox-driven conditional-required rule -- a
  reactive-forms validator concern, NOT an `AppointmentTypeFieldConfig` row.
- PQME type detection: per the locked Panel Number decision, key off the PQME type IDENTITY
  (seed GUID), NOT a name substring. Note the codebase's substring-match gotcha
  (`GenerateAppointmentPacketJob.cs:118-119` warns a naive `Contains("PQME")` silently missed
  "Panel QME") -- the GUID approach sidesteps it entirely.
- The `AnonymousUploadsContainer` "id-less staging precedent" is NOT a true pre-create
  precedent: `UploadByVerificationCodeAsync` (AppointmentDocumentsAppService.cs:376-404)
  OVERWRITES an already-existing row created by `AppointmentDocument.CreateQueued` after the
  parent appointment exists; the container's own docstring marks anonymous upload DEFERRED
  (AnonymousUploadsContainer.cs:5-14). It offers Option B less reuse than it appears.

External docs: none required -- this is reuse of an existing ABP AppService + Angular reactive
form; no new ABP/Angular/EF Core pattern is introduced.

## Approaches considered (with tradeoffs)

- Option A -- two-phase create-then-upload (CHOSEN). Stage files in browser memory during form
  fill; after `POST /api/app/appointments` returns its id, POST each staged file to the
  EXISTING `POST /api/app/appointments/{id}/documents`, in the same try block as the other
  child POSTs. Pros: zero backend schema/endpoint change; reuses BUG-025 size+format validation
  automatically; booker passes the read-access gate as Creator; matches the existing
  child-POST pattern. Cons: partial-failure window (appointment created, upload fails) -- but
  identical in shape to the already-accepted child-entity behavior; files held in browser until
  submit; needs the 10 MB client-cap fix. Effort S, risk Low.
- Option B -- stage-then-attach (REJECTED for now). New pre-create staging endpoint + temp
  container + `StagedDocumentIds` on `AppointmentCreateDto` + attach-in-UoW + cleanup job.
  Pro: truly atomic. Cons: largest new surface; must re-implement BUG-025 validation on the
  new endpoint or bypass it; the anonymous-upload "precedent" does not actually pre-stage;
  tenant-context/blob-lifecycle correctness risk. Effort L, risk Med. Reserve only if product
  later mandates that every created appointment ALWAYS already holds its documents in one
  transaction.
- Option C -- nullable `AppointmentId` / orphan-then-link (REJECTED). Make the FK nullable, let
  rows exist unlinked, then UPDATE. Cons: weakens a core invariant on a PHI table; every
  per-appointment query/gate/blob-path assumes a non-null id (AppointmentDocumentManager.cs:33-37,
  AppointmentDocument.cs:31); orphan rows are exactly the PHI cross-tenant hazard the codebase
  guards against; SQL migration on a PHI table plus orphan cleanup, for no benefit A or B does
  not already provide more safely. Effort L, risk High.

Why A wins: it ships the feature with the smallest, lowest-risk change and zero backend work,
reusing proven validation and authorization. The only new failure mode (create-ok /
upload-fails) already exists and is accepted for sibling child entities, and is contained by a
retry rather than a rollback.

## Decision (locked 2026-06-03)

Option A, two-phase create-then-upload, for ALL booking-form docs (not just the strike list):

- Stage selected files in browser memory during form fill in the new section between Claim
  Information and Additional Authorized User.
- In `onSubmit`, after the appointment POST returns its id, upload each staged file to the
  EXISTING `POST /api/app/appointments/{id}/documents` (UploadStreamAsync). Reuses BUG-025
  size (10 MB) + magic-byte format validation; the booker passes the read-access gate as
  Creator -- no backend authorization or schema change.
- Keep the booker on the page (do NOT navigate to '/') until uploads resolve. On upload
  failure, surface a retry that re-POSTs to the now-existing appointment id (the Pending
  appointment REMAINS; do not re-create it).
- Fix the 25 MB (client) -> 10 MB (server) cap mismatch so the friendly 10 MB message fires
  before the 12 MB framework 413.
- The PQME panel-strike-list mandatory gate (AF6) is a CLIENT-SIDE conditional reactive
  validator enforced BEFORE create: when the strike-list checkbox is ticked, block submit
  until a strike-list file is staged. This prevents a half-saved PQME appointment entirely.
- Reject Option B (stage-then-attach) and Option C (nullable FK) for now; reasons recorded
  above.

## Implementation outline (no code)

1. Section/template (Angular): add a new sibling block between Claim Information and Authorized
   Users in `angular/src/app/appointments/sections/`, rendering the AF6 PQME checkbox (when the
   selected type is the PQME seed GUID) and a multi-file picker. Section stays template-only;
   all form state lives in `AppointmentAddComponent` (per the FormGroup-lives-here-only rule).
2. Staging state (appointment-add.component.ts): hold selected `File[]` in component state;
   validate each against the centralized 10 MB cap + allowed extensions client-side at pick
   time (fix the 25 MB constant at appointment-documents.component.ts:114; share one constant).
3. Conditional PQME validator (appointment-add.component.ts): when type == PQME seed GUID AND
   the strike-list checkbox is ticked, block submit until a strike-list file is staged; clear
   the checkbox/staged strike-list flag if the user switches type away from PQME.
4. Post-create upload loop (onSubmit, appointment-add.component.ts:1158-1175): after the
   existing child POSTs, sequentially POST each staged file to
   `/api/app/appointments/{createdAppointment.id}/documents` via the raw `RestService` FormData
   approach already proven in appointment-documents.component.ts:159-189. Show per-file
   progress. Only navigate to '/' after all uploads resolve.
5. Retry on failure: on a failed upload, keep the Pending appointment, surface the error, and
   offer a retry that re-POSTs to the existing id (no re-create). No rollback (matches the
   existing non-atomic child-POST behavior).
6. Enforcement split: file size/format = SERVER-enforced (existing UploadStreamAsync 692-747)
   AND mirrored client-side (the 10 MB picker check). The PQME strike-list-mandatory rule is a
   pure pre-submit affordance, so client-side only is acceptable for AF6's submit gate;
   server-side defense-in-depth for PQME-requires-strike-list is OUT of scope here (it belongs
   with AF5/AF6's `IsPanelStrikeList` flag work if product wants it later).

No backend migration. No proxy regen for Option A (the existing controller route and DTOs are
unchanged; uploads use raw FormData, not the generated multipart proxy). The `IsPanelStrikeList`
flag (AF5) is a separate migration + proxy regen tracked under AF5/AF6, not AF7.

## Dependencies

- BLOCKS AF6 (PQME mandatory-upload gate) -- AF6 cannot function without this pre-submit
  upload mechanism; ship AF7 + AF6 together.
- Bundles with AF5 (panel strike list flag) for the same section/template work.
- Must fix BUG-025's 25 MB/10 MB client mismatch as part of this work.
- Depends on the PQME seed-GUID identity (from the Appointment Types / Panel Number locked
  decisions) for type detection.

## Residual open questions

- Whether AF5's `IsPanelStrikeList` flag should be set client-side at pre-submit upload time
  (so the strike-list file is tagged on creation) or applied by staff later -- resolve when
  AF5/AF6 are designed; AF7 only needs to upload files, not tag them.
