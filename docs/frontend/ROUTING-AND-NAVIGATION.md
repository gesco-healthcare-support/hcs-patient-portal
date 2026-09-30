# Routing & Navigation

> Purpose: Documents the Angular route table, the guards, the internal sidebar, and where a route's
> permission check comes from. Audience: frontend developers.

[Home](../index.md) > [Frontend](./) > Routing & Navigation

## Overview

Three separate files decide what a user can reach and see:

| Concern | Source |
|---------|--------|
| Which URLs exist, and their guards | `angular/src/app/app.routes.ts` (plus the per-feature `*-routes.ts` files it mounts as children) |
| The internal staff sidebar | `angular/src/app/shared/components/internal-shell/internal-nav.config.ts` |
| ABP menu registrations (route providers) | `angular/src/app/**/providers/*-base.routes.ts`, registered by the route providers in `app.config.ts` |

No layout renders the ABP menu registrations any more -- the LeptonX layout is gone -- but ABP's
`permissionGuard` still reads them (see [Where a route's permission comes from](#where-a-routes-permission-comes-from)).

## How the route table is shaped

`APP_ROUTES` is matched top to bottom:

1. **`/`** -- `postLoginRedirectGuard` (a `canMatch` guard) runs before the home chunk loads.
2. **Public pages** -- no guard at all; each page authorizes itself with the code or token in its URL.
3. **External-only pages** -- declared before the shell, gated by `canMatch: [externalUserOnlyMatchGuard]`.
   Two of them (`appointments/view/:id` and `appointments/request`) share their path with an in-shell copy:
   an external user matches the chrome-less copy here, and everyone else falls through to the shell's copy.
4. **External profile pages** -- the three `user-management/*/my-profile` routes, outside the shell.
5. **The internal shell** -- one parent route (`path: ''`) rendering `InternalShellLayoutComponent`, gated by
   `canMatch: [internalUserOnlyMatchGuard]` and `authGuard`, with every staff page as a child.
6. **`**`** -- the branded `NotFoundComponent`. It must stay last.

Re-derive the counts below with `grep -c "path:" angular/src/app/app.routes.ts` (62 on 2026-09-28) and
`grep -oE "(canMatch|canActivate|canDeactivate): \[[^]]*\]" angular/src/app/app.routes.ts | sort | uniq -c`.

## Route reference (as of 2026-09-28)

"Policy" is the route's `data.requiredPolicy`. Where the route has none, the policy shown in brackets
comes from the menu registration (see below).

### Outside the shell

| Path | Loads | Guards | Policy |
|------|-------|--------|--------|
| `/` | `ExternalHomeComponent` | `canMatch: postLoginRedirectGuard` | -- |
| `/public/document-upload/:id/:verificationCode` | `PublicDocumentUploadComponent` | none | -- |
| `/public/change-request-consent/:token` | `PublicChangeRequestConsentComponent` | none | -- |
| `/gdpr` | ABP GDPR module | none | -- |
| `/gdpr-cookie-consent` | cookie and privacy policy pages | none | -- |
| `/appointments/view/:id` (external copy) | `ExternalAppointmentDetailComponent` | `canMatch: externalUserOnlyMatchGuard`, `authGuard` | -- |
| `/appointments/request` (external copy) | `AppointmentWizardComponent` | `canMatch: externalUserOnlyMatchGuard`, `authGuard`, `permissionGuard`, `canDeactivate: appointmentWizardCanDeactivateGuard` | `CaseEvaluation.Appointments.Create` |
| `/user-management/patients/my-profile` | `PatientProfileRedesignComponent` | `authGuard` | -- |
| `/user-management/attorneys/my-profile` | `AttorneyProfileComponent` | `authGuard` | -- |
| `/user-management/claim-examiners/my-profile` | `ClaimExaminerProfileComponent` | `authGuard` | -- |
| `**` | `NotFoundComponent` | none | -- |

### Inside the internal shell

Every child below also sits behind the shell parent's `internalUserOnlyMatchGuard` and `authGuard`.
Unless noted, a child carries `authGuard` + `permissionGuard`.

| Path | Loads | Policy |
|------|-------|--------|
| `/dashboard` | `InternalDashboardComponent` | [`CaseEvaluation.Dashboard.Host \|\| CaseEvaluation.Dashboard.Tenant`] |
| `/appointments` | `APPOINTMENT_ROUTES`: the staff list, and `view/:id` (staff detail; `authGuard` only) | [`CaseEvaluation.Appointments`] |
| `/appointments/change-requests` | `CHANGE_REQUEST_ROUTES`: one tabbed inbox; `reschedules` and `cancellations` redirect to it | `CaseEvaluation.AppointmentChangeRequests` |
| `/appointments/request` (staff copy) | `AppointmentWizardComponent`, plus `canDeactivate: appointmentWizardCanDeactivateGuard` | `CaseEvaluation.Appointments.Create` |
| `/appointments/view/:id/change-log` | `AppointmentChangeLogsComponent` | `CaseEvaluation.AppointmentChangeLogs` |
| `/appointment-change-logs` | `AppointmentChangeLogListComponent` | `CaseEvaluation.AppointmentChangeLogs` |
| `/reports` | `AppointmentReportComponent` | `CaseEvaluation.Reports` |
| `/doctor-management/doctor-availabilities` | `DOCTOR_AVAILABILITY_ROUTES`: the list; `generate` and `add` load the generate form | [`CaseEvaluation.DoctorAvailabilities`] |
| `/doctor-management/schedule` | `InternalScheduleComponent` | `CaseEvaluation.DoctorAvailabilities` |
| `/doctor-management/locations` | `LOCATION_ROUTES` | [`CaseEvaluation.Locations`] |
| `/doctor-management/wcab-offices` | `WCAB_OFFICE_ROUTES` | [`CaseEvaluation.WcabOffices`] |
| `/doctor-management/doctors` | `DOCTOR_ROUTES` -- dormant feature, no menu entry | -- |
| `/configurations/states` | `STATE_ROUTES` | `CaseEvaluation.States` |
| `/appointment-management/appointment-types` | `APPOINTMENT_TYPE_ROUTES` | `CaseEvaluation.AppointmentTypes` |
| `/appointment-management/appointment-statuses` | `APPOINTMENT_STATUS_ROUTES` | `CaseEvaluation.AppointmentStatuses` |
| `/appointment-management/document-types` | `APPOINTMENT_DOCUMENT_TYPE_ROUTES` | `CaseEvaluation.AppointmentDocumentTypes` |
| `/appointment-management/appointment-languages` | `APPOINTMENT_LANGUAGE_ROUTES` | `CaseEvaluation.AppointmentLanguages` |
| `/user-management/patients` | `PATIENT_ROUTES` | `CaseEvaluation.Patients` |
| `/applicant-attorneys` | `APPLICANT_ATTORNEY_ROUTES` | `CaseEvaluation.ApplicantAttorneys` |
| `/defense-attorneys` | `DEFENSE_ATTORNEY_ROUTES` | `CaseEvaluation.DefenseAttorneys` |
| `/claim-examiners` | `CLAIM_EXAMINER_ROUTES` | `CaseEvaluation.ClaimExaminers` |
| `/users` | redirects to `/users/invite` | -- |
| `/users/invite`, `/users/pending` | `InternalUsersHubComponent` (one hub, one section per route) | `CaseEvaluation.UserManagement.InviteExternalUser` |
| `/users/internal` | `InternalUsersHubComponent` | `CaseEvaluation.InternalUsers.Create` |
| `/users/tenants` | `InternalUsersHubComponent` | `Saas.Tenants` |
| `/internal-users` | redirects to `/users/internal` | -- |
| `/host/intake-assignments` | `IntakeAssignmentsComponent` | `CaseEvaluation.IntakeAssignments` |
| `/host/my-offices` | `IntakeOfficeSwitcherComponent` | `CaseEvaluation.IntakeImpersonation` |
| `/host/branding` | `HostBrandingComponent` | `CaseEvaluation.Branding` |
| `/office-branding` | `OfficeBrandingComponent` | `CaseEvaluation.Branding.Edit` |
| `/admin` | redirects to the first admin section the caller can see (`firstVisibleAdminSection`), else `/dashboard` | -- |
| `/admin/templates` | `InternalAdminHubComponent` (one hub, one section per route) | `CaseEvaluation.NotificationTemplates` |
| `/admin/parameters` | `InternalAdminHubComponent` | `CaseEvaluation.SystemParameters` |
| `/admin/roles` | `InternalAdminHubComponent` | `AbpIdentity.Roles` |
| `/admin/audit` | `InternalAdminHubComponent` | `AuditLogging.AuditLogs` |
| `/admin/integration-failures` | `InternalAdminHubComponent` | `CaseEvaluation.Appointments.ViewIntegrationDeadLetters` |
| `/language-management` | `LanguageManagementComponent` (replaces the ABP module page) | `LanguageManagement.Languages` |
| `/file-management` | `FileManagementComponent` (replaces the ABP module page) | `FileManagement.FileDescriptor` |
| `/identity`, `/saas`, `/audit-logs`, `/openiddict`, `/text-template-management`, `/setting-management` | ABP module routes (`loadChildren`) | set by each module |

## Guards

| Guard | Source | Hook | What it does |
|-------|--------|------|--------------|
| `postLoginRedirectGuard` | `shared/auth/post-login-redirect.guard.ts` | `canMatch` on `/` | Anonymous: starts the AuthServer sign-in. Intake Staff at host scope: `/host/my-offices`. Other internal users: `/dashboard`. External users: stay on `/`. |
| `externalUserOnlyMatchGuard` | `shared/auth/external-user-match.guard.ts` | `canMatch` | Matches only when every role the user holds is an external role. Anyone with an internal role, and anonymous users, fall through. |
| `internalUserOnlyMatchGuard` | `shared/auth/internal-user-match.guard.ts` | `canMatch` | The exact complement: matches internal users and anonymous users (so a child's `authGuard` can start sign-in); pure-external users fall through. |
| `authGuard` | `@abp/ng.core` | `canActivate` | Requires a signed-in user; otherwise starts sign-in. |
| `permissionGuard` | `@abp/ng.core` | `canActivate` | Requires the route's policy; shows the 403 screen when it is not granted. |
| `appointmentWizardCanDeactivateGuard` | `appointments/wizard/appointment-wizard-can-deactivate.guard.ts` | `canDeactivate` | Delegates to the wizard's `canDeactivate()`: leaving a partly filled new booking offers Save, Discard or Stay; a clean form or a finished submit leaves without asking. |

Route guards decide what the browser shows. Every API call is authorized again on the server, which is the
boundary that matters.

## Where a route's permission comes from

ABP's `permissionGuard` (in `@abp/ng.core`) takes the policy from the route's `data.requiredPolicy`. When the
route has none, it looks up the ABP menu registration whose `path` matches the URL, walking up one segment at a
time, and uses that registration's `requiredPolicy`. If it finds no policy either way, it allows the route.

So:

- **Set `data.requiredPolicy` on every new guarded route.** Several feature child routes (for example the
  doctor availability, location and WCAB office routes) still take their policy from the menu registration, so
  deleting a route provider changes what those routes check.
- The route providers registered in `app.config.ts` are therefore not dead code, even though no layout renders
  their menu. `APP_ROUTE_PROVIDER` (`route.provider.ts`) registers Home, Dashboard, User Management, Change Logs
  and Reports; each feature's `*-base.routes.ts` registers its own entry. The Doctors feature registers none.

## The internal sidebar

`InternalShellLayoutComponent` renders the sidebar from `internal-nav.config.ts`:

- **`IN_NAV`** -- the office navigation: Workspace, Scheduling, Administration, Configuration and People groups.
- **`IN_NAV_HOST`** -- the host navigation: Overview, Practice Management and Administration.
- **`resolveNavGroups`** picks `IN_NAV_HOST` when the user is at host scope and is an IT Admin, the built-in
  admin, a Staff Supervisor or Intake Staff; everyone else, including staff who have switched into an office,
  gets `IN_NAV`.
- **`filterNavGroups`** keeps an item only when the user's role key is listed on it and its `requiredPolicy`
  (the same string the route checks) is granted, so a visible item never leads to a 403.

External users never get the shell; their pages render their own navbar. See [Role-Based UI](ROLE-BASED-UI.md).

## Lazy loading

Every page is lazy-loaded: feature pages with `loadComponent`, ABP modules with `loadChildren`, and the per-feature
`*_ROUTES` constants as `children` whose entries use `loadComponent`. The one eager import is
`InternalShellLayoutComponent` (`app.routes.ts`), which the shell parent route renders directly.

## Route order notes

1. The external copies of `appointments/view/:id` and `appointments/request` come before the shell parent, so
   `canMatch` can hand external users the chrome-less page and let staff fall through to the in-shell copy.
2. The three `my-profile` routes are top-level and outside the shell, so they match before the shell's
   `user-management/patients` children could.
3. `/appointments/add` no longer exists; booking has one path, `/appointments/request`, for every role. Do not
   add an alias for it: a redirect to a role-split route is what produced a 404 before.
4. `**` must stay last.

---

**Related Documentation:**

- [Angular Architecture](ANGULAR-ARCHITECTURE.md)
- [Role-Based UI](ROLE-BASED-UI.md)
- [Permissions](../backend/PERMISSIONS.md)
