---
status: draft
issue: invite-external-user
owner: AdrianG
created: 2026-05-15
approach: code (no tests; live verification deferred to the slow-loop test session)
---

# Invite-external-user: tokenized invite flow + menu wiring

## Goal

Internal staff (IT Admin / Staff Supervisor / Clinic Staff) can email a
prospective external user (Patient / Applicant Attorney / Defense Attorney
/ Claim Examiner) a one-time-use, time-limited invite link. The recipient
clicks the link, lands on the AuthServer Razor register page with their
email and role prefilled and locked, sets their own password, and
completes registration. The invitation is marked accepted; the same link
cannot be reused.

The invite link is the primary handoff from "external user called the
clinic" to "external user self-services on the tenant portal".

## Why

OLD app has no equivalent. The current NEW implementation is the
"D.2 (2026-04-30): DEV-ONLY admin-side invite form" at
`angular/src/app/external-users/components/invite-external-user.component.ts`
that emits an unbounded register URL with no token, no expiry, no
acceptance tracking. The component itself documents "anyone with the
URL can register that email" and is gated only by a DEV-ONLY banner.

Adrian's lock 2026-05-15:
- Front desk (Clinic Staff) needs this in production, not just dev.
- The link must be one-time-use with a 7-day TTL.
- The current AppService authorization (`admin, Staff Supervisor, IT Admin`)
  is too narrow; widen to add `Clinic Staff` so the front desk is included.
- The link must NOT be navigable from the SPA today (no menu, no
  dashboard card) -- needs wiring into a new "User Management" top-level
  LeptonX menu with "Invite External User" as a child.
- Email body is a branded "Dr. <tenant> invited you to register"
  template; the existing `INotificationDispatcher` + per-tenant
  `NotificationTemplate` row is the delivery path.

## Locked decisions (2026-05-15)

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | New `Invitation` domain entity (per tenant) with token, TTL, acceptance state | Required for one-time-use + expiry + audit. Lives in the existing `ExternalSignups` feature folder so domain-service ownership stays with `ExternalSignupAppService` / a new `InvitationManager`. |
| 2 | Token format: 32-byte cryptographic random, URL-safe Base64; stored as `HMACSHA256(token, server-side key)` (never plaintext) | Same approach ABP Identity uses for password-reset tokens (verified against `IdentityUserManager.GeneratePasswordResetTokenAsync`). Hashing at rest means a DB breach does not leak active tokens. |
| 3 | Invite URL targets the AuthServer Razor register page: `http://{tenant}.localhost:44368/Account/Register?inviteToken=<token>` | All authentication UI is owned by AuthServer per the locked design (see `project_authserver-ui-not-spa.md`). Register page reads `inviteToken`, validates server-side, prefills + locks email + role. |
| 4 | Authorization gate at AppService: `[Authorize(Roles = "admin,IT Admin,Staff Supervisor,Clinic Staff")]` | Adrian lock 2026-05-15. Internal-only. `admin` (Volo built-in tenant-admin) included for safety; never grants external users access. Doctor stays excluded -- not a user role. |
| 5 | Menu visibility: same 4 roles as the AppService gate, checked client-side via `permissionGuard` / role check in `route.provider.ts` | Server gate is authoritative; client gate is defensive (no 403-on-click surprise). |
| 6 | NotificationTemplate `InviteExternalUser` seeded per tenant; subject "You're invited to <tenant> Patient Portal" | Reuses the existing `INotificationDispatcher` path; IT Admin can edit per tenant; queued via Hangfire on the API host. |
| 7 | One-time-use: `AcceptedAt` is set inside the same transaction as the user create; double-accept is impossible | Optimistic concurrency on the Invitation row prevents a race where two simultaneous register attempts both see the row as Active. |
| 8 | Always show the invite URL to the admin with a "Copy link" button (in addition to firing the email) | SMTP delivery is unreliable in dev (BUG-020); production should also retain the manual fallback so a staff member can verbally read the link if needed. |
| 9 | After acceptance, reloading the invite URL renders a friendly "already accepted" page (NOT a 404) with two messages: (a) "This invitation has already been used. If that was you, sign in here." (b) "If you did not register with this invitation, contact the clinic to request a new link." Includes a "Sign In" button linking to `/Account/Login`. | The secondary message is a security cue: if the legitimate recipient sees "already accepted" but never registered, someone else may have intercepted the link. Surfacing this prompts the user to escalate to staff who can revoke + reissue. Friendlier than a 404 and doesn't leak whether the URL was valid in the first place. |

## What's in scope

### Domain layer (new)

