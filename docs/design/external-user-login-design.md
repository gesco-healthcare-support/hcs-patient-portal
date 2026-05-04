---
feature: external-user-login
date: 2026-05-04
phase: 2-frontend (ABP AuthServer handles login; post-login redirect guard implemented; no custom login component needed)
status: draft
old-source: patientappointment-portal/src/app/components/login/login/login.component.ts + .html
new-feature-path: angular/src/app/shared/auth/post-login-redirect.guard.ts (ABP AuthServer renders login page)
shell: unauthenticated (auth-shell -- no side-nav, no top-bar, full-screen form)
screenshots: partial (new/_non-role/01-login-page.png per status tracker; not yet captured)
---

# Design: External User -- Login

## Overview

The login page lets users (both external and internal) authenticate and be redirected to
their role-appropriate landing page.

In OLD, the Angular app renders a custom login page at `/login` (full-page Bootstrap form,
background image, doctor logo). In NEW, **login is handled entirely by ABP's AuthServer**
(OpenIddict OAuth2). The Angular SPA redirects to the AuthServer's LeptonX-styled login
page, receives an access token, and the `postLoginRedirectGuard` routes the user based on role.

**No custom Angular login component is needed or appropriate.** The design doc captures
the OLD form structure (for branding/UI parity reference), the NEW auth flow, the
post-login redirect logic, and the error states that must be handled.

---

## 1. Routes

| | OLD | NEW |
|---|---|---|
| Login page | `/login` (Angular component) | ABP AuthServer: `/connect/authorize` → AuthServer Razor Page |
| Post-login redirect | Component subscribes to login event; routes to `/home` or `/dashboard` | `postLoginRedirectGuard` on root `/` route |
| Sign up link | `/users/add` (from login page) | `/account/register` (ABP account module) |
| Forgot password link | `/forgot-password` (from login page) | `/account/forgot-password` (ABP account module) |

---

## 2. Shell

Unauthenticated (auth-shell): full-screen layout with no side-nav and no authenticated top-bar.

OLD: custom background image + doctor logo overlay. 
NEW: ABP LeptonX authentication pages (standard themed layout). The LeptonX theme applies
the app's brand colors; custom logo/background image can be configured via LeptonX theme settings.

---

## 3. OLD Login Form

```
+-------------------------------------------------------+
| [Doctor Logo -- centered]                            |
+-------------------------------------------------------+
| [Full-screen background image with dark overlay]     |
|                                                      |
| +--------------------------------------+             |
| | Patient Appointment Portal           |             |
| |                                      |             |
| | Email Address    [email input]       |             |
| | Password         [password input]    |             |
| |                                      |             |
| | [Forgot password!]  link (right)     |             |
| |                                      |             |
| | [Sign In]  button                    |             |
| |                                      |             |
| | Don't have an account yet? [Sign-Up] |             |
| +--------------------------------------+             |
+-------------------------------------------------------+
```

**Fields:**
- Email Address: `formControlName="emailId"`, `type="email"`, required
- Password: `formControlName="password"`, `type="password"`, required
- Sign In button: disabled when form invalid; submits on Enter key

OLD source: `login/login.component.html:1-40`

---

## 4. OLD Authentication Flow

1. `POST /api/UserAuthentication/login` with `{ emailId: lowercase(email), password }`.
2. Backend: email-lookup → **email-verification gate** (if `IsVerified == false`, auto-resend
   verification email and return error -- password is NOT checked yet).
3. Backend: password validation.
4. Backend: account-status checks (`InActive`, `Delete`).
5. Backend: JWT generation + **single-session enforcement** (delete all prior tokens for user).
6. Response: `{ token, modules, emailId, fullName, roleId, userTypeId, userId, isAccessor, isFirstTime }`.
7. Frontend: saves `token` and `userTypeId` to `localStorage`; broadcasts login event.
8. Router: external users → `/home`; internal users → `/dashboard`.

**Error messages displayed to user:**
- "User does not exist." (user not found)
- "Incorrect password." (wrong credentials)
- "Your account is not active." (InActive status)
- "A verification link has been sent to your email." (unverified + auto-resend triggered)

OLD source: `login/login.component.ts:78-120`,
`PatientAppointment.Domain/Core/UserAuthenticationDomain.cs:50-155`

---

## 5. NEW Authentication Flow (ABP OpenIddict)

1. Angular app redirects to ABP AuthServer: `GET /connect/authorize?...` (OAuth2 PKCE flow).
2. AuthServer renders LeptonX-styled login page (Razor Page).
3. User enters credentials; AuthServer validates:
   - Email confirmation gate: ABP setting `IdentitySettingNames.User.IsEmailConfirmationRequiredForLogin = true`.
   - Password: ABP `PasswordHasher` (BCrypt).
   - Account status: ABP `IsActive` flag.
4. AuthServer issues `access_token` + `refresh_token` (OpenIddict JWT).
5. Redirect back to Angular SPA: `GET /?code={auth_code}`.
6. ABP Angular module exchanges code for token; stores in session.
7. `postLoginRedirectGuard` reads `currentUser.roles` from `ConfigStateService`:
   - External role (`Patient`, `Adjuster`, `ApplicantAttorney`, `DefenseAttorney`, `ClaimExaminer`) → stay at `/home`.
   - Internal role → `/dashboard`.

**Error messages (ABP standard + localized):**
- Invalid username or password
- Email confirmation required (with link to resend)
- Account is locked out

---

## 6. Post-Login Redirect Guard

```
W:\patient-portal\replicate-old-app\angular\src\app\shared\auth\post-login-redirect.guard.ts
```

Logic:
1. If not authenticated → allow through to home page (shows login button).
2. If authenticated + external role → stay at `/home` (external user home component).
3. If authenticated + internal role → redirect to `/dashboard`.
4. Role classification in `shared/auth/external-user-roles.ts`.

---

## 7. Email Verification Gate

**OLD:** Unverified login attempt triggers auto-resend of verification email + returns error.
**NEW:** ABP identity setting `IsEmailConfirmationRequiredForLogin = true` enforces the same gate.
ABP's standard email confirmation flow handles verification link and resend.

The login page should show a clear "Your email is not verified. [Resend verification email]"
message when an unverified user attempts to log in.

---

## 8. Role Visibility

Login page is public (unauthenticated). All roles (external and internal) use the same
login form. Role-based redirect happens post-login via the `postLoginRedirectGuard`.

---

## 9. Branding Tokens

| Element | Token |
|---|---|
| Page background | Background image or solid `--brand-background-dark` |
| Logo | Gesco/clinic logo (configured in LeptonX theme settings) |
| Sign In button | `btn-primary` via `--brand-primary` |
| Link color | `--brand-primary` |
| Error message | `--status-rejected` (red) |
| Input focus border | `--brand-primary` |

ABP LeptonX applies brand tokens automatically to the AuthServer's login page.

---

## 10. Strict-Parity Exceptions

| # | Element | OLD behavior | NEW behavior | Reason |
|---|---|---|---|---|
| 1 | Custom Angular login component | Angular form at `/login` renders the login page | ABP AuthServer renders login (Razor Page at AuthServer URL) | Framework change: ABP OpenIddict mandates the AuthServer handles OAuth2 flows |
| 2 | Single-session enforcement | Delete all prior tokens on login | OAuth2 tokens are short-lived + refresh; ABP does not enforce single-session by default | Acceptable deviation; session management handled by token expiry |
| 3 | Email lookup lowercase normalization | `emailId.toLowerCase()` before lookup | ABP `UserManager` normalizes email to uppercase (ASP.NET Identity standard) | Framework behavior; both cases are case-insensitive for practical purposes |
| 4 | Auto-resend verification on unverified login | Silently auto-resends verification email on every failed unverified login | ABP shows error message; explicit "Resend" link for user to click | Intentional improvement: removes silent email flood on repeated unverified login attempts |
| 5 | Background image + doctor logo | Custom full-screen background with dark overlay | LeptonX login page (configurable theme; clinic logo set in LeptonX settings) | Framework change; branding achievable via LeptonX theme configuration |

---

## 11. OLD Source Citations

| File | Lines | Content |
|---|---|---|
| `login/login.component.html` | 1-40 | Login form layout (email, password, links, button) |
| `login/login.component.ts` | 78-120 | Submit handler, localStorage save, role-based redirect |
| `PatientAppointment.Domain/Core/UserAuthenticationDomain.cs` | 50-155 | Email gate, password check, JWT generation, single-session |
| `docs/parity/external-user-login.md` | all | Full parity audit: L1-L7 gaps, decision history, error message copy |

---

## 12. Verification Checklist

- [ ] Unauthenticated user navigates to the app root and sees the login page (AuthServer)
- [ ] Login page shows clinic logo and applies brand colors via LeptonX theme
- [ ] Email + password fields present; Sign In button submits on click and on Enter
- [ ] "Forgot password" link navigates to ABP password reset flow
- [ ] "Sign Up" link navigates to ABP/custom registration page
- [ ] Valid credentials for an external user → redirected to `/home`
- [ ] Valid credentials for an internal user → redirected to `/dashboard`
- [ ] Invalid password → "Invalid username or password" error shown
- [ ] Unverified email → clear error with "Resend verification email" link
- [ ] Inactive account → "Account is not active" error shown
- [ ] Verified + active external user logs in without email-confirmation gate
- [ ] Authenticated user navigating to `/` is immediately redirected (not shown login page)
