---
type: shell-layout
status: draft
audited: 2026-05-04
old-source-roots:
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\start\app.component.html
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\start\app.component.ts
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\shared\top-bar\top-bar.component.html
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\shared\side-bar\side-bar.component.html
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\shared\footer-bar\footer-bar.component.html
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\login\login\login.component.html
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\login\forgot-password\forgot-password.component.html
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\home\home.component.html
cross-reference:
  - docs/design/_design-tokens.md
  - docs/parity/_branding.md
strict-parity: true
---

# Shell layout -- OLD-anchored chrome contract

The page chrome OLD wraps every feature in: which shell variant a route
gets, the top-bar contents, the side-nav structure (role-conditional),
the footer, and the auth-page wrapper. Per-feature design docs link to
this file when describing how their content slots into a shell variant.

## Three shell variants in OLD

OLD's `start/app.component.html` (lines 9-60) selects between **three**
mutually-exclusive shells based on `isShowDashboard` / `isShowHome`
state flags. NEW preserves the same three variants; per-feature design
docs name the variant in their layout section.

### Shell variant 1 -- `unauthenticated` (login / forgot-password / reset / verify-email)

```
+----------------------------------------------------------+
|                                                          |
|        background-image: ./assets/images/login-bg.jpg    |
|        + dark scrim (rgba(0,0,0,.25))                    |
|                                                          |
|         +------------------------------------+           |
|         |  [LOGO IMAGE -- center-aligned]     |           |
|         |                                    |           |
|         |  <feature form fields>             |           |
|         |                                    |           |
|         |  [Submit Button -- btn-secondary]   |           |
|         |  [Footer link e.g. "Forgot pw?"]   |           |
|         +------------------------------------+           |
|                                                          |
+----------------------------------------------------------+
```

OLD source: `login.component.html`:1-39, `forgot-password.component.html`:2-30.

Key markup:

