---
feature: it-admin-package-details
date: 2026-05-04
phase: 5-frontend (backend Document + PackageDetail + DocumentPackage entities implemented 2026-05-03; Angular UI pending)
status: draft
old-source: patientappointment-portal/src/app/components/document-management/
old-components:
  - documents/list/ (Document master catalog list page)
  - documents/add/  (Add document modal with file upload)
  - documents/edit/ (Edit document modal with file replace + Is Active)
  - package-details/list/ (Packages list page + embedded DocumentPackage sub-list)
  - package-details/add/  (Add package modal)
  - package-details/edit/ (Edit package modal)
  - document-packages/list/ (embedded sub-list, selector app-document-package-list)
  - document-packages/add/  (Add document-to-package modal)
  - document-packages/edit/ (Edit document-package modal with Is Active)
new-feature-path:
  - angular/src/app/documents/
  - angular/src/app/package-details/
shell: internal-user-authenticated (top-bar + side-nav)
screenshots: pending
---

# Design: IT Admin -- Package Details (Documents + Packages + Package Document Linking)

This doc covers three related admin pages that together define the document-template
catalog: (1) the `Documents` master catalog page, (2) the `Packages` (PackageDetail)
page, and (3) the embedded `DocumentPackages` sub-list within the Packages page.
All three are IT Admin-only CRUD surfaces with no user-visible behavior on their own --
they seed the data consumed by the external user's document upload flows and the
Phase 12 auto-queue handler.

> **Parity note:** OLD's approval flow does NOT read `PackageDetail`. It downloads
> from hardcoded AWS S3 buckets. NEW replaces that with `PackageDocumentQueueHandler`
> (Phase 12) that reads this master-data. End-user behavior is the same; the source
> of "which docs" changes. See `docs/parity/it-admin-package-details.md` for the
> full correction note.

## 1. Routes

| Page | OLD URL | OLD `applicationModuleId` | NEW URL | NEW Permission |
|---|---|---|---|---|
| Documents (master catalog) | `/documents` | 27 | `/documents` | `CaseEvaluationPermissions.Documents.Default` |
| Packages | `/package-details` | 27 | `/package-details` | `CaseEvaluationPermissions.PackageDetails.Default` |

Document Packages have no standalone route -- they render as an embedded child
component on the Packages page only.

## 2. Shell

Internal-user authenticated shell for both pages. Side-nav items under a
"Document Management" section: "Documents" and "Packages".

---

## 3. Documents Page

### 3a. Layout (Documents List)

```
+-------------------------------------------------------+
| [H2] Documents                         [Search input] |
+-------------------------------------------------------+
| [Card]                                               |
|   [Card header]              [Add + button -- right] |
|   [Table]                                            |
|   Document Name | File Path | Status | Action        |
+-------------------------------------------------------+
```

OLD source: `documents/list/document-list.component.html:1-52`

### 3b. Table Columns (Documents)

| Column | OLD field | Notes |
|---|---|---|
| Document Name | `documentName` | Plain text |
| File Path | `documentFilePath` | Path string (OLD: local file path; NEW: blob name or public URL) |
| Status | `status` | String "Active"/"Inactive" |
| Action | -- | Pencil (edit) + trash (delete) icons |

### 3c. Add Document Modal (`DocumentAddComponent`)

Header: "Document Details" (no subtitle)

```
+-----------------------------------+
| Document Details            [X]  |
+-----------------------------------+
| Document Name  [text]            |
| File Path      [text, readonly]  |
| [Choose File to Upload] (btn-info)|
+-----------------------------------+
| [Save]          [Cancel]         |
+-----------------------------------+
```

**Document Name (required):**
- `type="text"`, `formControlName="documentName"`, placeholder "Document Name"

**File Path (readonly display):**
- `type="text"`, readonly, `value="{{filePath}}"` (bound to component property, not form control)
- Populated when a file is chosen; shows the filename
- Not a form control -- read-only display only

**File Upload button:**
- Label styled as `btn btn-info px-3 bold pull-left col-md-4`: "Choose File to Upload"
- `<input type="file" (change)="onFileChange($event)">` (hidden, overlapping the button via CSS)

**`onFileChange()` behavior:**
- Validates file size: `file.size >= (1000 * 1024)` = ~1MB -- silently aborts (no toast) if exceeded
  See Exception 1 (size cap bug)
- Accepted types: `DEFAULT_IMAGE_FILE_EXTENSTION = ".doc,.docx,.pdf"` (same const as JAL upload)
- On valid file: reads with `FileReader.readAsBinaryString`, base64-encodes via `btoa()`,
  patches form with: `fileData` (base64), `fileName`, `fileExtention` (lowercased), `statusId=Active`
