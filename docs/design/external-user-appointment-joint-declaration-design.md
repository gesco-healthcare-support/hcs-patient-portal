---
feature: external-user-appointment-joint-declaration
date: 2026-05-04
phase: 2-frontend (backend UploadJointDeclarationAsync + auto-cancel job done; Angular AppointmentDocumentsComponent handles display)
status: draft
old-source: patientappointment-portal/src/app/components/appointment-request/appointment-joint-declarations/
old-components:
  - list/appointment-joint-declaration-list.component.ts + .html (JDF list)
  - edit/appointment-joint-declaration-edit.component.ts + .html (upload form)
new-feature-path: angular/src/app/appointment-documents/appointment-documents.component.ts + .html (same component as package + ad-hoc docs)
shell: external-user-authenticated (top-bar; embedded in appointment-view page)
screenshots: pending
---

# Design: External User -- Appointment Joint Declaration Form (JDF)

## Overview

The Joint Declaration Form (JDF) is an **AME and AME-REVAL appointment-only** document
uploaded by the booking attorney (Applicant Attorney or Defense Attorney). It is a single
required document that must be submitted before the appointment date.

If the JDF is not uploaded before the auto-cancel cutoff (configured days before the
appointment DueDate), the appointment is automatically cancelled and stakeholder
notification emails are sent.

In OLD, JDF is a completely separate entity (`AppointmentJointDeclarations`) with its own
components. In NEW, it is unified as an `AppointmentDocument` with `IsJointDeclaration = true`,
displayed in the same `AppointmentDocumentsComponent`.

This doc covers the JDF-specific upload rules and visibility. The shared upload chrome is
described in `external-user-appointment-package-documents-design.md`.

---

## 1. Route

No dedicated route. JDF upload is embedded at `/appointments/view/:id` as part of the
`<app-appointment-documents>` component. The JDF document slot appears in the document list
automatically for AME/AME-REVAL appointments.

OLD route: `/appointment-joint-declarations/:appointmentId` (separate full page).

---

## 2. OLD JDF Page

In OLD, a separate `/appointment-joint-declarations/:appointmentId` page shows the JDF
upload form:

```
+-------------------------------------------------------+
| [H2] Joint Declaration Form       [Back]             |
+-------------------------------------------------------+
| [Upload section -- one file only]                    |
|   File input  [Choose File]                          |
|   [Upload]                                           |
+-------------------------------------------------------+
| [Current JDF status]                                 |
| Status | Uploaded By | Uploaded Date | Download      |
+-------------------------------------------------------+
```

Visible only to Applicant Attorney and Defense Attorney roles on AME appointments.
Internal users can view but not upload the JDF.

OLD source: `appointment-joint-declarations/list/` + `appointment-joint-declarations/edit/`
OLD domain: `AppointmentJointDeclarationDomain.cs` (322 lines)

---

## 3. Upload Gate (JDF-Specific)

An attorney can upload the JDF **only if**:

1. Appointment type is **AME or AME-REVAL**.
2. Appointment status is **Approved**.
3. Current date is **before the DueDate**.
4. The uploader has the **Applicant Attorney or Defense Attorney** role AND is the
   booking attorney for this appointment (`IsBookingAttorney` check).
5. JDF is not already Accepted (immutable once accepted).

Backend gate: `DocumentUploadGate.EnsureAme()` + `DocumentUploadGate.EnsureCreatorIsAttorney()` +
`DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate()` + `EnsureNotImmutable()`.

If the gate fails (wrong appointment type, wrong role, past due date), the upload is rejected
with an error toast.

---

## 4. Auto-Cancel Behavior

If the JDF remains in Pending status at the configured cutoff (N days before appointment DueDate),
a Hangfire background job automatically cancels the appointment:

1. Appointment status → `CancelledNoBill` (with cancellation reason noting JDF not submitted).
2. `AppointmentAutoCancelledEto` event fires → sends email to: Patient, Applicant Attorney,
   Defense Attorney, Responsible User, and any additional authorized users.

External users see the appointment as Cancelled in their list with the cancellation reason.

---

## 5. NEW Behavior

JDF document slot appears in `AppointmentDocumentsComponent` for AME/AME-REVAL appointments.
The slot is labelled "Joint Declaration Form" and has a distinct status badge.

Staff approve/reject the JDF using the same Approve / Reject buttons as other documents.
Approval is recommended before the due date to avoid the auto-cancel job.

---

## 6. Role Visibility Matrix

| Role | See JDF slot | Upload JDF | Download | Approve/Reject |
|---|---|---|---|---|
| Patient | No (Patient cannot upload JDF) | No | No | No |
| Adjuster / Claim Examiner | No | No | No | No |
| Applicant Attorney (booking) | Yes | Yes (if gates pass) | Yes | No |
| Defense Attorney (booking) | Yes | Yes (if gates pass) | Yes | No |
| Clinic Staff | Yes (view only) | No | Yes | Yes |
| Staff Supervisor | Yes | No | Yes | Yes |
| IT Admin | Yes | Yes (override) | Yes | Yes |

**JDF section is hidden entirely** for Patient, Adjuster, and Claim Examiner roles.
Only booking attorneys see the upload option; internal staff see it read-only.

---

## 7. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Separate page | Dedicated `/appointment-joint-declarations/:id` page | Embedded in appointment-view alongside other docs | Architecture improvement |
| 2 | Separate entity | `AppointmentJointDeclarations` table | `AppointmentDocument` with `IsJointDeclaration = true` | Simplification; same functional behavior |
| 3 | Rejection status bug | OLD only set `RejectedById` on rejection; never updated `DocumentStatus` | NEW sets `DocumentStatus = Rejected` AND `RejectedByUserId` | Clear bug fix (document status was never updated on rejection in OLD) |
| 4 | File type | Word docs only (.doc/.docx) | No extension restriction in UI; server validates | Same as other document uploads |
| 5 | File size | ~1MB (OLD bug) | 25MB | Known bug fix |

---

## 8. OLD Source Citations

| File | Content |
|---|---|
| `appointment-joint-declarations/list/appointment-joint-declaration-list.component.html` | JDF list with status + download |
| `appointment-joint-declarations/edit/appointment-joint-declaration-edit.component.ts` | Upload form + role check |
| `docs/parity/external-user-appointment-joint-declaration.md` | Full parity audit: AME gate, attorney check, auto-cancel job, status bug fix |

---

## 9. Verification Checklist

- [ ] JDF section is visible on AME and AME-REVAL appointments only
- [ ] JDF section is hidden for Patient, Adjuster, and Claim Examiner roles
- [ ] Booking attorney (Applicant or Defense) sees the JDF upload button
- [ ] JDF upload is rejected if appointment type is not AME/AME-REVAL
- [ ] JDF upload is rejected if uploader is not the booking attorney
- [ ] JDF upload is rejected if appointment is not in Approved status
- [ ] JDF upload is rejected if current date is past the DueDate
- [ ] JDF upload is rejected if a JDF is already Accepted
- [ ] Successful JDF upload changes status to Uploaded (yellow badge)
- [ ] Staff approves JDF: status changes to Approved (green badge)
- [ ] Staff rejects JDF: status changes to Rejected (red badge) + reason shown
- [ ] Auto-cancel job fires when JDF is still Pending at cutoff date
- [ ] Cancelled appointment (auto-cancel) shows cancellation reason to external user
- [ ] Stakeholder emails sent on auto-cancel (Patient, both attorneys, Responsible User)
- [ ] Non-AME appointments do not show a JDF section
