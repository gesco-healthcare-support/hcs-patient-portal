---
feature: document-upload-size-limit
date: 2026-05-21
status: in-progress
base-branch: main
working-branch: fix/document-upload-size-limit
related-issues: []
closes: [BUG-025]
spec: docs/runbooks/findings/bugs/BUG-025-no-document-upload-size-limit.md
worktree: W:\patient-portal\main
---

## Goal

Enforce a 10 MB per-document upload cap on `AppointmentDocumentsAppService` with defense-in-depth Kestrel/FormOptions configuration, returning HTTP 413 with a localized `BusinessException` carrying `data.MaxBytes` + `data.ActualBytes` so the SPA can render a friendly "file too large" message.

## Context

BUG-025 (filed 2026-05-14, re-confirmed 2026-05-21) -- only `fileSize <= 0` checks at 3 call sites. No upper bound. Effective limit today is ASP.NET / Kestrel default (~30 MB). Risk: a single upload can OOM Gotenberg during packet generation; storage quota is not bounded.

Existing patterns to mirror:
- `UserSignatureAppService.cs:35` -- `const long MaxFileSizeBytes = 1024 * 1024;` (1 MB image cap). Same shape; just bigger value.
- `CaseEvaluationDomainErrorCodes.cs` -- consistent project standard: `BusinessException` with code constants, explicit HTTP-status map in `CaseEvaluationHttpApiHostModule.cs:151-192`.
- `EnsureValidFileFormat` (line 658) -- existing private helper using `UserFriendlyException`. New `EnsureFileSizeWithinLimit` sits beside it but uses `BusinessException` per Adrian's directive (machine-parseable code + data).

Extension allowlist concern from the BUG-025 doc (PDF/JPG/PNG only) is **already addressed** by `EnsureValidFileFormat` -- T7 documents this so no separate finding is filed.

## Approach

Chosen: per-AppService validation helper + ABP-standard `BusinessException` + explicit 413 mapping + Kestrel/FormOptions defense-in-depth at 12 MB (2 MB buffer over the 10 MB AppService cap so multipart headers + small overhead don't trip framework-level 413 before the localized message fires).

Alternatives rejected:
- **`UserFriendlyException(L["..."])` (UserSignature precedent)**: simpler but loses machine-parseable error code + data. Codebase mainstream is BusinessException-with-code (see all of `CaseEvaluationDomainErrorCodes.cs`); UserSignature is older tech debt.
- **HTTP 400 instead of 413**: 400 is the codebase convention for business-rule violations. But 413 is RFC-canonical for size-exceeded and BUG-025's recommendation. The error-code map already mixes status codes; one 413 isn't disruptive.
- **Inline at 3 sites (no helper)**: mirrors existing inline `fileSize <= 0` shape, but duplicates the limit value 3x. Adrian picked DRY.
- **Bigger limit (25 / 50 MB)**: more permissive, fewer rejections, but expands the OOM/storage attack surface. Adrian picked 10 MB.
- **Per-appointment aggregate quota**: deferred per Adrian (requires aggregation query + UX for "you have X MB left").

## Tasks

- T1: Add `MaxFileSizeBytes` constant + `EnsureFileSizeWithinLimit` private static helper in `AppointmentDocumentsAppService.cs`
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs]
  - acceptance: `public const long MaxFileSizeBytes = 10 * 1024 * 1024;` defined at class scope (near the top, mirroring UserSignature pattern). Private static `EnsureFileSizeWithinLimit(long fileSize, IStringLocalizer<CaseEvaluationResource> L)` (or equivalent injection-free shape consistent with the file's other helpers) throws `BusinessException(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileTooLarge).WithData("MaxBytes", MaxFileSizeBytes).WithData("ActualBytes", fileSize)` when `fileSize > MaxFileSizeBytes`. `dotnet build` succeeds.

- T2: Wire helper at 3 call sites + collapse "empty file" string into a localized exception
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs]
  - acceptance: Each of the 3 `if (content == null || fileSize <= 0) { throw new UserFriendlyException("File is empty."); }` blocks (around lines 152, 289, 406) is replaced with `EnsureFileSizeWithinLimit(fileSize)` for the upper bound, AND the lower-bound `fileSize <= 0` throw is updated to use `BusinessException(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty)` (cleanup of pre-existing hardcoded string). `dotnet build` succeeds. Code compiles + the 3 sites no longer reference the magic string `"File is empty."`.

- T3: Add error-code constants
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs]
  - acceptance: Two new constants added with XML doc comments matching the file's existing style:
    - `AppointmentDocumentFileTooLarge = "CaseEvaluation:AppointmentDocument.FileTooLarge"`
    - `AppointmentDocumentFileEmpty = "CaseEvaluation:AppointmentDocument.FileEmpty"`
    - Both documented with the localization key + intended HTTP-status mapping in the doc comment (pattern matches the existing `AppointmentBookingDatePastMaxHorizon` block).

- T4: Add HTTP-status mappings (413 for too-large, 400 for empty)
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs]
  - acceptance: Two `options.Map(...)` lines added inside the existing `Configure<AbpExceptionHttpStatusCodeOptions>` block (around line 151):
    - `options.Map(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileTooLarge, System.Net.HttpStatusCode.RequestEntityTooLarge);` (= HTTP 413)
    - `options.Map(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty, System.Net.HttpStatusCode.BadRequest);` (= HTTP 400)
    - Each preceded by a single-line comment noting "BUG-025 follow-up (2026-05-21)" per the file's existing comment convention (line 145, 185, 194 etc).