- `<div class="authentication-wrapper authentication-2 ui-bg-cover ui-bg-overlay-container px-4">` -- full-viewport flex container.
- Inline `style="background-image: url('./assets/images/login-bg.jpg');"` -- NEW switches to `--brand-login-bg-url` CSS var (per `_design-tokens.md` strict-parity exception #1).
- `<div class="ui-bg-overlay bg-dark opacity-25">` -- 25% black scrim.
- `<div class="card login-page">` -- centered card, max-width 450px on reset (`site.css`:1-3) / 340px on login (`vendor/app.css`:3-5).
- Logo: centered `<img src="./assets/images/Doctor.png" class="img-fluid">` -- NEW uses `var(--brand-logo-url)`.

NEW maps these to:

- `angular/src/app/account/auth-shell.component.{html,scss}` -- a single shared shell wrapping the three Angular auth pages (Register, Login, Forgot, Reset, Verify-email).
- AuthServer Razor pages (LeptonX customization at Phase 19b) get the same shell via the brand-token cascade.

### Shell variant 2 -- `external user authenticated` (home + booking + view)

```
+----------------------------------------------------------+
| TOP-BAR (white bg, navbar-brand on left, user nav right) |
+----------------------------------------------------------+
|                                                          |
|         <feature content -- container.flex-grow-1>        |
|                                                          |
+----------------------------------------------------------+
| FOOTER (gray-bg, "<ClinicName>" copy)                    |
+----------------------------------------------------------+
```

OLD source: `app.component.html`:9-22 (`*ngIf="isShowHome"`,
`layout-without-sidenav` modifier).

`isShowHome` is true for the four external-user routes:
`/home`, `/appointments/add`, `/appointments/view/:id`,
`/appointment-search` -- per `app.component.ts` (verify in Session A
front-end work).

Key markup:

- Outer wrapper: `<div class="layout-wrapper layout-1 layout-without-sidenav">` -- enables single-column layout.
- `<app-top-bar *ngIf="showElement"></app-top-bar>` -- top nav (see Section "Top-bar" below).
- `<div class="container flex-grow-1 container-p-y">` -- centered content; Bootstrap 4 `.container` clamps width per breakpoint.
- `<router-outlet>` -- per-feature content drops here.
- `<app-footer-bar *ngIf="showElement"></app-footer-bar>`.

NEW preserves the same DOM. The `showElement` gate exists so that the
brief redirect after login does not flash an unstyled top-bar; NEW
keeps an equivalent `*ngIf` keyed on the auth-state observable.

### Shell variant 3 -- `internal user authenticated` (dashboard + admin)

```
+--------+------------------------------------------------+
| SIDE-  | TOP-BAR                                        |
| NAV    +------------------------------------------------+
|        |                                                |
| (full  |    <feature content -- container-fluid>        |
| height)|                                                |
|        |                                                |
|        +------------------------------------------------+
|        | FOOTER                                         |
+--------+------------------------------------------------+
```

OLD source: `app.component.html`:26-60 (`*ngIf="isShowDashboard"`,
`layout-2 default-style` modifier).

`isShowDashboard` is true for every internal-user route (Clinic Staff,
Staff Supervisor, IT Admin) -- dashboard, appointment management,
configurations, reports, etc.

Key markup:

- `<div class="layout-wrapper layout-2 default-style" [class.sidebar-hidden]="isSideBarActive">` -- two-column layout.
- `<div id="layout-sidenav" class="layout-sidenav sidenav sidenav-vertical bg-sidenav-theme">` -- left rail.
- `<div class="app-brand demo">` -- brand block at top of side rail with `<img src="./assets/images/header-logo.png" width="245" height="40" class="img-fluid full-logo">` (collapsed: `<img src="./assets/images/fav-logo.png" width="55" height="55">`).
- `<app-side-bar></app-side-bar>` -- nav links (see Section "Side-bar" below).
- `<div class="layout-container">` -- right content area: top-bar + scrollable layout-content + footer.
- `<div class="container-fluid flex-grow-1 container-p-y">` -- full-width fluid container (vs. `.container` in external variant).

NEW preserves the same DOM and uses ABP's LeptonX shell components
(`@volo/abp.ng.theme.lepton-x`) only where their default chrome
matches OLD's contract; otherwise overrides via custom shell components
in `angular/src/app/shared/`.

## Top-bar component

OLD source: `top-bar.component.html`:9-89 (the live block; lines
1-8 and 90-end are commented-out alternates).

Live structure (post-login, both external + internal):

- `<nav class="layout-navbar navbar navbar-expand-lg align-items-lg-center bg-white" id="layout-navbar">` -- white-bg fixed top nav.
- Brand region (logo image, conditionally rendered):
  - Internal users (`isShowToggleSideNavbar = true`): `routerLink="dashboard"` + `header-logo.png` + side-nav toggle button (Ionicons `ion ion-md-menu`). Hidden on `lg+` because the side-nav itself shows the brand.
  - External users (`isShowToggleSideNavbar = false`): `routerLink="home"` + `header-logo.png`. Always visible.
- Mobile collapse toggler: `<button class="navbar-toggler p-2">` -- Bootstrap 4 default.
- Right region (`navbar-nav header-navbar align-items-lg-center ml-auto`):
  - `Welcome, {{loggedInName}} ({{userType}})` (line 72)
  - `|` divider (line 73)
  - `My profile` link with `ion ion-ios-person` icon (lines 74-76)
  - Conditional `|` + `Help` link (`onAddQuery()`) with `ion ion-md-help` icon (lines 77-80) -- shown when `isShowHelp` truthy
  - Final `|` + `Log Off` link with `ion ion-ios-log-out` icon (lines 81-85)

NEW maps to:

- `angular/src/app/shared/top-bar/top-bar.component.{html,ts,scss}`.
- `userType` resolved from the IdentityUser's role (one role per user enforced by Phase 8 registration).
- `loggedInName` resolved from `IdentityUser.Name + " " + Surname`.
- `Help` link's `onAddQuery()` deferred -- query/submit feature is post-MVP.
- All link colors use `var(--brand-primary-text)` (`text-primary`).

## Side-bar component (internal users only)

OLD source: `side-bar.component.html`:1-160.

The side-nav lives only in shell variant 3. Each menu item is
permission-gated via `*ngIf="hasPermission(modules.<Module>)"`. OLD's
hard-coded module enum maps to NEW ABP permission keys per the
audit-doc permission tables.

Top-level menu structure (verbatim from OLD):

| Position | Label | Icon | Route | Permission key (OLD module) | NEW permission key |
|---|---|---|---|---|---|
| 1 | Dashboard | `fas fa-tachometer-alt` | `/dashboard` | Dashboard | `CaseEvaluation.Dashboard.Tenant` (or `.Host` for IT Admin host scope) |
| 2 | Book Appointment | `ion ion-ios-save` | `/appointments/add` | BookAppointments | `CaseEvaluation.Appointments.Create` |
| 3 | Check-in & Check-out | `fas fa-calendar-check` | `/appointment-approve-request` | AppointmentCheckInCheckOut | DEFERRED (post-parity; see master plan deferred items) |
| 4 | Appointments (collapsible) | `far fa-calendar-alt` | -- | composite | composite |
| 4a | All Appointments | -- | `/appointment-search` | AllAppointmentRequest | `CaseEvaluation.Appointments.Default` |
| 4b | Pending Appointments | -- | `/appointment-pending-request` | AppointmentPendingRequest | `CaseEvaluation.Appointments.Approve` |
| 4c | Rescheduled Requests | -- | `/appointment-rescheduled-requests` | AppointmentRescheduleRequests | `CaseEvaluation.AppointmentChangeRequests.Default` (filtered to Reschedule) |
| 4d | Cancel Requests | -- | `/appointment-cancel-requests` | AppointmentCancelRequests | `CaseEvaluation.AppointmentChangeRequests.Default` (filtered to Cancel) |
| 4e | Change / Audit Log | -- | `/appointment-change-logs` | AppointmentChangeLogs | `CaseEvaluation.AppointmentChangeLogs` (deferred per master plan §20) |
| 5 | Document (collapsible) | `fas fa-file-medical` | -- | composite | composite |
| 5a | Appointment Documents | -- | `/appointment-documents-search` | AppointmentDocuments | `CaseEvaluation.AppointmentDocuments.Default` |
| 5b | Document Types | -- | `/appointment-document-types` | (none gate -- IT Admin) | `CaseEvaluation.AppointmentDocuments.Default` (IT Admin only -- per parity audit) |
| 6 | Doctor Management (collapsible) | `fas fa-user-md` | -- | composite | composite |
| 6a | Doctor Details | -- | `doctor-add` | Doctors | `CaseEvaluation.Doctors.Default` |
| 6b | Availability & Time slots | -- | `doctor-availibilities` | DoctorsAvailabilitiesList | `CaseEvaluation.DoctorAvailabilities.Default` |
| 6c | Location Management | -- | `locations` | LocationsList | `CaseEvaluation.Locations.Default` |
| 7 | Configurations (collapsible) | `fas fa-cog` | -- | composite | composite |
| 7a | System Parameters | -- | `system-parameters` | SystemParameters | `CaseEvaluation.SystemParameters.Default` |
| 7b | Users | -- | `users` | Users | `AbpIdentity.Users` (ABP-managed) |
| 7c | Custom Fields | -- | `custom-fields` | CustomFields | `CaseEvaluation.CustomFields.Default` |
| 8 | Reports | `fas fa-file-signature` | `/report` | Reports | DEFERRED (post-parity) |

OLD-typo / verbatim deviations:

- "doctor-availibilities" route (OLD typo: "availibilities") -- NEW
  fixes to `doctor-availabilities`. Documented in `_old-docs-index.md`.

NEW menu structure preserves the visual hierarchy 1:1; only routes and
permission keys translate per the table above. Per-feature design docs
do not duplicate this matrix; they cite this section.

### Side-nav state behavior (preserved verbatim)

- `activeMenu` tracks which top-level group is open.
- `activeSubMenu` tracks which leaf is highlighted.
- `toggleMenu` is the user's manual override of group-open state.
- Below `lg` breakpoint (992px) the side-nav collapses to off-canvas
  (`layout-offcanvas` / `layout-fixed-offcanvas` modifiers); the
  top-bar's `sidenav-toggle` button shows it. See
  `demo-falkinstein.css`:94-99.

## Footer component

OLD source: `footer-bar.component.html`:1-15.

```html
<nav class="layout-footer footer bg-footer-theme">
  <div class="container-fluid d-flex flex-wrap justify-content-sm-between justify-content-center text-center container-p-x pb-3">
    <div class="pt-3 mr-3">
      <span class="footer-text font-weight-bolder">SoCal</span> ©
    </div>
    <div>
      <!-- links commented out in OLD live -->
    </div>
  </div>
</nav>
```

OLD ships a stripped-down footer (just the clinic name + copyright).
The commented links (`About Us` / `Help` / `Contact` / `Terms`) are
inactive in OLD live. NEW preserves the live form.

NEW substitution:

- `SoCal` literal -> `var(--brand-clinic-name)` so per-tenant override
  is one declaration.
- Future deferred items (terms-and-conditions page link, support
  email/phone tap-to-call) referenced by `docs/parity/terms-and-conditions.md`
  go behind a feature flag.

## External-user "home" page (shell variant 2 content)

OLD source: `home.component.html`:1-26.

The home page itself is barely a page -- it is two CTAs ("Book
Appointment" / "Book Re-evaluation") above a `<router-outlet>` that
renders the patient's appointment list. The CTAs are hidden when the
user is an Accessor (`*ngIf="!isAccessor"`).

This is the canonical example of how the external shell composes:
shell variant 2 wraps the top-bar + container + footer; the route
content (here, `home.component`) is just CTAs + the nested router
outlet for the embedded appointment list.

## Container vs container-fluid

OLD's two authenticated shells differ on container style:

- External shell (variant 2): `<div class="container flex-grow-1 container-p-y">` -- max-width clamped per Bootstrap breakpoints (576/720/960/1140 px).
- Internal shell (variant 3): `<div class="container-fluid flex-grow-1 container-p-y">` -- full viewport width minus the side-nav rail.

NEW preserves the distinction so external pages have OLD's clamped
look-and-feel and internal pages take advantage of full width for data
tables.

## Permission resolution mapping (OLD modules -> NEW ABP keys)

The OLD `modules.*` enum (referenced in `side-bar.component.html`) maps
to NEW's `CaseEvaluation.*` permission tree. Per-feature design docs
cite the NEW key; the side-bar table above documents the mapping
verbatim. There is no behavioral change -- a Clinic Staff user sees
the same items in OLD and NEW after migration.

## Strict-parity exceptions

1. **Inline `style="background-image: url(...)"`** on auth-shell
   wrapper (OLD `login.component.html`:1, `forgot-password.component.html`:2)
   -> NEW uses `--brand-login-bg-url` CSS var. Visible pixels identical.
2. **Hard-coded "SoCal" footer literal** -> NEW uses `var(--brand-clinic-name)`.
3. **Notifications + Messages dropdowns** in OLD top-bar (lines 93-199,
   commented out in OLD live) -> NEW does not implement; defer to a
   future phase aligned with in-app notification work.
4. **Side-nav modules with no NEW counterpart yet** ("Check-in &
   Check-out", "Reports") -> NEW omits the menu items rather than
   showing dead links. Re-add when the feature ships post-parity.

## NEW current-state delta

`angular/src/app/shared/top-header-navbar.component.ts` is the only
shared shell component currently in NEW (per the frontend audit). The
side-bar, footer-bar, auth-shell components are NOT yet built. Phase
19a/19b owns the implementation; this doc is the contract.

ABP LeptonX provides default top-bar + side-nav + footer chrome that
matches the **OLD's structure but not OLD's tokens / colors / link
list**. Phase 19a's CSS-variable layer + Phase 19b's LeptonX overrides
adjust LeptonX's defaults to OLD's contract.

## Verification (post-impl)

When Phase 19a/19b lands, manually verify:

- [ ] Login page background image + 25% scrim visible.
- [ ] Logo appears center-top of login card; comes from `var(--brand-logo-url)`.
- [ ] After internal-user login: side-nav with 8 top-level items as
      enumerated above; correct items hidden per role.
- [ ] After external-user login: top-bar visible, side-nav hidden,
      home page shows two CTAs (or zero for accessor).
- [ ] Footer renders clinic name + copyright; no inactive links.
- [ ] Below 992px: side-nav collapses to off-canvas, top-bar gets a
      hamburger toggle.
- [ ] Click "My profile" -> ABP user-profile page.
- [ ] Click "Log Off" -> ABP signout endpoint, redirects to login.
