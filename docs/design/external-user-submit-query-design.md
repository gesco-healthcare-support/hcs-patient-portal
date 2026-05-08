---
feature: external-user-submit-query
date: 2026-05-04
phase: 2-frontend (NOT YET IMPLEMENTED in NEW; decision pending -- Adrian must confirm if UserQuery is in scope for Phase 1 demo)
status: draft
old-source: patientappointment-portal/src/app/components/user-query/user-queries/add/user-query-add.component.ts + .html
new-feature-path: n/a (UserQuery entity, AppService, and Angular component do not yet exist in NEW)
shell: external-user-authenticated (top-bar; modal overlay from top-nav Help link or appointment-view Help button)
screenshots: pending
---

# Design: External User -- Submit Query (Help / Contact Us)

## Overview

External users can submit a free-form help question at any time via a modal popup.
There are two entry points:
1. **Global Help link** in the top navigation bar (free-form query with no appointment context).
2. **Help button** on the appointment-view page (appointment-context query, pre-fills
   the confirmation number).

In OLD, submitting a query stores the message in a `UserQueries` table and sends an email
to either the appointment's responsible user (appointment-context) or all IT Admin users
(free-form). There is no inbox or reply tracking -- it is send-only.

In NEW, **this feature is not yet implemented**. A decision is pending on whether to
port the full send-only flow or replace it with a `mailto:` link. This doc captures
the OLD behavior as the design contract pending that decision.

**Action required before Phase 19b UI build:** Adrian must confirm the UserQuery
implementation approach (see parity doc question Q11).

---

## 1. Routes

No dedicated route. The submit-query form is a modal popup, not a page.

OLD entry points:
- Global top-nav: `onAddQuery()` → `RxPopup.show(UserQueryAddComponent, {})`
- Appointment-view: `onAddQuery()` → `RxPopup.show(UserQueryAddComponent, { appointmentId, isAskConfirmationNumber: true, requestConfirmationNumber })`

NEW entry points (to be implemented):
- Global top-nav Help icon → `MatDialog.open(SubmitQueryDialogComponent, {})`
- Appointment-view Help button → `MatDialog.open(SubmitQueryDialogComponent, { data: { appointmentId, confirmationNumber } })`

---

## 2. Shell

Modal overlay. Visible from both external-user and internal-user shells (the Help link
should be available to all authenticated users).

---

## 3. OLD Query Modal

```
+---------------------------------------------+
| Need Question?                        [X]   |
| "If you have any further inquiries,         |
|  please feel free to contact us by          |
|  sending your question. We will get back    |
|  to you soon."                              |
+---------------------------------------------+
| Confirmation Number   [readonly text]       |
| (only shown when isAskConfirmationNumber)   |
|                                             |
| Please write down your question below.      |
| [textarea -- required, max 500 chars]       |
+---------------------------------------------+
| [Send]        [Close]                       |
+---------------------------------------------+
```

**Fields:**
- Confirmation Number: read-only, pre-filled from appointment context.
  Hidden (`*ngIf="isAskConfirmationNumber"`) in free-form mode.
- Message: required textarea, max 500 characters.

**Send button:** disabled when `message` is empty or invalid.

OLD source: `user-queries/add/user-query-add.component.html:1-34`

---

## 4. OLD Submit Flow

1. `POST /api/UserQueries` with body:
   ```json
   { "userId": int, "message": string, "appointmentId": int?, "requestConfirmationNumber": string? }
   ```
2. Backend stores in `UserQueries` table: `UserQueryId`, `UserId`, `Message`, `CreatedDate`, `CreatedById`.
3. Email routing:
   - **With appointmentId**: looks up `PrimaryResponsibleUserId` from the appointment; sends email to that user.
   - **Without appointmentId** (free-form): sends email to all users with `RoleId == ITAdmin` (semicolon-separated blast).
4. Email subject: `Patient Appointment Portal - (Patient: {FirstName} {LastName} - Claim: {claimNumber} - ADJ: {adjNumber}) - User query`
5. Email body: `EmailTemplate.UserQuery` HTML template.
6. Success toast: query submitted confirmation.
7. No reply tracking; no inbox UI.