- On invalid extension: clears `fileData`/`fileName`/`fileExtention`, sets `fileData` required
  validator (no toast shown -- silent failure in OLD; Exception 2)

OLD source: `documents/add/document-add.component.ts:58-90`

### 3d. Edit Document Modal (`DocumentEditComponent`)

Header: "Document Details" (same as Add)
Identical visible fields to Add PLUS **Is Active checkbox**:

```
+-----------------------------------+
| Document Details            [X]  |
+-----------------------------------+
| Document Name  [text]            |
| File Path      [text, readonly]  |
| [Choose File to Upload] (btn-info)|
| Is Active      [checkbox]        |
+-----------------------------------+
| [Update]        [Cancel]         |
+-----------------------------------+
```

**Is Active checkbox:**
- `formControlName="statusId"`
- `[checked]="documentFormGroup.value.statusId == statusTypeEnums"` (Active enum)
- `(change)="statusChange(documentFormGroup.value.statusId)"`
- Not present in the Add modal (new documents always created Active)

Buttons: "Update" (`btn btn-primary`) / "Cancel" (`btn btn-secondary`)

OLD source: `documents/edit/document-edit.component.html:24-38`

### 3e. Delete Document

`dialog.confirmation([documentName], "delete")` then
`documentsService.delete(documentId)`. No separate delete component.

OLD source: `documents/list/document-list.component.ts` (standard pattern)

---

## 4. Packages Page

### 4a. Layout (Packages List + Embedded Sub-list)

```
+-------------------------------------------------------+
| [H2] Packages                          [Search input] |
+-------------------------------------------------------+
| [Card]                                               |
|   [Card header]              [Add + button -- right] |
|   [Table]                                            |
|   Appointment Type | Package Name | Action           |
+-------------------------------------------------------+
| [app-document-package-list -- only if packageDetailId > 0 AND row is Active] |
|   [H2] Document Packages              [Search input] |
|   [Card]                                             |
|   [Card header]              [Add + button -- right] |
|   [Table]                                            |
|   Package Name | Document Name | Document File Path | Status | Action |
+-------------------------------------------------------+
```

The `app-document-package-list` component renders below the Packages table when
the user clicks the "Package Documents" icon in a row's Action column. It is bound
via `[packageDetailId]="packageDetailId"`. If `packageDetailId == 0` (initial state
or after an active package is deactivated), the sub-list is not rendered.

OLD source: `package-details/list/package-detail-list.component.html:60-62`

### 4b. Table Columns (Packages)

| Column | OLD field | Notes |
|---|---|---|
| Appointment Type | `appointmentTypeName` | Resolved from `appointmentTypeId` FK |
| Package Name | `packageName` | Plain text |
| Action | -- | Pencil (edit) + trash (delete) + file-alt icon (show sub-list) |

Status column is commented out in OLD.

OLD source: `package-details/list/package-detail-list.component.html:32-50`

### 4c. Action Column -- "Package Documents" Icon

The file-alt icon (`fas fa-file-alt`) calls `showDocumentPackages(event, 1)`.

`showDocumentPackages()` logic:
```ts
showDocumentPackages(row: any): void {
  this.packageDetailId = 0;
  if (row.statusId == StatusEnum.Active) {
    window.setTimeout(() => {
      this.packageDetailId = row.packageDetailId;
    }, 100);
  }
}
```

- Only shows the sub-list if the package's `statusId == Active`.
- The `window.setTimeout(..., 100)` resets packageDetailId to 0 first then sets it
  after 100ms -- forces Angular's `*ngIf` to re-render the child component
  (DOM tear-down + re-create). This is an OLD implementation pattern.
- In NEW: use a simple toggle or router-navigate to a package-detail child route
  instead of the timeout trick. See Exception 3.

OLD source: `package-details/list/package-detail-list.component.ts:112-118`

### 4d. Add Package Modal (`PackageDetailAddComponent`)

Header: "Add Package" (no subtitle)

```
+-----------------------------------+
| Add Package                 [X]  |
+-----------------------------------+
| Package Name   [text]            |
| Appointment Type [select]        |
+-----------------------------------+
| [Save]          [Cancel]         |
+-----------------------------------+
```

**Package Name (required):**
- `type="text"`, `formControlName="packageName"`, placeholder "Package Name"

**Appointment Type (required):**
- `<select>` bound to `documentManagementLookups.appointmentTypeLookUps`
- Value: `item.appointmentTypeId`, display: `item.appointmentTypeName`
- Default: "Select Appointment Type" (disabled placeholder, value "null")

