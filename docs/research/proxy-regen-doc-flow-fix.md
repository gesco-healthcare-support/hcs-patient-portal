---
research: proxy-regen-doc-flow-fix
date: 2026-05-04
audience: Adrian
scope: post-`abp generate-proxy` consumer breakage in `appointment-packet` + `appointment-documents`
old-source-root: P:\PatientPortalOld
new-source-root: W:\patient-portal\replicate-old-app
---

# Proxy regeneration breakage: AppointmentPacket, document upload/download, DocumentStatus

OLD-side citations are read-only, ground truth. NEW-side citations are
the current crude state and a candidate for repair.

---

## Q1: AppointmentPacket vs DocumentPackage -- OLD design

### What OLD actually has

OLD has **template-style** `DocumentPackage` rows tied to a master
`PackageDetail` (a "package name" -- e.g. "PR2 evaluation packet")
which optionally maps to an `AppointmentTypeId`. There is **no
per-appointment packet entity**.

| OLD entity | File | What it is |
| --- | --- | --- |
| `PackageDetail` | `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\PackageDetail.cs` (lines 14-46) | Template named "package" optionally tied to AppointmentTypeId. Parent. |
| `DocumentPackage` | `P:\PatientPortalOld\PatientAppointment.DbEntities\Models\DocumentPackage.cs` (lines 14-73) | Join row: which `Document` (master) belongs to which `PackageDetail`. NOT per-appointment. Has `DocumentId` + `PackageDetailId` FKs only. |
| `vDocumentPackage` | `Models\vDocumentPackage.cs` (lines 14-57) | Read view flattening DocumentPackage + Document + PackageDetail. |
| `vDocumentPackageRecord` | `Models\vDocumentPackageRecord.cs` | Sibling read view. |
| `vPackageDetailLookUp` | `Models\vPackageDetailLookUp.cs` | Lookup view (PackageDetailId, AppointmentTypeId, PackageName). |
| `AppointmentDocument` | `Models\AppointmentDocument.cs` (lines 14-138) | **Per-appointment** uploaded doc. Carries `DocumentPackageId` FK (line 60). One row per file actually uploaded for a given appointment. |
| `AppointmentNewDocument` | `Models\AppointmentNewDocument.cs` (lines 14-124) | Sibling per-appointment ad-hoc doc (no DocumentPackageId; no IsJoinDeclaration). |

Searches for any OLD entity called `AppointmentPacket`,
`PatientAppointmentPackage`, `AppointmentDocumentPackage`,
`AppointmentPackageAssignment` returned **zero hits** under
`P:\PatientPortalOld\PatientAppointment.DbEntities` and
`P:\PatientPortalOld\PatientAppointment.Api`. (Top-level grep for
`AppointmentPacket` over the whole tree timed out at 20s; the two
narrower greps confirmed absence in DbEntities and Api.)

### How OLD models "documents required for THIS patient's appointment"

- **Template:** `PackageDetail` defines "what package applies to this
  appointment type". Each `DocumentPackage` row binds one master
  `Document` to that `PackageDetail`.
- **Per-appointment, per-file:** `AppointmentDocument.DocumentPackageId`
  ties an actual uploaded file back to one of the template's slots.
  Joining `AppointmentDocuments` -> `DocumentPackage` -> `PackageDetail`
  yields the package contents for one appointment. There is **no
  separate "AppointmentPacket" instance row**; the package contents
  for an appointment ARE just the `AppointmentDocument` rows whose
  `DocumentPackageId` is in the appointment-type's `PackageDetail`.
- **Output of the package** in OLD = a generated `.docx` via
  `FileOperations.GetJointAgreementLetter()` (see
  `DocumentDownloadController.Get`,
  `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\DocumentDownloadController.cs:21-30`).
  No per-appointment row is persisted for this file -- it is generated
  on demand and downloaded.

### What NEW currently has