- T5: Add Kestrel + FormOptions defense-in-depth config
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs]
  - acceptance: Two `Configure<>` blocks added to the host module (preferably grouped with the other infrastructure Configure calls at the top of `ConfigureServices`):
    - `Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(o => o.Limits.MaxRequestBodySize = 12 * 1024 * 1024);` (12 MB)
    - `Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = 12 * 1024 * 1024);` (12 MB)
    - Single-line comment explaining "BUG-025 -- 2 MB buffer above the 10 MB AppService cap so multipart headers don't trip framework 413 before the localized message fires." `dotnet build` succeeds.

- T6: Add localization keys
  - approach: code
  - files-touched: [src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json]
  - acceptance: Two new keys added near the existing `UserSignature:File*` keys (around line 220 in en.json):
    - `"AppointmentDocument:FileEmpty": "The selected file is empty."`
    - `"AppointmentDocument:FileTooLarge": "File is too large. Maximum allowed size is 10 MB."`
    - Mirrors the UserSignature pattern verbatim with adjusted size. Any other locale JSON files present (check via `Glob src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/*.json`) get the same key with the English value as a placeholder for future translation (per the codebase's existing fallback convention).

- T7: Update BUG-025 finding doc
  - approach: code
  - files-touched: [docs/runbooks/findings/bugs/BUG-025-no-document-upload-size-limit.md]
  - acceptance: Frontmatter updated to:
    - `status: fixed-after-merge` (note convention from sibling fixes: actual `status: fixed` flips on merge to main; this PR's branch carries `fixed-after-merge`)
    - `last-replayed: 2026-05-21`
    - `fixed-on: fix/bug-025-upload-size-limit (PR <number-tbd>)` -- placeholder until PR opens
    - Quote-block at the top documenting the chosen approach + closing the extension-allowlist branch with reference to existing `EnsureValidFileFormat`.

## Risk / Rollback

- **Blast radius**: 5 source files (AppService + ErrorCodes + HttpApi.Host module + en.json + finding doc). No DB schema change. No migration. No API contract break (additive validation -- valid existing uploads still succeed).
- **Rollback**: revert the single PR. Existing stored documents are unaffected.
- **Edge case mitigated by T5**: Kestrel cap 12 MB vs AppService 10 MB. The 2 MB buffer prevents the framework from emitting an opaque 413 before the localized BusinessException can fire.
- **Edge case unmitigated (acceptable)**: a client uploading exactly 10485761 bytes (1 byte over) hits the AppService check, NOT Kestrel. Localized message fires correctly. Confirmed by manual verify step.
- **Test impact**: hardening suite Phase 7 already uses an 82-byte file -- well under cap. No suite changes needed.

## SDE workflow (gates BEFORE commits + push)

Per Adrian's directive 2026-05-21: "complete build, backend, frontend, and live tests before even committing and pushing to branch." Adapted for this backend-only PR:

**Per-task gate (T1-T6)**: after each code task, `dotnet build` clean (zero errors, zero new warnings). If pass -> commit that task locally. If fail -> fix-in-place, retry build, then commit.

**Full gate (after T6, before T7)**:
1. Full-solution `dotnet build` clean.
2. `dotnet test` against the Application.Tests + Domain.Tests projects. Zero failures.
3. Bring up the replicate-old-app Docker stack (this worktree's offset-port stack, already running from this session). `docker compose restart api authserver` to pick up the new built containers.
4. Run the 4-scenario live verification (next section). All four must pass.

**T7 status flip**: only after full gate passes. Flip BUG-025 doc `status: open` -> `status: fixed` with `last-replayed: 2026-05-21` AND `fixed-on: fix/bug-025-upload-size-limit` AND a quote-block summarizing the live-verify outcomes. NOT `fixed-after-merge` -- per Adrian's rule, "fixed" means tested and verified.

**Push to branch**: only after T7 commit. No PR opens; Adrian decides PR + merge.

## Verification (the live-test suite that runs BEFORE commit + push)

End-to-end test procedure executed as part of the full gate above:

1. Confirm `dotnet build` clean for the whole solution after T1-T6 (no T7 build impact, that's a doc edit).
2. Bring up replicate-old-app stack on offset ports: `docker compose up -d --build`.
3. Wait for containers healthy. Log in as patient1 (or any role with upload permission on an approved appointment).
4. Navigate to an approved appointment's view page. Click "Upload Documents".
5. **9 MB happy-path test**:
   - Stage a 9 MB PDF.
   - Click File picker, select it, click Upload.
   - Expected: HTTP 200, document row created in `AppAppointmentDocuments`.
6. **11 MB rejection test**:
   - Stage an 11 MB PDF.
   - Click File picker, select it, click Upload.
   - Expected: HTTP 413, response body JSON:
     ```json
     {
       "error": {
         "code": "CaseEvaluation:AppointmentDocument.FileTooLarge",
         "message": "File is too large. Maximum allowed size is 10 MB.",
         "data": { "MaxBytes": 10485760, "ActualBytes": 11534336 }
       }
     }
     ```
   - DevTools Network panel: status 413.
   - SPA UX: friendly toast/banner uses the localized message (NOT raw 500 / framework 413).
7. **Empty file test**:
   - Manually craft an empty (0-byte) file or have the SPA send `fileSize=0` via fetch.
   - Expected: HTTP 400, code `CaseEvaluation:AppointmentDocument.FileEmpty`.
8. **Kestrel-cap test** (defense-in-depth verification):
   - Upload a 50 MB file via direct `curl` (bypassing the SPA's client-side gate if any).
   - Expected: Kestrel rejects with framework 413 BEFORE the body is fully read. No AppService log entry for the request (because the handler never executes). Response is the generic Kestrel 413 page.
9. **Hardening Phase 7 retest**:
   - Re-run HRD-P7.1 with the standard 82-byte test file -- must still succeed (regression check).
