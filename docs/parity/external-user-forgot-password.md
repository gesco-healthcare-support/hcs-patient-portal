---
feature: external-user-forgot-password
old-source:
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\UserAuthenticationController.cs (PostForgotPassword + PutCredential)
  - P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs (PostForgotPassword + PutCredential + ForgotPasswordValidation + PutCredentialValidation + SendEmail)
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\login\forgot-password\forgot-password.component.ts
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\login\reset-password\reset-password.component.ts
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\login\login.service.ts
old-docs:
  - socal-project-overview.md (lines 237-239)
  - socal-api-documentation.md (UserAuthentication/postforgotpassword + putforgotpassword sections)
audited: 2026-05-01
status: audit-only
priority: 1
depends-on:
  - external-user-registration  # shares VerificationCode field (single column reused for email-verify and password-reset)
  - external-user-login         # forgot-password is reachable from login form
---

# External user forgot password (+ password reset)

## Purpose

User who has forgotten their password enters their email on `/forgot-password`, receives a reset link, clicks the link to land on `/reset-password?activationkey=<code>&emailId=<email>`, enters a new password, and is redirected to `/login`. **Verified users only** -- unverified or inactive accounts cannot use forgot-password.

## OLD behavior

### Forgot-password form (UI)

`patientappointment-portal/src/app/components/login/forgot-password/forgot-password.component.ts`. Fields:

- `EmailId` -- text input (binds to `ForgotCredentialModel`)
- "Submit" button calls `submitForgotPassword()`
- "Back to login" link calls `gotoLogin()` -> `/login`

### Step 1 backend (`POST /api/UserAuthentication/postforgotpassword` -> `UserAuthenticationDomain.PostForgotPassword`)

Reading `UserAuthenticationDomain.cs` lines 157-217:

1. **Validation (`ForgotPasswordValidation`):**
   - Look up user by `EmailId == userCredential.EmailId` AND `StatusId != Status.Delete`
   - If user not found -> `UserNotExist` validation message
   - If `StatusId == Status.Delete` -> `UserNotExist` (defensive duplicate of above)
   - If `!user.IsVerified` -> `"We have sent a verification link to your registered email id, please verify your email address to do further process."` (note: misleading -- backend is NOT actually re-sending the verification email here, just blocking forgot-password)
   - If `StatusId == Status.InActive` -> `"Your account is not activated"`
2. **If validation passes (`PostForgotPassword`):**
   - Generate `user.VerificationCode = Guid.NewGuid()` (**overwrites any pending email-verification code**)
   - Set `user.ModifiedOn = DateTime.Now`
   - `LoginUow.RegisterDirty(user); LoginUow.Commit()`
   - Build reset link: `{clientUrl}/reset-password/?activationkey={verificationCode}&emailId={email}`
   - Send email via `EmailTemplate.ResetPassword` with subject `"Patient Appointment Portal - Reset Password"`
3. Return `UserAuthenticationViewModel { FailedLogin = true, ValidationMessage = ResetPasswordLink }` -- the `FailedLogin = true` flag is misleading; the front-end shows a success toast `validation.message.custom.resetLinkSend` regardless.

### Reset-password form (UI)

`components/login/reset-password/reset-password.component.ts`. Reads from query string:

- `activationkey` -> `activationKey` (the `VerificationCode` GUID)
- `emailId`
- `isActivateUser` (unused in this flow; legacy)
- `userId` (unused in this flow; legacy)

Fields on the form (`ChangeCredentialViewModel`):

- `Password`
- `ConfirmPassword`

Submit (`resetPassword()`):

- Sets `changeCredentialViewModel.emailId = emailId` (from query)
- Sets `changeCredentialViewModel.verificationCode = activationKey` (from query)
- Sets `changeCredentialViewModel.isResetPassword = true`
- Calls `loginService.put(changeCredentialViewModel)` -> `PUT /api/UserAuthentication/putforgotpassword`
- On success: shows toast `validation.message.custom.passwordReset`, redirects to `/login` after 500ms
- On error: shows validation dialog

### Step 2 backend (`PUT /api/UserAuthentication/putforgotpassword` -> `UserAuthenticationDomain.PutCredential`)