- `src/HealthcareSupport.CaseEvaluation.Domain/ExternalSignups/Invitation.cs` (new entity, IMultiTenant)
- `src/HealthcareSupport.CaseEvaluation.Domain/ExternalSignups/IInvitationRepository.cs` (interface)
- `src/HealthcareSupport.CaseEvaluation.Domain/ExternalSignups/InvitationManager.cs` (issue + validate + accept; transactions + concurrency)
- `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/ExternalSignups/EfCoreInvitationRepository.cs`
- `CaseEvaluationDbContext` -- `DbSet<Invitation>` + `OnModelCreating` config + EF migration

### Application layer (changes)

- Replace existing `IExternalSignupAppService.InviteExternalUserAsync` with:
  - `CreateInviteAsync(InviteExternalUserDto)` -- creates Invitation row, enqueues email, returns `InviteExternalUserResultDto { inviteUrl, email, roleName, tenantName, expiresAt }`. Drops `emailEnqueued` (always queues).
  - `ValidateInviteAsync(string token)` -- anonymous; returns `InvitationValidationDto { email, userType, tenantSlug, expiresAt }` or throws `BusinessException(InviteInvalidOrExpired)`.
  - `AcceptInviteAsync(AcceptInviteInput { token, password, firstName, lastName, ... })` -- anonymous; atomic create-user + mark-accepted.
- Add localization keys + `CaseEvaluationDomainErrorCodes.InviteInvalidOrExpired`, `InviteAlreadyAccepted`.

### AuthServer (changes)

**Revised approach 2026-05-15 (during verification)**: extend the existing JS overlay at `src/HealthcareSupport.CaseEvaluation.AuthServer/wwwroot/global-scripts.js` instead of overriding the stock Razor page. The overlay already hijacks the stock `/Account/Register` form, injects role + name + confirm-password fields, resolves tenant from `?__tenant=` / subdomain / cookie, and POSTs to `/api/public/external-signup/register`. Extending it is dramatically less invasive than a standalone Razor override and reuses the tested submit interception layer.

Changes to `global-scripts.js`:

- On page load, read `?inviteToken=X` from the query string. If present, fetch validation via `GET /api/public/external-signup/validate-invite?token=X` (new endpoint).
- On 200, prefill email + role, mark both as readonly, render a small "You've been invited as <role> by <tenant>" banner above the form.
- On 4xx, render a friendly banner per the error code:
  - `InviteAlreadyAccepted` -> "This invitation has already been used. If that was you, [Sign In]. If you did not register with this invitation, contact the clinic to request a new link."
  - `InviteExpired` -> "This invitation has expired. Contact the clinic to request a new link."
  - `InviteInvalid` -> "This invitation link is invalid. Contact the clinic to request a new link."
  Submit button is hidden / disabled in all three cases.
- On submit, include `inviteToken` in the request body so the server can atomically validate + accept.

No new Razor file required. The Razor page stays stock; the JS overlay is the customization seam.

