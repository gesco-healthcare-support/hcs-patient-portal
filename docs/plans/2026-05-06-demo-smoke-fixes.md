---
feature: demo-smoke-fixes
date: 2026-05-06
status: in-progress
base-branch: feat/replicate-old-app-track-domain
related-issues: [demo-smoke-report-2026-05-05]
---

# Demo Smoke Fixes — 2026-05-06

## Goal

Fix every blocker / major issue surfaced by the 2026-05-05 demo smoke test plus four new issues Adrian raised in review, then re-run the 7-flow smoke and prove flows 4-7 pass.

## Context

Source report: `.playwright-mcp/demo-smoke-report-2026-05-05.md`. Adrian's decisions per issue locked in chat 2026-05-06. Two new architectural memories filed:
- `project_root-redirect-anonymous.md` — bare `/` should redirect to login (no public landing).
- `project_external-registration-role.md` — public register form is Patient-only; non-Patient external accounts are admin-provisioned.

OLD investigation findings (cited in plan tasks below) come from the 2026-05-06 Explore agent runs.

## Approach

Strict OLD-parity choices for every issue except where Adrian explicitly deviated (Issue 3 option B, public-register Patient-only). One PR slice per logical change so review can isolate. Backend changes in `Domain.Shared` / `Domain` / `Application` first, then SPA changes, then localization sweep.

## Tasks

### T1 — Strip Doctor M2M from booking-side lookups (Issue 1)

- approach: tdd
- files-touched:
  - `src/HealthcareSupport.CaseEvaluation.Application/Appointments/AppointmentsAppService.cs`
  - `test/HealthcareSupport.CaseEvaluation.Application.Tests/Appointments/AppointmentsAppService_LookupTests.cs` (new)
- acceptance:
  - `GetAppointmentTypeLookupAsync` queries `IRepository<AppointmentType>` directly under the IMultiTenant filter; no Doctor join.
  - `GetLocationLookupAsync` queries `IRepository<Location>` directly under the IMultiTenant filter; no Doctor join.
  - Same fix applied to any other `_doctorRepository.SelectMany(...)` lookup sibling in the same file.
  - New unit test asserts: tenant has 6 AppointmentTypes + 2 Locations + ZERO Doctors → both lookups return the expected 6 / 2 rows.
  - Re-running Flow 4 in the smoke test shows non-empty AppointmentType + Location dropdowns.

### T2 — Extend `ExternalUsersDataSeedContributor` to create domain entities (Issue 2 + 3 option B)

- approach: code
- files-touched:
  - `src/HealthcareSupport.CaseEvaluation.Domain/Identity/ExternalUsersDataSeedContributor.cs`
- acceptance:
  - For each seeded external user, the matching domain row is created using the same `*Manager.CreateAsync` calls that `ExternalSignupAppService.RegisterAsync` uses (Patient via `_patientManager`, Adjuster has no domain row, ApplicantAttorney via `_applicantAttorneyManager`, DefenseAttorney via `_defenseAttorneyManager`).
  - At least 2 distinct external users (`patient@<slug>.test` + `applicant.attorney@<slug>.test`) have `EmailConfirmed=true` so they can log in without the verify-email gate.
  - Idempotent: re-running `db-migrator` does not duplicate domain rows.

### T3 — Add anonymous-route guard (Issue 4)

- approach: code
- files-touched:
  - `angular/src/app/account/anonymous.guard.ts` (new)
  - `angular/src/app/account/account.routes.ts` (or wherever account routes are registered)
- acceptance:
  - Visiting `/account/login` or `/account/register` while authenticated redirects to `/dashboard` (internal roles) or `/home` (external roles) without showing the form. Mirrors OLD's `CanActivatePage` `anonymous: true` behavior.
  - Visiting the same routes anonymously renders the form normally.
  - No regression in normal logout → login → dashboard flow.

### T4 — Hide tenant box on SPA `/account/*` routes (Issue 5)

- approach: code
- files-touched:
  - `angular/src/app/shared/no-op-tenant-box.component.ts` (new minimal component returning empty template)
  - `angular/src/app/app.config.ts` (register `replaceableComponents` entry mapping `eAccountComponents.TenantBox` → no-op)
- acceptance:
  - SPA register / login / forgot-password pages no longer render the `Tenant: Falkinstein / switch` panel.
  - No console error and no missing-component crash.
  - AuthServer-side login (port 44368) was already clean (T2 of ADR-006) — confirm no regression.

### T5 — Anonymous root URL redirects to login (new issue N3)

- approach: code
- files-touched:
  - `angular/src/app/app.routes.ts` (root path entry)
  - `angular/src/app/home/home.routes.ts` (or wherever the current `/` "click Login" page is registered)
- acceptance:
  - `http://falkinstein.localhost:4200/` for an anonymous user redirects directly to the AuthServer login URL (or to `/account/login` if SPA-side login exists; otherwise the AuthServer authorize URL).
  - Authenticated user visiting `/` lands on their role-appropriate home (`/dashboard` for internal, patient home for external).
  - The "Click Login or Register" CTA page is removed from the root path. (Component itself can stay if used elsewhere.)

### T6 — Remove User Type field from public register form (new issue N4)

- approach: code
- files-touched:
  - `angular/src/app/account/register/register.component.ts`
  - `angular/src/app/account/register/register.component.html`
  - `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs` (server-side defense-in-depth check)
