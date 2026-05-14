---
feature: document-upload-download
old-source:
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\DocumentDownloadController.cs
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\DocumentUploadController.cs
audited: 2026-05-01
status: audit-only
priority: 4
strict-parity: true
internal-user-role: any logged-in user
depends-on:
  - external-user-appointment-package-documents
  - external-user-appointment-ad-hoc-documents
  - external-user-appointment-joint-declaration
required-by: []
---

# Generic document upload + download endpoints

## Purpose

Cross-cutting utility endpoints used by package docs, ad-hoc docs, JDF, and other file-bearing features. Wraps the underlying file storage with paginated retrieval + per-file get/upload/delete.

**Strict parity with OLD.** Replace local-fs storage with ABP `IBlobStorage`.

## OLD behavior (binding)

### `DocumentDownloadController`

- `GET /api/DocumentDownload/Download` -- generic file download
- `GET /api/DocumentDownload/DownloadFile` -- specific file download

### `DocumentUploadController`

- `GET /api/DocumentUpload/GetFiles/{appointmentId}/{orderByColumn}/{sortOrder}/{pageIndex}/{rowCount}/{userTypeId}/{verificationCode}` -- paginated list of uploaded files for an appointment, with verification-code-based unauthenticated access
- `GET /api/DocumentUpload/GetFile/{id}` -- get specific file
- `PUT /api/DocumentUpload/UploadFile/{id}` -- upload or replace
- `PATCH /api/DocumentUpload/{id}` -- partial update of metadata
- `DELETE /api/DocumentUpload/{id}` -- delete

### Critical OLD behaviors

- **VerificationCode-based unauthenticated access** in `GetFiles` -- same as package-docs flow; allows non-logged-in users to retrieve file lists via the email link.
- **Paginated file lists per appointment** -- used by document upload UI.
- **Storage:** local fs at `wwwroot/Documents/...` (per package-docs audit).

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Api/Controllers/Api/Core/DocumentDownloadController.cs` | Download endpoints |
| `PatientAppointment.Api/Controllers/Api/Core/DocumentUploadController.cs` | Upload + paginated list |

## NEW current state

- `Application/AppointmentDocuments/AppointmentDocumentsAppService.cs` -- per-feature upload methods.
- ABP `IBlobStorage` for storage abstraction.
- TO VERIFY: cross-cutting download + paginated-list endpoints.

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Generic file download | OLD | -- | **Add `GetFileAsync(Guid documentId, string? verificationCode)`** on `IAppointmentDocumentsAppService` (or generic `IDocumentsAppService`) | I |
| Paginated upload list per appointment | OLD | TO VERIFY | **Add `GetFilesByAppointmentAsync(Guid appointmentId, PagedAndSortedInput, string? verificationCode)`** | I |
| VerificationCode-based unauthenticated access | OLD | -- | **`[AllowAnonymous]` endpoints** with code validation + rate limit (per package-docs audit) | I |
| Storage | OLD: local fs | NEW: `IBlobStorage` | **Use IBlobStorage**; per-appointment container | -- |
| Multipart vs base64 | OLD: base64 in body | -- | **Multipart upload** (modern; standard ASP.NET Core) | -- |
| Per-document metadata update | OLD: PATCH | -- | **Add PATCH endpoint** for metadata-only updates | I |

## Internal dependencies surfaced

- `IBlobStorage` configuration.
- VerificationCode rate limiting.

## Branding/theming touchpoints

- Upload UI styling.

## Replication notes

### ABP wiring

- `IBlobStorage`-backed file repository abstraction.
- Per-feature AppService (already exists for `AppointmentDocuments`); aggregate methods on a generic `IDocumentDownloadAppService` if many features need raw download.
- Multipart upload via `IFormFile` parameter on Controller method.
- Anonymous endpoints rate-limited via ASP.NET Core Rate Limiting.

### Things NOT to port

- Base64 in body -- use multipart.
- Local fs paths -- IBlobStorage.

### Verification

1. Logged-in user uploads via UI -> file in blob storage
2. Logged-in user downloads -> file streams
3. Unauthenticated user with verification code -> downloads list of files for appointment
4. Bad verification code -> 401
5. Rate-limit exceeded -> 429
