---
feature: external-user-forgot-password
status: draft
audited: 2026-05-04
old-source:
  - patientappointment-portal/src/app/components/login/forgot-password/forgot-password.component.html
  - patientappointment-portal/src/app/components/login/forgot-password/forgot-password.component.ts
  - patientappointment-portal/src/app/components/login/reset-password/reset-password.component.html
  - patientappointment-portal/src/app/components/login/reset-password/reset-password.component.ts
  - patientappointment-portal/src/app/components/login/login.service.ts
  - PatientAppointment.Domain/Core/UserAuthenticationDomain.cs
new-source:
  - src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalAccount/IExternalAccountAppService.cs
  - src/HealthcareSupport.CaseEvaluation.Application/ExternalAccount/ExternalAccountAppService.cs
  - src/HealthcareSupport.CaseEvaluation.Application/ExternalAccount/PasswordResetGate.cs
  - src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/ExternalAccount/ExternalAccountController.cs
parity-audit: docs/parity/external-user-forgot-password.md
shell-variant: 1-unauthenticated
strict-parity: true
---

# external-user-forgot-password -- design

Two-step password reset flow that sits in shell variant 1
(unauthenticated auth-shell). Step 1: user enters email on
`/account/forgot-password`, receives a reset link. Step 2: user clicks
the link and lands on `/account/reset-password?...`, sets a new
password, and is redirected to login. Backend Phase 10 has already
shipped (`IExternalAccountAppService` + rate limiter + localized email
templates, see parity audit Section "Phase 10 implementation summary"
2026-05-03). This design.md captures the matching Angular UI contract.

## 1. Routes

- OLD: `/forgot-password` -> NEW: `/account/forgot-password`
- OLD: `/reset-password?activationkey=:guid&emailId=:email` -> NEW: `/account/reset-password?userId=:guid&resetToken=:token`

The OLD reset URL ships `activationkey` (raw GUID) + `emailId`. NEW
ships ABP's `DataProtectionTokenProvider` cryptographic token +
`userId` (Guid). Cite parity audit Section C "Plan correction (binding
for Phase 10 implementation)".

## 2. Screen layout

### Forgot-password form

```
+--------------------------------------------------------+
| login-bg.jpg image (full viewport) + dark scrim         |
|                                                        |
|        +----------------------------------+            |
|        |   [Doctor.png logo, centered]    |            |
|        |                                   |            |
|        |   Enter your email address and    |            |
|        |   we will send you a link to       |            |
|        |   reset your password.             |            |
|        |                                   |            |
|        |   [Enter your email address]      |            |
|        |                                   |            |
|        |   [Send Reset Password Email -    |            |
|        |    btn-secondary, btn-block]      |            |
|        |                                   |            |
|        |                       Login?      |   <- right-aligned text-muted
|        +----------------------------------+            |
|                                                        |
+--------------------------------------------------------+

OLD: ./screenshots/old/external-user-forgot-password/01-forgot-form.png
NEW: NEW UI not yet built.
```

Cite: `forgot-password.component.html`:1-29.

The instructional `<p>` text is verbatim from OLD line 14-16:
"Enter your email address and we will send you a link to reset your password."

### Reset-password form (success path)

```
+--------------------------------------------------------+
| login-bg.jpg image + dark scrim                         |
|                                                        |
|        +----------------------------------+            |
|        |   [Doctor.png logo, centered]    |            |
|        |   ----- inner card divider -----  |            |
|        |                                   |            |
|        |   [Email - read-only, prefilled]  |            |
|        |                                   |            |
|        |   [New password]                  |            |
|        |                                   |            |
|        |   [Confirm password]              |            |
|        |                                   |            |
|        |   [Reset Password -                |            |
|        |    btn-secondary, btn-block]      |            |
|        +----------------------------------+            |
|                                                        |
+--------------------------------------------------------+

OLD: ./screenshots/old/external-user-forgot-password/02-reset-form.png
```

Cite: `reset-password.component.html`:26-62. The lines 1-23 of OLD's
file are commented-out legacy markup -- ignore.