- acceptance:
  - Register form does not render a User Type select.
  - Form's `userType` form-control defaults to `ExternalUserType.Patient` and is non-editable (set in constructor / form initialization).
  - Submit POST sends `userType: Patient` regardless of any client tampering.
  - Server-side: `RegisterAsync` validates `input.UserType == ExternalUserType.Patient` for the public route. (Existing internal-admin-driven creation paths should already use a separate code path; confirm none rely on the public route for non-Patient roles.)
  - Conditional template fragments keyed off attorney roles are removed to keep template small.

### T7 — Fix AppName branding on AuthServer pages (new issue N1)

- approach: code
- files-touched:
  - `src/HealthcareSupport.CaseEvaluation.AuthServer/CaseEvaluationAuthServerModule.cs` (verify localization resource is added; debug why `_localizer["AppName"]` resolves to the literal "AppName")
  - `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainSharedModule.cs` (likely missing localization wiring on AuthServer side)
- acceptance:
  - AuthServer login page header shows "Welcome to Dr. Falkenstein's Appointment Portal" (or whatever en.json's `AppName` evaluates to), not the literal text "AppName".
  - Same fix carries to register page header on AuthServer.
  - SPA-side branding (header navbar) shows the same name once T8 below also lands.

### T8 — Wire up email-confirmation flow end-to-end (Issue 8)

- approach: tdd
- files-touched:
  - `angular/src/app/account/email-confirmation/email-confirmation.component.ts` (new)
  - `angular/src/app/account/email-confirmation/email-confirmation.component.html` (new)
  - `angular/src/app/account/account.routes.ts` (add `/account/email-confirmation` route)
  - `src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/UserRegisteredEmailHandler.cs` (point email link at the SPA route, not AuthServer's `/Account/EmailConfirmation`)
  - `appsettings.Development.json` (set `Settings:Notifications:AuthServerBaseUrl` or whatever holds the link base)
- acceptance:
  - Email link format: `http://falkinstein.localhost:4200/account/email-confirmation?userId=<guid>&confirmationToken=<urlencoded>`.
  - Component on init parses query params, calls `IAccountAppService.SendPasswordResetCodeAsync`-equivalent for confirmation (check ABP API: likely `IAccountAppService.VerifyEmailAsync` or `IIdentityUserAppService.ConfirmEmailAsync`).
  - On success: shows "Email confirmed" message + button to navigate to `/account/login`.
  - On failure (expired/invalid): shows error message + button to request a new confirmation email.
  - Re-running Flow 3 in the smoke test, then opening the email link from logs, lands on the SPA, confirms, and lets the user log in.

### T9 — Populate missing localization keys (Issue 6)

- approach: code
- files-touched:
  - `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`
- acceptance:
  - Keys observed leaking in flows 2-4: `Menu:Home`, `Menu:Dashboard`, `Menu:AppointmentManagement`, `Menu:Configurations`, `Menu:DoctorManagement`, `Menu:Doctors`, `Menu:Locations`, `Menu:WcabOffices`, `Menu:DoctorAvailabilities`, `Menu:Patients`, `Menu:Appointments`, `Menu:ApplicantAttorneys`, `Menu:DefenseAttorneys`, `Dashboard:Title`, `SetAvailabilitySlot`, `SlotByDates`, `SlotByWeekdays`, `BookingStatusId`, `Enum:BookingStatus.8`, `Enum:BookingStatus.9`, `Enum:BookingStatus.10`, `Enum:Gender.1-3`, `Enum:PhoneNumberType.28-29`, `RefferedBy`, `NewAppointment`. All resolved to plain English in en.json.
  - Re-running Flow 2 + Flow 4 shows English text everywhere; no `Menu:`, `Enum:`, or `Foo:Bar` raw strings rendered to the page.

### T10 — Update / delete drifted docs (Issue 7)

- approach: code
- files-touched:
  - any spec / parity audit doc referencing `/doctor-availabilities/generate` (the actual route is `/doctor-management/doctor-availabilities/generate`).
- acceptance:
  - `grep -r "/doctor-availabilities" docs/` returns zero matches with the wrong route.

## Risk / Rollback

- **Blast radius:** lookup query rewrite (T1) is theoretically a behavior change for every booking, but the new query is strictly more permissive than the old broken one (returns rows where the old returned 0). No data corruption risk.
- **Seed change (T2):** Development-environment-gated (matches the Internal seed pattern); does not run in prod.
- **Tenant-box hide (T4):** purely a UI surface change, no data path.
- **Email confirmation (T8):** changes the link format. Existing in-flight emails (none in dev) would break. Redeploy plan: ship config + SPA route together.
- **Rollback:** revert the merge commit; nothing migrates DB schema, so a revert is clean.

## Verification

End-to-end re-run of the 7-flow smoke test. Specifically:
1. Flow 1 / 2: regression check (should still pass).
2. Flow 3: register a new patient, watch the email-send log, copy the link, paste it -> should land on the SPA email-confirmation page and flip EmailConfirmed.
3. Flow 4: log in as the just-confirmed e2e patient (or `patient@falkinstein.test`), open `/appointments/add?type=1`, verify dropdowns populated, fill form, pick the seeded slot, submit. Confirmation number returned.
4. Flow 5: navigate to appointments list as patient; confirm just-booked row visible.
5. Flow 6: log out patient, log in as supervisor, approve the booking via the modal. Status changes.
6. Flow 7: confirm SubmissionEmail + ApprovalEmail send-job logs fire. Packet PDF still expected blocked per audit doc.
