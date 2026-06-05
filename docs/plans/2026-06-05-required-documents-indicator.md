---
feature: required-documents-indicator
date: 2026-06-05
status: in-progress
base-branch: feat/replicate-old-app
related-issues: []
---

## Goal

On an appointment, show which *required* documents are still outstanding (with their
current state) and let users filter the document list by type -- PR3 of the
document-type master initiative (`docs/plans/2026-06-03-document-type-master.md`).

## Context

PR1 (#285) shipped the document-category master; PR2 (#287) shipped the upload picker
and stamped `AppointmentDocument.SourceDocumentId = master Document.Id` on queued package
rows. PR3 consumes that linkage. The legacy app had the required-docs *data* (PackageDetail
/ DocumentPackage) and backend incomplete-doc email reminders, but no UI indicator and no
type filter -- so this surfaces an existing OLD concept rather than inventing one (plan-
sanctioned enhancement).

Confirmed data path: `Appointment.AppointmentTypeId` -> active `PackageDetail`
(`IsActive && AppointmentTypeId==`) -> `DocumentPackage` (M2M, `IsActive`) -> master
`Document`s = the required set. A requirement is satisfied only when an `AppointmentDocument`
for the appointment has `SourceDocumentId == Document.Id` AND `Status == Accepted`
(`DocumentStatus`: Uploaded=1, Accepted=2, Rejected=3, Pending=4). No endpoint computes this
today (`GetCombinedForAppointmentAsync` returns everything, unfiltered).

## Approach

- **Backend read, no writes/schema/migration.** A new `GetMissingRequiredDocumentsAsync`
  read on the existing `IAppointmentDocumentsAppService`, gated by `AppointmentDocuments.Default`
  + the per-appointment `AppointmentReadAccessGuard` (same trust model as
  `GetCombinedForAppointmentAsync` / `GetDocumentTypeOptionsAsync`) so external uploaders see
  their own outstanding docs. Least-privilege: read-only, scoped to readable appointments.
- **Extract the matching/state logic as a pure, deterministic evaluator** in the Domain layer
  and TDD it (status precedence, multi-row, union, empty). The AppService only fetches inputs
  (active-package required Documents + the appointment's docs) and calls the evaluator.
- **Filter is purely client-side** over the already-loaded `documents[]` in
  `AppointmentDocumentsComponent` -- no server round-trip (the list is already loaded).
- **Indicator loads the new read** via `RestService` directly (the ABP proxy mis-types
  `List<>` returns as a single object -- same workaround PR2 uses for `document-type-options`).
- Rejected alternative: compute "missing" from the queued `AppointmentDocument` rows
  (the submission-time snapshot). Rejected because the *active PackageDetail* is the current
  source of truth -- if the package changed after submission, the snapshot is stale; the
  package-based set stays correct.
- Rejected alternative: refactor `PackageDocumentQueueHandler` to share a single
  "required docs for a type" resolver. Deferred -- it touches shipped PR2 code and grows blast
  radius; the duplicated query is small and flagged below as debt (propose separately).

## Tasks

- T1: Required-document evaluator (pure domain logic) + state enum.
  - approach: tdd
  - files-touched:
    - `src/HealthcareSupport.CaseEvaluation.Domain.Shared/AppointmentDocuments/RequiredDocumentState.cs` (new enum: NotUploaded, AwaitingReview, Rejected)
    - `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/RequiredDocumentEvaluator.cs` (new; pure static/stateless: given required (Id,Name) + appointment docs (SourceDocumentId,Status) -> ordered list of missing {DocumentId, Name, State})
    - `test/HealthcareSupport.CaseEvaluation.Domain.Tests/AppointmentDocuments/RequiredDocumentEvaluatorTests.cs` (new; plain non-abstract xUnit class)
  - acceptance: unit tests pass for -- satisfied only when an Accepted row matches by SourceDocumentId; precedence when multiple rows for one required doc (Accepted > Uploaded=AwaitingReview > Rejected > Pending/none=NotUploaded); union across many required docs; empty required set -> empty result; a required doc with no AppointmentDocument row -> NotUploaded.

- T2: Application read -- DTO + AppService method.
  - approach: code (logic covered by T1; data-fetch live-verified; fetch query mirrors the proven PackageDocumentQueueHandler)
  - files-touched:
    - `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentDocuments/MissingRequiredDocumentDto.cs` (new: Guid DocumentId (internal), string Name, RequiredDocumentState State)
    - `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentDocuments/MissingRequiredDocumentsResultDto.cs` (new wrapper: int RequiredCount + List<MissingRequiredDocumentDto> Missing. REFINEMENT found during T5: the UI must distinguish "no required package" (RequiredCount 0 -> render nothing) from "all received" (Missing empty -> green banner) per decision 3, which a bare missing-list cannot convey.)
    - `src/HealthcareSupport.CaseEvaluation.Application.Contracts/AppointmentDocuments/IAppointmentDocumentsAppService.cs` (add `Task<MissingRequiredDocumentsResultDto> GetMissingRequiredDocumentsAsync(Guid appointmentId)`)
    - `src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs` (impl: resolve appointment + AppointmentTypeId; query active PackageDetail(s) for the type -> active DocumentPackage -> active Document (Id+Name), unioned; load the appointment's AppointmentDocuments (SourceDocumentId,Status); call RequiredDocumentEvaluator; map to DTO. `[Authorize(AppointmentDocuments.Default)]` + `_readAccessGuard.EnsureCanReadAsync(appointment)`. Inject a PackageDetail repository + a Document name source; reuse the existing appointment + appointment-document repositories.)
  - acceptance: backend builds clean; method returns the union of active-package required docs minus Accepted, each with its state; an appointment whose type has no active package returns empty.

- T3: Controller route + Angular proxy regen.
  - approach: code (controller is a one-line delegation; proxy is generated)
  - files-touched:
    - `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/AppointmentDocuments/AppointmentDocumentController.cs` (add `[HttpGet("missing-required")]` delegating to the AppService)
    - `angular/src/app/proxy/**` (regenerate via `abp generate-proxy`; stage only real-change files -- `git diff --ignore-cr-at-eol` then `git restore` the EOL-only churn)
  - acceptance: build green; the endpoint responds at `GET /api/app/appointments/{id}/documents/missing-required`; proxy compiles.

- T4: Angular type filter (client-side).
  - approach: code (UI; live-verified)
  - files-touched:
    - `angular/src/app/appointment-documents/appointment-documents.component.ts` (add `selectedFilterTypeId`, a `filteredDocuments` getter/computed grouping by type incl. an "Other / Uncategorized" bucket for `otherDocumentTypeName`/untyped)
    - `angular/src/app/appointment-documents/appointment-documents.component.html` (a `<select>` above the list: "All types" + each type present + "Other / Uncategorized"; render `filteredDocuments`)
  - acceptance: selecting a type narrows the list; "Other / Uncategorized" shows free-text-labelled + untyped docs; "All types" shows everything.

- T5: Angular missing-required indicator.
  - approach: code (UI; live-verified)
  - files-touched:
    - `angular/src/app/appointment-documents/appointment-documents.component.ts` (load missing-required on `appointmentId` change + on `documentsChanged`/after approve-reject-upload, via RestService; field `missingRequired: MissingRequiredDocumentDto[]`)
    - `angular/src/app/appointment-documents/appointment-documents.component.html` (alert block at the top of the card body: list outstanding docs with a state label per item; green "All required documents received" when required>0 and none missing; render nothing when there is no required package i.e. the read returns empty AND the appointment had a package -- see verification note)
  - acceptance: the indicator lists outstanding required docs with their state; turns into the positive banner once all are Accepted; refreshes after upload/approve/reject.

- T6: Docs in the same change.
  - approach: code
  - files-touched:
    - `src/HealthcareSupport.CaseEvaluation.Domain/AppointmentDocuments/CLAUDE.md` (or the AppointmentDocuments feature doc) -- record the missing-required read + that requirement satisfaction = SourceDocumentId match + Accepted; note the resolution-query duplication with PackageDocumentQueueHandler as flagged debt
    - `docs/plans/2026-06-03-document-type-master.md` (mark PR3 done in the slicing section)
  - acceptance: docs reflect the shipped behavior; no stale "later slice" language.

## Risk / Rollback

- Blast radius: one new read endpoint (no writes, no schema, no migration) + one Angular
  component's UI. The new AppService query mirrors the proven queue-handler query.
- Rollback: revert the PR; nothing persisted, no migration to undo.
- Correctness risk concentrated in the missing-set logic -> mitigated by T1 unit tests.
- No PHI surface change: the read returns document *category labels* + state, not patient data.

## Verification

1. `dotnet test` -- T1 `RequiredDocumentEvaluator` unit tests pass (show output).
2. Backend build (HttpApi.Host) + Angular build (in-container) green.
3. Live on the Docker dev stack (offset ports; SPA 4230):
   - Prereq: an appointment whose AppointmentType has an active `PackageDetail` with >=2
     linked active `Document`s. If absent, create one via the master Document + PackageDetails
     admin (synthetic data) -- record what was seeded.
   - Indicator lists the outstanding required docs with correct state (Not uploaded / Awaiting
     review / Rejected); upload + approve one and confirm it drops off; when all are Accepted
     the green positive banner shows.
   - Type filter: each option narrows the list; "Other / Uncategorized" bucket behaves.
4. Self-review (code-simplifier + code-reviewer), then open the PR into `feat/replicate-old-app`
   with the picker/indicator screenshot, and STOP. Expect the SonarCloud Quality Gate to be the
   only failing check (accepted exception).
