---
feature: it-admin-notification-templates
date: 2026-05-04
phase: 18-frontend (backend renderer + dispatcher implemented 2026-05-04; Angular UI pending)
status: draft
old-source: patientappointment-portal/src/app/components/template-management/templates/
old-components:
  - list/ (templates list page)
  - add/ (create template modal)
  - edit/ (edit template modal)
  - delete/ (unused -- delete is inline confirmation)
old-ts: list/template-list.component.ts
new-feature-path: angular/src/app/notification-templates/
shell: internal-user-authenticated (top-bar + side-nav)
screenshots: pending
---

# Design: IT Admin -- Notification Templates

## 1. Routes

One list page. Add and Edit are modals launched from the list.

| | OLD | NEW |
|---|---|---|
| URL | `/templates` | `/notification-templates` |
| Guard | `PageAccess` `applicationModuleId: 19` | `[Authorize(CaseEvaluationPermissions.NotificationTemplates.Default)]` |

Backend: `GET api/app/notification-templates` + `POST api/app/notification-templates` +
`PUT api/app/notification-templates/{id}` + `DELETE api/app/notification-templates/{id}`.

The backend `NotificationTemplate` entity and renderer infrastructure were
implemented in Phase 18 (2026-05-04). Angular proxy regenerates via
`abp generate-proxy` after AppService surface is locked.

## 2. Shell

Internal-user authenticated shell. Side-nav item "Notification Templates" under
an IT Admin section.

## 3. Page Layout (List)

```
+-------------------------------------------------------+
| [H2] Notification Templates          [Search input]  |
+-------------------------------------------------------+
| [Card]                                               |
|   [Card header]         [Add + button -- right]     |
|   [Table]                                            |
|   Template Code | Email Body | SMS Body | Status     |
|   Action (edit | delete)                             |
+-------------------------------------------------------+
```

No filter by template type or status in OLD -- search is free-text over all
visible columns.

OLD source: `list/template-list.component.html:1-65`

## 4. Table Columns

| Column | OLD field | Notes |
|---|---|---|
| Template Code | `templateCode` (int) | Resolved to display name via `getTemplateCode(templateCodeId)` from `TEMPLATE_CODE_LIST` |
| Email Body | `bodyEmail` | Raw HTML/text; truncated by column width |
| SMS Body | `bodySMS` | Plain text |
| Status | `status` | String display ("Active" / "Inactive") |
| Action | -- | Pencil (edit) + trash (delete) icons |

Commented out in OLD (intentionally hidden from list):
- `templateCode` raw field (hidden; code resolved to name via custom template)
- `templateTypeName`
- `subject`
- `description`

NEW should show the same visible set: Template Code name + Email Body + SMS Body
+ Status + Action. Email Body column may truncate to ~80 chars with a "show more"
tooltip since bodies are long HTML strings.

OLD source: `list/template-list.component.html:32-56`

## 5. Add Modal (`TemplateAddComponent`)

Header: "Template Details" (no subtitle)

```
+-----------------------------------+
| Template Details            [X]  |
+-----------------------------------+
| Template Code [select]           |
| Subject       [readonly text]    |
| Description   [text]             |
| Body SMS      [textarea]         |
+-----------------------------------+
| [Save]          [Cancel]         |
+-----------------------------------+
```

### 5a. Form Fields (Add)

**Template Code select (required):**
- Label: "Template Code"
- `<select>` bound to `TEMPLATE_CODE_LIST` (16 codes for DB-managed templates)
- `(change)="setSubject()"` -- auto-fills Subject from the selected code's fixed
  subject string
- Default: "Select Template Code" (disabled placeholder)

In NEW: `ng-select` bound to the full unified list of 59 template codes.
The display name replaces the OLD `templateCodeName` strings with the corrected
names from the parity audit (e.g., "Rejected Joint Declaration Document" not
"Rejected Join Declaration Document").

**Subject (auto-filled, readonly):**
- Label: "Subject"
- `type="text"`, `readonly`
- Pre-filled by `setSubject()` when Template Code is selected
- Not user-editable in Add flow

**Description:**
- Label: "Description"
- `type="text"`, editable
- No max length in OLD

**Body SMS:**
- Label: "Body SMS"
- `<textarea>`, control `bodySms`
- Plain text

**Body Email (COMMENTED OUT in OLD -- NEW adds it):**
- OLD: TinyMCE `<editor>` with TinyMCE API key `0glqyfe3m14pnnt38oz544qwfurnhnq1l0dimaxvhqf9svv7`
  was commented out in OLD HTML (line 50-66 of add component). No email body
  editor existed in OLD's UI. Email bodies came from seed data and were
  not editable from the portal.
