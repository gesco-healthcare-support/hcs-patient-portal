---
id: BUG-025
title: AppointmentDocuments upload has no application-level file-size limit
severity: medium
status: open
found: 2026-05-14 hardening R2 (Phase 7 failure mode)
flow: document-upload-limits
component: src/HealthcareSupport.CaseEvaluation.Application/AppointmentDocuments/AppointmentDocumentsAppService.cs + HttpApi.Host Kestrel config
---

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