**Is Active (COMMENTED OUT in Add):**
- OLD HTML lines 22-28 are commented out; new packages always created Active by default
- NEW: surface Is Active in Add if Adrian wants to pre-stage inactive packages;
  otherwise keep the same behavior (always Active on create). Surface to Adrian -- see Exception 4.

OLD source: `package-details/add/package-detail-add.component.html:1-37`

### 4e. Edit Package Modal (`PackageDetailEditComponent`)

Header: "Edit Package" (no subtitle)

```
+-----------------------------------+
| Edit Package                [X]  |
+-----------------------------------+
| Package Name   [text, editable]  |
| Appointment Type [select, DISABLED] |
+-----------------------------------+
| [Update]        [Cancel]         |
+-----------------------------------+
```

**Appointment Type:** `disabled` attribute on the select -- cannot change which
appointment type a package serves once created.

**Is Active (COMMENTED OUT in Edit):**
- OLD HTML lines 20-26 are commented out; no soft-deactivate available from UI
- This means packages can only be deleted (hard-delete route) but not deactivated
- See Exception 4 for NEW behavior

OLD source: `package-details/edit/package-detail-edit.component.html:1-35`

### 4f. Delete Package

`dialog.confirmation([packageName], "delete")` then
`packageDetailsService.delete(packageDetailId)`.
On delete, `isOnEditDelete = true` which calls `showDocumentPackages()` again
to refresh the sub-list (or hide it if the deleted package was the selected one).

OLD source: `package-details/list/package-detail-list.component.ts:87-100`

---

## 5. Document Packages Sub-list

The `DocumentPackageListComponent` is an embedded child, not a standalone page.
It renders as a second card below the Packages table.

### 5a. Table Columns (Document Packages)

| Column | OLD field | Notes |
|---|---|---|
| Package Name | `packageName` | Comes from the parent `PackageDetail` -- read-only context |
| Document Name | `documentName` | The template document being linked |
| Document File Path | `documentFilePath` | Path/blob name of the template file |
| Status | `status` | "Active"/"Inactive" string |
| Action | -- | Pencil (edit) + trash (delete) icons |

OLD source: `document-packages/list/document-package-list.component.html:34-49`

### 5b. Add Document Package Modal (`DocumentPackageAddComponent`)

Header: "Document Package" (no subtitle; same header for Add and Edit)

```
+-----------------------------------+
| Document Package            [X]  |
+-----------------------------------+
| Package Name   [select, DISABLED]|
| Document Name  [select]          |
+-----------------------------------+
| [Save]          [Cancel]         |
+-----------------------------------+
```

**Package Name select (DISABLED):**
- Bound to `documentPackageLookupGroup.packageDetailLookUps`
- Pre-selected with the `packageDetailId` passed from the parent
- Disabled -- user cannot change which package they're adding to
- Value: `package.packageDetailId`, display: `package.packageName`

**Document Name select (required):**
- Bound to `documentPackageLookupGroup.documentLookUps`
- Shows all active Documents from the master catalog
- Value: `document.documentId`, display: `document.documentName`
- Default: "Select Document" (disabled placeholder)

OLD source: `document-packages/add/document-package-add.component.html:1-39`

### 5c. Edit Document Package Modal (`DocumentPackageEditComponent`)

Header: "Document Package" (same as Add)
Identical layout to Add PLUS **Is Active checkbox**:

```
+-----------------------------------+
| Document Package            [X]  |
+-----------------------------------+
| Package Name   [select, DISABLED]|
| Document Name  [select]          |
| Is Active      [checkbox]        |
+-----------------------------------+
| [Update]        [Cancel]         |
+-----------------------------------+
```

**Is Active checkbox:**
- `formControlName="statusId"`
- `[checked]="documentPackageFormGroup.value.statusId == statusTypeEnums"` (Active enum)
- `(change)="statusChange(documentPackageFormGroup.value.statusId)"`
- Not present in the Add modal

Buttons: "Update" (`btn btn-primary`) / "Cancel" (`btn btn-secondary`)

OLD source: `document-packages/edit/document-package-edit.component.html:33-44`

### 5d. Delete Document Package

`dialog.confirmation([documentName], "delete")` then
`documentPackagesService.delete(packageDetailId, documentPackageId)` (nested route).
`packageDetailId` is the `@Input()` passed from the parent Packages list component.

OLD source: `document-packages/list/document-package-list.component.ts:56-67`

---

## 6. Buttons

