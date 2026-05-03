---
feature: external-user-login
old-source:
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\UserAuthenticationController.cs
  - P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs (PostLogin)
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\login\login\login.component.ts
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\login\login.service.ts
old-docs:
  - socal-project-overview.md (lines 229-235)
  - data-dictionary-table.md (Users + ApplicationUserTokens)
  - socal-api-documentation.md (UserAuthentication/login section)
audited: 2026-05-01
re-verified: 2026-05-03
status: in-progress
priority: 1
depends-on:
  - external-user-registration  # shares IsVerified + VerificationCode + email-verification side-effects
---

# External user login

## Purpose

Authenticated entry point for all 4 external roles (Patient, Adjuster, Applicant Attorney, Defense Attorney) AND the 3 internal roles. Login validates credentials, enforces an email-verification gate, issues a session token, and redirects the user. Unverified accounts trigger an automatic re-send of the verification email.

## OLD behavior

### Login form (UI)

`patientappointment-portal/src/app/components/login/login/login.component.{ts,html}`. Fields:

- `EmailId` -- text input
- `Password` -- password input
- "Forgot Password" link -> navigates to `/forgot-password`
- "Sign Up" link (`gotoSignup()`) -> navigates to `/users/add`
- Enter key submits form (`handleKeyDown` -> `login()`)
- `failedCount` tracked in `localStorage` and sent in body (purpose unclear in this code path; backend appears to ignore it -- TO VERIFY for lockout logic)

### Backend flow (`POST /api/UserAuthentication/login` -> `UserAuthenticationDomain.PostLogin`)

Reading `UserAuthenticationDomain.cs` lines 50-155:

1. Look up user by `EmailId == userCredentialViewModel.EmailId.ToLower()` and `StatusId != Status.Delete`.
2. **If user not found** -> return `FailedLogin = true` with `UserNotExist` message.
3. **If `user.IsVerified == false`:**
   - Set `user.VerificationCode = user.VerificationCode ?? Guid.NewGuid()` (reuse existing code if non-null, else regenerate).
   - `LoginUow.RegisterDirty(user); LoginUow.Commit();`
   - Send verification email via `EmailTemplate.UserRegistered` with link `{clientUrl}/verify-email/{userId}?query={verificationCode}`.
   - Return `FailedLogin = true` with message: `"We have sent a verification link to your registered email id, please verify your email address to login."`
4. **If verified, validate password** via `IPasswordHash.VerifySignature(input, stored.Password, stored.Salt)`:
   - If mismatch -> `FailedLogin = true` with `InvalidUserNamePassword` message.
5. **Account-state checks** (after successful password):
   - `StatusId == InActive || !IsActive` -> `FailedLogin = true` with `UserInactivated` message.
   - `StatusId == Delete` -> `FailedLogin = true` with `UserNotExist` message.
6. Look up `ApplicationTimeZone` for user (used downstream).
7. Look up `RoleUserType` to derive `UserTypeId` (External=1 / Internal=2 per `UserType` enum).
8. **Generate JWT** via `JwtTokenProvider.WriteToken(user)` which returns an `ApplicationUserToken` row with `JwtToken`, `SecurityKey` (binary 32 bytes), `TokenIssuer`, `AccessedPlatform`, `CreatedDateTime`, `ExpiresAt`, `IsActive`.
9. **Single-session enforcement:** delete ALL existing `ApplicationUserToken` rows for the user, then insert the new one. `LoginUow.Commit()`.
10. Build `UserAuthenticationViewModel` response:

| Field | Source |
|-------|--------|
| `Token` | `applicationUserToken.JwtToken` |
| `Modules` | `UserAuthorization.GetAccessModules(0, roleId)` -- permission tree |
| `EmailId` | `user.EmailId` (lowercased) |
| `FullName` | `string.Format("{0} {1}", user.FirstName, user.LastName)` |
| `RoleId` | int |
| `UserTypeId` | int (External=1 / Internal=2) |
| `UserId` | int |
| `FailedLogin` | `false` |
| `IsFirstTime` | `user.IsAccessor.HasValue` (true if `IsAccessor` is set, regardless of value) |
| `IsAccessor` | `user.IsAccessor.Value` if set, else `false` |

### Frontend success handling (`login.component.ts` lines 78-110)