The Email input is `disabled` and pre-filled from the `emailId` query
param. NEW preserves `disabled` so a user can confirm they're resetting
the right account but cannot edit.

### Post-submit toasts

OLD `forgot-password.component.ts` shows toast
`validation.message.custom.resetLinkSend` on success (a localization
key). NEW renders the localized string via ABP `IStringLocalizer`. The
literal copy is in OLD's localization JSON -- TO LOCATE during impl;
default copy: "We have sent a password reset link to your registered
email address."

OLD `reset-password.component.ts` shows toast
`validation.message.custom.passwordReset` on success then redirects to
`/login` after 500ms. Default copy: "Your password has been reset.
Please log in with your new password."

## 3. Form fields

### Forgot-password form

| Label (sr-only) | Placeholder | Field name | Type | Validation | Default | Conditional visibility | OLD citation |
|---|---|---|---|---|---|---|---|
| (none -- placeholder only) | "Enter your email address" | `emailId` | text | required + email regex (HTML5 `pattern` attribute) | -- | always | `forgot-password.component.html`:17-19 |

The OLD `pattern=` attribute is a long inline regex matching standard
email shape:

```regex
^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$
```

NEW uses Angular's built-in `Validators.email` -- behaviorally close,
not regex-identical. Strict-parity exception #1 below.

### Reset-password form

| Label (sr-only) | Placeholder | Field name | Type | Validation | Default | Conditional visibility | OLD citation |
|---|---|---|---|---|---|---|---|
| Email | (none) | `emailId` | email | (disabled, read-only) | from `?emailId=` query | always | `reset-password.component.html`:43-45 |
| New Password | "New password" | `password` | password | required + maxLength 50 + matches `RegexConstant.systemPasswordPattern` (server-side) | -- | always | `reset-password.component.html`:46-49 |
| Confirm Password | "Confirm password" | `confirmPassword` | password | required + maxLength 50 + must equal `password` | -- | always | `reset-password.component.html`:50-53 |

Server-side validation in NEW is enforced via
`IdentityUserManager.ResetPasswordAsync` (cite NEW
`ExternalAccountAppService.ResetPasswordAsync`). Password regex is the
same ABP policy mapping documented in `external-user-registration-design.md`
Section 3.

## 4. Tables / grids

None.

## 5. Modals + interactions

| Trigger | Modal | Body | Primary action | Secondary action | OLD citation |
|---|---|---|---|---|---|
| Submit error (forgot) | `RxDialog.validation` (NEW: `<app-validation-popup>`) | server validation message (e.g., "Please verify your email address before resetting your password.") | OK | -- | `forgot-password.component.ts` (`error => dialog.validation(error)`) |
| Submit error (reset) | same | password mismatch / weak / token invalid / inactive user | OK | -- | `reset-password.component.ts` (`error => dialog.validation(error)`) |

OLD's gate messages (per parity audit Section A and Phase 10 impl):

| Gate | OLD message | NEW localization key |
|---|---|---|
| Email not found / soft-deleted | "User does not exist" | (NEW: silently returns to avoid info leak; OLD-bug-fix exception) |
| Email not verified | "We have sent a verification link to your registered email id, please verify your email address to do further process." | `Account:EmailNotConfirmedForPasswordReset` -> "Please verify your email address before resetting your password." |
| Inactive account | "Your account is not activated" | `Account:UserInactiveForPasswordReset` |