The server-side enforcement is what makes the flow secure -- the client cannot bypass the gate even if the user opens dev tools and edits the prefilled fields, because `RegisterAsync` re-validates the token on the server and uses the server-resolved email + role (not the form's).

### Notification template (new)

- Seed `InviteExternalUser` row in the `NotificationTemplate` table for every tenant via `NotificationTemplateSeederContributor`.
- Body template at `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/InviteExternalUser.html` with token replacement variables: `##PatientFirstName##` (best-effort -- left blank when not derivable from email), `##URL##`, `##TenantName##`, `##InvitingUserName##`, `##ExpiresAt##`.

### Angular (changes)

- `angular/src/app/external-users/components/invite-external-user.component.ts` + `.html`:
  - Drop the yellow "DEV-ONLY" banner.
  - Show `expiresAt` next to the invite URL.
  - Update the success state copy to mention email delivery + the copy-the-link fallback.
- `angular/src/app/route.provider.ts`:
  - Register new top-level menu "User Management" (`::Menu:UserManagement` localization key) with child "Invite External User" (`::Menu:InviteExternalUser`) routing to `/users/invite`.
  - Add `requiredPolicy` (or role check) gating menu visibility to IT Admin + Staff Supervisor + Clinic Staff + `admin`. ABP convention: define a permission `CaseEvaluation.UserManagement.InviteExternalUser` in `CaseEvaluationPermissionDefinitionProvider`, grant to those four roles via seed contributor, and reference the permission name on the menu route + the AppService.
- `angular/src/app/app.routes.ts`:
  - Add `canActivate: [authGuard, permissionGuard]` + `data: { requiredPolicy: 'CaseEvaluation.UserManagement.InviteExternalUser' }` on the existing `/users/invite` route.
- Localization JSON entries (`en.json`):
  - `Menu:UserManagement`, `Menu:InviteExternalUser`, plus the new error codes and any copy strings.

### Permission definitions

- New permission group `CaseEvaluation.UserManagement`
- Child permission `CaseEvaluation.UserManagement.InviteExternalUser` (the name explicitly says WHO is being invited so the perm-grants UI is unambiguous to IT Admin)
- Register in `CaseEvaluationPermissionDefinitionProvider.cs`
- Default-grant via seeder to: `admin` (Volo built-in), `IT Admin`, `Staff Supervisor`, `Clinic Staff`. Verify the convention used for role-permission grants in this codebase before writing the seeder update.

### Cleanup carried in this PR

- Delete `docs/plans/2026-05-14-forgot-password-fix.md` (merged via PR #201).
- File `BUG-021-login-tempdata-success-banner.md` -- done in this same PR.

## What's out of scope (separate tickets)

- Revoke / resend invite UI (admin can re-issue from the form today)
- Invitation history page (audit table)
- BUG-020 SMTP password decrypt warning (still open; unrelated)
- BUG-021 Login.cshtml TempData banner override (filed; fix in a follow-up PR)
- The Adobe Acrobat PDF conversion path (already deferred elsewhere)

## File map

| File | Action | Layer | Lines (approx) |
|---|---|---|---|
| `Domain/ExternalSignups/Invitation.cs` | NEW | Domain | ~80 |
| `Domain/ExternalSignups/IInvitationRepository.cs` | NEW | Domain | ~20 |
| `Domain/ExternalSignups/InvitationManager.cs` | NEW | Domain | ~150 |
| `EntityFrameworkCore/ExternalSignups/EfCoreInvitationRepository.cs` | NEW | EF Core | ~40 |
| `EntityFrameworkCore/CaseEvaluationDbContext.cs` | UPDATE | EF Core | +20 |
| `EntityFrameworkCore/Migrations/{timestamp}_AddInvitations.cs` | NEW | EF Core | ~60 (generated) |
| `Application.Contracts/ExternalSignups/InviteExternalUserDto.cs` | UPDATE | Contracts | +5 (rename methods upstream) |
| `Application.Contracts/ExternalSignups/InviteExternalUserResultDto.cs` | UPDATE | Contracts | +5 (add expiresAt) |
| `Application.Contracts/ExternalSignups/InvitationValidationDto.cs` | NEW | Contracts | ~20 |
| `Application.Contracts/ExternalSignups/AcceptInviteInput.cs` | NEW | Contracts | ~30 |
| `Application.Contracts/ExternalSignups/IExternalSignupAppService.cs` | UPDATE | Contracts | +15 |
| `Application/ExternalSignups/ExternalSignupAppService.cs` | UPDATE | Application | +150 |
| `HttpApi/Controllers/ExternalUsers/ExternalUserController.cs` | UPDATE | HttpApi | +30 |
| `Domain.Shared/CaseEvaluationDomainErrorCodes.cs` | UPDATE | Domain.Shared | +6 |
| `Domain.Shared/Localization/CaseEvaluation/en.json` | UPDATE | Domain.Shared | +12 |
| `Domain.Shared/NotificationTemplates/NotificationTemplateConsts.cs` | UPDATE | Domain.Shared | +2 |
| `Domain/NotificationTemplates/EmailBodies/InviteExternalUser.html` | NEW | Domain | ~80 |
| `Domain/NotificationTemplates/NotificationTemplateSeederContributor.cs` | UPDATE | Domain | +25 |
| `Application.Contracts/Permissions/CaseEvaluationPermissions.cs` | UPDATE | Contracts | +10 |
| `Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs` | UPDATE | Contracts | +15 |
| `Domain/Identity/InternalUserRoleDataSeedContributor.cs` (or similar) | UPDATE | Domain | grant new permission to the four roles |
| `AuthServer/wwwroot/global-scripts.js` | UPDATE | AuthServer | +120 (invite-token branch) |
| `angular/src/app/external-users/components/invite-external-user.component.ts` | UPDATE | SPA | -10 (drop DEV-ONLY) |
| `angular/src/app/external-users/components/invite-external-user.component.html` | UPDATE | SPA | template tweaks |
| `angular/src/app/route.provider.ts` | UPDATE | SPA | +30 (new menu) |
| `angular/src/app/app.routes.ts` | UPDATE | SPA | +permissionGuard on /users/invite |
| `docs/parity/wave-1-parity/internal-user-invite-external-user.md` | NEW | Docs | ~120 |
| `docs/plans/2026-05-14-forgot-password-fix.md` | DELETE | Docs | -- |
| `docs/runbooks/findings/bugs/BUG-021-*.md` | NEW | Docs | -- (filed alongside) |

## Slow-loop test plan (deferred to test session)

Login as `it.admin@hcs.test` (host) AND `admin@falkinstein.test` (tenant) AND `clinicstaff@falkinstein.test` (or seed if absent) to cover the role gate.

| # | Step | Expected |
|---|---|---|
| 1 | Navigate to `http://falkinstein.localhost:4200/dashboard` after login | Side menu shows "User Management" parent with "Invite External User" child for IT Admin, Staff Supervisor, Clinic Staff. Hidden for external roles. |
| 2 | Click "Invite External User" | Lands on `/users/invite`. Form renders with email + role dropdown. No DEV-ONLY banner. |
| 3 | Enter `software.four@gesco.com`, role = Applicant Attorney, submit | Success card with inviteUrl, copy button, expiresAt label. Inbox check: email arrived from `InviteExternalUser` template. |
| 4 | Open the URL in a clean incognito context | AuthServer Razor `/Account/Register?inviteToken=...` renders. Email field is prefilled with `software.four@gesco.com` and disabled. Role is prefilled and disabled. |
| 5 | Set first name, last name, password, submit | Account created. Invitation row `AcceptedAt` set. Browser redirected to AuthServer Login with success banner (note: BUG-021 still blocks the banner -- expected). |
| 6 | Reload the URL after step 5 (token consumed) | AuthServer Register page shows "This invitation has already been used. If that was you, sign in here." with a Sign In button to `/Account/Login`. Secondary line: "If you did not register with this invitation, contact the clinic to request a new link." No 500. |
| 7 | Wait 7 days (or manually expire the row in DB), reload the URL | "This invitation has expired. Contact the clinic to request a new link." friendly message. |
| 8 | Tamper the token (one char change) | "This invitation link is invalid. Contact the clinic to request a new link." friendly message. |
| 9 | As Clinic Staff, try the same flow | Identical behavior to Staff Supervisor / IT Admin (gate passes). |
| 10 | As an external user (e.g. patient), navigate to `/users/invite` | Permission guard hides the menu link; direct URL access returns 403 from the AppService POST. |
| 11 | Inbox after step 5 | Recipient gets a "Welcome to <tenant>" registration confirmation (existing `UserRegistered` template, no change required). |

## Risks

- **Token storage migration**: existing `InviteExternalUserAsync` is in use today (DEV-ONLY mode); replacing it with the tokenized path is a breaking change for any tests or seed scripts that depended on the old contract. Mitigation: keep the AppService method name `InviteExternalUserAsync` but change its semantics; update the proxy regen step in the test plan.
- **JS overlay coupling**: extending `global-scripts.js` instead of overriding the Razor page means the invite UX lives inside a 1100-line JS file that's already complex. Mitigation: the new code is a self-contained module-level branch (one init function, one banner-renderer, one prefill applier) gated on the presence of `?inviteToken=` -- everything else in the overlay is untouched, so existing self-register flow stays stable.
- **Permission grant seeding**: the project's pattern for granting permissions to roles needs verification before implementation. The `InternalUserRoleDataSeedContributor` is a likely home but its current shape needs to be read first.
- **Multi-tenant token uniqueness**: the token is the URL key; need to verify it can be resolved without prior tenant context. Plan is to encode the token in a way that the host-scope register page can identify the tenant from the Invitation row itself.

## Acceptance (when slow-loop test session signs off)

- Internal user with one of the 4 gate roles sees the "User Management > Invite External User" menu link.
- POST `/api/app/external-users/invite` from a non-gated role returns 403.
- Submitting an invite creates an `Invitation` row, queues an `InviteExternalUser` template email, and renders the invite URL + copy button.
- The invite URL opens the AuthServer Razor Register page with email + role prefilled and locked.
- Completing registration creates the IdentityUser, sets `Invitation.AcceptedAt`, and bounces to Login.
- Reusing the URL after acceptance shows a friendly "already used" message.
- Reusing the URL after expiry shows a friendly "expired" message.
- A bogus / tampered token shows a friendly "invalid" message; no 500.
- BUG-021 file is present in this PR.
- The merged `2026-05-14-forgot-password-fix.md` plan file is deleted in this PR.

## Rollback

Revert the PR. The new entity table can stay in the DB (no FK weight). The DEV-ONLY component reverts to its prior state.