1. Save `t.token` to `localStorage['auth']`.
2. Save `t.userTypeId` to `localStorage['userTypeId']`.
3. Set in-memory `user.data = { fullName, emailId, userId, userTypeId, roleId, isAccessor }`.
4. Set `user.authorizationPermissionItem = t.modules`.
5. For each `rootModuleId` in `t.modules`, push a `UserPermissionCache(rootModuleId, permission, requestedDate)` to `user.permissions`.
6. Set cookie `document.cookie = "requestContext=abc"` (a marker cookie, not a token cookie).
7. Trigger `applicationBroadCaster.loginBroadCast({ callBackUrl, isClicked: true })` after `setTimeout(50ms)`. Router subscribes and redirects:
   - **External user:** -> `home` (or `callBackUrl` query param if present)
   - **Internal user:** -> `dashboard`

### Critical OLD behaviors

- **Email lowercased on lookup.** `t.EmailId == userCredentialViewModel.EmailId.ToLower()`.
- **Email-verification gate is BEFORE password check.** Unverified users see "verify your email" even if their password is correct. (The password check is gated by `if (user.IsVerified) {...}` block.)
- **Auto-resend verification on login attempt.** No separate resend endpoint; trying to log in re-triggers the email.
- **Verification code reused if non-null.** A user who clicks the original email after a failed login will still verify successfully with the original code.
- **Single session per user.** Each successful login deletes all prior tokens. Users on multiple devices get logged out when they log in elsewhere.
- **`IsAccessor` is tri-state on User entity:** `null` (regular user), `false` (created via accessor flow but not actually an accessor), `true` (active accessor). The `IsFirstTime` flag uses `HasValue` to detect accessor-flow-created users -- a roundabout way to flag "this user was provisioned via appointment-share invite, show them onboarding."
- **No backend account lockout** on failed login (failedCount is client-side; backend doesn't seem to use it -- VERIFY during impl).
- **Email verification does NOT auto-login.** After verifying, user redirects to `/login` (per `verify-email.component.gotoLogin`).

## OLD code map

### Backend (.NET / C#)

| Layer | File | Purpose |
|-------|------|---------|
| API controller | `PatientAppointment.Api/Controllers/Api/Core/UserAuthenticationController.cs` lines 41-54 | `POST /api/UserAuthentication/login` -> `UserAuthenticationDomain.PostLogin` |
| Domain | `PatientAppointment.Domain/Core/UserAuthenticationDomain.cs` lines 50-155 | `PostLogin` -- the full flow above |
| Token | `Rx.Core.Security.Jwt.IJwtTokenProvider` (in-house pkg) | `WriteToken(user)` -- builds `ApplicationUserToken` row |
| Hash | `Rx.Core.Security.IPasswordHash` (in-house pkg) | `VerifySignature(input, stored, salt)` |
| Permission | `PatientAppointment.Infrastructure.Authorization.IUserAuthorization` | `GetAccessModules(parentModuleId, roleId)` -- builds permission tree |
| Models | `PatientAppointment.Models.ViewModels.UserAuthenticationViewModel` | Response shape |
| Models | `PatientAppointment.Models.ViewModels.UserCredentialViewModel` | Request shape |
| DB entities | `PatientAppointment.DbEntities.Models.{User, ApplicationUserToken, RoleUserType, ApplicationTimeZone}` | -- |

### Frontend (Angular)

| Component | File | Purpose |
|-----------|------|---------|
| Login form | `components/login/login/login.component.{ts,html}` | UI + submit handling |
| Login service | `components/login/login.service.ts` | HTTP wrapper for the 4 UserAuthentication endpoints |
| Models | `components/login/domain/login.models.ts` | `UserCredentialModel`, `UserAuthenticationViewModel` shapes |
| User store | `@rx/security` package -- `user.data`, `user.permissions`, `user.authorizationPermissionItem` | In-memory user state |
| Storage | `@rx/storage` -- `RxStorage.local` | `auth`, `userTypeId`, `failedCount` keys |
| Routing | `app.lazy.routing.ts` | `/login`, `/home`, `/dashboard`, `/users/add`, `/forgot-password`, `/verify-email/:userId` |

## NEW current state

### Backend (ABP + OpenIddict)

- **No custom login AppService.** Login is handled by ABP's AuthServer (separate Razor Pages app on port 44368) and OpenIddict for token issuance.
- **OpenIddict** is configured in `src/HealthcareSupport.CaseEvaluation.AuthServer/` (TO VERIFY layout). Issues access tokens + refresh tokens via OAuth2 Authorization Code + PKCE.
  - (For Adrian, Node analogue: ABP AuthServer is roughly Passport.js with a built-in OIDC provider, running as a separate Express app from the API. The Angular SPA redirects to it for login, then back with an authorization code that the SPA exchanges for tokens.)
- **No custom email-verification gate.** ABP setting `IdentitySettingNames.User.IsEmailConfirmationRequiredForLogin` (when `true`) blocks unverified users with a built-in error.
- **No auto-resend** of verification email on login attempt. ABP returns an error; user is expected to click "resend confirmation" link separately.

### Frontend (Angular)

- **No login component in Angular SPA.** Login is via redirect to AuthServer.
- **`@abp/ng.oauth` package** handles the OIDC flow on the SPA side -- redirects to AuthServer, receives authorization code, exchanges for token, stores in browser via `OAuthService` (angular-oauth2-oidc internally).
- **`ABP_LOCALIZATION_RESOURCE_NAME` token** + `IdentityModule` provide localized user-facing strings.

### AuthServer login UI

- ABP Commercial ships with **LeptonX** theme. Login page is at `src/HealthcareSupport.CaseEvaluation.AuthServer/Pages/Account/Login.cshtml` (or via theme override).
- Customization options (in priority order):
  1. Theme settings (logo, primary color) via `LeptonXThemeOptions`
  2. Razor view override at `Pages/Account/Login.cshtml`
  3. Custom controllers / view components

## Gap analysis

3-column gap table. Severity: **B** = blocker, **I** = important, **C** = cosmetic.

| OLD | NEW (current) | Gap | Sev |
|-----|---------------|-----|-----|
| Custom JWT issued by `JwtTokenProvider`; stored in `ApplicationUserTokens` table | OpenIddict issues access + refresh tokens; OpenIddict tables (`OpenIddictTokens`, etc.) store them | None -- framework swap. OAuth2 is strictly better than custom JWT. | -- |
| Single-session enforcement (delete prior tokens on login) | OpenIddict supports multiple concurrent sessions by default | **Decision needed:** preserve OLD's single-session behavior, or accept multi-session? OLD's behavior probably wasn't an explicit security feature -- likely a side effect of the in-house JWT design. **Recommend accept multi-session** unless Adrian wants single-session. | I |
| Email-verification gate via `if (!user.IsVerified)` in PostLogin | ABP setting `IsEmailConfirmationRequiredForLogin` | None -- enable the setting. | -- |
| Auto-resend verification email on unverified login attempt | ABP requires explicit "resend" link click | **Add explicit "Resend confirmation email" link to AuthServer login page** (when error indicates `EmailNotConfirmed`). Hits `IAccountAppService.SendEmailConfirmationLinkAsync`. Closer to standard UX; less surprising than auto-resend. | I |
| Backend lookup uses `EmailId.ToLower()` | ABP's `ILookupNormalizer` uppercases for lookup; user records store original casing in `Email` and uppercased in `NormalizedEmail` | None -- ABP normalizes both sides of the comparison transparently. Verify the registration audit decision (use lowercased on save) doesn't conflict; if `Email` is stored lowercased but `NormalizedEmail` is uppercased, lookups still work. | -- |
| `Status.InActive` -> "user inactivated" error | ABP `IdentityUser.IsActive` -> ABP returns `LockedOut`/`NotAllowed` errors | Map ABP errors to OLD's user-facing strings (or just use ABP defaults -- they're equivalent). | C |
| `Status.Delete` (soft delete) -> "user not exist" error | ABP `ISoftDelete` -> ABP returns `UserNotFound` | None -- equivalent behavior. | -- |
| Response shape: `{ Token, Modules, EmailId, FullName, RoleId, UserTypeId, IsAccessor, IsFirstTime, FailedLogin, ValidationMessage }` | OAuth2 token response: `{ access_token, refresh_token, expires_in, token_type, id_token, ... }` | Different. Frontend won't pre-fetch user metadata in login response; it'll call `/api/identity/my-profile` after login. **NEW Angular code patterns this differently.** | I |
| `Modules` permission tree (custom shape) | ABP `IPermissionChecker` + `/api/abp/application-configuration` returns granted permissions | Map OLD's "module" concept to ABP permissions. During impl, define an OLD-module-to-ABP-permission table. | I |
| `IsAccessor` + `IsFirstTime` flags returned from login | Not in OAuth2 token response | Surface via `/api/identity/my-profile` (custom property on `IdentityUser` via `IObjectExtensionManager`, registered 2026-05-01 per registration audit). | I |
| External user redirected to `/home`, internal to `/dashboard` | ABP default redirects everyone to `/`; SPA handles routing afterward | Add SPA-side guard that reads the user's `IsExternalUser` extension property post-login and redirects to `home` (external) or `dashboard` (internal). | I |
| `callBackUrl` query param honored on success | OAuth2 standard `redirect_uri` parameter handled by OpenIddict | None -- OAuth2 has this built in. Ensure the Angular SPA passes intended target as state. | -- |
| Single login form for external + internal users | Same -- single AuthServer login page | None | -- |
| Login page UI: branding, copy, etc. | LeptonX default | Customize LeptonX login page to match OLD's visual design (covered by branding investigation, task #6). | I |
| Form validation messages (e.g., "Invalid username or password") | ABP localized strings | Port OLD validation messages to NEW localization JSON (`src/.../Domain.Shared/Localization/CaseEvaluation/en.json`). | C |
| Cookie `document.cookie = "requestContext=abc"` set on success | Not used in NEW | Drop -- OLD used this for some legacy flow, doesn't fit OAuth2 model. Verify nothing in OLD reads this cookie before dropping. | C |

## Internal dependencies surfaced

- **Permission/module mapping.** OLD's `UserAuthorization.GetAccessModules` returns a tree of `ApplicationModule` rows with per-action permissions (CanView, CanAdd, CanEdit, CanDelete) per role. Mapping this to ABP's permission keys is non-trivial and feature-by-feature. **Strategy:** as each feature is audited, define its ABP permission keys (e.g., `CaseEvaluation.Appointments.View`). Don't try to do all 65+ tables at once.
- **`IsAccessor` flag.** Created by the appointment-sharing flow (covered in booking audit slice). Login surfaces it but doesn't create it. **Out of scope for login audit.**
- **Resend confirmation email endpoint.** Needs a UI link on AuthServer login page. The endpoint itself is ABP-provided (`IAccountAppService.SendEmailConfirmationLinkAsync`). Just wire the link.

**Ask Adrian:** confirm "accept multi-session" decision (gap row above), and confirm "explicit resend link" over auto-resend (gap row above).

## Branding/theming touchpoints

- **AuthServer login page** (`Pages/Account/Login.cshtml` or LeptonX override): logo, primary color, background image, page title, button styles, form labels
- **Email subject line** for re-sent verification email: same as registration (covered there)
- **Brand-token surfaces in login flow:**
  - Page title: "Login - {ClinicName}"
  - "Welcome to {ClinicName}" or similar greeting
  - Footer: support email, support phone (per-tenant)

Output during impl: feeds into `docs/parity/_branding.md`.

## Replication notes

### ABP-specific wiring

- **No new AppService for login.** Use ABP's built-in `OpenIddict` token endpoint at `/connect/token`. Angular SPA uses `@abp/ng.oauth`'s `OAuthService.initLoginFlow()`.

- **Email confirmation gate:** in `CaseEvaluationSettingDefinitionProvider` (or wherever settings are defined), set:
  ```csharp
  context.GetOrNull(IdentitySettingNames.User.IsEmailConfirmationRequiredForLogin)
      ?.DefaultValue = true.ToString();
  ```
  ABP doc: https://docs.abp.io/en/abp/latest/Modules/Account#settings

- **"Resend confirmation email" link on login page:** customize `Pages/Account/Login.cshtml` to detect `IdentityResult.Failed` with `EmailNotConfirmed` cause and render a link to `IAccountAppService.SendEmailConfirmationLinkAsync(SendEmailConfirmationLinkDto { Email, AppName, ReturnUrl })`. The DTO's `AppName` controls which email template is used.

- **Post-login redirect by user type:**
  - In Angular, after token acquisition, call `currentUser$` (or `IdentityUserAppService.GetMyProfileAsync`) to read `IsExternalUser` extension property.
  - If true -> `router.navigate(['home'])`. If false -> `router.navigate(['dashboard'])`.
  - Wrap in an Angular guard or in the OAuth callback handler.

- **Multi-session (recommended):** accept ABP's default. No special configuration needed.

- **Single-session (only if Adrian asks):** override `IIdentityUserManager` to revoke all OpenIddict tokens for the user on login. ABP doc: https://docs.abp.io/en/commercial/latest/modules/openiddict-pro

### Things NOT to port

- `JwtTokenProvider` + `ApplicationUserTokens` table -- OpenIddict.
- `IPasswordHash` + custom `Salt` + `Password` columns -- ABP `IdentityUser.PasswordHash`.
- `RoleUserTypes` table -- replaced by `IsExternalUser` extension property on `IdentityUser`.
- `ApplicationModules` + `RolePermissions` + `UserAuthorization.GetAccessModules` -- ABP `IPermissionChecker` + permission keys.
- `ApplicationTimeZones` table -- ABP timezone setting + NodaTime.
- `failedCount` localStorage tracking on login form -- if Adrian wants lockout, ABP supports `MaxFailedAccessAttempts` natively. Skip the client-side counter.
- Cookie `document.cookie = "requestContext=abc"` -- legacy marker; drop.

### Open items for impl

- Verify ABP's `IsEmailConfirmationRequiredForLogin` is enabled in NEW (probably not currently -- check `CaseEvaluationSettingDefinitionProvider`).
- Locate where `failedCount` is read on backend in OLD (if anywhere) -- to determine if lockout was actually implemented.
- Map OLD `ApplicationModule` -> ABP permission keys per feature as audits progress.
- Customize LeptonX login page (in branding investigation).

### Verification (manual test plan, post-impl)

1. Click "Login" on Angular SPA -> redirects to AuthServer login page
2. Enter unverified user creds -> error shown + "Resend confirmation email" link visible
3. Click resend link -> email received
4. Verify via email link -> redirects to login page
5. Enter verified creds -> redirects back to SPA, lands on `/home` (external) or `/dashboard` (internal)
6. Wrong password -> error message shown
7. Inactive user -> error message shown
8. Soft-deleted user -> "user not found" message
9. `callBackUrl` (originally requested URL) honored after login

Tests (xUnit + Shouldly):
- Setting `IsEmailConfirmationRequiredForLogin` is `true` in `CaseEvaluationSettingDefinitionProvider`
- Custom `IsExternalUser` extension property is registered
- Synthetic data only.

---

## Review updates (2026-05-01)

### Q1 (multi-session vs single-session) -- RESOLVED

Adrian's call: **(a) Accept multi-session** (ABP default). No special configuration needed. OLD's single-session was a side effect of in-house JWT design, not an explicit security policy; OAuth2 multi-session is standard and acceptable.

### Q2 (auto-resend vs explicit-resend verification email) -- RESOLVED

Adrian's call: **(a) Add explicit "Resend confirmation email" link** to AuthServer login page. Triggered only when login fails with `EmailNotConfirmed`. Calls `IAccountAppService.SendEmailConfirmationLinkAsync` with a pre-filled email field. Standard UX, less surprising than OLD's auto-resend.

Implementation note: customize `Pages/Account/Login.cshtml` (or LeptonX override) to:
1. Detect the `EmailNotConfirmed` failure cause via `IdentityResult` check.
2. Render a "Resend confirmation email" link/button below the error message.
3. POST to `IAccountAppService.SendEmailConfirmationLinkAsync(SendEmailConfirmationLinkDto { Email, AppName, ReturnUrl })`.
4. Show success toast/inline message after resend.

ABP doc reference: https://docs.abp.io/en/abp/latest/Modules/Account#sendemailconfirmationlinkasync

---

## Phase 9 verification pass (2026-05-03)