**PHI in email subject:** OLD includes patient name and claim number in the subject line.
This should NOT be replicated in NEW (PHI must not appear in email subjects). See Exception 1.

OLD source: `user-queries/add/user-query-add.component.ts:73-86`,
`PatientAppointment.Api/Controllers/UserQueriesController.cs`

---

## 5. NEW Implementation Options

**Option A -- Port full send-only flow (recommended if UserQuery is in scope):**
1. Create `UserQuery` entity + EF migration.
2. Create `IUserQueriesAppService` + `UserQueriesAppService` with `SubmitQueryAsync`.
3. Create Angular `SubmitQueryDialogComponent` (MatDialog -- matches OLD's popup).
4. Wire email handler to `UserQuerySubmittedEto`.
5. Update email subject to exclude PHI (Exception 1).
6. Connect global Help icon in top-nav and appointment-view Help button.

**Option B -- Replace with `mailto:` link (zero backend work):**
1. Configure `supportEmail` in `SystemParameters`.
2. Help icon opens `mailto:{supportEmail}?subject=Help%20Request` (pre-populated subject, no PHI).
3. No UserQuery entity, no storage, no email templates.
4. Simpler but loses the audit trail (no `UserQueries` table).

**Decision gate:** Present both options to Adrian before beginning Phase 19b UI work.

---

## 6. Role Visibility Matrix

| Role | Submit query | View queries (admin) |
|---|---|---|
| Patient / Adjuster / Attorney | Yes | No |
| Claim Examiner | Yes | No |
| Clinic Staff | Yes (optional -- confirm with Adrian) | No |
| Staff Supervisor | Yes (optional) | No |
| IT Admin | Optional | Yes (receives email blast for free-form queries) |

---

## 7. Branding Tokens

| Element | Token |
|---|---|
| Modal header | `--brand-primary` (consistent with other modals) |
| Send button | `btn-primary` via `--brand-primary` |
| Help icon in nav | `--brand-primary` icon color |
| Error/validation text | `--status-rejected` (red) |

---

## 8. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | PHI in email subject | Subject includes patient name + claim number + ADJ number | Subject must NOT include PHI | HIPAA compliance; patient-identifiable data must not appear in email subject lines. Use generic subject like "Patient Portal Help Request" |
| 2 | IT Admin email blast | Sends to all IT Admin users when no appointment context | Replace with configured `supportEmail` from `SystemParameters` | Prevents unintended PHI exposure to all IT Admins; a single support inbox is better practice |
| 3 | Send-only / no reply | No inbox or tracking UI | Same (if Option A) | Parity; reply tracking is out of scope for Phase 1 |
| 4 | `RxPopup` overlay | In-house `RxPopup.show()` modal | `MatDialog.open()` (Angular Material CDK) | Framework replacement; visually equivalent |

---

## 9. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `user-queries/add/user-query-add.component.html` | 1-34 | Modal form layout (header, confirmation number, message textarea, buttons) |
| `user-queries/add/user-query-add.component.ts` | 73-86 | `submitQuery()` -- POST + toast |
| `appointments/edit/appointment-edit.component.ts` | 1474-1476 | `onAddQuery()` with appointment context |
| `docs/parity/external-user-submit-query.md` | all | Audit: endpoint contract, email routing, send-only decision |

---

## 10. Verification Checklist

*(Pending implementation -- verify once Option A or B is chosen)*

- [ ] Help icon visible in top navigation bar for authenticated external users
- [ ] Clicking global Help icon opens query modal with single Message textarea
- [ ] Clicking appointment-view Help button opens modal with pre-filled Confirmation Number (readonly)
- [ ] Message field is required; Send button disabled when empty
- [ ] Message max 500 characters enforced; excess input truncated or rejected
- [ ] Submitting sends email to the appointment's responsible user (appointment-context)
- [ ] Submitting free-form query sends email to configured support email (NOT PHI subject)
- [ ] Success toast confirms query was submitted
- [ ] Modal closes after successful submission
- [ ] Query is stored in `UserQueries` table with UserId, Message, and CreatedDate
- [ ] Email subject does NOT contain patient name, claim number, or any PHI