Reading `UserAuthenticationDomain.cs` lines 219-256:

1. **Validation (`PutCredentialValidation`):**
   - Look up user by `EmailId AND VerificationCode == provided code`
   - Validate `Password == ConfirmPassword` -> else `ConfirmPasswordValidation` message
   - Validate password matches `RegexConstant.systemPasswordPattern` regex -> else `PasswordPatternValidation` message
   - If user found AND `StatusId != Status.Active` -> `UserInactivated` message
   - **Note:** if user is NOT found by email+code combo, validation passes silently (no "Invalid token" error). The actual update step skips if user is null.
2. **Action (`PutCredential`):**
   - Look up user again by `EmailId AND VerificationCode == provided code`
   - If user found:
     - Hash new password (`Encrypt` -> `Signature + Salt`)
     - Update `user.Password = Signature`, `user.Salt = Salt`
     - **Set `user.VerificationCode = null`** (one-time use; clears for future reset)
     - `LoginUow.RegisterDirty(user); LoginUow.Commit()`
     - Send confirmation email via `EmailTemplate.PasswordChange` with subject `"Your password has been successfully changed - Patient Appointment portal"` (typo "Your" should be "Your password" -- actually this one reads correctly).
   - Returns the User entity (or null if lookup failed -- frontend treats null as success since `failedLogin` won't be set).

### Critical OLD behaviors

- **`VerificationCode` is shared between email-verification and password-reset.** A user with a pending verification who initiates forgot-password has their verification code overwritten -- they will need to log in once (which auto-resends a new verification email) before completing verification. Note: ABP separates these into different token providers, so this edge case disappears in NEW.
- **`VerificationCode` is cleared (`null`) after successful reset.** One-time use.
- **Verified-only forgot-password.** Unverified users cannot use forgot-password (see `ForgotPasswordValidation`). They must verify first via the email-verification flow.
- **Password regex enforced on reset** (`RegexConstant.systemPasswordPattern`) -- same regex as registration.
- **No auto-login after reset.** User must navigate to login and authenticate with new password.
- **Confirmation email after reset** to the user's registered email.
- **No rate-limiting visible in OLD code.** A bad actor could spam `postforgotpassword` for any email; OLD doesn't appear to rate-limit -- TO VERIFY by reading auth middleware.

## OLD code map

### Backend (.NET / C#)

| Layer | File | Purpose |
|-------|------|---------|
| API controller | `PatientAppointment.Api/.../UserAuthenticationController.cs` lines 56-66 | `POST /postforgotpassword` -> `UserAuthenticationDomain.PostForgotPassword` |
| API controller | `PatientAppointment.Api/.../UserAuthenticationController.cs` lines 69-79 | `PUT /putforgotpassword` -> `UserAuthenticationDomain.PutCredential` |
| Domain | `PatientAppointment.Domain/Core/UserAuthenticationDomain.cs` lines 157-180 | `ForgotPasswordValidation` |
| Domain | `PatientAppointment.Domain/Core/UserAuthenticationDomain.cs` lines 182-217 | `PostForgotPassword` (sends email with reset link) |
| Domain | `PatientAppointment.Domain/Core/UserAuthenticationDomain.cs` lines 219-241 | `PutCredentialValidation` |
| Domain | `PatientAppointment.Domain/Core/UserAuthenticationDomain.cs` lines 242-256 | `PutCredential` (saves new password, clears VerificationCode) |
| Domain | `PatientAppointment.Domain/Core/UserAuthenticationDomain.cs` lines 300-319 | `SendEmail` (post-reset confirmation email) |
| Email templates | `EmailTemplate.ResetPassword`, `EmailTemplate.PasswordChange` | TO LOCATE during impl |
| Models | `PatientAppointment.Models.ViewModels.{UserCredentialViewModel, ChangeCredentialViewModel, vEmailSenderViewModel}` | -- |

### Frontend (Angular)

| Component | File | Purpose |
|-----------|------|---------|
| Forgot-password form | `components/login/forgot-password/forgot-password.component.{ts,html}` | Email input -> `POST /postforgotpassword` |
| Reset-password form | `components/login/reset-password/reset-password.component.{ts,html}` | Reads `activationkey` + `emailId` from query, captures new password -> `PUT /putforgotpassword` |
| Login service | `components/login/login.service.ts` | Wraps both endpoints (`postForgotPassword`, `put`) |
| Models | `components/login/domain/login.models.ts` -- `ForgotCredentialModel` | Email-only model |
| Models | `components/login/domain/change-password.models.ts` -- `ChangeCredentialViewModel` | New password + confirmation + token + email + flags |

## NEW current state

### Backend (ABP)

- **No custom forgot-password AppService.** ABP provides:
  - `IAccountAppService.SendPasswordResetCodeAsync(SendPasswordResetCodeDto { Email, AppName, ReturnUrl })` -- sends reset email
  - `IAccountAppService.ResetPasswordAsync(ResetPasswordDto { UserId, ResetToken, Password })` -- verifies token + sets new password
  - Endpoints (default routes): `POST /api/account/send-password-reset-code`, `POST /api/account/reset-password`
- **Token format:** ASP.NET Core Identity `DataProtectionTokenProvider` token. NOT a GUID stored on the user record. Generated cryptographically and verified on demand. **No reuse of email-confirmation token** (separate token providers).
- **No "verified-only" gate** by default -- ABP allows password reset for unverified users.

(For Adrian, Node analogue: ABP's `DataProtectionTokenProvider` is similar to using `jwt.sign()` / `jwt.verify()` with a server-side secret -- the token is self-validating, not stored. Cleaner than OLD's "store a GUID in the DB and look it up" approach.)

### Frontend

- **No custom Angular forgot-password / reset-password components.** ABP's AuthServer provides Razor Pages at `/Account/ForgotPassword` and `/Account/ResetPassword`.
- **LeptonX theme** styles these pages.
- **Default flow:** AuthServer login page has a "Forgot Password" link that redirects to `/Account/ForgotPassword`. After reset, user redirects back to login.

## Gap analysis

3-column gap table. Severity: **B** = blocker, **I** = important, **C** = cosmetic.

| OLD | NEW (current) | Gap | Sev |
|-----|---------------|-----|-----|
| Custom `VerificationCode` GUID stored in user record, reused across email-verify + password-reset | Separate cryptographic tokens via different token providers | None -- ABP improvement; OLD's reuse edge case (forgot-password overwrites pending verification) disappears | -- |
| URL: `{clientUrl}/reset-password/?activationkey={guid}&emailId={email}` | ABP default: `/Account/ResetPassword?userId={guid}&resetToken={signed-token}` | URL shape differs. **Acceptable** since AuthServer hosts the page (not Angular SPA). LeptonX customization handles visual styling. | I |
| User-id type: int | Guid | Unavoidable -- ABP user IDs are Guid. URL format change is the visible effect. | -- |
| **Verified-only password reset** -- unverified users get `"please verify your email address to do further process"` | ABP default allows password reset for unverified users | **Decision needed:** mirror OLD's verified-only rule, or accept ABP's default? OLD's rule is more restrictive. **Recommend mirror OLD** for parity -- override `IAccountAppService.SendPasswordResetCodeAsync` or check `EmailConfirmed` before sending the reset code. | I |
| Inactive account check (`Status.InActive`) -> `"Your account is not activated"` | ABP returns a different error for `IsActive = false` (or `LockedOut`) | Map ABP errors to OLD's user-facing strings (or accept ABP defaults -- equivalent). | C |
| Password regex enforced on reset (`systemPasswordPattern`) | ABP enforces password policy from `IdentityOptions` (currently weakened in NEW per registration audit) | Same fix as registration -- tighten ABP password policy in `ChangeIdentityPasswordPolicySettingDefinitionProvider`. | I |
| Confirmation email sent after successful reset | ABP does NOT send a confirmation email after reset by default | **Add post-reset notification email** via `IEmailSender` invocation in a custom `IAccountAppService` override OR via `IDistributedEventBus` subscriber to ABP's password-changed event. **Recommend distributed event subscriber** -- decoupled, doesn't require overriding ABP. | I |
| Reset email subject: `"Patient Appointment Portal - Reset Password"` | ABP default | Customize ABP email template + subject to match (with `{ClinicName}` token for multi-tenant). | C |
| Confirmation email subject: `"Your password has been successfully changed - Patient Appointment portal"` | Not sent by ABP | Add via the event subscriber above. Subject: `"Your password has been successfully changed - {ClinicName}"`. | C |
| Frontend redirects to `/login` after success | AuthServer Razor page redirects similarly (configurable) | None | -- |
| No rate limiting on `/postforgotpassword` (TO VERIFY) | ABP supports rate limiting via `Volo.Abp.AspNetCore.Mvc.RateLimiting` | **Add rate limiting** to NEW's password-reset endpoints regardless of OLD behavior. Standard hardening. | I |
| `failedLogin = true` returned even on success (frontend ignores; shows success toast) | ABP returns proper success/failure | None -- OLD was buggy here, NEW is correct. | -- |

## Internal dependencies surfaced

- **Email templates** (`ResetPassword`, `PasswordChange`) live in OLD's `EmailTemplate` enum + HTML resource path. **Locate during impl.** Same as registration audit's open item.
- **`SystemParameters` table** has no fields directly relevant to password reset (no token TTL configured -- ABP defaults to 1-day token expiry; acceptable).
- **No internal user dependencies** -- forgot-password is self-contained for external users. Internal users use the same flow (no separate IT-Admin-driven reset; if internal user forgets password, they use the same forgot-password page or contact IT Admin out-of-band).

## Branding/theming touchpoints

- **Forgot-password page** (`Pages/Account/ForgotPassword.cshtml` or LeptonX override): logo, primary color, page title, copy
- **Reset-password page** (`Pages/Account/ResetPassword.cshtml` or LeptonX override): same as above
- **Reset email subject + body** (`EmailTemplate.ResetPassword`): logo, primary color, footer copy, support email/phone, "{ClinicName}"
- **Confirmation email subject + body** (`EmailTemplate.PasswordChange`): same brand surfaces

Output during impl: feeds into `docs/parity/_branding.md` (task #6).

## Replication notes

### ABP-specific wiring

- **Reuse ABP's built-in flow** -- no custom AppService for the happy path.
- **Verified-only gate (recommended for parity):** override `IAccountAppService` impl OR add a `IBeforePasswordResetCodeRequestedHandler` (if ABP exposes such a hook -- TO VERIFY). Simplest: extend `AccountAppService` and override `SendPasswordResetCodeAsync` to throw if `user.EmailConfirmed == false`:

  ```csharp
  // pseudocode
  public override async Task SendPasswordResetCodeAsync(SendPasswordResetCodeDto input)
  {
      var user = await UserManager.FindByEmailAsync(input.Email);
      if (user != null && !user.EmailConfirmed)
      {
          throw new BusinessException("ExternalUser:EmailNotConfirmedForPasswordReset");
      }
      await base.SendPasswordResetCodeAsync(input);
  }
  ```

  Localization key in `Domain.Shared/Localization/CaseEvaluation/en.json`:
  - `"ExternalUser:EmailNotConfirmedForPasswordReset"`: `"Please verify your email address before resetting your password."`

- **Post-reset confirmation email:** subscribe to ABP's `UserPasswordChangedEto` distributed event:

  ```csharp
  // pseudocode
  public class SendPasswordChangedEmailHandler : IDistributedEventHandler<UserPasswordChangedEto>
  {
      private readonly IEmailSender _emailSender;
      private readonly ITemplateRenderer _templateRenderer;
      public async Task HandleEventAsync(UserPasswordChangedEto eventData) { ... }
  }
  ```

  ABP doc: https://docs.abp.io/en/abp/latest/Distributed-Event-Bus

  (For Adrian, Node analogue: this is similar to subscribing to a Mongoose post-save hook or emitting an event on EventEmitter and subscribing in another module.)

- **Password policy:** same fix as registration audit -- `RequireDigit=true`, `RequireNonAlphanumeric=true`, `RequiredLength=8` in `ChangeIdentityPasswordPolicySettingDefinitionProvider`.

- **Email templates** customized to match OLD copy (with `{ClinicName}` brand token) via ABP's `IEmailTemplateProvider` or stored in localization resource.

- **Rate limiting** on `/api/account/send-password-reset-code` and `/api/account/reset-password`: add `[RateLimit]` attribute or configure via `AbpAspNetCoreMvcOptions`. Suggested: 5 requests per email address per hour.

### Things NOT to port

- `VerificationCode` GUID column reuse -- ABP uses separate token providers per purpose
- Custom `IPasswordHash` -- ABP `IdentityUser.PasswordHash`
- Returning `FailedLogin = true` on success (OLD bug) -- NEW returns proper status
- Frontend's "send `isActivateUser` query param" plumbing -- legacy, unused; drop in NEW Angular routing
- Custom Angular forgot-password / reset-password components -- use ABP AuthServer Razor pages

### Open items requiring code reads during impl

| Item | Status |
|------|--------|
| Locate OLD `EmailTemplate.ResetPassword` HTML | TO READ during impl |
| Locate OLD `EmailTemplate.PasswordChange` HTML | TO READ during impl |
| Verify OLD has no rate limiting on forgot-password | TO VERIFY (probably none, but check `Infrastructure/` for any throttling middleware) |
| Confirm ABP exposes a hook to gate `SendPasswordResetCodeAsync` (or fall back to overriding `AccountAppService`) | TO VERIFY in ABP source |
| Confirm `UserPasswordChangedEto` event exists (or equivalent) | TO VERIFY in ABP source |

### Verification (manual test plan, post-impl)

1. Click "Forgot Password" on AuthServer login page -> /Account/ForgotPassword (or custom URL)
2. Enter unverified user's email -> error: "Please verify your email address before resetting your password" (per verified-only rule)
3. Enter inactive user's email -> error: "Your account is not activated" (or equivalent ABP error)
4. Enter verified active user's email -> success message + email arrives
5. Click reset link in email -> /Account/ResetPassword loads with token in URL
6. Enter new password not matching regex -> validation error (digit + special char + min length)
7. Enter mismatched confirmation -> validation error
8. Enter valid new password -> success, redirected to login
9. Confirmation email received with subject "Your password has been successfully changed - {ClinicName}"
10. Old password rejected on login; new password accepted
11. Reset link from email no longer works (token consumed) -> error
12. Rate limit: try 6 forgot-password requests within an hour for same email -> 6th rejected with 429 Too Many Requests

Tests (xUnit + Shouldly):
- `ForgotPasswordTests.SendResetCode_UnverifiedUser_Throws` (verified-only rule)
- `ForgotPasswordTests.ResetPassword_ValidToken_UpdatesPasswordHash`
- `ForgotPasswordTests.ResetPassword_ConsumedToken_Fails` (one-time use)
- `ForgotPasswordTests.ResetPassword_SendsConfirmationEmail` (mocking `IEmailSender`)
- `ForgotPasswordTests.ResetPassword_WeakPassword_Throws`
- Synthetic data only.

---

## Review updates (2026-05-01)

### Q1 (verified-only password reset gate) -- RESOLVED

Adrian's call: **(a) Mirror OLD** -- override `AccountAppService.SendPasswordResetCodeAsync` to throw a `BusinessException` when `user.EmailConfirmed == false`. Strict parity. Localization key `ExternalUser:EmailNotConfirmedForPasswordReset` -> `"Please verify your email address before resetting your password."`

### Q2 (post-reset confirmation email) -- RESOLVED

Adrian's call: **(a) Subscribe to `UserPasswordChangedEto` distributed event.** Clean, decoupled.

**Cost note:** zero monetary cost. ABP's `IDistributedEventBus` is part of the open-source `Volo.Abp.EventBus.Distributed` package included with ABP Commercial. In-process event handling has no external dependencies and no extra fees. Adding an event handler is the same overhead as adding any other class in the codebase.

### Q3 (rate limiting on password-reset endpoints) -- RESOLVED

Adrian's call: **5 requests per email per hour** on both `/api/account/send-password-reset-code` and `/api/account/reset-password`. Implementation via ASP.NET Core Rate Limiting middleware with a sliding window keyed on the request's email field. Microsoft doc: https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit
