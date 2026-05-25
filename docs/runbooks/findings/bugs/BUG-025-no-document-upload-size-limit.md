---
id: BUG-025
title: AppointmentDocuments upload has no application-level file-size limit
severity: medium
status: fixed
fixed: 2026-05-21
last-replayed: 2026-05-23 INITIALLY-misdiagnosed-as-regressed; ACTUALLY-working-as-designed -- the 15 MB test file exceeds both the AppService 10-MB cap AND the framework's 12-MB cap (CaseEvaluationHttpApiHostModule.ConfigureUploadLimits, line 271). Per the in-code documentation lines 252-267, uploads above 12 MB receive a framework-level 413 / ERR_CONNECTION_RESET without the localized AppService error. To verify the friendly AppService 413 path, upload a file between 10 MB and 12 MB. The 15-MB ERR_CONNECTION_RESET is the documented framework behavior.
fixed-on: fix/document-upload-size-limit
found: 2026-05-14 hardening R2 (Phase 7 failure mode)
flow: document-upload-limits
component: src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs + HttpApi.Host Kestrel config
---

> **Fixed 2026-05-21 on branch `fix/document-upload-size-limit`.**
>
> Two-layer cap:
>
> 1. **AppService layer** (10 MB) -- `AppointmentDocumentsAppService.MaxFileSizeBytes = 10 * 1024 * 1024` enforced via `public static EnsureFileSizeWithinLimit(long fileSize, IStringLocalizer<CaseEvaluationResource>? localizer = null)` at the 3 call sites in `AppointmentDocumentsAppService.cs` (UploadStreamAsync, UploadJointDeclarationAsync, OverwriteUploadedFileAsync). Throws `UserFriendlyException(localizedMessage, code = CaseEvaluationDomainErrorCodes.AppointmentDocumentFileTooLarge)` with `.WithData("MaxBytes", ...)` + `.WithData("ActualBytes", ...)`. Mapped to HTTP 413 Payload Too Large via `AbpExceptionHttpStatusCodeOptions` in `CaseEvaluationHttpApiHostModule.cs`. UserFriendlyException (not BusinessException) is used because ABP only forwards UserFriendlyException messages to clients by default; BusinessException messages get replaced with the generic "An internal error occurred" fallback. Localized message comes from `AppointmentDocument:FileTooLarge` in `en.json`.
>
> 2. **Framework layer** (12 MB) -- new `ConfigureUploadLimits` helper in `CaseEvaluationHttpApiHostModule.cs` sets `Kestrel.Limits.MaxRequestBodySize = 12 * 1024 * 1024` AND `FormOptions.MultipartBodyLengthLimit = 12 * 1024 * 1024`. The 2 MB buffer above the AppService cap is intentional: it lets the localized 413 from the AppService fire for files between 10-12 MB (with friendly `MaxBytes`/`ActualBytes` data) while still rejecting anything above 12 MB at the framework layer before the request body is fully buffered.
>
> Pre-existing tech debt cleanup also done: the 3 hardcoded `throw new UserFriendlyException("File is empty.")` strings (lines 152, 289, 406) were replaced with `BusinessException(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty)`. (The controller layer at `AppointmentDocumentController.cs:38-40` still has a pre-check that intercepts empty multipart submissions with a 403 "File is required." -- that behavior is unchanged; the new AppService code is a backstop for non-controller call paths.)
>
> **Live-verified 2026-05-21** against `main-api-1` rebuilt from the fix branch:
>
> | Test | Status | Body |
> |---|---|---|
> | 9 MB upload (SPA UploadStream) | 200 | FileSize=9437184, Status=Uploaded |
> | 11 MB upload (SPA UploadStream) | 413 | code=`CaseEvaluation:AppointmentDocument.FileTooLarge`, message="File is too large. Maximum allowed size is 10 MB.", data={MaxBytes:10485760, ActualBytes:11534336} |
> | 11 MB upload (HTTP UploadStream) | 413 | same body as above |
> | 11 MB upload (HTTP UploadJointDeclaration) | 413 | same body as above (size gate fires before AME / attorney gates) |
> | 0-byte upload | 403 | "File is required." (controller pre-check, pre-existing) |
> | 50 MB upload | 400 | "Failed to read the request form. Request body too large. The max request body size is 12582912 bytes." (Kestrel framework cap; ABP wraps as ValidationException -> 400) |
>
> **SPA UX confirmed**: the LeptonX `abp-confirmation` modal renders `"An error has occurred! / File is too large. Maximum allowed size is 10 MB. / Close"` (screenshot at `.playwright-mcp/bug-025-localized-modal.png`).
>
> **Build + tests**: solution-wide `dotnet build` clean. `dotnet test` against the full slnx: 801 tests, 0 failures, 19 skipped (Domain.Tests 13 + Application.Tests 532 + EntityFrameworkCore.Tests 256), including 7 new unit tests at `test/.../AppointmentDocuments/AppointmentDocumentSizeLimitTests.cs` covering: exactly-at-limit (does not throw), one-byte-under (does not throw), one-byte-over (throws with MaxBytes + ActualBytes data), 11 MB (throws), 50 MB (throws), zero bytes (does not throw -- empties handled by separate empty-file gate), and MaxFileSizeBytes constant invariant.
>
> **Extension allowlist branch closed**: `EnsureValidFileFormat` in `AppointmentDocumentsAppService.cs` already enforces `.pdf, .jpg, .jpeg, .png` + magic-byte validation. No separate BUG-025b needed.
>
> Files touched (6):
> - `src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs` (const + helper + 3 call sites + 3 string replacements + DI'd IStringLocalizer<CaseEvaluationResource>)
> - `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs` (2 new constants)
> - `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` (2 new HTTP-status mappings + new ConfigureUploadLimits helper)
> - `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` (2 new localization keys)
> - `test/HealthcareSupport.CaseEvaluation.Application.Tests/AppointmentDocuments/AppointmentDocumentSizeLimitTests.cs` (new, 7 tests)
> - This finding doc.
>
> **Deferred** (per Adrian 2026-05-21):
> - Per-appointment aggregate quota (e.g., 50 MB total per appointment). Adrian's TODO; needs aggregation query + UX for "X MB remaining".
> - Renaming or consolidating the controller's pre-check `"File is required."` UserFriendlyException with the AppService's `AppointmentDocumentFileEmpty` BusinessException (would need a separate small PR; the empty-file path works as-is via the controller).
> - JointDeclaration + UploadPackage + UploadByCode SPA UX flows were not exercised end-to-end via the SPA because they are role-gated (attorney for JDF; package-doc UI gates the others). Their wiring is verified by (a) the HTTP-level 11 MB test against the JointDeclaration endpoint above and (b) the helper unit tests, since all four entry points route to the same `EnsureFileSizeWithinLimit` helper.

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
