---
type: cross-cutting-aggregation
audited: 2026-05-01
status: investigation-complete
phase: 1 (parameterize from day one)
---

# Branding -- aggregation + parameterization plan

Where the OLD application hard-codes the doctor's clinic branding, what surfaces it touches, and how NEW must parameterize all of it from day one so multi-doctor extension in Phase 2 does not require a UI rewrite.

This file aggregates every per-feature `Branding/theming touchpoints` section from the 32 audit docs into a single canonical inventory, plus a NEW implementation plan.

## Why this matters now

OLD is single-doctor-per-deploy: each clinic gets its own database AND its own deployed front+backend, so OLD can hard-code the clinic's logo, name, colors, and email subjects. NEW must support multi-tenant deployment where one application instance serves multiple clinics. Per Adrian's directive (`project_old-app-context.md` -> branding-extensibility): wire branding hooks in Phase 1 (one tenant) so that adding a second tenant in Phase 2 is a config + asset upload, not code.

## Branding surfaces inventory (from OLD)

### A -- Logos and image assets

`P:\PatientPortalOld\patientappointment-portal\src\assets\images\`:

| File | Purpose |
|------|---------|
| `Doctor-logo.png` | Main clinic logo (header + email signature). Doctor-specific. |
| `Doctor.png` | Doctor's profile photo (rendered on dashboard / appointment view). Doctor-specific. |
| `fav-logo.png` | Browser favicon. |
| `header-logo.png` | Top-bar header logo (likely identical or related to Doctor-logo). |
| `login-bg.jpg` | Login page background image. |

All five are referenced from HTML templates and email-template HTML; all five must become per-tenant assets in NEW.

### B -- Theme CSS palettes

`P:\PatientPortalOld\patientappointment-portal\src\assets\theme\css\`:

- `demo.css` -- Lighthouse Theme default palette
- `demo-falkinstein.css` -- alternate palette
- `demo-longacre.css` -- alternate palette
- `demo-pelton.css` -- alternate palette

OLD ships with 4 selectable Lighthouse theme skins. Each defines a distinct color scheme (primary / secondary / accent). One is active per deploy (selection mechanism TO VERIFY -- likely a build-time or settings-time choice).

`P:\PatientPortalOld\patientappointment-portal\src\assets\css\site.css` -- app-specific CSS overrides on top of the theme.

The four palettes are likely tied to four different doctor brands the OLD developers prepared as templates (Falkinstein, Longacre, Pelton are common surnames; one default). NEW must collapse these into a single tenant-themed palette mechanism.

### C -- Hardcoded strings (frontend)

Files containing branded strings:

- `app.module.ts` -- module-level title or config metadata.
- `index.html` -- HTML `<title>` and any pre-render branding.
- `top-bar.component.html` -- header layout with logo + clinic name.
- `footer-bar.component.html` -- footer with copyright + support contact.
- `appointment-add.component.html`, `appointment-edit.component.html` -- form section headings.
- `doctor-edit.component.html`, `location-add/edit.component.html`, `appointment-document-type-add/edit.component.html`, `user-edit.component.html` -- master-data form chrome.
- `appointment-validation.component.html` -- validation summary popup.

Common strings observed in code:

- `"Patient Appointment Portal"` -- the product/clinic name as displayed.
- `"Patient appointment portal"` (lowercase variant) -- inconsistency in OLD.
- `"socal"` (lowercase) -- in `UserDomain.AddInternalUser` welcome email subject `"Welcome to socal"`.
- Page titles, section headings, button labels referencing the clinic.

### D -- Hardcoded strings (backend / email subjects)

Files in `P:\PatientPortalOld\PatientAppointment.Domain\` that emit branded strings (per grep):

| File | Branded surface |
|------|-----------------|
| `UserModule/UserDomain.cs` | Welcome email subject `"Welcome to socal"`, registration email subject `"Your have registered successfully - Patient Appointment portal"` (typo), password-change subject `"Your password has been successfully changed - Patient Appointment portal"` |
| `Core/UserAuthenticationDomain.cs` | Reset-password subject `"Patient Appointment Portal - Reset Password"`, registration verification subject `"Your have registered successfully - Patient Appointment portal"` |
| `AppointmentRequestModule/AppointmentDomain.cs` | Stakeholder email subject builder for appointment status changes |
| `AppointmentRequestModule/AppointmentAccessorDomain.cs` | Accessor-invite email subject `"Patient appointment portal - ({Patient}) - Accessor details"` |
| `AppointmentRequestModule/AppointmentDocumentDomain.cs` | Document review email subject `"Patient Appointment Portal - ({Patient} - Claim: {claim} - ADJ: {adj}) - Appointment document is {Status}."` |
| `AppointmentRequestModule/AppointmentChangeRequestDomain.cs` | Cancel/reschedule notification subjects |
| `AppointmentRequestModule/AppointmentJointDeclarationDomain.cs` | JDF email subjects |
| `AppointmentRequestModule/AppointmentNewDocumentDomain.cs` | Ad-hoc doc email subjects |
| `UserQueryModule/UserQueryDomain.cs` | Query notification email subject |
| Plus the `Core/SchedulerDomain.cs` reminder subjects (e.g., `"Pending Appointment Request"` to `clinicStaffEmail`). |

All embed "Patient Appointment Portal" (or the lowercase variant) as the brand fragment.

### E -- Email template HTML

OLD references templates via `EmailTemplate` enum + `ApplicationUtility.GetEmailTemplateFromHTML(...)`. The actual HTML files (TO LOCATE in OLD's `wwwroot/EmailTemplates/...` or similar) contain the doctor's logo URL, primary color CSS-inlined, footer copy with support email + phone. Each template is per-event.

Templates in OLD by name (as discovered in audits):

- `UserRegistered`, `ResetPassword`, `PasswordChange`, `AddInternalUser`
- `PatientDocumentUploaded`, `PatientDocumentAccepted`, `PatientDocumentRejected`
- `AppointmentApproved` (TO VERIFY exact name), `AppointmentRejected`
- Cancellation Approved / Rejected templates
- Reschedule Approved / Rejected templates
- `PendingAppointmentDailyNotification`
- JDF reminders + auto-cancel notification

### F -- SystemParameter and config

Server-side config likely contains: `clinicStaffEmail` (recipient of UserQuery + scheduler digest), `applicationUrl.clientUrl` (used to build email links), and other clinic-specific values read via `ServerSetting.Get<string>(...)`.

## Per-feature touchpoints (aggregated from audit docs)

Every audit doc has a "Branding/theming touchpoints" section. Consolidated:

| Feature audit | Branded surface |
|---------------|-----------------|
| `external-user-registration.md` | Email subject ("Patient Appointment portal"), email body template, registration form copy ("Welcome to {Clinic}"), T&C content per tenant |
| `external-user-login.md` | AuthServer login page (logo, primary color, background, page title), "Welcome to {Clinic}" greeting |
| `external-user-forgot-password.md` | Forgot/reset email subjects + bodies, AuthServer reset-password page chrome |
| `external-user-appointment-request.md` | Booking form section headings, success toast copy, email subjects on submit, brand-token surfaces in form chrome |
| `external-user-appointment-package-documents.md` | Email subject + body for upload/accept/reject events, document upload page chrome |
| `external-user-appointment-ad-hoc-documents.md` | Email templates (shared with package docs), upload page chrome |
| `external-user-appointment-joint-declaration.md` | JDF email subjects + bodies, auto-cancel notification |
| `external-user-view-appointment.md` | Field labels, section headings, view page logo + primary color |
| `external-user-appointment-cancellation.md` | Email templates per event; cancel UI chrome |
| `external-user-appointment-rescheduling.md` | Email templates per event; reschedule UI; calendar picker styling |
| `it-admin-system-parameters.md` | Edit form UI |
| `staff-supervisor-doctor-management.md` | Slot calendar UI, doctor profile page logo + photo |
| `clinic-staff-appointment-approval.md` | Edit appointment page UI, approval/rejection email templates (patient + responsible-user + stakeholder versions) |
| `clinic-staff-document-review.md` | Email templates per status, review UI chrome |
| `it-admin-package-details.md` | Master document templates (logo + copy) |
| `it-admin-custom-fields.md` | Booking form custom-field rendering chrome |
| `it-admin-notification-templates.md` | All email + SMS templates -- THIS is the central editing surface for branded copy in NEW |
| `staff-supervisor-change-request-approval.md` | Approve/reject UI; per-event email templates |
| `appointment-change-log.md` | List page chrome |
| `internal-user-dashboard.md` | Counter card styling, widget layout, primary-color surfaces |
| `clinic-staff-check-in-check-out.md` | Today-view chrome, status pill colors |
| `internal-user-view-all-appointments.md` | List page chrome |
| `internal-user-reports.md` | Report UI chrome, Excel/PDF export header (logo + clinic name + footer) |
| `external-user-submit-query.md` | Form UI; query notification email |
| `appointment-notes.md` | Notes UI |
| `terms-and-conditions.md` | Modal styling; T&C content (per-tenant text) |
| `it-admin-user-management.md` | UI chrome; welcome email template (`Welcome to socal` -> `Welcome to {ClinicName}`) |
| `master-data-crud.md` | Master data UI screens |
| `scheduler-background-jobs.md` | All reminder email subjects + bodies; daily digest HTML template |
| `application-configurations.md` | This is the home of localized strings -- supports branding copy via tenant-overridable localization |
| `document-upload-download.md` | Upload UI styling |

## Brand-token surface model

Every branded surface in NEW resolves to one of these tokens. NEW exposes them via:

- **CSS custom properties** (for visual styling)
- **Config-driven branding object** (for assets and per-tenant strings)
- **Localization keys** (for copy that varies in tone or language)

### CSS custom properties (per tenant)

Defined in `:root` of a per-tenant generated stylesheet, e.g.:

```css
:root {
  --brand-primary: #1976d2;
  --brand-primary-hover: #1565c0;
  --brand-secondary: #f57c00;
  --brand-accent: #00897b;
  --brand-background: #ffffff;
  --brand-text-on-primary: #ffffff;
  --brand-link: #1976d2;
  --brand-success: #4caf50;
  --brand-warning: #ffa000;
  --brand-error: #d32f2f;
  --brand-logo-url: url('/api/branding/asset/header-logo.png');
  --brand-logo-favicon-url: url('/api/branding/asset/favicon.png');
  --brand-login-bg-url: url('/api/branding/asset/login-bg.jpg');
}
```

All Angular component SCSS uses these custom properties exclusively. Hardcoded color literals are forbidden in feature components (per branch CLAUDE.md "no smart quotes / em dashes / Unicode" + this branding rule).

### Config-driven branding object

A single per-tenant config endpoint, e.g., `GET /api/app/branding/current-tenant`, returns:

```json
{
  "clinicName": "Riverside Family Medical Group",
  "clinicShortName": "Riverside Med",
  "appProductName": "Patient Appointment Portal",
  "logos": {
    "header": "/api/branding/asset/header-logo.png",
    "doctorLogo": "/api/branding/asset/doctor-logo.png",
    "favicon": "/api/branding/asset/favicon.png",
    "loginBackground": "/api/branding/asset/login-bg.jpg",
    "doctorPhoto": "/api/branding/asset/doctor.png",
    "emailHeader": "/api/branding/asset/email-header.png"
  },
  "support": {
    "email": "support@riverside-med.example",
    "phone": "+1 555 123 4567",
    "hours": "Mon-Fri 8am-5pm PT"
  },
  "address": "123 Main St, Riverside, CA 92501",
  "doctor": {
    "displayName": "Dr. Jane Smith, M.D.",
    "specialty": "PQME / AME"
  },
  "termsAndConditions": "<html>... per-tenant T&C text ...</html>",
  "themePalette": "default"
}
```

Loaded once at SPA bootstrap by `BrandingService` (Angular) and made available app-wide via `inject(BrandingService)`. Asset URLs resolve through ABP `IBlobStorage` -> CDN.

### Localization keys (Domain.Shared/Localization/CaseEvaluation/en.json)

For per-feature copy that uses brand tokens:

```json
{
  "Brand:ProductName": "Patient Appointment Portal",
  "Brand:Welcome": "Welcome to {0}",
  "Email:Subject:Registration": "You have registered successfully - {0}",
  "Email:Subject:PasswordReset": "{0} - Reset Password",
  "Email:Subject:PasswordChanged": "Your password has been successfully changed - {0}",
  "Email:Subject:AppointmentDocumentStatus": "{0} - ({1}) - Appointment document is {2}",
  "Email:Subject:AccessorInvite": "{0} - ({1}) - Accessor details",
  "Email:Subject:DailyDigest": "Pending Appointment Request",
  "Email:Subject:WelcomeInternalUser": "Welcome to {0}"
}
```

The `{0}`, `{1}`, etc. placeholders accept the brand tokens (`ClinicName`, `Patient + Claim + ADJ` formatted patient details, etc.) at render time.

Localization KEYS are language-agnostic; values are translated per language file (`es.json`, `zh.json`). Tenant-specific overrides happen via ABP's `LocalizationManagement` if needed -- defer to Phase 2.

## Migration map: OLD hardcoded -> NEW token

| OLD code | NEW token / mechanism |
|----------|------------------------|
| `"Patient Appointment Portal"` literal | `IStringLocalizer["Brand:ProductName"]` -> reads `BrandingService.config.appProductName` (default "Patient Appointment Portal" until tenant overrides) |
| `"Welcome to socal"` literal | `L["Brand:Welcome", clinicShortName]` |
| `"Patient Appointment Portal - Reset Password"` | `L["Email:Subject:PasswordReset", clinicShortName]` |
| `"Patient Appointment Portal - ({patient}) - Appointment document is {status}"` | `L["Email:Subject:AppointmentDocumentStatus", clinicShortName, patientDetails, status]` |
| `header-logo.png` reference in `top-bar.component.html` | `<img [src]="branding.logos.header">` via `BrandingService` |
| `Doctor-logo.png` reference | `<img [src]="branding.logos.doctorLogo">` |
| `login-bg.jpg` reference | `background-image: var(--brand-login-bg-url)` in CSS |
| Color literals in CSS (e.g., `#1976d2`) | `var(--brand-primary)` |
| Footer "Patient Appointment Portal (c) 2024" copy | Footer component uses `branding.clinicName` + dynamic year |
| Welcome internal user email body inlined in `UserDomain.AddInternalUser` | `INotificationTemplate` with TemplateCode `WelcomeInternalUser` rendering Razor body using brand tokens |
| 4 demo CSS palettes (theme skins) | A single per-tenant generated stylesheet linked at runtime; legacy demo files dropped |

