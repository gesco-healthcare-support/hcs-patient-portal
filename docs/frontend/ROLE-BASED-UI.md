# Role-Based UI

> Purpose: Describes how the SPA gives external users and internal staff different pages and chrome, and
> how each side's content varies by role. Audience: frontend developers.

[Home](../index.md) > [Frontend](./) > Role-Based UI

## Overview

The split between **external users** (Patient, Applicant Attorney, Defense Attorney, Claim Examiner) and
**internal staff** (IT Admin, Staff Supervisor, Intake Staff, and the built-in `admin`) is made by the
**router**, not by CSS or DOM changes. The root component renders a bare `<router-outlet />`; every page
brings its own chrome:

| Aspect | External users | Internal staff |
|--------|----------------|----------------|
| Chrome | `ExternalNavbarComponent` at the top of each external page | `InternalShellLayoutComponent`: sidebar and top bar around every staff page |
| Landing page | `/` -- `ExternalHomeComponent` | `/dashboard` (Intake Staff at host scope: `/host/my-offices`) |
| Navigation | Buttons on the home page and the navbar | The sidebar from `internal-nav.config.ts` |
| Pages | Home, the appointment detail, the booking wizard, and their own profile | Everything under the shell (see [Routing & Navigation](ROUTING-AND-NAVIGATION.md)) |

## How a user is classified

Two helpers in `angular/src/app/shared/auth/` read `currentUser.roles` from ABP's `ConfigStateService`
(compared lower-cased and trimmed):

- **`hasOnlyExternalRoles(roles)`** (`external-user-roles.ts`) -- true when the user has at least one role and
  **every** role is one of the four external roles. A user holding any internal role counts as internal.
- **`resolveInternalRoleKey(roles)`** (`internal-user-roles.ts`) -- maps an internal role to the sidebar's
  role key: `admin` wins when present, otherwise the first of `it admin` -> `itadmin`,
  `staff supervisor` -> `supervisor`, `intake staff` -> `intake`. Returns `null` for an external user.

`isHostScope(config)` (also `internal-user-roles.ts`) is true when there is no current tenant, which decides
between the host sidebar and the office sidebar.

## Where the split happens

1. **`/`** -- `postLoginRedirectGuard` sends internal users to `/dashboard` (Intake Staff at host scope to
   `/host/my-offices`) before the external home chunk loads. External users stay on `/`.
2. **Shared paths** -- `appointments/view/:id` and `appointments/request` each exist twice. The external copy
   is gated by `externalUserOnlyMatchGuard` and renders chrome-less; the staff copy sits inside the shell,
   behind `internalUserOnlyMatchGuard`.
3. **Everything else under the shell** -- only internal users (and anonymous users, who are then sent to sign
   in) match the shell parent route, so an external user who types a staff URL gets the 404 page.

The booking wizard is one component for both sides. It checks `isInternalBooker` to hide its own external
navbar inside the shell and to adjust its copy and its exit route (`/appointments` for staff, `/` otherwise).

## External pages

`ExternalNavbarComponent` (`shared/components/external-navbar/`) is rendered by the external home, the
external appointment detail, the booking wizard and the three profile pages. It shows the office's logo and
name, the user's name and role, notifications, and Profile, Documents, Help and Sign-out actions (emitted as
outputs for the page to handle).

### The external home (`home/external-home.component.ts`)

- A per-role configuration (`ROLE_CONFIGS`) sets the labels, the default view (cards for a Patient, a table
  for the other three roles) and whether a patient column is shown. All four roles can book and request a
  re-evaluation.
- **Book** opens `/appointments/request?type=1`; **Re-evaluation** opens `/appointments/request?type=2`.
- The list shows only the appointments the server returns for this user (see below); a row opens
  `/appointments/view/:id`.

### Profile pages

| Route | Page |
|-------|------|
| `/user-management/patients/my-profile` | Patient profile; shows a read-only card for the other three roles |
| `/user-management/attorneys/my-profile` | Attorney self-edit |
| `/user-management/claim-examiners/my-profile` | Claim examiner self-edit |

The appointment detail page links each role to its own profile page.

## Internal pages

`InternalShellLayoutComponent` (`shared/components/internal-shell/`) builds the sidebar with
`resolveNavGroups(roleKey, hostScope, isGranted)` from `internal-nav.config.ts`:

- At host scope, IT Admin, `admin`, Staff Supervisor and Intake Staff get the host navigation (`IN_NAV_HOST`).
- Inside an office, everyone gets the office navigation (`IN_NAV`).
- An item shows only when the user's role key is listed on it **and** its `requiredPolicy` is granted. The
  policy is the same string the route's guard checks, so a visible item never leads to a 403.

## Which appointments an external user sees

There is no client-side filtering: the server narrows the list. An external caller sees an appointment when
they are its **creator**, an explicit **AppointmentAccessor**, the **patient identity** on it, or when one of
the appointment's party-email columns equals their email **and** they hold that column's role
(`PatientEmail` -> Patient, `ApplicantAttorneyEmail` -> Applicant Attorney, `DefenseAttorneyEmail` -> Defense
Attorney, `ClaimExaminerEmail` -> Claim Examiner). The email-and-role rule is
`AppointmentAccessRules.IsAppointmentEmailRoleVisible`; the list query and the per-appointment read guard
(`AppointmentReadAccessGuard`) use the same rule, so a row in the list opens without a 403.

## Sign-up fields by role (firm-based attorney accounts)

The AuthServer sign-up page (`src/HealthcareSupport.CaseEvaluation.AuthServer/wwwroot/global-scripts.js`)
changes its fields with the selected role: Patient and Claim Examiner enter first and last name; Applicant and
Defense Attorney enter a **firm name** instead and register as firm accounts, so their first and last name
stay blank. Every display surface therefore uses `resolveExternalUserDisplayName`
(`shared/auth/external-user-display-name.ts`): first and last name, else firm name, else email.

---

**Related Documentation:**

- [Routing & Navigation](ROUTING-AND-NAVIGATION.md)
- [Permissions](../backend/PERMISSIONS.md)
- [User Roles & Actors](../business-domain/USER-ROLES-AND-ACTORS.md)
