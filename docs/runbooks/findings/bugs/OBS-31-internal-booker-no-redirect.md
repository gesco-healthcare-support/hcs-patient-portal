---
id: OBS-31
title: Internal-staff booking via /appointments/add does not redirect after Book; appointment is created silently
severity: observation
status: open
found: 2026-05-23 hardening HRD-P3.5
flow: booking-internal-vs-external
component: angular/src/app/appointments/appointment-add.component.ts (Book button handler)
---

# OBS-31 - Internal staff booker: no redirect after successful submit

## Symptom

P3.5 scenario - `clistaff1` (Clinic Staff) deep-links to
`/appointments/add` (per OBS-19 the click-nav "+ New Appointment Request"
opens the admin CRUD modal, NOT this form; deep-linking is required).

After filling all sections + claim info + clicking `Book an appointment`:

- **External roles** (Patient, AA, DA, CE): redirect to `/` with the
  appointment created. Confirmed for HRD-P3.1 through P3.4.
- **Internal Clinic Staff** (HRD-P3.5): the form does NOT redirect. The
  user sits on `/appointments/add` with the form still populated and no
  visible feedback. The appointment IS created in DB (verified as
  A00005 with Status=2, immediately approved per BUG-030's now-fixed
  behavior).

## Differential observations

A00005 was created at 2026-05-23T18:27:14Z with:

- AppointmentStatus = 2 (Approved at create-time per internal-staff
  fast-path)
- AppointmentApproveDate populated correctly (BUG-030 fix verified)
- All 4 party emails populated (AA / DA / CE / Patient)
- 2 packets generated (Kind=1 + Kind=2)

So the booking itself worked. Only the UI feedback is missing.

## Why this is observation-worthy

Two paths a fix could take:

- **Same redirect for everyone**: internal staff sees `/` (the same
  dashboard external users see). Could be confusing for internal-staff
  users who expect to land on the new appointment's `/view/<id>` page.
- **Internal staff redirects to the new appointment's view page**:
  `/appointments/view/<id>` makes more sense for the internal workflow
  - they can immediately upload documents, see packets, etc.

## Recommended next step

In `appointment-add.component.ts` Book handler:

```ts
this.appService.createAsync(dto).subscribe(result => {
  // After successful create...
  if (this.currentRole === 'ClinicStaff' || ...) {
    this.router.navigate(['/appointments/view', result.id]);
  } else {
    this.router.navigate(['/']);
  }
  this.toastr.success('Appointment created.');
});
```

Either way, surface a toast / banner.

## Functional impact

Low. The appointment IS created. But the internal user has no idea -
they could click `Book` again, potentially generating duplicate
appointments or 409 conflict errors (BUG-008 pattern).

## Related

- OBS-19 (internal-booker uses admin CRUD modal - separate workstream).
- BUG-030 (internal-staff auto-approve - now fixed; ApproveDate is
  populated).
- BUG-008-pattern 409s observed on /api/app/patients/me when
  re-submitting rapidly (related to the silent-success).