## Phase 1 implementation order (cross-link master plan)

When implementing in NEW, do branding wiring in this order. Each step unlocks subsequent feature work that needs the token:

1. **Define the `IBrandingConfig` interface** + DTO + AppService + per-tenant settings storage. AppService route: `GET /api/app/branding/current-tenant` (`[AllowAnonymous]` for the login page; cached in-memory for ~10 min).
2. **Seed the default tenant's branding config** via `BrandingDataSeedContributor` -- copying the OLD hardcoded values verbatim ("Patient Appointment Portal", `header-logo.png` blob, default theme palette) into the new config + blob storage. This guarantees Phase 1 visual identity matches OLD exactly.
3. **`BrandingService` Angular singleton** -- bootstraps in `app.config.ts`, loads config before any feature component renders, exposes via `Signal` so templates and SCSS can react to tenant switch (Phase 2).
4. **CSS custom properties wired** in `styles.scss`. Top-level `:root` declares the variables; subscribes to `BrandingService.themePalette` to swap palettes if needed.
5. **Email template renderer** registered as `INotificationTemplateRenderer` -- reads `INotificationTemplate.Subject + BodyEmail`, runs Razor with model `{ Branding, EventData }`, injects clinic name + logo URL + colors.
6. **Localization keys** added to `en.json` per the table above. Every feature audit's "Branding/theming touchpoints" section references one or more of these keys.
7. **Asset endpoint** (`/api/branding/asset/{key}`) -- streams from `IBlobStorage` container `branding-{tenantId}`, with the default-tenant container pre-seeded with copies of OLD's 5 PNGs.
8. **Per-feature wiring as features are implemented** -- when an audit doc's branding section says "logo, primary color, page title", the implementer wires `<img [src]="branding.logos.X">`, `var(--brand-primary)`, and `IStringLocalizer["Page:Title"]` accordingly.