| Button | Context | OLD class | Disabled condition |
|---|---|---|---|
| Add + | Documents list header | `btn btn-primary btn-sm` | never |
| Add + | Packages list header | `btn btn-primary btn-sm` | never |
| Add + | Document Packages sub-list header | `btn btn-primary btn-sm` | never |
| Edit (pencil) | All list Action columns | `.oi.oi-pencil` icon | never |
| Delete (trash) | All list Action columns | `fas fa-trash-alt` icon | never |
| Package Documents (file-alt) | Packages Action column | `fas fa-file-alt` icon | never (click ignored if package inactive) |
| Save | Documents Add, Package Add, Doc-Package Add | `btn btn-primary` | `!formGroup.valid` |
| Cancel | All Add modals | `btn btn-secondary` | never |
| Update | Documents Edit, Package Edit, Doc-Package Edit | `btn btn-primary` | `!formGroup.valid` |
| Cancel | All Edit modals | `btn btn-secondary` | never |

---

## 7. Role Visibility Matrix

| Role | Documents | Packages | Doc-Package sub-list |
|---|---|---|---|
| IT Admin | Full CRUD | Full CRUD | Full CRUD |
| Staff Supervisor | Read-only (view only -- no Add/Edit/Delete) | Read-only | Read-only |
| Clinic Staff | No access | No access | No access |
| External users | No access | No access | No access |

Both URLs share `applicationModuleId: 27`. In OLD, role restriction is at the route
guard level. NEW should restrict Add/Edit/Delete buttons by role at the component level
(defense in depth -- same pattern as notification templates).

---

## 8. Branding Tokens

| Element | Token |
|---|---|
| Page heading H2 | `--text-color-primary` |
| Card header background | Bootstrap default (light grey) -- no explicit brand color in OLD |
| Add button | `--brand-primary` via `btn-primary` |
| File upload button | `btn-info` style -- use a neutral secondary variant in NEW |

---

## 9. NEW Stack Delta

1. **File storage:** OLD stores document template files as local paths
   (`DocumentFilePath` = a local server path). NEW replaces with `IBlobStorage`
   (`MasterDocumentsContainer` blob container, Phase 5 backend). The Angular form
   reads the file from `<input type="file">` and POSTs base64 to the API, which
   stores in blob storage. The displayed "File Path" column in NEW shows the blob
   name or a short identifier, not a full path.

2. **File upload in NEW:** Replace OLD's `FileReader.readAsBinaryString` + `btoa()`
   with the ABP-standard `FormData` upload to the Documents API endpoint that accepts
   `IFormFile`. The base64 approach works but is 33% larger than binary -- ABP's
   blob API accepts binary POST.

3. **Package Documents expand via route or panel:** OLD uses `window.setTimeout(100)`
   to tear down / recreate the embedded child. NEW should use either:
   - A `mat-expansion-panel` row that expands inline (cleaner UX), or
   - A child route `/package-details/:id/documents` that navigates to a detail page.
   Either is acceptable; the inline expand approach (closest to OLD) is preferred
   for parity.

4. **File size cap:** Raise from ~1MB (`1000 * 1024`) to 10MB (`10 * 1024 * 1024`)
   with a visible error toast (Exception 1).

5. **Is Active on Packages:** Surface Is Active checkbox in both Add and Edit Package
   modals. The entity has `IsActive`. Package deactivation prevents the auto-queue
   handler from picking it up (Phase 12). See Exception 4.

6. **ABP proxy:** After finalizing `DocumentDto`, `PackageDetailDto`, `DocumentPackageDto`
   surfaces, run `abp generate-proxy` to regenerate
   `angular/src/app/proxy/documents/`,
   `angular/src/app/proxy/package-details/`,
   `angular/src/app/proxy/document-packages/`.

7. **One active package per AppointmentType:** Backend already enforces
   `EnsureNoActiveDuplicateAsync`. Angular should surface the API error clearly:
   "An active package already exists for this appointment type."

---

## 10. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | File upload size cap | `file.size >= (1000 * 1024)` = ~1MB; aborts silently with no toast | Raise to 10MB; show error toast on rejection | Same bug as JAL upload (~1MB vs stated 10MB). Document template PDFs commonly exceed 1MB. Silent abort is a severe UX failure for IT Admin |
| 2 | Invalid file extension: no error toast | `onFileChange()` clears the form silently; no user feedback | Show error toast: "Only .doc, .docx, .pdf files are accepted" | Silent failure leaves user confused; easy to add without behavior change |
| 3 | `window.setTimeout(100)` for sub-list toggle | Resets `packageDetailId` to 0 then back to the real ID to force Angular re-render | Use `mat-expansion-panel` or toggle flag without setTimeout | Implementation smell, not a user-visible behavior. Sub-list still shows/hides based on the selected row |
| 4 | Is Active on Package modals commented out | No activate/deactivate from Edit modal; only hard-delete available | Surface Is Active checkbox in both Add (default=true) and Edit Package modals | The `statusId` field exists and is used by the sub-list's `showDocumentPackages()` guard. Without this, IT Admin cannot soft-deactivate a package without deleting it |
| 5 | Local file path in Documents table | `documentFilePath` shows a full server filesystem path | Show blob container name / short identifier | OLD's local paths are not portable; NEW's IBlobStorage doesn't produce local paths. Display a blob key or a "Download" link instead |
| 6 | No success toast on any Add/Edit/Delete | All operations close the popup on success with no notification | Show success toast via ABP notification service | Consistent with all other IT Admin pages in this session |

