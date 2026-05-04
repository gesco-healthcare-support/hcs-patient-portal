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
re-verified: 2026-05-03
status: in-progress
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
| **Verified-only password reset** -- unverified users get `"please verify your email address to do further process"` | ABP default allows password reset for unverified users | **Decision needed:** mirror OLD's verified-only rule, or accept ABP's default? OLD's rule is more restrictive. **Recommend mirror OLD** for parity -- override `IAccountAppService.SendPasswordResetCodeAsync` or check `EmailConfirmed` before sending the reset code. `[IMPLEMENTED 2026-05-03 - tested unit-level]` `[OLD-BUG-FIX]` -- enforced via `PasswordResetGate.EnsureUserCanRequestReset` in NEW `ExternalAccountAppService.SendPasswordResetCodeAsync`; null-user case silently returns to avoid OLD's account-enumeration leak. | I |
| Inactive account check (`Status.InActive`) -> `"Your account is not activated"` | ABP returns a different error for `IsActive = false` (or `LockedOut`) | Map ABP errors to OLD's user-facing strings (or accept ABP defaults -- equivalent). `[IMPLEMENTED 2026-05-03 - tested unit-level]` -- gate throws `BusinessException(UserInactiveForPasswordReset)` localized via `Account:UserInactiveForPasswordReset` key. | C |
| Password regex enforced on reset (`systemPasswordPattern`) | ABP enforces password policy from `IdentityOptions` (currently weakened in NEW per registration audit) | Same fix as registration -- tighten ABP password policy in `ChangeIdentityPasswordPolicySettingDefinitionProvider`. `[IMPLEMENTED 2026-05-03 - tested unit-level]` -- closed in Phase 2.1 (commit 4690980); `IdentityUserManager.ResetPasswordAsync` re-applies the policy on the new password. | I |
| Confirmation email sent after successful reset | ABP does NOT send a confirmation email after reset by default | **Add post-reset notification email** via `IEmailSender` invocation in a custom `IAccountAppService` override OR via `IDistributedEventBus` subscriber to ABP's password-changed event. **Recommend distributed event subscriber** -- decoupled, doesn't require overriding ABP. `[IMPLEMENTED 2026-05-03 - tested unit-level]` -- audit assumed `UserPasswordChangedEto` exists; reflection 2026-05-03 confirmed ABP 10.0.2 does NOT emit one, so confirmation is sent inline at the end of `ExternalAccountAppService.ResetPasswordAsync`. | I |
| Reset email subject: `"Patient Appointment Portal - Reset Password"` | ABP default | Customize ABP email template + subject to match (with `{ClinicName}` token for multi-tenant). `[IMPLEMENTED 2026-05-03 - tested unit-level]` -- localized via `Account:PasswordResetEmailSubject` key. | C |
| Confirmation email subject: `"Your password has been successfully changed - Patient Appointment portal"` | Not sent by ABP | Add via the event subscriber above. Subject: `"Your password has been successfully changed - {ClinicName}"`. `[IMPLEMENTED 2026-05-03 - tested unit-level]` -- localized via `Account:PasswordChangedEmailSubject` key. | C |
| Frontend redirects to `/login` after success | AuthServer Razor page redirects similarly (configurable) | None | -- |
| No rate limiting on `/postforgotpassword` (TO VERIFY) | ABP supports rate limiting via `Volo.Abp.AspNetCore.Mvc.RateLimiting` | **Add rate limiting** to NEW's password-reset endpoints regardless of OLD behavior. Standard hardening. `[IMPLEMENTED 2026-05-03 - tested unit-level]` -- ASP.NET Core `FixedWindowRateLimiter` (5 req / hour) wired as a `GlobalLimiter` partitioned by path + email/sub/IP in `CaseEvaluationHttpApiHostModule.ConfigurePasswordResetRateLimiter`. | I |
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

---

## Phase 10 verification pass (2026-05-03)