- NEW: surface a `<textarea>` (plain HTML or basic rich-text via Angular Material
  + a simple WYSIWYG if available). At minimum, raw HTML textarea so IT Admin
  can paste / edit email bodies. See Exception 1.

**Buttons:** Save (`btn btn-primary`) / Cancel (`btn btn-secondary`)
- Save: `[disabled]="!templateFormGroup.valid"`

**Template Type select (commented out in OLD):**
- Not shown in ADD. OLD silently defaults to templateTypeId=1 (Email). NEW should
  set it based on context (all NEW templates support both Email + SMS bodies in
  one record, following NEW's unified model).

OLD source: `add/template-add.component.html:1-75`

## 6. Edit Modal (`TemplateEditComponent`)

Header: "Template Details" (no subtitle)

```
+-----------------------------------+
| Template Details            [X]  |
+-----------------------------------+
| Template Code [disabled select]  |
| Subject       [disabled text]    |
| Description   [text, editable]   |
| Body SMS      [textarea]         |
| Body Email    [textarea -- NEW]  |
| Is Active     [checkbox]         |
+-----------------------------------+
| [Update]        [Cancel]         |
+-----------------------------------+
```

### 6a. Form Fields (Edit -- diff from Add)

- **Template Code:** DISABLED (cannot change the event a template is for)
- **Subject:** DISABLED (auto-filled from code; NOT user-editable even in edit)
  - OLD source: `edit/template-edit.component.html:36` (`disabled`)
  - See Exception 2 below -- NEW should allow editing Subject
- **Is Active checkbox:** NEW field, not in Add modal
  - Label: "Is Active"
  - Bound to `statusId`; checked when `statusId == statusTypeEnums` (Active enum)
  - `(change)="statusChange()"` on toggle
  - OLD source: `edit/template-edit.component.html:73-79`

Buttons: Update (`btn btn-primary`) / Cancel (`btn btn-secondary`)

OLD source: `edit/template-edit.component.html:1-91`

## 7. Delete

Delete does NOT use the `TemplateDeleteComponent` (that file contains only
`<h1>Template</h1>` -- it's a shell never completed in OLD). Instead, OLD
calls `dialog.confirmation()` inline with the template code as the
confirmation text, then calls `service.delete(templateId)`.

NEW: same pattern -- inline confirmation dialog from the Action column's
delete icon. No separate delete component needed.

Confirmation text: shows the template code name.

OLD source: `list/template-list.component.ts:67-79`

## 8. Buttons

| Button | Context | OLD class | Disabled |
|---|---|---|---|
| Add + | List page header | `btn btn-primary btn-sm` | never |
| Edit (pencil) | Action column | `.oi.oi-pencil` icon | never |
| Delete (trash) | Action column | `fas fa-trash-alt` icon | never |
| Save | Add modal | `btn btn-primary` | `!templateFormGroup.valid` |
| Cancel | Add modal | `btn btn-secondary` | never |
| Update | Edit modal | `btn btn-primary` | `!templateFormGroup.valid` |
| Cancel | Edit modal | `btn btn-secondary` | never |

OLD source: `list/template-list.component.html:25`, `edit/template-edit.component.html:85`

## 9. Role Visibility Matrix

| Role | Access |
|---|---|
| IT Admin | Full CRUD (list, add, edit, delete) |
| Staff Supervisor | View list only (no add/edit/delete) -- NEW design decision; see Exception 3 |
| Clinic Staff | No access |
| External users | No access |

OLD uses `PageAccess` guard (`applicationModuleId: 19`). In OLD, the Add button
renders for all authenticated users who reach the page -- role restriction is
at the route guard level, not the button level. NEW should hide Add/Edit/Delete
buttons from non-IT-Admin roles.

## 10. Branding Tokens

No explicit brand-color on modal headers in OLD (close button has no `text-white`,
unlike other modals). However, consistent treatment across the app means the
modal-header background uses `--brand-primary` in the Lighthouse Theme.
Apply `--brand-primary` header in NEW for consistency.

## 11. NEW Stack Delta

1. **Unified template code list:** The NEW `NotificationTemplate` entity covers
   all 59 template codes (16 DB-managed + 43 disk HTML migrated). The Add
   modal's Template Code select should show all 59 codes. OLD only showed 16 in
   the DB-managed UI; the other 43 were never user-editable.

2. **Subject editable in Edit:** OLD's edit modal has `disabled` on Subject.
   In NEW, allow editing Subject (it's stored in DB per template row). Remove
   the `disabled` attribute. See Exception 2.

3. **Body Email editable:** Surface a `<textarea>` (or rich-text editor) for
   `bodyEmail` in both Add and Edit modals. OLD commented this out; the
   field exists in the entity and is used at send time.

4. **Variable reference panel:** Add a collapsible help section listing the
   `##VariableName##` substitution tokens available for each template code.
   This is a NEW addition (OLD had no in-UI help). Keep it minimal:
   a simple static mapping of code -> variable list.

5. **Pagination:** `PaginationSettingEnums.RowCount` rows per page in OLD.
   Preserve server-side pagination in NEW with Angular Material paginator.

6. **ABP proxy:** After finalizing `NotificationTemplateDto` surface, run
   `abp generate-proxy` to regenerate `angular/src/app/proxy/notification-templates/`.

## 12. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Body Email not editable in UI | TinyMCE editor commented out in both add + edit HTML | Surface textarea/editor for `bodyEmail` | The field exists in DB and is critical for notification behavior; hiding it from IT Admin was an incomplete implementation in OLD, not a deliberate design choice |
| 2 | Subject field disabled in Edit | `disabled` attribute on Subject input in edit modal | Allow editing Subject in NEW | Subject is per-template data in DB; no reason to freeze it once set. OLD's disabled state may have been a usability mistake -- template codes map to specific events, not fixed subjects. Flag this with `// PARITY-FLAG` until Adrian confirms |
| 3 | Role restriction at route only | OLD: any user who reaches the route sees the Add button | NEW: hide Add/Edit/Delete from non-IT-Admin roles at component level | Defense in depth: role should be checked at both route and component level |
| 4 | Template Code display name "Rejected Join Declaration Document" | Missing 't' in "Join" | "Rejected Joint Declaration Document" | Typo fix per parity audit 2026-05-03 |
| 5 | Template Code display name "Appointment Approved Stackholder Emails" | "Stackholder" misspelling | "Appointment Approved Stakeholder Emails" | Typo fix per parity audit 2026-05-03 |
| 6 | Delete component is a stub | `template-delete.component.html` contains only `<h1>Template</h1>` | Use inline `dialog.confirmation()` same as OLD's `deleteTemplate()` method | Completing OLD's unfinished stub |
| 7 | No email body in list column | `bodyEmail` column shows raw HTML | Truncate to first ~80 chars, strip HTML tags for display | Email bodies are multi-KB HTML; raw display is unusable. Behavior matches intent |

## 13. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `list/template-list.component.html` | 1-65 | Full list page |
| `list/template-list.component.html` | 24-55 | Card header (Add button) + table |
| `list/template-list.component.ts` | 51-79 | Add/edit/delete popup launchers |
| `list/template-list.component.ts` | 111-116 | `getTemplateCode()` code->name resolver |
| `add/template-add.component.html` | 23-31 | Template Code select (with `setSubject()`) |
| `add/template-add.component.html` | 36-45 | Subject (readonly) + Description |
| `add/template-add.component.html` | 46-66 | Body SMS textarea + Body Email (commented out) |
| `edit/template-edit.component.html` | 22-31 | Template Code select (disabled) |
| `edit/template-edit.component.html` | 33-42 | Subject (disabled) + Description |
| `edit/template-edit.component.html` | 47-50 | Body SMS |
| `edit/template-edit.component.html` | 73-79 | Is Active checkbox |
| `const/template-code-list.ts` | 1-66 | All 16 DB-managed template codes with display names |
| `docs/parity/it-admin-notification-templates.md` | all | Full parity audit (template code matrix, Phase 18 implementation, unified 59-code list) |

## 14. Verification Checklist

- [ ] IT Admin navigates to `/notification-templates` and sees the list
      (Template Code name, Email Body preview, SMS Body, Status, Action)
- [ ] Search filters the list
- [ ] "Add +" button opens the Add modal
- [ ] Template Code select shows all 59 unified codes (16 original DB-managed +
      43 disk-HTML codes, now DB-stored)
- [ ] Selecting a Template Code auto-fills Subject field
- [ ] Subject is read-only in Add modal
- [ ] Description and Body SMS are editable
- [ ] Body Email textarea is present and editable (NEW addition)
- [ ] Save creates the template; list refreshes
- [ ] Edit pencil opens Edit modal with current values pre-filled
- [ ] Template Code and Subject fields -- verify with Adrian whether Subject should
      remain disabled or become editable (PARITY-FLAG)
- [ ] Description, Body SMS, Body Email are editable in Edit
- [ ] Is Active checkbox state pre-filled; toggling changes status
- [ ] Update saves changes; list refreshes
- [ ] Delete icon opens confirmation dialog with template code name
- [ ] Confirming delete removes the template; list refreshes
- [ ] Non-IT-Admin role does not see Add/Edit/Delete controls
- [ ] After editing the "Appointment Approved" template body, approving an
      appointment sends the new email body (integration test)
- [ ] `##VariableName##` placeholders in body are substituted at send time
