---
status: draft
created: 2026-06-03
owner: Adrian
feature: Document-type master + packet linkage (parity Group E, enhanced)
---

# Document-type master + packet linkage

Restore (and extend beyond) the legacy `AppointmentDocumentType` capability:
a managed list of document categories, a picker on upload, auto-tagging of
generated packets, a per-appointment type filter + missing-required-documents
indicator, and a fully-built public upload page. Covers parity records
G-03-01, G-03-03, G-03-05, G-10-10.

## Locked decisions (from Adrian)
1. Self-service lists, managed by internal staff, **scoped per appointment type**.
2. Build a per-appointment **"missing required documents" indicator** now; a
   **type filter** on the per-appointment document list. Cross-appointment
   reporting stays in the later Reporting group (out of scope here).
3. Management = a full page under the **Appointment Management** menu, editable
   by **IT Admin + Staff Supervisor** only. The upload picker shows for **both**
   internal and external uploaders.
4. Generated/queued packet docs auto-tagged with **one** reserved system
   category ("Generated Packet"), never shown in the picker.
5. Keep **"Other"** -> reveals a free-text box; the document is stored/displayed
   under the typed label (not the word "Other").
6. **Build the anonymous upload path fully usable** (public page + link helper);
   the only deferred step is dropping the link into an email template (Adrian
   will name the template later).
7. Track required docs by a stable internal **ID**; **UI shows labels**, never IDs.

## Design decisions (emergent from Stage-1 research; recommendations -- confirm at review)
- **D-A. Multitenancy of `AppointmentDocumentType`.** Recommend **IMultiTenant
  (tenant-scoped)** so each office curates its own lists (matches decision 1 +
  the IMultiTenant `AppointmentDocument`). NOTE: this diverges from the host-only
  `AppointmentStatuses` template the entity is otherwise modeled on. `AppointmentTypeId`
  is a loose nullable Guid reference (NoAction; AppointmentType is host-scoped).
- **D-B. "Other".** Model as a fixed UI option in the picker (sets
  `OtherDocumentTypeName`, `AppointmentDocumentTypeId = null`), not a seeded
  per-list row. Display = `OtherDocumentTypeName` when present, else the type name.
  Avoids the legacy magic-number "Other == id 4".
- **D-C. "Generated Packet" category.** One seeded row with `IsSystem = true`
  (and `AppointmentTypeId = null`): hidden from the picker, not editable/deletable
  by admins, auto-applied to queued package rows.
- **D-D. `SourceDocumentId`.** Nullable FK to the master `Document` (NoAction);
  populated only on queued package rows; UI never shows it.
- **D-E. Public upload route.** A dedicated **anonymous** SPA route (bypasses
  authGuard + the post-login redirect guard), reads `id` + `verificationCode`,
  uploads via the existing `PublicDocumentUploadService`. Backend endpoint + gate
  + rate-limit (5/hr per code) already exist.

## Data model
- NEW entity `AppointmentDocumentType` (IMultiTenant, `FullAuditedAggregateRoot<Guid>`):
  `Name` (max 100), `AppointmentTypeId` (Guid?, loose ref), `IsSystem` (bool),
  `IsActive`/status. Modeled on `AppointmentStatuses` across all layers.
- `AppointmentDocument` gains 3 nullable columns: `AppointmentDocumentTypeId`
  (Guid?), `OtherDocumentTypeName` (string?, max 100), `SourceDocumentId` (Guid?).
- Seed: one `IsSystem` "Generated Packet" row. (User types are created at runtime;
  no other seed.)

## PR slicing (4 PRs, into feat/replicate-old-app)

### PR1 -- Document-type master + admin management page
- Backend: `AppointmentDocumentType` entity + repo + manager (name-uniqueness
  per appointment type + reserved-system-row guard -- OLD-bug fixes; the
  in-use-before-delete guard moves to PR2 because it needs the
  `AppointmentDocument.AppointmentDocumentTypeId` FK shipped there),
  DTOs, permissions group (`AppointmentDocumentTypes` Default/Create/Edit/Delete)
  granted to IT Admin + Staff Supervisor only, AppService + Mapperly mappers,
  dual-DbContext config + 1 migration, manual controller, seed of the system
  "Generated Packet" row.
- Angular: new admin CRUD feature under Appointment Management
  (`appointment-management/document-types`), modeled on `appointment-status`;
  list is organized/filterable by appointment type. Proxy regen.
- Verify: IT Admin + Supervisor can CRUD per-type lists; Clinic Staff + external
  cannot; "Generated Packet" is not editable/deletable; name-uniqueness holds.
  (In-use-before-delete guard is verified in PR2, once the FK exists.)

### PR2 -- Wire types into uploads (+ generated tagging + source id)
- Backend: add the 3 columns to `AppointmentDocument` (+ migration); thread
  `appointmentDocumentTypeId` + `otherDocumentTypeName` through `UploadStreamAsync`
  + `AppointmentDocumentDto`; auto-tag queued rows with the system category and
  set `SourceDocumentId` in `PackageDocumentQueueHandler` (documentId already in
  scope); thread `sourceDocumentId` through `CreateQueued`/`CreateQueuedAsync`.
  Add the in-use-before-delete guard to `AppointmentDocumentTypeManager.DeleteAsync`
  (deferred from PR1): block deleting a type still referenced by an
  `AppointmentDocument`.
- Angular: add the Document Type `<select>` + "Other -> custom label" to the
  upload form (RestService FormData; shows for internal + external). Show the
  type label in the document list.
- Verify: upload with a type / with "Other" + label; generated docs show
  "Generated Packet"; queued rows carry SourceDocumentId (DB check).

### PR3 -- Type filter + missing-required-documents indicator
- Backend: a per-appointment "missing required documents" read (required =
  active PackageDetail docs for the type; missing = required minus Accepted,
  matched by `SourceDocumentId`).
- Angular: a type filter on the document list (client-side over the loaded list)
  + a "missing required documents" indicator on the appointment's document panel.
- Verify: indicator lists outstanding required docs; filter narrows by type.

### PR4 -- Public anonymous upload page + link helper (independent)
- Angular: a public, no-auth route + component reading `id` + `verificationCode`,
  uploading via `PublicDocumentUploadService`. Backend: a link-generation helper
  composing the tenant-aware URL. Email wiring deferred (Adrian names the template).
- Verify: a valid code link uploads anonymously; a bad/expired code is rejected;
  rate-limit holds.

Dependencies: PR1 -> PR2 -> PR3; PR4 independent. Each is its own SOP cycle
(Stage-1 recheck, build, live verify, self-review, PR, STOP).

## Flags / risks
- **Beyond legacy parity (deliberate):** per-type lists, the filter, the
  missing-docs indicator, and a live public upload page all exceed the legacy app.
  Recorded as intentional enhancements per Adrian's decisions.
- **Scope growth:** far larger than the original "~37h flat-list" estimate;
  hence the 4-PR split.
- **Public upload = anonymous internet write surface** (PR4): gated by per-link
  code + 5/hr rate limit; email link stays unwired until Adrian picks the template.
- **Multitenancy choice (D-A):** confirm tenant-scoped vs host-only before PR1.

## Verification & conventions
- ABP conventions: manual controller + `[RemoteService(IsEnabled=false)]`
  AppService extending `CaseEvaluationAppService`; Mapperly (not AutoMapper);
  permission group + provider; localization keys before use; never edit
  `angular/src/app/proxy/` (regenerate). No automated tests (live verification).
- Each PR: build green, exercise live on the Docker stack, self-review
  (code-simplifier + code-reviewer), PR into `feat/replicate-old-app`, STOP.