---

## 11. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `documents/list/document-list.component.html` | 1-52 | Full Documents list page |
| `documents/add/document-add.component.html` | 1-29 | Add Document modal (Name, File Path, upload button) |
| `documents/add/document-add.component.ts` | 58-90 | `onFileChange()` -- file validation + base64 encode |
| `documents/edit/document-edit.component.html` | 1-37 | Edit Document modal (same + Is Active checkbox) |
| `package-details/list/package-detail-list.component.html` | 1-65 | Full Packages page (list + embedded sub-list trigger) |
| `package-details/list/package-detail-list.component.ts` | 87-119 | `deletePackage()` + `showDocumentPackages()` timeout pattern |
| `package-details/add/package-detail-add.component.html` | 1-37 | Add Package modal (Is Active commented out) |
| `package-details/edit/package-detail-edit.component.html` | 1-35 | Edit Package modal (Appointment Type disabled, Is Active commented out) |
| `document-packages/list/document-package-list.component.html` | 1-59 | Document Packages sub-list |
| `document-packages/list/document-package-list.component.ts` | 56-67 | `deleteDocumentPackage()` -- nested delete |
| `document-packages/add/document-package-add.component.html` | 1-39 | Add Document-Package modal (Package disabled, Document select) |
| `document-packages/edit/document-package-edit.component.html` | 1-48 | Edit Document-Package modal (same + Is Active checkbox) |
| `documents/documents.routing.ts` | 10 | `applicationModuleId: 27` for Documents |
| `package-details/package-details.routing.ts` | 10 | `applicationModuleId: 27` for Packages |
| `docs/parity/it-admin-package-details.md` | all | Full parity audit (schema, CRUD, OLD correction re: approval flow, Phase 5 backend status) |

---

## 12. Verification Checklist

### Documents page
- [ ] IT Admin navigates to `/documents` and sees the list
      (Document Name, File Path, Status columns + Action)
- [ ] "Add +" opens the Add Document modal
- [ ] Choosing a .pdf/.doc/.docx file: File Path field fills with the filename
- [ ] Choosing a file > 10MB shows an error toast and does not submit
- [ ] Choosing an invalid extension shows an error toast and clears the file
- [ ] Saving creates the document; blob is stored; list refreshes
- [ ] Edit pencil opens Edit modal with current values + Is Active pre-filled
- [ ] Replacing the file in Edit replaces the blob; File Path updates
- [ ] Toggling Is Active and Updating changes the status; list refreshes
- [ ] Delete icon confirms with document name; confirming removes the row

### Packages page
- [ ] IT Admin navigates to `/package-details` and sees the list
      (Appointment Type, Package Name columns + Action)
- [ ] "Add +" opens Add Package modal with Appointment Type select
- [ ] Saving a package with an Appointment Type that already has an active package
      shows the "one active package per type" error
- [ ] Edit pencil opens Edit Package modal; Appointment Type field is disabled
- [ ] Is Active checkbox is present in both Add and Edit modals (NEW addition)
- [ ] Deactivating a package via Is Active checkbox hides the Package Documents
      icon for that row (or greys it out)
- [ ] Delete icon confirms with package name; confirming removes the package

### Document Packages sub-list
- [ ] Clicking the file-alt icon on an Active package shows the Document Packages
      sub-list below the Packages table
- [ ] Clicking the icon on an Inactive package shows no sub-list
- [ ] Sub-list "Add +" opens the Add Document Package modal with Package Name
      pre-filled (disabled) and Document Name select showing active documents
- [ ] Saving a document-package links the document to the package; sub-list refreshes
- [ ] Edit pencil opens Edit modal with Package Name disabled + Is Active pre-filled
- [ ] Toggling Is Active on a document-package deactivates the link without deleting it
- [ ] Delete icon confirms with document name; confirming removes the link; sub-list refreshes
- [ ] After Phase 12: booking an appointment of a given type creates AppointmentDocument
      rows for all Active documents in the active package for that type