| NEW item | File | Notes |
| --- | --- | --- |
| `AppointmentPacket` entity | `src\HealthcareSupport.CaseEvaluation.Domain\AppointmentDocuments\AppointmentPacket.cs:29-74` | Per-appointment merged-PDF row with `Status` (`Generating`/`Generated`/`Failed`) + `BlobName` + `GeneratedAt`. **NEW invention -- has no OLD analog.** |
| `IAppointmentPacketsAppService` | `src\HealthcareSupport.CaseEvaluation.Application.Contracts\AppointmentDocuments\IAppointmentPacketsAppService.cs:9-23` | Methods: `GetByAppointmentAsync(Guid)`, `DownloadAsync(Guid)`. |
| `AppointmentPacketController` | `src\HealthcareSupport.CaseEvaluation.HttpApi\Controllers\AppointmentDocuments\AppointmentPacketController.cs:22-43` | Routes: `GET /api/app/appointments/{appointmentId}/packet` and `GET .../packet/download`. |
| `AppointmentPacketComponent` | `angular\src\app\appointment-packet\appointment-packet.component.ts:55-165` | UI for status badge + Download + Regenerate. Calls `getByAppointment`, `buildDownloadUrl`, `regeneratePacket`. |
| Pre-regen proxy folder | `angular\src\app\proxy\appointment-packets\` | **Deleted by latest `abp generate-proxy`.** |
| Post-regen proxy service | `angular\src\app\proxy\appointment-documents\appointment-packet.service.ts:9-28` | Methods exposed: `download(appointmentId)` (GET, declared but Angular component does not use), `getByAppointment(appointmentId)`. |
| `regeneratePacket` proxy method | `angular\src\app\proxy\appointment-documents\appointment-document.service.ts:46-51` | Lives on the document service, not the packet service. POST `/api/app/appointments/{appointmentId}/packet/regenerate`. |

### Component <-> proxy method match

| Component call (`angular\src\app\appointment-packet\appointment-packet.component.ts`) | Method exists in post-regen proxy? | Closest replacement |
| --- | --- | --- |
| `packetService.getByAppointment(this.appointmentId)` (line 91) | YES (`appointment-packet.service.ts:22`) | Same name -- only the **import path is wrong**. Imports from `../proxy/appointment-packets/appointment-packet.service` (line 13). Should be `../proxy/appointment-documents`. |
| `packetService.buildDownloadUrl(this.appointmentId)` (line 111) | NO -- never existed (custom helper that pre-regen consumer code expected) | Two options: (a) build the URL inline against `environment.apis.default.url + '/api/app/appointments/' + id + '/packet/download'` and `window.open(url, '_blank')`; (b) keep the typed method and call `restService.request(..., {responseType: 'blob'})` then create an object URL. Pre-regen behaviour was a plain `window.open`. |
| `documentService.regeneratePacket(this.appointmentId)` (line 120) | YES (`appointment-document.service.ts:46`) | Same name; **import path wrong**. Imports from `../proxy/appointment-packets/models` (line 15) and `../proxy/appointment-documents/appointment-document.service` (line 14). The latter is correct. The former is broken. |
| `AppointmentPacketDto` | YES (`models.ts:21-29`) | Import from `../proxy/appointment-documents` (or `models`). |
| `PacketGenerationStatus` enum | YES (`packet-generation-status.enum.ts:3-7` -- values 1/2/3 -- Generating/Generated/Failed) | Import from `../proxy/appointment-documents`. |

### Recommendation: option (a) -- repair `appointment-packet.component.ts`, do NOT delete

- This is a **NEW-only feature** by entity model (OLD has no
  AppointmentPacket entity), but the user-visible behavior IS in OLD:
  the office downloads a generated bundle for the appointment.
  Adrian's CLAUDE.md is explicit: "Reports -- PDF replaces DOCX:
  OLD generated `.docx` reports; NEW generates PDF". The packet UI is
  the NEW-stack realization of OLD's
  `DocumentDownloadController.Get` "joint agreement letter" download
  (lines 21-30) plus the status/regenerate ledger that the OLD app did
  NOT need (its docx was regenerated on every download). Keep the
  component.
- Required edits in `angular\src\app\appointment-packet\appointment-packet.component.ts`:
  1. Line 13: change import to
     `from '../proxy/appointment-documents'` (use the barrel).
  2. Line 14: same module already imports
     `AppointmentDocumentService` from
     `'../proxy/appointment-documents/appointment-document.service'`;
     consolidate both imports through the barrel `../proxy/appointment-documents`.
  3. Line 15: change to
     `import { AppointmentPacketDto, PacketGenerationStatus } from '../proxy/appointment-documents';`.
  4. Line 111 (`buildDownloadUrl`): replace with a small helper at
     `angular\src\app\appointment-packet\appointment-packet-download.helper.ts`
     (proposal -- see Q2 below) that returns
     `${environment.apis.default.url}/api/app/appointments/${appointmentId}/packet/download`.
     Component then `window.open`s the helper's result.
- The `appointment-packet/` component folder stays put. No repackaging
  required.

### Acceptance criteria (Q1)

- `appointment-packet.component.ts` compiles with no unresolved
  imports.
- `getByAppointment(id)` returns the AppointmentPacketDto on the
  appointment-view page when a packet exists.
- Download button opens the merged PDF via
  `/api/app/appointments/{id}/packet/download` (already wired in
  `AppointmentPacketController.DownloadAsync`).
- Regenerate button (gated on
  `CaseEvaluation.AppointmentPackets.Regenerate`) POSTs to
  `/api/app/appointments/{id}/packet/regenerate` and the panel
  re-polls.

### Parity-flag candidates (Q1)

- The `AppointmentPacket` entity itself is a NEW-only invention but
  faithfully implements OLD's "package contents" + "joint agreement
  letter" download behaviour with PDF instead of DOCX. NOT a
  parity-flag -- this is an explicit Adrian directive in
  `CLAUDE.md` (Primary Mission "Reports -- PDF replaces DOCX").

---

## Q2: Document upload + download multipart shape

### OLD upload endpoints (binding)

#### A. `AppointmentNewDocumentsController.UploadLocal` (canonical OLD multipart)

- Path: `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentNewDocumentsController.cs:101-132`.
- Route: `POST /api/appointments/{appointmentId}/AppointmentNewDocuments/UploadLocal`.
- Signature:
  `UploadLocal([FromForm] IFormFile file, [FromForm] string fileName, [FromForm] string confirmationNumber, [FromForm] string appointmentId)`.
- Form fields, exact names and order:
  1. `file` (IFormFile)
  2. `fileName` (string)
  3. `confirmationNumber` (string)
  4. `appointmentId` (string)
- Behaviour: writes the bytes to
  `wwwroot/Documents/submittedDocuments/{fileName}`, returns
  `{ success: true, message, filePath: "/Documents/submittedDocuments/<urlencoded>" }`.
- Auth: inherits BaseController, OLD's session-cookie gate; not
  anonymous.

#### B. Earlier `AppointmentDocumentsController` and `AppointmentDocumentsController` (Document/)

- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\AppointmentRequest\AppointmentDocumentsController.cs:31-52`
  -- pure JSON-body POST/PUT for `AppointmentDocument` (no IFormFile).
  This is **metadata** insert; the actual file bytes are uploaded via
  the OLD AWS S3 client-side path (`appointment-new-document-add.component.ts:140-194`)
  or the local-fs `UploadLocal` (above).