OLD source re-read with citations against `P:\PatientPortalOld\`.

### A. OLD claims confirmed verbatim

| Audit claim | OLD evidence | Verdict |
|---|---|---|
| Login lookup uses `EmailId.ToLower()` + `StatusId != Status.Delete` | `UserAuthenticationDomain.cs:52` | CONFIRMED |
| Email-verification gate fires BEFORE password check | `UserAuthenticationDomain.cs:55-122` (outer `if (user.IsVerified)` wraps the password check) | CONFIRMED |
| Unverified login auto-resends verification | `UserAuthenticationDomain.cs:124-145` -- regenerates VerificationCode if null, commits, sends email, returns "verify your email" with `FailedLogin = true` | CONFIRMED |
| `VerificationCode` reused if non-null | `UserAuthenticationDomain.cs:126`: `user.VerificationCode = user.VerificationCode != null ? user.VerificationCode : Guid.NewGuid()` | CONFIRMED |
| Single-session enforcement: prior tokens deleted on each login | `UserAuthenticationDomain.cs:84-89` -- iterates `applicationUserTokens` and `RegisterDeleted` each before `RegisterNew(applicationUserToken)` | CONFIRMED |
| `IsAccessor` tri-state semantics | `UserAuthenticationDomain.cs:102, 105-112` -- `IsFirstTime = user.IsAccessor.HasValue` flags accessor-flow-created users; `IsAccessor` defaults to `false` when null | CONFIRMED |
| Status checks: `InActive` -> "user inactivated" | `UserAuthenticationDomain.cs:60-66` -> `ValidationFailedCode.UserInactivated` | CONFIRMED |
| Status checks: `Delete` -> "user not exist" | `UserAuthenticationDomain.cs:68-74, 149-153` -> `ValidationFailedCode.UserNotExist` | CONFIRMED |
| Wrong password -> "Invalid username or password" | `UserAuthenticationDomain.cs:115-121` -> `ValidationFailedCode.InvalidUserNamePassword` | CONFIRMED |
| Email subject typo "Your have registered successfully" on auto-resend | `UserAuthenticationDomain.cs:129` -- same typo as registration's `UserDomain.cs:321` | CONFIRMED -- typo to fix in NEW (already addressed by Phase 4 NotificationTemplate seed) |

### B. OLD verbatim error messages -- the four to localize

Per the master plan Phase 9 line 455 + the audit's gap table (line 167):

| OLD code | OLD-verbatim user-facing message | Trigger condition |
|---|---|---|
| `ValidationFailedCode.UserNotExist` | `User not exist` | User not found by email OR `StatusId == Status.Delete` |
| `ValidationFailedCode.InvalidUserNamePassword` | `Invalid username or password` | Verified user, password mismatch |
| `ValidationFailedCode.UserInactivated` | `Your account is not activated` | `StatusId == InActive` OR `IsActive == false` |
| (hardcoded literal at `UserAuthenticationDomain.cs:143`) | `We have sent a verification link to your registered email id, please verify your email address to login.` | `IsVerified == false` after auto-resend |

These are the four user-facing strings Phase 9 must surface in NEW localization.

### C. NEW state vs audit assumptions

- Phase 2.2 already enabled `IdentitySettingNames.SignIn.RequireConfirmedEmail = true` in
  `ChangeIdentityEmailConfirmationSettingDefinitionProvider.cs:19`.
- ABP `IAccountAppService.SendEmailConfirmationLinkAsync` and `VerifyEmailAsync` already
  ship in `Volo.Abp.Account.Pro.Public.Application` (10.0.2). No NEW AppService method
  is required for Phase 9 itself; the audit's Q2 resolution is to wire the existing ABP
  method from a "Resend confirmation email" link on the login page.
- ABP exposes `MyProfile` via `IProfileAppService` with extension props auto-surfaced
  through `IObjectExtensionManager` (Phase 2.4 registered FirmName / FirmEmail /
  IsExternalUser / IsAccessor). The Angular SPA can read `IsExternalUser` post-login
  without a new endpoint.
- Phase 8's `ExternalSignupAppService.GetMyProfileAsync` returns `ExternalUserProfileDto`
  but does NOT yet surface `IsExternalUser` / `IsAccessor`. Phase 9 should add these so
  Angular routing can branch external -> `/home`, internal -> `/dashboard` without a
  second roundtrip.
- AuthServer's `Pages/Account/` directory does NOT exist yet. The stock LeptonX login
  view ships embedded in `Volo.Abp.Account.Pro.Public.Web`. Phase 9 needs to create
  override files in the AuthServer project to inject the "Resend confirmation email"
  link.

### D. New audit gaps surfaced (NOT in original gap table)

| # | Gap | OLD ref | NEW | Sev | Action |
|---|---|---|---|---|---|
| L1 | Localization keys for the 4 OLD-verbatim error messages do not exist in NEW `en.json` | `UserAuthenticationDomain.cs:65, 73, 120, 143, 152` | -- | I | Add 4 keys: `Login:UserNotExist`, `Login:InvalidUsernameOrPassword`, `Login:AccountNotActivated`, `Login:VerificationLinkSent`. |
| L2 | `ExternalUserProfileDto` does NOT surface `IsExternalUser` / `IsAccessor` extension props (Phase 2.4). Angular post-login routing (Phase 9 audit gap row 162) can't branch without these. | -- | `ExternalUserProfileDto.cs:5-16` | I | Add `bool IsExternalUser`, `bool IsAccessor` to DTO. Read via `user.GetProperty<bool>(...)` in `GetMyProfileAsync`. |
| L3 | OLD's `failedCount` localStorage tracking on the login form is not implemented server-side in OLD (audit speculation). Quick verify: search OLD backend for `failedCount`. | (OLD search) | -- | C | If zero matches in OLD backend, drop `failedCount` entirely. Phase 9 verification: confirmed below in section E. |
| L4 | ABP error-code mapping to OLD-verbatim user-facing strings is not yet in place. ABP returns `IdentityResult.Errors` with codes like `EmailNotConfirmed`, `InvalidUserName`, `LockedOut`. Mapping helper needed for both AuthServer Razor + future API surfaces. | -- | -- | I | Add `internal static LoginErrorMapper.MapToLocalizationKey(string identityErrorCode)` in `Application/ExternalSignups/`. Returns the matching `Login:*` localization key per OLD-verbatim mapping. Pure function, testable via `InternalsVisibleTo`. |
| L5 | AuthServer `Pages/Account/Login.cshtml` override missing | -- | `src\HealthcareSupport.CaseEvaluation.AuthServer\Pages\` has only `Index.cshtml` | I | Add `Pages/Account/Login.cshtml` + model that extends ABP's `LoginModel` to render the "Resend confirmation email" link when the failure cause is `EmailNotConfirmed`. (Razor work; not unit-testable but in scope per master plan line 453.) |
| L6 | Drop `document.cookie = "requestContext=abc"` from NEW (per audit row 167) | OLD `login.component.ts:97` | NEW Angular doesn't have a login component yet; nothing to drop | C | NO ACTION REQUIRED -- nothing in NEW reads or writes this cookie. |
| L7 | Login + verify-email Angular routing (audit row 162) | -- | -- | I | Defer to Session A's UI-coordination phase (Angular files outside Session B's owned roots). Backend-only Phase 9 plan. |

### E. OLD `failedCount` verify

Searching `P:\PatientPortalOld` backend for `failedCount` reveals:

```
PatientAppointment.Models\ViewModels\UserCredentialViewModel.cs : public int FailedCount
```

The DTO has the field, but no domain code reads or writes it server-side -- it's a
client-only field captured from `localStorage` and round-tripped without effect. NEW
omits it entirely (no DTO field, no localStorage write). **L3 closed: no port needed.**

### F. Phase 4 license blocker -- still in effect

Integration tests for the login + email-verification flow (HTTP-level via
WebApplicationFactory) cannot execute until the ABP Pro license placeholder in
`appsettings.secrets.json` is replaced. Phase 9 ships the same kind of unit-level
coverage as Phase 8: extract the error-code mapper as `internal static`, exercise
via `InternalsVisibleTo`. Razor + Angular bits land without unit coverage in this
session and verify in a manual session post-license-fix.

### G. Audit-doc lifecycle annotations

| Gap-table row (original lines 150-167) | Status |
|---|---|
| Custom JWT issued by `JwtTokenProvider` | [DESCOPED 2026-05-03 - documented framework deviation] -- OpenIddict ships the equivalent. |
| Single-session enforcement | [DESCOPED 2026-05-01 - audit Q1 resolved] -- accept ABP multi-session per Adrian's call. |
| Email-verification gate via `IsVerified` | [IMPLEMENTED 2026-05-01 - Phase 2.2] -- `RequireConfirmedEmail = true` enabled. |
| Auto-resend verification email on unverified login | [IMPLEMENTED 2026-05-03 - tested unit-level + AuthServer Razor deferred] -- `LoginErrorMapper.ShouldShowResendLink` + matching localization keys land in this commit; Razor override is gap L5 deferred. |
| Backend lookup uses `EmailId.ToLower()` | [DESCOPED 2026-05-03 - documented framework deviation] -- ABP `LookupNormalizer` produces equivalent visible behavior. |
| `Status.InActive` -> "user inactivated" error | [IMPLEMENTED 2026-05-03 - tested unit-level] -- `LoginErrorMapper` maps `LockedOut` / `NotAllowed` / `IsNotActive` to `Login:AccountNotActivated`. |
| `Status.Delete` (soft delete) -> "user not exist" | [IMPLEMENTED 2026-05-03 - tested unit-level] -- maps `UserNotFound` / `InvalidUserName` to `Login:UserNotExist`. |
| Response shape (Token / Modules / RoleId / etc.) | [DESCOPED 2026-05-03 - documented framework deviation] -- OAuth2 replaces; SPA reads metadata via `/api/app/external-users/me` extended this commit with `IsExternalUser` + `IsAccessor`. |
| `Modules` permission tree | [DESCOPED 2026-05-03 - per-feature work] -- ABP `IPermissionChecker` + per-feature permission keys. |
| `IsAccessor` + `IsFirstTime` flags | [IMPLEMENTED 2026-05-03 - tested unit-level] -- `ExternalUserProfileDto.IsAccessor` + Phase 2.4 extension prop. |
| External -> /home, internal -> /dashboard routing | [DESCOPED 2026-05-03 - Angular UI work] -- DTO ready; Angular guard lands in Session A's UI phase. |
| `callBackUrl` query param honored | [DESCOPED 2026-05-03 - documented framework deviation] -- OAuth2 `redirect_uri`. |
| Login page UI branding | [DESCOPED 2026-05-03 - Phase 19b LeptonX work] -- Session A. |
| Form validation messages | [IMPLEMENTED 2026-05-03 - tested unit-level] -- 6 `Login:*` keys in `en.json`. |
| `requestContext=abc` cookie | [N/A 2026-05-03] -- nothing in NEW reads / writes it. |

New gaps L1-L7 closed by Phase 9:

| Gap | Status |
|---|---|
| L1 Localization keys for 4 OLD-verbatim error messages | [IMPLEMENTED 2026-05-03 - tested unit-level] |
| L2 `ExternalUserProfileDto` missing `IsExternalUser` / `IsAccessor` | [IMPLEMENTED 2026-05-03 - tested unit-level] |
| L3 OLD `failedCount` server-side verify | [VERIFIED 2026-05-03 - no port needed] -- DTO has the field but no domain code reads it. |
| L4 `LoginErrorMapper` helper | [IMPLEMENTED 2026-05-03 - tested unit-level] -- 11 mapping families + `IdentityResult` walker; 46 unit tests cover every branch + null / empty / unknown defaults. |
| L5 AuthServer Razor override | [DESCOPED 2026-05-03 - Razor override pending] -- ABP Pro 10.0.2's `LoginModel` constructor signature (positional args + `IdentityDynamicClaimsPrincipalContributorCache` namespace) needs a follow-up session with the live `Volo.Abp.Account.Pro.Public.Web` source. The C#-side mapper + localization keys are in place; the Razor template just needs the right base-class wiring. Recommended next step: `abp get-source Volo.Abp.Account.Pro` to read the exact signature, then add the override file. `LoginErrorMapper.ShouldShowResendLink` is the contract the Razor consumes. |
| L6 Drop `requestContext` cookie | [N/A 2026-05-03] -- nothing in NEW reads or writes it. |
| L7 Angular post-login routing guard | [DESCOPED 2026-05-03 - Session A UI coordination] -- DTO surfaces `IsExternalUser`; Angular guard lands in Session A's UI-coordination phase per the two-session split memory. |