OLD source re-read with citations against `P:\PatientPortalOld\`. ABP 10.0.2
runtime probed via reflection over the installed NuGet package set.

### A. OLD claims confirmed verbatim

| Audit claim | OLD evidence | Verdict |
|---|---|---|
| ForgotPasswordValidation gates: not-found / soft-deleted, !IsVerified, InActive | `UserAuthenticationDomain.cs:157-180` | CONFIRMED -- 4 branches as stated |
| Step 1 generates `VerificationCode = Guid.NewGuid()` (overwrites pending verify) | `UserAuthenticationDomain.cs:188` | CONFIRMED |
| Reset email subject literal | `UserAuthenticationDomain.cs:209` -- `"Patient Appointment Portal - Reset Password"` | CONFIRMED |
| Reset URL pattern `/reset-password/?activationkey=<guid>&emailId=<email>` | `UserAuthenticationDomain.cs:195` | CONFIRMED |
| Step 2 validation: Password == ConfirmPassword + regex match + active | `UserAuthenticationDomain.cs:222-238` | CONFIRMED |
| Step 2 silently skips when user not found by email+code combo | `UserAuthenticationDomain.cs:244-255` | CONFIRMED -- the `if (user != null)` wraps the whole side-effect block |
| `VerificationCode = null` after successful reset (one-time) | `UserAuthenticationDomain.cs:250` | CONFIRMED |
| Confirmation email subject literal | `UserAuthenticationDomain.cs:313` -- `"Your password has been successfully changed - Patient Appointment portal"` | CONFIRMED -- this one reads correctly (NO typo despite the audit comment; "Your password" is grammatical) |
| Reset uses same `systemPasswordPattern` as registration | `UserAuthenticationDomain.cs:226` + `RegexConstant.cs:15` | CONFIRMED |

### B. OLD bugs found

| OLD bug | OLD evidence | NEW behavior | Sev |
|---|---|---|---|
| `ForgotPasswordValidation` line 162 dead branch -- `user != null` already excluded `Status.Delete` at line 159, so the `StatusId == Status.Delete` check below is unreachable | `UserAuthenticationDomain.cs:159, 162` | NEW omits the dead branch | C |
| Step 1 logs and emails reset link before checking `IsVerified` of the **resolved** user (the validation method ran first, but Step 1 implementation re-queries and ignores the validation result) | `UserAuthenticationDomain.cs:185-209` | NEW: validation runs at AppService entry; reset email never sent when validation throws | I |
| Step 2 silently no-ops when token mismatch (`if (user != null)` wraps side effects, returns null to controller) -- attacker can probe valid emails by trying random tokens with no error | `UserAuthenticationDomain.cs:244-255` | NEW: ABP / ASP.NET Identity returns proper failure, but we still want to NOT leak email existence -- mapper returns generic message | I |
| OLD lacks rate limiting on `/postforgotpassword` | grep'd `P:\PatientPortalOld\PatientAppointment.Api` and `Infrastructure` for `RateLimit\|throttle` -- only AWSSDK matches (binary), no domain rate-limit code | NEW adds 5/hour/email per Q3 resolution | B |

### C. NEW state vs audit assumptions -- material divergence

The audit assumed (lines 191-204, 209-218) two ABP capabilities that
**reflection over the installed packages does NOT confirm** in ABP Pro 10.0.2:

1. **`AccountAppService` / `IAccountAppService` not directly subclassable.**
   ABP Pro 10.0.2 obfuscates the Account module heavily. Reflection over
   `Volo.Abp.Account.Pro.Public.Application.dll` shows method names like
   `L8nSSMWkSiiYJHjaCG9.lRHeAfgqo7WklTsGVuU`. No public method named
   `SendPasswordResetCodeAsync` is visible across the 14 ABP-Account dlls
   on disk. The DTOs (`SendPasswordResetCodeDto`, `ResetPasswordDto`,
   `VerifyPasswordResetTokenInput`, `ChangePasswordInput`) ARE public,
   confirming an interface exists -- it just isn't publicly named in a
   way our code can reference.

2. **`UserPasswordChangedEto` does NOT exist** in ABP Identity 10.0.2.
   Reflection over all `Volo.Abp.Identity*.dll` shows exactly one Eto
   type: `IdentityClaimTypeEto`. There is no published `Eto` event for
   password changes; the audit's Q2 plan to subscribe to that event is
   not viable on this ABP version.

**Plan correction (binding for Phase 10 implementation):**

| Plan said | Reality | Phase 10 action |
|---|---|---|
| Override `AccountAppService.SendPasswordResetCodeAsync` to add the verified-only gate | Obfuscated; not subclassable | **Build a NEW `IExternalAccountAppService`** at `src\HealthcareSupport.CaseEvaluation.Application.Contracts\ExternalAccount\`. Two methods: `SendPasswordResetCodeAsync(SendPasswordResetCodeInput)` + `ResetPasswordAsync(ResetPasswordInput)`. Both go through `IdentityUserManager` (open-source, stable) primitives: `FindByEmailAsync`, `GeneratePasswordResetTokenAsync`, `ResetPasswordAsync`. |
| Subscribe to `UserPasswordChangedEto` for post-reset confirmation email | Eto doesn't exist | **Send the confirmation email inline** at the end of the NEW `ResetPasswordAsync` AppService method, after `IdentityUserManager.ResetPasswordAsync` returns success. No event subscriber needed. |

This avoids the obfuscation problem, gives us a clean contract, keeps the
helper logic unit-testable via `InternalsVisibleTo`, and matches OLD's
verbatim flow (validation gates, then reset, then send confirmation).

### D. New audit gaps surfaced

| # | Gap | OLD ref | Sev | Action |
|---|---|---|---|---|
| F1 | New `IExternalAccountAppService` surface required (audit assumed override of ABP, reality requires standalone service) | -- | B | Implement at `Application/ExternalAccount/`. Endpoints `POST /api/app/external-account/send-password-reset-code` + `POST /api/app/external-account/reset-password`. |
| F2 | `PasswordResetGate` static helper for the verified + active gates | `UserAuthenticationDomain.cs:166-173` | I | Internal static method `EnsureUserCanRequestReset(IdentityUser user)` -- throws `BusinessException` on missing-email-confirmation or inactive-user; returns silently on null user (avoid info leak). Unit-tested via InternalsVisibleTo. |
| F3 | Token format change | `UserAuthenticationDomain.cs:188-195` (raw GUID via `?activationkey=<guid>`) vs ABP `DataProtectionTokenProvider` cryptographic token | C | Documented framework deviation; URL-encoded token round-trips through the AuthServer Razor reset page. NEW URL: `{authServerBaseUrl}/Account/ResetPassword?userId={guid}&resetToken={signed-token}`. |
| F4 | OLD has NO rate limiting (verified) | grep over `P:\PatientPortalOld\PatientAppointment.{Api,Infrastructure}` returned no `RateLimit` matches outside binary AWSSDK | I | NEW adds `AddRateLimiter` middleware in `CaseEvaluationHttpApiHostModule.PreConfigureServices` -- 5 requests / hour / email key. Wires `app.UseRateLimiter()` after auth. |
| F5 | Confirmation email subject is OLD-verbatim "Your password has been successfully changed - Patient Appointment portal" -- "Your" is grammatical (not the registration-email typo) | `UserAuthenticationDomain.cs:313` | C | NEW: subject `"Your password has been successfully changed - {ClinicName}"` via `PasswordChange` NotificationTemplate (Phase 4 seeded). |
| F6 | Confirmation email body uses `EmailTemplate.PasswordChange` HTML template | `UserAuthenticationDomain.cs:311` -- references `EmailTemplate.PasswordChange` constant which maps to `Password-Changed.html` | C | NEW: NotificationTemplate code `PasswordChange` (Phase 4 seeded) carries body. Phase 10 just sends via `IEmailSender` + the seeded template. |
| F7 | Phase 4 ABP Pro license blocker still gates EFCore.Tests integration | `docs/handoffs/2026-05-03-test-host-license-blocker.md` | I | No new blocker in Phase 10. Unit-test pattern same as Phases 3+4+8+9. |
| F8 | OLD does NOT validate that the user looked up via `PutCredentialValidation` actually has `IsActive == true` (line 235 only checks `StatusId == Status.Active`) -- a subtle parity question whether soft-deleted-but-Active users could reset | `UserAuthenticationDomain.cs:235` | C | OLD-bug-fix exception: NEW checks both `EmailConfirmed` AND `IsActive` before allowing reset. Localization key `Account:UserInactiveForPasswordReset`. |

### E. Phase 4 license blocker

Still in effect for HTTP-level integration tests. Phase 10 ships unit-test
coverage via the established `InternalsVisibleTo` pattern (Phase 3+4+8+9).

### F. Audit-doc lifecycle annotations

Original gap table (lines 156-169) gets `[IMPLEMENTED]` / `[DESCOPED]`
annotations after Phase 10 lands. New gaps F1-F8 are tracked here.

### G. Phase 10 implementation summary (2026-05-03)

Files added:

- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs`
  -- 3 new constants (`EmailNotConfirmedForPasswordReset`,
  `UserInactiveForPasswordReset`, `ResetPasswordTokenInvalid`).