- `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Document\AppointmentDocumentsController.cs:34-69`
  -- same: JSON body, no file upload.
- Conclusion: **the only OLD multipart upload endpoint is
  `UploadLocal` above**.

#### Angular consumer that produces the multipart in OLD

- `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-new-documents\appointment-new-documents.service.ts:95-104`
  builds `FormData`:
  ```ts
  const formData = new FormData();
  formData.append('file', file);
  formData.append('fileName', fileName);
  formData.append('confirmationNumber', confirmationNumber);
  formData.append('appointmentId', appointmentId);
  return this.http.post(this.uploadApi, formData, false);
  ```
  i.e. the OLD service hands a raw `FormData` to the HTTP layer; the
  HTTP layer does NOT type-check the body shape.

### OLD download endpoints (binding)

- `DocumentDownloadController` at
  `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\DocumentDownloadController.cs`:
  - `GET /api/DocumentDownload/Download` (lines 21-30): hard-coded
    "joint agreement letter" stream from `reportFilePath` setting.
  - `GET /api/DocumentDownload/DownloadFile?filePath=<path>` (lines 33-51):
    arbitrary local-fs file download (within `wwwroot/`).
- Angular consumer at
  `P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointment-new-documents\list\appointment-new-document-list.component.ts:197-217`
  builds the URL inline:
  ```ts
  const apiUrl = this.hostUri + `api/DocumentDownload/DownloadFile?filePath=${encodeURIComponent(decodedFilePath)}`;
  this.http.get(apiUrl, { responseType: 'blob' }).subscribe(...)
  ```
  i.e. **OLD has no `buildDownloadUrl` helper either** -- the pattern
  is "hostUri + api path + querystring, fetched as blob, anchor +
  click".

### NEW backend state

