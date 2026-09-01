---
id: BUG-037
title: Clinic Staff role returns HTTP 403 on POST /api/app/appointments/{id}/documents
severity: medium
status: open
found: 2026-05-23 hardening HRD-P7.3
flow: appointment-document-upload
component: src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/AppointmentDocuments/AppointmentDocumentsController.cs + permission policy
---

# BUG-037 - Clinic Staff cannot upload documents to appointments

## Symptom

P7.3 scenario - clinic staff uploads PDF to an approved appointment they
booked. Failed with HTTP 403.

Steps:
1. Logged in as `clistaff1@gesco.com` (Clinic Staff role).
2. Navigated to `/appointments/view/<A00005-id>` (the appointment
   clistaff1 booked themselves; auto-approved at create-time per
   BUG-030's fix).
3. Filled `Document Name` field: `P7.3 - hardening clinic-staff upload`.
4. Clicked the `File` button to open the chooser, selected `test.pdf`.
5. Clicked the `Upload` button.

Observed:

```
[browser console]
Failed to load resource: the server responded with a status of 403 (Forbidden)
  @ http://falkinstein.localhost:44327/api/app/appointments/42a6f114-d44c-cadc-0d87-3a2168443cdf/documents:0
```

The DB has no new `AppAppointmentDocuments` row for this appointment.

Counter-evidence (the same flow works for other roles):
- P7.1 patient1 uploads PNG to A00001 -> 200, row created (IsAdHoc=1,
  image/png).
- P7.2 appatty1 (AA) uploads PDF to A00002 -> 200, row created
  (IsAdHoc=1, application/pdf).

So the upload pipeline works; the failure is specific to the
Clinic-Staff role.

## 2026-05-23 ROOT CAUSE (post-diagnosis)

Confirmed: `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUserRoleDataSeedContributor.cs` is the seeder that grants `CaseEvaluation.AppointmentDocuments.Create` (and related Document permissions) to roles. Its `SeedAsync` (lines 31-66) loops over `{ "Patient", "Claim Examiner", "Applicant Attorney", "Defense Attorney" }` ONLY (line 60). The `BookingBaselineGrants()` enumeration (line 88+) includes `$"{Group}.AppointmentDocuments.Create"` (line 110).

The class name is literally `ExternalUserRoleDataSeedContributor` -- it only seeds external roles. **There is no corresponding `InternalUserRoleDataSeedContributor`** (grep confirms no parallel seeder exists). Clinic Staff and Staff Supervisor (defined as internal roles) inherit nothing from this contributor.

Result: the `[Authorize(CaseEvaluationPermissions.AppointmentDocuments.Create)]` attribute on `AppointmentDocumentsAppService.UploadStreamAsync` (line 146) returns 403 for ClinicStaff because the permission is unset for that role.

Suspected fix paths:
- Add `"Clinic Staff"` and `"Staff Supervisor"` to the seed loop on line 60. They should get the same Document grants as external roles AND additionally `.Edit`, `.Delete`, `.Approve` (which external roles intentionally don't have).
- Or create a parallel `InternalUserRoleDataSeedContributor` with the appropriate grants. Cleaner separation of concerns.
- Verify the IT Admin role also has these grants (admin@ uses, but as a system role with all-permissions, this might already be the case).

## Hypothesis (3 in priority order)

1. **Missing permission on Clinic Staff role** (most likely) - the
   permission `CaseEvaluation.Appointments.UploadDocument` (or similar)
   is granted to Patient + Attorney roles but not to Clinic Staff.
   Check `CaseEvaluationPermissionDefinitionProvider.cs` for the
   permission definition + the role default grants.
2. **Authorization filter mismatch** - the documents controller may
   have an attribute like `[Authorize(Policy = "ExternalUser")]` that
   intentionally excludes internal roles. Check
   `AppointmentDocumentsController` and the registered policies.
3. **Scope check in app service** - the `AppointmentDocumentsAppService`
   may include an `EnsureCallerOwnsAppointment()` style check that
   returns false for Clinic Staff who is not on the appointment as
   AA/DA/CE/Patient. This would be intentional but is inconsistent
   with the expectation that internal staff can act on behalf of any
   appointment.

## Recommended next step

1. Confirm which hypothesis applies by inspecting:
   - `CaseEvaluationPermissionDefinitionProvider.cs`
   - `AppointmentDocumentsController.cs`
   - `AppointmentDocumentsAppService.UploadAsync` (or
     `CreateAsync` - whatever the action method is)
2. If permission is the issue: grant the appropriate permission to
   ClinicStaff + StaffSupervisor roles via
   `CaseEvaluationDataSeedContributor` or admin UI.
3. Add a runbook P7.3 expected outcome that includes the IsAdHoc=0
   flag (internal uploads).
4. Add an xUnit test to `AppointmentsAppServiceTests` covering each
   role's upload permission.

## Repro for fix verification

```javascript
// In a browser logged in as clistaff1:
const token = localStorage.getItem('access_token');
const formData = new FormData();
formData.append('file', new Blob(['test'], { type: 'application/pdf' }), 'test.pdf');
formData.append('documentName', 'test from clistaff1');
fetch('http://falkinstein.localhost:44327/api/app/appointments/<approved-id>/documents', {
  method: 'POST',
  headers: { 'Authorization': 'Bearer ' + token },
  body: formData
}).then(r => r.status);  // Currently 403; expected 200 after fix.
```

## Functional impact

Medium severity. Clinic Staff is supposed to be able to upload documents
on behalf of an appointment for internal record-keeping (e.g.,
attaching scanned forms received in the mail). The 403 blocks this
entire workflow path for internal staff.

External users (Patient + AA + DA + CE) can upload, so the core feature
works - just not for the role most likely to need it for back-office
work.

## Related

- HRD-P7.3 (the scenario that surfaced this).
- BUG-030 (internal-staff auto-approve - now fixed; related internal
  flow surface).
- OBS-31 (internal-staff booker no redirect - related internal-flow UX).
- OBS-20 (Playwright DataTransfer driver limit - partially obsoleted
  by the working `browser_file_upload` MCP tool path).
- Prior 2026-05-21 state file referenced a BUG-031 about
  `/api/app/appointment-injury-details` returning 403 for clinic-staff;
  that finding was never persisted as a file but may share root cause
  (missing role permissions for clinic-staff on related endpoints).
