---
title: Role-vs-permission matrix audit + demo embarrassment list
date: 2026-05-25
status: ready
audience: Adrian (presenter)
sources:
  - src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs
  - src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs
  - src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs
  - src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUserRoleDataSeedContributor.cs
---

# Permission Matrix Audit

Audit of the 7 roles' permission seeding for Tuesday demo. Full code
review by subagent 2026-05-25.

**Role abbreviations:** ITA = IT Admin, SS = Staff Supervisor, CS =
Clinic Staff, PT = Patient, AA = Applicant Attorney, DA = Defense
Attorney, CE = Claim Examiner.

## Surprises / unintentional grants

1. **DoctorPreferredLocations registered but seeded to no role.**
   Per `CaseEvaluationPermissions.cs:295-299` the docstring says
   "IT Admin / Staff Supervisor toggles" but neither receives the
   grant. If any UI page queries this endpoint, all callers 403.
   Verified locally: the booking form's Location dropdown queries
   `/api/app/appointments/location-lookup` (Locations.Default,
   which IS granted), not DoctorPreferredLocations. Demo path is
   unaffected. Outside the demo, this is likely a regression.

2. **All 4 external roles get Create+Edit on the master
   ApplicantAttorneys and DefenseAttorneys tables.** A Patient can
   create/edit a Defense Attorney master record. Per-record
   ownership filtering at the AppService layer is the only
   protection. Acknowledged in seeder header comments.

3. **All 4 external roles get the same BookingBaselineGrants.**
   Zero permission-level distinction between Patient, Applicant
   Attorney, Defense Attorney, Claim Examiner. Documented as
   "AppService-layer ownership filter is the only protection."

4. **Clinic Staff has AppointmentInjuryDetails.Create but not
   .Edit.** Multi-injury edit during a re-book will 403 for CS.

5. **No role holds Dashboard.Tenant from the host pass.** IT Admin
   operating cross-tenant cannot see the tenant dashboard. By
   design but worth noting.

6. **External roles never get AppointmentChangeLogs.Default.** They
   cannot see audit history on their own appointment.

## Demo-script flow verification

| Flow | Required perm | Holders | Match? |
|---|---|---|---|
| Issue invite | UserManagement.InviteExternalUser | ITA, SS, CS | yes |
| Tenant dashboard | Dashboard.Tenant | SS, CS | yes |
| Approve / Reject appointment | Appointments.Approve / .Reject | ITA, SS, CS | yes |
| Approve / Reject change request | AppointmentChangeRequests.Approve / .Reject | ITA, SS only | **MISMATCH if demo shows CS approving cancellation** |
| Upload document | AppointmentDocuments.Create | ITA, SS, all 4 external | yes |
| Approve uploaded document | AppointmentDocuments.Approve | ITA, SS, CS | yes |
| Patient creates appointment | BookingBaselineGrants set | all 4 external | yes |

## Potential demo embarrassments (ranked by likelihood)

1. **Patient clicks "Dashboard" in nav -> 403.** External users have
   no Dashboard.Tenant. Confirm external-portal default route does
   not point at `/dashboard`. (Patient lands at `/` -> `Home`
   component, verified live tonight.)

2. **Doctor-Location preference dropdown empty.** Not seeded; if
   any UI queries `/api/app/doctor-preferred-locations`, callers
   401/403. Booking form does NOT use this endpoint (verified) so
   the demo flow is unaffected.

3. **Clinic Staff opens change-request inbox, clicks Approve ->
   403.** Only ITA + SS hold AppointmentChangeRequests.Approve.
   Either hide the Approve button on CS view or demo this as SS.

4. **IT Admin clicks "Delete" on an appointment -> works, hard
   delete.** Only ITA has Appointments.Delete. If demo accidentally
   triggers, no recovery short of DB restore. **Mitigation: do not
   sign in as IT Admin on the production-like demo DB.**

5. **Tenant `admin` static role auto-grants every tenant
   permission, including Delete.** ABP auto-feature. If demo signs
   in as `admin` instead of a custom role, hard-delete is one click
   away. **Mitigation: do not use admin login for the demo.**

6. **Audience: "What stops a Defense Attorney from editing the
   Applicant Attorney master record?"** Nothing in permissions.
   Per-record AppService filter is the only gate. Standard
   answer: "Permission alone is broad to match OLD's no-Authorize
   behavior on booking endpoints. Per-record ownership filter at
   the AppService is the actual gate." (Worth verifying that gate
   exists for these specific entities before saying so.)

7. **Patient clicks "Audit History" on their own appointment ->
   403.** External roles never have AppointmentChangeLogs.Default.
   Hide the tab for external roles.

8. **Staff Supervisor tries to create a new internal Clinic Staff
   account -> 403.** InternalUsers.Create is IT-Admin-only. If
   demo script claims "supervisor onboards a receptionist," it
   requires IT Admin.

9. **Patient/external user has no .Delete on AppointmentXxx
   subtables.** If a patient mistakenly attaches the wrong
   attorney, they cannot remove the link -- must call the office.

## Pre-demo decisions

- Do not log in as `admin` static role during demo. Use the named
  internal roles (stafsuper1, clistaff1).
- If demoing change-request approval (Reschedule / Cancel),
  perform as stafsuper1, not clistaff1.
- Patient demo accounts should land at `/` (home), not `/dashboard`.
- Hide Appointments.Delete UI in production (out of scope for
  Tuesday but worth noting).