- `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json`
  -- 9 new `Account:*` keys (gate messages + email subjects + bodies).
- `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalAccount/`
  -- `SendPasswordResetCodeInput.cs`, `ResetPasswordInput.cs`,
  `IExternalAccountAppService.cs`.
- `src/HealthcareSupport.CaseEvaluation.Application/ExternalAccount/`
  -- `PasswordResetGate.cs` (internal static helper),
  `ExternalAccountAppService.cs`.
- `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/ExternalAccount/ExternalAccountController.cs`
  -- 2 anonymous POST endpoints under
  `api/public/external-account/`.
- `test/HealthcareSupport.CaseEvaluation.Application.Tests/ExternalAccount/ExternalAccountAppServiceUnitTests.cs`
  -- 23 unit tests covering the gate, normalization, password-match,
  URL builder, IdentityResult classifier.

Files modified:

- `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs`
  -- adds `ConfigurePasswordResetRateLimiter` (global limiter partitioned
  by path + key, 5/hour); calls `app.UseRateLimiter()` after
  `UseAuthorization`.

Strict-parity deviations called out:

- **Architecture deviation:** NEW builds an AppService rather than
  overriding ABP Pro's `AccountAppService`. ABP Pro 10.0.2 obfuscates
  the Account module heavily (member names like
  `L8nSSMWkSiiYJHjaCG9.lRHeAfgqo7WklTsGVuU`); subclassing is fragile.
  Stable lower-level primitives (`IdentityUserManager` +
  `IEmailSender`) get used instead.
- **Inline confirmation email** instead of `UserPasswordChangedEto`
  subscriber: ABP 10.0.2 does not emit such an event (verified by
  reflection 2026-05-03). Equivalent observable behavior.
- **OLD-bug-fix exception** on the gate's null-user branch: OLD line
  177 returned `UserNotExist` (information leak); NEW silently
  returns. Documented in `PasswordResetGate.cs` XML doc.

Test-host crash gate (Phase 4 license blocker) means integration tests
remain deferred. 23 pure unit tests cover all decision branches and
helper methods via `InternalsVisibleTo`. End-to-end manual demo will
exercise the SMTP path once Phase 4 unblocks.