| Item | File | Shape |
| --- | --- | --- |
| `AppointmentDocumentController.UploadAsync` | `src\HealthcareSupport.CaseEvaluation.HttpApi\Controllers\AppointmentDocuments\AppointmentDocumentController.cs:32-50` | `[HttpPost] [Consumes("multipart/form-data")] public Task<AppointmentDocumentDto> UploadAsync(Guid appointmentId, [FromForm] UploadAppointmentDocumentForm form)`. Fields: `DocumentName` + `File`. |
| `UploadAppointmentDocumentForm` | same file lines 138-142 | `{ string? DocumentName; IFormFile File; }` |
| `UploadJointDeclarationAsync` | lines 112-130 | Same shape, route `/upload-jdf`. |
| `UploadPackageAsync` | lines 88-106 | Same shape, route `/{id}/upload-package`. |
| `DownloadAsync` (per-doc) | lines 52-57 | `GET /api/app/appointments/{appointmentId}/documents/{id}/download` -> `FileStreamResult`. |
| `AppointmentPacketController.DownloadAsync` | `src\HealthcareSupport.CaseEvaluation.HttpApi\Controllers\AppointmentDocuments\AppointmentPacketController.cs:37-42` | `GET /api/app/appointments/{appointmentId}/packet/download` -> `FileStreamResult`. |

### NEW Angular proxy state (post-regen)

```ts
// angular/src/app/proxy/appointment-documents/appointment-document.service.ts:63-69
upload = (appointmentId: string, form: UploadAppointmentDocumentForm, config?: Partial<Rest.Config>) =>
  this.restService.request<any, AppointmentDocumentDto>({
    method: 'POST',
    url: `/api/app/appointments/${appointmentId}/documents`,
    body: form.file,                // <-- BUG: uploads only the IFormFile shadow, no DocumentName, no multipart wrapper
  },
  { apiName: this.apiName,...config });
```

The ABP TS proxy generator does not know how to encode `[FromForm] +
multipart` from a wrapper class -- it picks the first
`IFormFile`-compatible field and assigns it to `body`. The pre-regen
consumer code (`appointment-documents.component.ts:147-152`) builds
`FormData` itself and calls `service.upload(appointmentId, formData)`
-- which works at runtime because `restService.request` happily
forwards the FormData -- but does NOT match the typed signature
(`UploadAppointmentDocumentForm`). After regen the call site no longer
type-checks (TS rejects passing a `FormData` where
`UploadAppointmentDocumentForm` is required).

### Recommended fix -- option (c): reshape backend wrapper class

This is the lowest-friction, highest-correctness option for an ABP
project. The wrapper class is a Swashbuckle workaround; **expanding
the params back to flat `[FromForm]` parameters makes the proxy
generator emit a usable signature**.

#### Backend change

In `AppointmentDocumentController.cs` (and parallel
`UploadJointDeclaration`, `UploadPackage`), replace the wrapper class
with flat form params:

```csharp
[HttpPost]
[Consumes("multipart/form-data")]
public virtual async Task<AppointmentDocumentDto> UploadAsync(
    Guid appointmentId,
    [FromForm(Name = "documentName")] string? documentName,
    [FromForm(Name = "file")] IFormFile file)
{
    if (file == null || file.Length == 0)
    {
        throw new UserFriendlyException("File is required.");
    }
    await using var stream = file.OpenReadStream();
    return await _service.UploadStreamAsync(
        appointmentId,
        documentName ?? string.Empty,
        file.FileName,
        file.ContentType,
        file.Length,
        stream);
}
```

Reason flat params work where the wrapper failed: ABP's TS proxy
generator emits one `body` field per HTTP request. With a wrapper, it
sees one parameter and writes `body: form.file`. With flat params it
falls back to the multipart path and emits `body: form` where `form`
is a `FormData` because at least one `IFormFile` is present.
(Confirmation HIGH from ABP source -- see
`Volo.Abp.Http.Client.Proxying` codegen rules; reproduce locally to
confirm before shipping. If the generator still emits a wrong shape,
fall back to option (a) below.)

If Swashbuckle complains about the flat shape, suppress with
`[ProducesResponseType]` + a manual `OperationFilter` rather than
re-introducing the wrapper class.

#### Angular consumer change (after backend reshape + regen)

Pre-regen consumer code stays valid:

```ts
const form = new FormData();
form.append('file', selectedFile, selectedFile.name);
form.append('documentName', name);
service.upload(appointmentId, form).subscribe(...)
```

The proxy method's typed signature changes from
`upload(id, form: UploadAppointmentDocumentForm)` to
`upload(id, file, documentName?)` (or a generated
`UploadAppointmentDocumentInput` with the same flat fields). Either is
strictly better than the broken wrapper.

#### Fallback -- option (b): keep wrapper, hand-edit consumer

If the regenerator still produces `body: form.file` after the backend
reshape, work around it by **bypassing the typed proxy method** in the
consumer and using `RestService.request` directly with FormData:

```ts
// angular/src/app/appointment-documents/appointment-documents.component.ts -- replace upload() body
const form = new FormData();
form.append('file', this.selectedFile, this.selectedFile.name);
form.append('documentName', this.documentName.trim() || this.selectedFile.name);
this.restService.request<FormData, AppointmentDocumentDto>(
  {
    method: 'POST',
    url: `/api/app/appointments/${this.appointmentId}/documents`,
    body: form,
  },
  { apiName: 'Default' }
).subscribe(...)
```

This bypasses the typed `upload()` method but stays inside ABP's
`RestService` so auth headers + tenant interceptors still fire. NOT
preferred because it duplicates the URL outside of `proxy/`.

#### Why NOT option (a) -- "wrap the typed method"

A wrapper helper that converts
`UploadAppointmentDocumentForm` to FormData and calls the typed proxy
method does not work: the typed `upload()` method's body is
hard-coded to `form.file` (no `FormData` construction), so the call
silently uploads only the file bytes without `documentName`. Skip.

### `buildDownloadUrl` helper (replacing the missing method)

OLD has no equivalent helper -- it builds the URL inline. NEW should
introduce ONE small helper module to avoid the URL leaking into 5+
component files (`appointment-packet`, `appointment-documents`,
joint-decl upload, package upload, public anonymous download).

#### Recommended path

`angular\src\app\shared\helpers\appointment-document-urls.ts` (new
file -- not under `proxy/`, so safe from regen).

```ts
import { inject } from '@angular/core';
import { EnvironmentService } from '@abp/ng.core';

/** Returns the absolute URL the browser can hit to download a packet PDF. */
export function buildPacketDownloadUrl(env: EnvironmentService, appointmentId: string): string {
  const apiBase = env.getEnvironment().apis.default.url;
  return `${apiBase}/api/app/appointments/${appointmentId}/packet/download`;
}

/** Returns the absolute URL the browser can hit to download a single appointment document. */
export function buildDocumentDownloadUrl(env: EnvironmentService, appointmentId: string, documentId: string): string {
  const apiBase = env.getEnvironment().apis.default.url;
  return `${apiBase}/api/app/appointments/${appointmentId}/documents/${documentId}/download`;
}
```

Consumer change in `appointment-packet.component.ts:107-113`:

```ts
private env = inject(EnvironmentService);
download(): void {
  if (!this.appointmentId || this.packet?.status !== PacketGenerationStatus.Generated) return;
  const url = buildPacketDownloadUrl(this.env, this.appointmentId);
  window.open(url, '_blank');
}
```

Consumer change in `appointment-documents.component.ts:171-177`:

```ts
download(doc: AppointmentDocumentDto): void {
  if (!this.appointmentId) return;
  const url = buildDocumentDownloadUrl(this.env, this.appointmentId, doc.id);
  window.open(url, '_blank');
}
```

Auth caveat: bearer token does NOT travel on `window.open`. ABP's
`/api/app/...` endpoints require auth, so `window.open` will fail
unless the endpoint is anonymous OR the call is changed to a fetch
with `responseType: 'blob'` plus an object-URL anchor click (OLD's
`downloadJointAgreementLetter` pattern,
`appointment-new-document-list.component.ts:197-217`). For the MVP and
to match OLD UX, **switch the consumer call from `window.open` to a
service-method fetch returning a Blob** and synthesize the
`URL.createObjectURL` + anchor click pattern. The
`buildDocumentDownloadUrl` helper still builds the URL; the consumer
just consumes the URL via fetch instead of `window.open`.

A single shared `downloadBlob(url: string, suggestedName: string)`
in the same `shared/helpers/` module avoids duplicating that snippet.

### Acceptance criteria (Q2)

- After backend reshape + `abp generate-proxy`, `upload(id, file, docName)`
  produces a valid multipart request that hits
  `AppointmentDocumentController.UploadAsync` with both `file` and
  `documentName` populated. Round-trip test: upload a 1KB synthetic
  text file, retrieve via `GET /documents/{id}/download`, byte-equal.
- `appointment-packet.component.ts` Download button streams the PDF
  via the helper-built URL and an authenticated fetch.
- `appointment-documents.component.ts` Download button does the same
  for individual documents.
- No reference to a missing `buildDownloadUrl` proxy method anywhere.
- Joint Declaration + Package Document upload paths share the same
  fix (same wrapper class, same generator behaviour).

### Parity-flag candidates (Q2)

- OLD's `UploadLocal` endpoint accepts `appointmentId` AND
  `confirmationNumber` as separate form fields; NEW's takes
  `appointmentId` from the route and does not require
  `confirmationNumber`. Confirmation is derivable from the
  appointment lookup; OLD passing it as a form field is a redundancy,
  not an intentional design. **NOT a parity flag** -- safe to drop.
- OLD's storage path `wwwroot/Documents/submittedDocuments/` is
  replaced by ABP `IBlobContainer<AppointmentDocumentsContainer>`.
  This deviation is sanctioned by CLAUDE.md ("Do NOT match: framework
  choice, library set ..."). Not a flag.

---

## Q3: DocumentStatus enum -- structural placement

### OLD enum (binding)

OLD's `DocumentStatus` is a TABLE, not a C# enum. Rows live in
`spm.DocumentStatuses` keyed by `DocumentStatusId int` with name
`DocumentStatusName nvarchar(50)`
(`P:\PatientPortalOld\PatientAppointment.DbEntities\Models\DocumentStatus.cs:14-57`).

Concrete values are not stored in the entity file, but the consumer
constants in OLD's Angular code are authoritative:

```ts
// P:\PatientPortalOld\patientappointment-portal\src\app\const\document-statuses.ts
// (referenced by appointment-new-document-list.component.ts:48-51, 12-13)
export enum DocumentStatusEnum {
  Uploaded = 1,
  Accepted = 2,
  Rejected = 3,
  Pending  = 4,
  Deleted  = 5,
}
```

Confirmed by `appointment-new-document-list.component.html:36-47`:
- `documentStatusId == 1` -> "Uploaded"
- `documentStatusId == 2` -> "Accepted"
- `documentStatusId == 3` -> "Rejected"
- `documentStatusId == 4` -> "Pending"

`Deleted = 5` is referenced in
`appointment-new-document-list.component.ts:139` for the soft-delete
pattern (OLD writes `documentStatusId = Deleted` instead of removing
the row).

### NEW enum (binding)

`src\HealthcareSupport.CaseEvaluation.Domain.Shared\AppointmentDocuments\DocumentStatus.cs:17-23`:

```csharp
public enum DocumentStatus
{
    Uploaded = 1,
    Accepted = 2,
    Rejected = 3,
    Pending  = 4,
}
```

XML doc explicitly states OLD's `Deleted=5` is replaced by ABP's
`ISoftDelete` filter. (Adrian-sanctioned framework deviation.)

Generated Angular proxy enum at
`angular\src\app\proxy\appointment-documents\document-status.enum.ts:3-8`:

```ts
export enum DocumentStatus {
  Uploaded = 1,
  Accepted = 2,
  Rejected = 3,
  Pending  = 4,
}
```

### Drift between enum and consumer

The post-regen enum has values **`Uploaded / Accepted / Rejected /
Pending`** (= OLD).

The consumer at
`angular\src\app\appointment-documents\appointment-documents.component.ts`
references **`DocumentStatus.Approved`** (lines 251, 262) -- a name
that does **not** exist in either OLD or post-regen NEW. This is
pre-existing drift (the consumer was written against an earlier
informal enum that had `Approved` instead of `Accepted`). HTML
template at lines 89, 99 has the same bug.

### Drift between enum file location and re-export

Pre-regen consumer code in this branch imports:

```ts
import { AppointmentDocumentDto, DocumentStatus } from '../proxy/appointment-documents/models';
```

(`appointment-documents.component.ts:16`).

Post-regen, `DocumentStatus` is in
`document-status.enum.ts` and re-exported from `index.ts:3`, but it is
**not** re-exported from `models.ts`. The consumer's import path is
broken.

### Recommendation (Q3)

#### Enum reference fix

- Rename consumer references from `DocumentStatus.Approved` to
  `DocumentStatus.Accepted` (and the badge / button strings from
  "Approved" to "Accepted") in:
  - `angular\src\app\appointment-documents\appointment-documents.component.ts:251, 262`
  - `angular\src\app\appointment-documents\appointment-documents.component.html:89, 99`
  - any other call site (re-grep after rename).
- This matches OLD verbatim ("Accepted" was the OLD label) AND matches
  the regenerated proxy.

#### Import path fix

- Use the barrel export: `import { DocumentStatus } from '../proxy/appointment-documents'`.
- Do NOT import directly from
  `'../proxy/appointment-documents/document-status.enum'` -- direct
  paths are fine technically but the barrel is the convention used
  across the rest of the regenerated proxy
  (`appointment-documents.component.ts:16` should also pull
  `AppointmentDocumentDto` from `../proxy/appointment-documents`,
  consolidating the two imports into one line).
- Reason: less churn when the proxy generator changes file layout
  (which it just did -- pre-regen `models.ts` had the enum, post-regen
  it doesn't). Importing from the barrel insulates consumers from
  internal proxy reshuffles.

### Acceptance criteria (Q3)

- `DocumentStatus.Accepted` resolves in every consumer; no remaining
  `DocumentStatus.Approved` references in `angular/src/app/`.
- All consumer imports use the barrel
  `'../proxy/appointment-documents'`.
- Angular build (`npx ng build --configuration development`) succeeds
  with no TS2339 ("Property 'Approved' does not exist") or TS2305
  ("Module has no exported member 'DocumentStatus'") errors.
- Document Status badges render the OLD-verbatim label "Accepted"
  (matches `appointment-new-document-list.component.html:38-41`).

### Parity-flag candidates (Q3)

- OLD's `Deleted = 5` row replaced by ABP's `ISoftDelete` filter.
  CLAUDE.md sanctions framework deviation; this is **NOT** a parity
  flag.

---

## Summary table -- concrete edits

| File | Edit |
| --- | --- |
| `angular\src\app\appointment-packet\appointment-packet.component.ts:13-15` | Replace `proxy/appointment-packets/...` imports with `proxy/appointment-documents` (barrel). |
| `angular\src\app\appointment-packet\appointment-packet.component.ts:107-113` | Replace `buildDownloadUrl` call with helper from `shared/helpers/appointment-document-urls.ts` + a fetch-blob anchor-click download. |
| `angular\src\app\appointment-documents\appointment-documents.component.ts:16` | Import `DocumentStatus` + `AppointmentDocumentDto` from barrel `'../proxy/appointment-documents'`. |
| `angular\src\app\appointment-documents\appointment-documents.component.ts:251, 262` | `DocumentStatus.Approved` -> `DocumentStatus.Accepted`. |
| `angular\src\app\appointment-documents\appointment-documents.component.html:89, 99` | `DocumentStatus.Approved` -> `DocumentStatus.Accepted`. Optionally update badge label "Approved" -> "Accepted" to match OLD. |
| `angular\src\app\appointment-documents\appointment-documents.component.ts:171-177` | Replace `service.buildDownloadUrl(...)` with helper + blob download. |
| `src\HealthcareSupport.CaseEvaluation.HttpApi\Controllers\AppointmentDocuments\AppointmentDocumentController.cs:32-50, 88-106, 112-130, 138-142` | Flatten `UploadAppointmentDocumentForm` wrapper into `[FromForm] string? documentName, [FromForm] IFormFile file` parameters; delete the wrapper class. Re-run `abp generate-proxy`. |
| `angular\src\app\shared\helpers\appointment-document-urls.ts` (NEW) | Add `buildPacketDownloadUrl`, `buildDocumentDownloadUrl`, `downloadBlob` helpers. |

## Risk + verification

- **Blast radius:** localized to `appointment-packet` +
  `appointment-documents` + the document upload controller. No
  database migration needed (enum values unchanged; entity unchanged).
- **Verification path:** (1) run `abp generate-proxy` after backend
  reshape, (2) `npx ng build --configuration development` to confirm
  type errors gone, (3) end-to-end upload + download + approve +
  reject + regenerate-packet smoke test against a synthetic seed
  appointment (see `.claude\rules\test-data.md`).
- **Rollback:** revert per-file commits; the wrapper class fix is
  isolated to one controller and one regen run.