## Strict-parity bug fixes allowed in NEW (already noted in feature audits)

Per the `_appointment-form-validation-deep-dive.md` and `_slot-generation-deep-dive.md` outputs, OLD has typos that are explicitly allowed to fix:

- `"Welcome to socal"` -> `"Welcome to {ClinicName}"` (capitalization fix + tokenization)
- `"Your have registered successfully"` -> `"You have registered successfully"`
- Mixed casing `"Patient appointment portal"` (lowercase variant in some emails) -> consistent `{ClinicName}` token
- Email subjects vary inconsistently across domain files -- normalize by using the localization key + branding token throughout

Document each as "OLD bug, fixed for correctness" in the implementation plan.

## NEW current state -- known UI gaps

From the existing audit docs and `Appointments/CLAUDE.md`:

- NEW's `appointment-add.component.ts` has `console.log('Date check:', ...)` left in -- not branding, but production-bound debug; remove.
- NEW's view page uses `ngModel` (legacy pattern). Refactor to `FormBuilder` for consistency. Branding wiring goes through `BrandingService` regardless of form pattern.
- NEW's `angular/src/assets/images/login/` contains login-bg SVGs (3 variants for theme modes). Verify these remain after branding aggregation OR replace with `--brand-login-bg-url` token.

## Risks + mitigations

