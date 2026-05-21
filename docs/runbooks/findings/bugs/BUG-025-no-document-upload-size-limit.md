---
id: BUG-025
title: AppointmentDocuments upload has no application-level file-size limit
severity: medium
status: fixed
fixed: 2026-05-21
last-replayed: 2026-05-21
fixed-on: fix/document-upload-size-limit
found: 2026-05-14 hardening R2 (Phase 7 failure mode)
flow: document-upload-limits
component: src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs + HttpApi.Host Kestrel config
---

> **Fixed 2026-05-21 on branch `fix/document-upload-size-limit`.**
>
> Two-layer cap:
>
> 1. **AppService layer** (10 MB) -- `AppointmentDocumentsAppService.MaxFileSizeBytes = 10 * 1024 * 1024` enforced via private static `EnsureFileSizeWithinLimit(long fileSize)` at the 3 upload entry points (UploadStreamAsync line 168, UploadJointDeclarationAsync line 306, OverwriteUploadedFileAsync line 424). Throws `BusinessException(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileTooLarge).WithData("MaxBytes", ...).WithData("ActualBytes", ...)`. Mapped to HTTP 413 Payload Too Large via `AbpExceptionHttpStatusCodeOptions` in `CaseEvaluationHttpApiHostModule.cs`.
>
> 2. **Framework layer** (12 MB) -- new `ConfigureUploadLimits` helper in `CaseEvaluationHttpApiHostModule.cs` sets `Kestrel.Limits.MaxRequestBodySize = 12 * 1024 * 1024` AND `FormOptions.MultipartBodyLengthLimit = 12 * 1024 * 1024`. The 2 MB buffer above the AppService cap is intentional: it lets the localized 413 from the AppService fire for files between 10-12 MB (with friendly `MaxBytes`/`ActualBytes` data) while still rejecting anything above 12 MB at the framework layer before the request body is fully buffered.
>
> Pre-existing tech debt cleanup also done: the 3 hardcoded `throw new UserFriendlyException("File is empty.")` strings (lines 152, 289, 406) were replaced with `BusinessException(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty)`. (The controller layer at `AppointmentDocumentController.cs:38-40` still has a pre-check that intercepts empty multipart submissions with a 403 "File is required." -- that behavior is unchanged; the new AppService code is a backstop for non-controller call paths.)
>
> **Live-verified 2026-05-21** against `main-api-1` rebuilt from the fix branch:
>
> | Test | Status | Body |
> |---|---|---|
> | 9 MB upload | 200 | FileSize=9437184, Status=Uploaded, DocumentName="BUG-025 test 9MB" |
> | 11 MB upload | 413 | code=`CaseEvaluation:AppointmentDocument.FileTooLarge`, data={MaxBytes:10485760, ActualBytes:11534336} |
> | 0-byte upload | 403 | "File is required." (controller pre-check, pre-existing) |
> | 50 MB upload | 400 | "Failed to read the request form. Request body too large. The max request body size is 12582912 bytes." (Kestrel framework cap; ABP wraps as ValidationException -> 400, transmitted ~9.6 MB before closing connection) |
>
> **Build + tests**: solution-wide `dotnet build` clean. `dotnet test` against the full slnx: 813 tests, 0 failures, 19 skipped (Domain.Tests + Application.Tests + EntityFrameworkCore.Tests).
>
> **Extension allowlist branch closed**: `EnsureValidFileFormat` at `AppointmentDocumentsAppService.cs:683` (renumbered after T1+T2 inserts) already enforces `.pdf, .jpg, .jpeg, .png` + magic-byte validation. No separate BUG-025b needed.
>
> Files touched (5):
> - `src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs` (const + helper + 3 call sites + 3 string replacements)
> - `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs` (2 new constants)
> - `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` (2 new HTTP-status mappings + new ConfigureUploadLimits helper)
> - `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` (2 new localization keys)
> - This finding doc.
>
> **Deferred** (per Adrian 2026-05-21):
> - Per-appointment aggregate quota (e.g., 50 MB total per appointment). Adrian's TODO; needs aggregation query + UX for "X MB remaining".
> - Renaming or consolidating the controller's pre-check `"File is required."` UserFriendlyException with the AppService's `AppointmentDocumentFileEmpty` BusinessException (would need a separate small PR; the empty-file path works as-is via the controller).

# BUG-025 - No per-document upload size limit enforced

## Symptom
Code review for R2 Phase 7 (upload limits):
- `AppointmentDocumentsAppService.UploadAsync` accepts `long fileSize` and the only size check is `fileSize <= 0`. There is no upper bound.
- `CaseEvaluationHttpApiHostModule.cs` does not configure `MaxRequestBodySize`, `MultipartBodyLengthLimit`, or `FormOptions`. So the only effective limit is the ASP.NET / Kestrel default: ~30 MB.
- Comparison: `UserSignatureAppService.cs:35` explicitly defines `MaxFileSizeBytes = 1024 * 1024` (1 MB) for user signatures. The same discipline should apply to ad-hoc appointment documents.

## Impact
- A user can attach 25+ MB junk PDFs to one appointment. MinIO storage grows unchecked.
- Patient packet generation (Phase 6) reads documents into Gotenberg memory; a huge attached doc could OOM the Gotenberg container.
- Tenant isolation: a single tenant could exhaust shared MinIO disk by uploading max-size files in a loop. No quota.

## Hypothesis (single)
The team hasn't decided on a per-document or per-appointment quota yet, so the limit was left at framework default. SEED-2 follow-up territory.

## Recommended fix
1. Add `MaxFileSizeBytes` constant in `AppointmentDocumentsAppService.cs` (e.g., 10 MB for ad-hoc docs).
2. In every `Upload*` method, reject with `BusinessException("CaseEvaluation:AppointmentDocument.FileTooLarge")` when `fileSize > MaxFileSizeBytes`. Include `data.MaxBytes` for the UI to render a friendly message.
3. Add the error code to `AbpExceptionHttpStatusCodeOptions` map (HTTP 413 Payload Too Large or 400, pick one and document).
4. (Defense in depth) Configure `Kestrel.Limits.MaxRequestBodySize` and `FormOptions.MultipartBodyLengthLimit` to the same value + small overhead in `CaseEvaluationHttpApiHostModule.cs`.
5. (Optional) Add per-appointment aggregate quota (e.g., 50 MB total) - cap the abuse vector even if individual files are within limit.

## Verified during this R2 run
- The AppService code path reads `fileSize` from the input DTO without validating against an upper bound.
- `wc -l` of the file (or `Grep` of the codebase) finds no other size constant for AppointmentDocuments.

## Allowed file extensions
Separate check (not done this session): is there a content-type allowlist? Patient/AA/Staff should be able to upload PDF, DOCX, PNG, JPG. The current code accepts arbitrary `contentType` from the input. **TODO**: confirm whether an allowlist exists or file BUG-025b.

## Related
- [[OBS-20]] - Playwright driver couldn't exercise this through the UI; finding lives at the code-review level.
- [[BUG-024]] - sibling validation gap in same family (server-side input checks missing where UI assumed sufficient).