NEW shortens the unverified message (OLD's was misleading -- it
implied auto-resend that didn't actually happen).

## 6. Buttons + actions

### Forgot-password page

| Label | Variant | Permission gate | Pre-action confirm? | Success toast | Error toast | OLD citation |
|---|---|---|---|---|---|---|
| Send Reset Password Email | primary CTA (`btn btn-secondary btn-block`) | anonymous | N | "We have sent a password reset link to your registered email address." | (server message via validation popup) | `forgot-password.component.html`:20 |
| Login? | link, right-aligned text-muted | anonymous | N | n/a | n/a | `forgot-password.component.html`:21-23 |

### Reset-password page

| Label | Variant | Permission gate | Pre-action confirm? | Success toast | Error toast | OLD citation |
|---|---|---|---|---|---|---|
| Reset Password | primary CTA (`btn btn-secondary btn-block`) | anonymous (token-bound) | N | "Your password has been reset. Please log in with your new password." | (server message via validation popup) | `reset-password.component.html`:54 |

After success, navigate to `/account/login` after 500ms (OLD's literal
delay -- preserved verbatim).

## 7. Role visibility matrix

Anonymous flow. The 4 external roles all use the same form; internal
users (Clinic Staff / Staff Supervisor / IT Admin) also use the same
form per parity audit Section "Internal dependencies surfaced" (no
separate IT-Admin reset path).

| UI element | (anonymous visitor) | Patient | Adjuster | Applicant Atty | Defense Atty | Clinic Staff | Staff Supervisor | IT Admin |
|---|---|---|---|---|---|---|---|---|
| Forgot-password form | Y | (N/A) | (N/A) | (N/A) | (N/A) | (N/A) | (N/A) | (N/A) |
| Reset-password form (token-bound) | Y when token valid | -- | -- | -- | -- | -- | -- | -- |

The token-bound gate means: anyone with a valid reset token can land
on the reset page; without one, the form rejects.

## 8. Branding tokens used

Same auth-shell token set as `external-user-registration-design.md`
plus the email template branding:

- `--brand-login-bg-url`, `--scrim-image`, `--brand-logo-url` (auth shell)
- `--bg-card`, `--border-light`, `--shadow-login-form` (card)
- `--brand-primary` (Send Reset Password Email + Reset Password buttons), `#fff` text
- `--text-muted` (instruction `<p>`, "Login?" link wrapper)
- `--brand-primary-text` (Login? link)
- `--color-danger` (invalid bottom border)
- Email subjects per `_branding.md` Section "Localization keys":
  - `L["Account:PasswordResetEmailSubject", clinicShortName]` -> "Patient Appointment Portal - Reset Password" (OLD verbatim, with clinic token)
  - `L["Account:PasswordChangedEmailSubject", clinicShortName]` -> "Your password has been successfully changed - {ClinicName}"

## 9. NEW current-state delta

**Backend already shipped** (Phase 10, 2026-05-03 -- cite parity audit
Section G). Files:

- `IExternalAccountAppService` exposes `SendPasswordResetCodeAsync(SendPasswordResetCodeInput)` + `ResetPasswordAsync(ResetPasswordInput)`.
- `ExternalAccountController` mounts at `api/public/external-account/` with two anonymous POST endpoints (`send-password-reset-code` + `reset-password`).
- `PasswordResetGate.EnsureUserCanRequestReset` enforces verified + active gates.
- 23 unit tests cover the gate, normalization, password match, URL builder, IdentityResult classifier.
- `CaseEvaluationHttpApiHostModule.ConfigurePasswordResetRateLimiter` adds 5/hour/email rate limit (Phase 10 / Q3 resolution).
- 9 new `Account:*` localization keys in `Domain.Shared/Localization/CaseEvaluation/en.json`.

**Frontend NEW UI not yet built.** No `account/forgot-password` or
`account/reset-password` Angular components exist; ABP AuthServer's
LeptonX Razor pages currently host these by default. Two impl options
per parity audit:

- **(a) Use ABP AuthServer Razor pages** (default; LeptonX-themed). NEW
  customizes copy + branding via LeptonX overrides (Phase 19b). The
  AuthServer URL replaces `/account/...`.
- **(b) Build Angular components in the SPA** that POST to
  `api/public/external-account/...`. More work, more control over
  per-tenant theming via `BrandingService`.

This design.md targets **option (b)** because the ExternalAccount
AppService is already custom (not ABP's `AccountAppService`), the
rate-limited endpoints already exist, and the SPA's brand-token
cascade gives the cleanest per-tenant override path. If Adrian
prefers (a), the AuthServer Razor pages still consume the same
backend; only the chrome moves to LeptonX.

## 10. Strict-parity exceptions

1. **Email regex validator simplified.** OLD ships an inline RFC-5322-ish regex on the email input (`pattern=`). NEW uses Angular's `Validators.email` for the client-side guard. Both reject obviously invalid emails; the server-side ASP.NET Identity validator is the load-bearing check.
2. **Token format change.** OLD's `?activationkey=:guid` -> NEW's `?resetToken=:signed-token` (ABP `DataProtectionTokenProvider`). Cryptographic, time-limited, not stored in DB. URL shape differs but UX identical.
3. **No 500ms artificial delay before redirect.** OLD ships a literal `setTimeout(...500)` between toast + navigate. NEW preserves this delay so the toast is readable; if toast service supports `onClose` callback, prefer that.
4. **Missing-email branch silently returns.** OLD line 177 returns `UserNotExist` (info leak -- attacker can enumerate registered emails). NEW silently returns success-shape per Phase 10 `PasswordResetGate.cs` doc -- documented OLD-bug-fix exception.
5. **Subject literal "Patient Appointment Portal" -> "{ClinicName}".** Per `_branding.md` localization keys; preserves OLD verbatim with a per-tenant token.
6. **5 req/hour/email rate limit added.** OLD has none (verified via grep over `P:\PatientPortalOld\PatientAppointment.{Api,Infrastructure}` -- only AWSSDK matches); standard hardening.
7. **Architecture deviation.** AppService (`IExternalAccountAppService`) instead of overriding ABP Pro's `AccountAppService` because ABP Pro 10.0.2 obfuscates the Account module (`L8nSSMWkSiiYJHjaCG9.lRHeAfgqo7WklTsGVuU` member names). Cite parity audit Section C.
8. **Inline confirmation email** instead of `UserPasswordChangedEto` event subscriber: ABP Identity 10.0.2 does not emit such an event (verified by reflection). Equivalent observable behavior.
9. **Reset-password page Email field is `disabled`** (read-only) -- OLD's behavior, preserved. NEW: same; the field cannot be tampered with via DOM.

## 11. OLD source citations (consolidated)

```
- patientappointment-portal/src/app/components/login/forgot-password/forgot-password.component.html
- patientappointment-portal/src/app/components/login/forgot-password/forgot-password.component.ts
- patientappointment-portal/src/app/components/login/login.service.ts
- patientappointment-portal/src/app/components/login/reset-password/reset-password.component.html
- patientappointment-portal/src/app/components/login/reset-password/reset-password.component.ts
- patientappointment-portal/src/app/database-models/v-user-record.ts (ChangeCredentialViewModel shape)
- PatientAppointment.DbEntities/Constants/RegexConstant.cs
- PatientAppointment.Domain/Core/UserAuthenticationDomain.cs
```

## 12. Verification (post-implementation)

- [ ] Visit `/account/forgot-password` -- the email input + instruction `<p>` + "Login?" link render per the wireframe above.
- [ ] Submit unverified user's email -- the validation popup renders `Account:EmailNotConfirmedForPasswordReset` localized message.
- [ ] Submit inactive user's email -- the popup renders `Account:UserInactiveForPasswordReset`.
- [ ] Submit unknown / soft-deleted email -- the form returns success toast (silent return; no info leak).
- [ ] Submit verified active user's email -- success toast + email arrives with subject `"Patient Appointment Portal - Reset Password"` (or per-tenant equivalent) + reset link.
- [ ] Click reset link -- `/account/reset-password?userId=...&resetToken=...` loads with email pre-filled (read-only).
- [ ] Submit weak password -- popup renders password-policy message.
- [ ] Submit mismatched confirmation -- popup renders confirmation-mismatch message.
- [ ] Submit valid new password -- success toast `"Your password has been reset..."`, redirect to `/account/login` after 500ms.
- [ ] Confirmation email arrives with subject `"Your password has been successfully changed - {ClinicName}"`.
- [ ] Old password rejected on login; new password accepted.
- [ ] Click reset link a second time after success -- token rejected (one-time use; ABP enforces).
- [ ] 6th forgot-password request within an hour for the same email -- 429 Too Many Requests.
- [ ] "Login?" link on forgot-password page -- routes to `/account/login`.
- [ ] Per-tenant theme: swap brand-primary -- both buttons + email subject template re-render with new branding end-to-end.