| Risk | Mitigation |
|------|------------|
| Adding branding hooks late in implementation = full UI rewrite to swap | Start branding in Phase 1; every feature's UI uses tokens from day one. |
| Per-tenant asset upload fails or returns stale | Cache invalidation on `IBlobStorage.SetAsync(...)`; bust SPA's `BrandingService` cache via signal on update. |
| Two tenants with identical branding accidentally share assets | Container-per-tenant in IBlobStorage; never share blob URLs. |
| Email rendering misses clinic name token (renders `{0}` literal) | Razor renderer fails fast on missing token; render unit tests cover all template codes. |
| Default tenant config diverges from OLD's exact look | Pixel-diff regression test comparing OLD screens (manual capture) to NEW screens during Phase 1 demo. |

## Verification

When Phase 1 is feature-complete, the NEW app must visually + verbally match OLD's clinic for the seeded default tenant:

1. Header logo identical to OLD's `header-logo.png`.
2. Login page background identical to OLD's `login-bg.jpg`.
3. Email subjects matching OLD's exact strings (with typo fixes documented as bugs fixed for correctness).
4. Color palette matching OLD's active demo CSS (whichever was production).
5. Footer copy + support contact matching OLD.
6. Doctor profile photo + logo on the doctor's appointment view matches OLD.

When Phase 2 begins, swapping to a second tenant requires only:

- Upload 5 PNGs to the new tenant's IBlobStorage container.
- Insert one row in `BrandingConfig` table with the new tenant's strings.
- No code changes.
