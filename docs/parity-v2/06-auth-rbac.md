# 06. Auth & RBAC -- OLD vs NEW behavioral parity

Area 06 of the 10-area parity audit. Scope: login, registration, forgot-
password, email verification, and the role-based access-control model
(7 roles, role x appointment-type matrix, permission tree, external-user
view-scope). Defers external/internal user CRUD management screens to
area 07 (except the auth/identity behavior itself), accessor-at-booking
to area 01, and email content/delivery to area 04.

Source of truth is CODE on both sides. The legacy single-tenant custom-JWT
app at `P:\PatientPortalOld` is compared against the ABP/OpenIddict rebuild
at `W:\patient-portal\replicate-old-app`. Framework swaps (custom JWT ->
OpenIddict; custom Role/RolePermission/ApplicationModule tables -> ABP
PermissionDefinitionProvider; SPA auth pages -> AuthServer Razor; GUID
VerificationCode -> ABP DataProtection token; renamed Adjuster ->
ClaimExaminer; removed Doctor role) are EXPECTED and listed under
"Equivalent -- different implementation", not as gaps.

## Coverage

OLD anchors read in full:
- `PatientAppointment.Api\Controllers\Api\Core\UserAuthenticationController.cs`
- `PatientAppointment.Api\Controllers\Api\Core\UserAuthorizationController.cs`
- `PatientAppointment.Api\Controllers\Api\User\UsersController.cs`
- `PatientAppointment.Api\Controllers\Api\Lookups\UserLookupsController.cs`
- `PatientAppointment.Domain\Core\UserAuthenticationDomain.cs`
- `PatientAppointment.Domain\UserModule\UserDomain.cs`
- `PatientAppointment.Infrastructure\Authorization\UserAuthorization.cs`
- `PatientAppointment.Infrastructure\Authorization\ApplicationAuthorization.cs`
- `PatientAppointment.DbEntities\Models\{User,Role,RolePermission,RoleUserType,RoleAppointmentType,ApplicationModule,ApplicationUserToken}.cs`
- `PatientAppointment.DbEntities\Enums\UserType.cs`, `PatientAppointment.Models\Enums\Roles.cs`
- `PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs` (RoleAppointmentType enforcement only)
- `patientappointment-portal\src\app\components\{login,user,term-and-condition}`, `domain\authorization\`
- `Documents_and_Diagrams\Architecture\SoCal Project Overview Document.pdf` (role x access matrix)
- `_local\{fix-permissions.sql,seed-external-user-roles.sql}` (dev-seed context)

NEW anchors read in full:
- `Application\ExternalSignups\ExternalSignupAppService.cs`
- `Application\ExternalAccount\{ExternalAccountAppService,PasswordResetGate}.cs`
- `Application\Appointments\{BookingFlowRoles,BookingPolicyValidator,AppointmentReadAccessGuard}.cs`
- `Application.Contracts\Permissions\{CaseEvaluationPermissions,CaseEvaluationPermissionDefinitionProvider}.cs`
- `Application.Contracts\ExternalSignups\ExternalUserSignUpDto.cs`, `Domain.Shared\ExternalSignups\ExternalUserType.cs`
- `Domain\Identity\{ExternalUserRoleDataSeedContributor,InternalUserRoleDataSeedContributor}.cs`
- `AuthServer\Pages\Account\{Login,ForgotPassword,ConfirmUser}.cshtml.cs` + page list
- `angular\src\app\shared\auth\post-login-redirect.guard.ts`, `angular\src\app\home\home.component.ts`

## Summary counts

| Class | Count |
| --- | --- |
| Missing behavior | 3 |
| Partial behavior | 4 |
| Intent deviation | 2 |
| Equivalent (different implementation) | 11 |
| OLD-bug (do not port) | 5 |

## Behavioral gaps

### G-06-01 -- Role x appointment-type matrix is a hardcoded AME rule, not the full data-driven table

- **Class:** Partial behavior
- **OLD:** `RoleAppointmentType` table (`DbEntities\Models\RoleAppointmentType.cs`) joins
  RoleId x AppointmentTypeId; enforced at booking in
  `AppointmentDomain.cs:640-642,688-691` --
  `RoleAppointmentType.All().Any(x => x.RoleId == currentUserRoleId && x.AppointmentTypeId == appointment.AppointmentTypeId)`,
  failing with `AppointmentCanNotBook`. Canonical matrix (SoCal Overview p.5):
  PQME = Patient/Adjuster/PatientAttorney/DefenseAttorney; AME = PatientAttorney/DefenseAttorney;
  PQME-REVAL = all 4; AME-REVAL = PatientAttorney/DefenseAttorney.
- **NEW:** `BookingFlowRoles.IsAmeAppointmentType` + `IsAttorneyCaller`, wired in
  `AppointmentsAppService.cs:679-686`: external callers booking an AME-substring type
  who are NOT attorneys are rejected with `AppointmentAmeRequiresAttorneyRole`. Internal
  callers bypass. There is NO `RoleAppointmentType` join entity; restriction is a
  hardcoded substring allow-list.
- **What it is:** The full configurable matrix collapsed to one rule ("AME types require
  an attorney for external callers").
- **Why it existed:** OLD let SoCal admins, in principle, configure any role->type
  combination per doctor via table rows. In practice the seed always encoded the
  4-row matrix above.
- **What it does + user impact:** For the four DEFAULT appointment types, NEW's outcome
  is identical to OLD's seeded matrix (AME/AME-REVAL contain "AME"; PQME/PQME-REVAL do
  not). Divergence only appears if a tenant adds a NEW appointment type whose name
  happens to contain "AME" (would wrongly be attorney-gated) or an AME-style type whose
  name omits "AME" (would wrongly be open to all). OLD would have required a table row
  either way; NEW guesses from the name.
- **Plain-English:** OLD had a settings table saying "which kinds of users can book which
  kinds of visits." NEW hardcodes the one rule that table actually used. Same result
  today, but you can't reconfigure it without a code change, and a future visit type
  with an odd name could be mis-gated.
- **Keep in NEW?** Acceptable for Phase 1 (matches the only matrix that ever shipped).
  Revisit if/when appointment types become tenant-configurable -- at that point port a
  real role-x-type join or add an explicit `RequiresAttorney` flag on AppointmentType.

### G-06-02 -- No `isAccessor` search-only home variant

- **Class:** Partial behavior
- **OLD:** Login returns `IsAccessor` (`UserAuthenticationDomain.cs:102-112`) from
  `User.IsAccessor`. The home component (`home.component.ts:37-39`) sets `isAccessor=true`
  and the template renders a restricted, confirmation-number-search-only home for accessor
  users (a user added to someone else's appointment as a viewer, not a self-registrant).
- **NEW:** `GetMyProfileAsync` surfaces `IsAccessor` (extension property,
  `ExternalSignupAppService.cs:425-426`), but `home.component.ts` does not branch on it --
  all external roles get the same `isPatientUser` home (search + book). No accessor-only
  layout.
- **What it is:** A reduced home layout for view-only accessor users.
- **Why it existed:** OLD distinguished a self-registered external user (can book) from an
  accessor (email added to an appointment, view-only). The accessor home hid booking.
- **What it does + user impact:** A NEW accessor sees the full home including "Book
  Appointment". The booking attempt is still permission/ownership-gated downstream, so
  the security outcome is bounded, but the UX differs from OLD.
- **Plain-English:** Someone invited only to view an appointment used to get a stripped-down
  screen. NEW shows them the normal screen with a book button that won't fully work.
- **Keep in NEW?** Low priority; replicate the accessor home variant when the accessor
  flow (area 01) is finalized so the layout matches OLD.

### G-06-03 -- Forgot-password gate messages collapsed to one generic message

- **Class:** Intent deviation
- **OLD:** `ForgotPasswordValidation` (`UserAuthenticationDomain.cs:157-179`) returns
  DISTINCT messages: not-verified -> "we have sent a verification link..."; inactive ->
  "Your account is not activated"; not-found -> `UserNotExist`. The page renders whichever
  fired.
- **NEW:** `PasswordResetGate.EnsureUserCanRequestReset` throws distinct
  `EmailNotConfirmedForPasswordReset` / `UserInactiveForPasswordReset`, BUT
  `ForgotPasswordModel.OnPostAsync` (`ForgotPassword.cshtml.cs:79-90`) catches ALL
  exceptions and always shows the same generic "if the email matches a registered account,
  a link is on its way." Distinct outcomes are never user-visible.
- **What it is:** OWASP anti-enumeration: every forgot-password outcome looks identical.
- **Why it existed:** OLD leaked account state (existence, verified, active) through distinct
  messages -- the prompt-flagged OLD behavior. NEW deliberately suppresses it.
- **What it does + user impact:** A NEW user who forgot their password and is unverified/
  inactive gets a generic "check your email" and no email arrives, with no hint why. OLD
  told them to verify first. Outcome differs (less helpful, more secure).
- **Plain-English:** OLD told you exactly why a reset didn't work; NEW says "we'll email you
  if you have an account" no matter what, so attackers can't fish for valid emails.
- **Keep in NEW?** Yes -- this is an intentional security improvement, not a defect. Logged
  as intent deviation for completeness; do not revert.

### G-06-04 -- Login active-only / verified-only gates rely on ABP framework defaults

- **Class:** Partial behavior
- **OLD:** Explicit, in domain code. `PostLogin` (`UserAuthenticationDomain.cs:55-145`):
  if `!IsVerified` -> resend verification link + block; if `StatusId==InActive || !IsActive`
  -> block with `UserInactivated`. Two independent inactive signals (`StatusId` enum AND
  `IsActive` bool) both gate.
- **NEW:** No explicit login domain code. ABP `SignInManager` (via
  `OpenIddictSupportedLoginModel`) enforces `IsActive` (rejects inactive) and confirmed-
  email by framework default; the custom `LoginModel` only reshapes the unverified-email
  redirect into a generic banner (`Login.cshtml.cs:138-145`). There is no second `StatusId`
  field -- ABP has only `IsActive`.
- **What it is:** The verified-only + active-only login gate, now framework-provided.
- **Why it existed:** OLD had a dual status model (`StatusId` + `IsActive`). ABP collapses
  this to one `IsActive` flag.
- **What it does + user impact:** Outcome matches OLD for the common cases (unverified ->
  blocked + can resend; inactive -> blocked). The OLD dual-flag edge case (StatusId Active
  but IsActive false, or vice versa) cannot exist in NEW because there is only one flag --
  arguably cleaner. Flagged as Partial because the gate is implicit (framework) rather than
  explicit (code), so it is invisible to a code reader and depends on ABP config not being
  loosened.
- **Plain-English:** OLD's code spelled out "must be verified and active to log in." NEW
  leans on the framework to do the same. Same result, but it's not written down in our code.
- **Keep in NEW?** Yes. Recommend an integration test asserting inactive + unverified users
  cannot obtain a token, so the implicit gate is pinned.

### G-06-05 -- "Adjuster" and "Claim Examiner" exist as two distinct external role values

- **Class:** Intent deviation
- **OLD:** Exactly 4 external roles (`Roles.cs:14-17`): Patient(4), Adjuster(5),
  PatientAttorney(6), DefenseAttorney(7). Adjuster is a real, registerable role.
- **NEW:** `ExternalUserType` enum (`Domain.Shared\ExternalSignups\ExternalUserType.cs`)
  has FIVE values: Patient(1), ClaimExaminer(2), ApplicantAttorney(3), DefenseAttorney(4),
  Adjuster(5). `ToRoleName` maps Adjuster->"Adjuster" and ClaimExaminer->"Claim Examiner"
  as DIFFERENT role strings. The external-role SEEDER
  (`ExternalUserRoleDataSeedContributor.cs:35-38`) only seeds 4 roles -- "Patient",
  "Claim Examiner", "Applicant Attorney", "Defense Attorney" -- and its comment (lines
  39-49) asserts OLD Adjuster == NEW Claim Examiner (same role, renamed).
- **What it is:** A naming/identity contradiction. The seeder and locked memory
  (`project_role-model.md`, `BookingFlowRoles.cs:65-72`) treat Adjuster and Claim Examiner
  as the SAME role under two names; the enum treats them as two separate values, and
  `ToRoleName` produces two distinct role strings.
- **Why it existed:** Mid-port renaming: Adjuster was renamed Claim Examiner, but the enum
  kept BOTH values (Adjuster=5 re-added in Phase 8 "per OLD parity", ClaimExaminer=2 left
  in from an earlier session). The enum self-documents this as audit gap G1.
- **What it does + user impact:** A registration or invite posting `UserType=Adjuster`
  would auto-create an unseeded "Adjuster" role (`EnsureRoleAsync` /
  `RegisterAsync:496`), producing a role with NO permission grants and NO place in any
  lookup -- a dead-end account. Claim Examiner is the seeded, grant-bearing role. Outcome
  diverges from OLD only if something actually submits `Adjuster`; the live UI uses
  Claim Examiner.
- **Plain-English:** OLD's "Adjuster" became "Claim Examiner" in NEW, but the code still
  has both names. If anything books as "Adjuster" it lands in a broken, permission-less
  role.
- **Keep in NEW?** No -- collapse to one. Remove `Adjuster` from the enum (or make
  `ToRoleName(Adjuster)` return "Claim Examiner") so there is exactly one external role
  for the OLD Adjuster concept. Tracked as enum gap G1.

### G-06-06 -- Single-session enforcement (one-token-per-user) not reproduced

- **Class:** Missing behavior
- **OLD:** On successful login, `PostLogin` (`UserAuthenticationDomain.cs:84-90`) deletes
  ALL existing `ApplicationUserToken` rows for the user, then inserts the new one --
  effectively one active session per user; a new login invalidated prior tokens.
- **NEW:** OpenIddict issues a standard access/refresh token per login with no
  prior-token revocation. A user can hold multiple concurrent valid sessions across
  devices/browsers.
- **What it is:** Forced single concurrent session.
- **Why it existed:** OLD's custom JWT store tracked one token row per user and overwrote
  it on each login (also how OLD did logout -- `JwtTokenProvider.LogOut()`).
- **What it does + user impact:** In OLD, logging in on a second device silently logged out
  the first. In NEW, both stay logged in. Behavioral difference; not a security regression
  in the modern model (short-lived tokens + refresh rotation), but a UX/policy change.
- **Plain-English:** OLD only let you be signed in one place at a time; signing in
  elsewhere kicked out the first session. NEW lets you stay signed in on multiple devices.
- **Keep in NEW?** Likely drop (OLD's single-session was a side effect of its token storage,
  not a stated requirement). Flag for Adrian: if single-session is a deliberate policy,
  configure OpenIddict token revocation on login; otherwise document as intentionally
  dropped.

### G-06-07 -- No explicit login rate-limit / lockout parity confirmation

- **Class:** Partial behavior
- **OLD:** No server-side login rate-limit or lockout. The frontend tracked `failedCount`
  in localStorage (`login.component.ts:81-82,106`) and sent it to the server, but the
  server never read or acted on it (see OLD-bug B-06-03). Effectively unlimited login
  attempts.
- **NEW:** ABP Identity lockout is enabled by framework default (failed-attempt threshold
  -> `/Account/LockedOut`, page present at `AuthServer\Pages\Account\LockedOut.cshtml`).
  Forgot-password and resend-verification have explicit per-email rate limits
  (`ExternalAccountAppService.cs:77-79` resend 3/hr + 60s cooldown; HTTP-layer 5/hr on
  reset).
- **What it is:** Brute-force protection.
- **Why it existed:** OLD had none server-side (the client counter was cosmetic). NEW adds
  framework lockout + endpoint rate limits.
- **What it does + user impact:** NEW is STRICTER than OLD (a user can be locked out;
  reset/resend are throttled). This is a security improvement, not a regression, but it is
  a behavioral divergence from OLD's unlimited attempts.
- **Plain-English:** OLD let you guess a password forever; NEW locks the account after too
  many tries and limits reset emails. Better, but different.
- **Keep in NEW?** Yes. Logged so the divergence (and OLD's no-op failedCount) is on record.

### G-06-08 -- Registration captures full demographics OLD did not (no hardcoded Gender/DOB defaults parity)

- **Class:** Missing behavior (minor / inverse)
- **OLD:** Registration (`UserDomain.Add`) persisted the User row directly from the form;
  there were no hardcoded Gender/DateOfBirth/PhoneType defaults at registration -- those
  fields lived on the User row and were filled (or left null) by the register form. The
  prompt's "defaults Gender/DOB/PhoneType" refers to the NEW behavior.
- **NEW:** When `UserType==Patient`, `RegisterAsync` (`ExternalSignupAppService.cs:566-583`)
  creates a separate `Patient` row with HARDCODED placeholders: `genderId: Gender.Male`,
  `dateOfBirth: DateTime.MinValue`, `phoneNumberTypeId: PhoneNumberType.Home`,
  empty name strings. These are placeholders the booker form overwrites later.
- **What it is:** NEW splits identity (IdentityUser) from the Patient demographic row and
  seeds the Patient row with placeholder demographics at registration.
- **Why it existed:** NEW's data model has a dedicated Patient entity distinct from the
  login account; OLD had one User row carrying both. The placeholders avoid a null-required
  Patient row before the patient fills the booking intake form.
- **What it does + user impact:** A freshly registered NEW Patient has a Patient row that
  reads "Male, 0001-01-01, Home phone" until they book. If any read path surfaces that row
  before a booking corrects it, it shows wrong demographics. OLD had no such intermediate
  row. HIPAA-adjacent: placeholder DOB/gender on a real person.
- **Plain-English:** When a patient signs up, NEW immediately makes a half-empty patient
  record guessing they're male, born in year one, with a home phone -- filled in properly
  only when they book. OLD didn't create that stub.
- **Keep in NEW?** Acceptable as a stub, but recommend nulling these (the Patient entity
  fields are nullable per the registration call passing `stateId: null`) rather than using
  real-looking `Gender.Male` -- to avoid surfacing a fabricated gender/DOB. Defer demographic
  capture to booking. Flag for Adrian.

### G-06-09 -- No anonymous root redirect parity check in this area (covered by memory, noted only)

- **Class:** Missing behavior (informational)
- **OLD:** Bare `/` and the login page guard redirected as in
  `login.component.ts:44-69` (authenticated external -> home, internal -> dashboard;
  anonymous -> login). No public landing page.
- **NEW:** `postLoginRedirectGuard` (`shared\auth\post-login-redirect.guard.ts`) replicates
  this exactly: anonymous -> `auth.navigateToLogin()` (out to AuthServer Razor login);
  external -> `/home`; internal/mixed -> `/dashboard`. Matches locked memory
  `project_root-redirect-anonymous.md`.
- **What it is:** Post-login + anonymous routing.
- **Why it existed:** OLD had no public landing; everything required login.
- **What it does + user impact:** NEW outcome is identical. Listed here only to record that
  the behavior WAS verified and is NOT a gap.
- **Plain-English:** Going to the bare site address sends you to login (if signed out) or
  your home/dashboard (if signed in) -- same as OLD.
- **Keep in NEW?** Yes -- already correct. (This row is informational; net not counted as a
  gap. See Equivalent E-06-10.)

## Equivalent -- different implementation

| ID | Behavior | OLD | NEW | Why equivalent |
| --- | --- | --- | --- | --- |
| E-06-01 | Authentication mechanism | Custom JWT (`Rx.Core.Security.Jwt`, `ApplicationUserToken` table) | OpenIddict OAuth + ABP Identity | Both issue a bearer token after credential check; framework swap is expected. |
| E-06-02 | Permission model | `Role`/`RolePermission`/`ApplicationModule`/`ApplicationObject` tables + `spPermissions` proc returning module CanView/Add/Edit/Delete JSON (`UserAuthorization.cs`) | ABP `PermissionDefinitionProvider` tree (`CaseEvaluationPermissionDefinitionProvider.cs`) + per-role grants (`InternalUserRoleDataSeedContributor`) | Both express per-role CRUD-style permissions; OLD's CanView/Add/Edit/Delete maps to NEW's Default/Create/Edit/Delete children. |
| E-06-03 | Per-user authorization endpoint | `UserAuthorizationController` `access`/`authorize` returning module map; client caches `UserPermissionCache` | ABP `currentUser.permissions` in ConfigState; Angular `permissionGuard` | Both deliver the caller's effective permissions to the SPA; outcome-equivalent. |
| E-06-04 | Permission cache | `ApplicationPermission.RoleAccess` in-memory dictionary keyed by role | ABP permission-management cache | Outcome-equivalent caching; explicitly not-a-gap per brief. |
| E-06-05 | Auth UI surface | SPA components (login/forgot/reset/verify-email) | AuthServer Razor pages (Login/ForgotPassword/ResetPassword/EmailConfirmation/ResendVerification) | SPA-auth -> Razor-auth swap is expected (memory `project_authserver-ui-not-spa.md`). |
| E-06-06 | Email-verification token | Single nullable `User.VerificationCode` GUID (reused for verify AND reset) | ABP DataProtection email-confirmation + password-reset tokens (separate, expiring) | Token swap expected; NEW's separate expiring tokens are strictly better but outcome-equivalent (link verifies/ resets). |
| E-06-07 | Adjuster -> Claim Examiner rename | Role `Adjuster` (5) | Role "Claim Examiner" | Renamed role per decision; same concept. (Note the residual enum-dup defect tracked separately as G-06-05.) |
| E-06-08 | Doctor role removed | (Doctor never a login role in OLD either; `InternalUserRoles` lists it for the booking fast-path) | No Doctor user role; Doctor is a reference entity | Decided removal; not a gap. |
| E-06-09 | Confirm-password match + FirmName-required-for-attorneys | `UserDomain.AddValidation:88` (confirm match) + `CommonValidation:272` (FirmName) | `ValidateRegistrationInput` (`ExternalSignupAppService.cs:1101-1142`) | Same two validation rules; NEW additionally fixes OLD's PatientAttorney-checked-twice bug (see B-06-01). |
| E-06-10 | Post-login redirect by user-type + anonymous->login | `login.component.ts:44-69` | `postLoginRedirectGuard` | Identical outcome: external->/home, internal->/dashboard, anon->login. |
| E-06-11 | Email-normalized login lookup | `EmailId == userCredential.EmailId.ToLower()` (`UserAuthenticationDomain.cs:52`) | ABP normalizes username/email to `NormalizedEmail`; `NormalizeEmail` helper trims+lowercases reset/resend lookups | Both do case-insensitive email lookup; framework provides it for login. |

## OLD bugs (do not port)

| ID | OLD bug | OLD source | NEW status |
| --- | --- | --- | --- |
| B-06-01 | Duplicate `PatientAttorney` check -- FirmName-required validation tests `RoleId == PatientAttorney \|\| RoleId == PatientAttorney` (should be PatientAttorney OR DefenseAttorney), so DefenseAttorney FirmName was never required server-side. | `UserDomain.cs:272` | FIXED -- `IsAttorneyRole` checks both attorney roles (`ExternalSignupAppService.cs:1082-1084,1129`). Do not reintroduce the typo. |
| B-06-02 | Dead `FirmName` self-assignment -- `user.FirmName = user.FirmName` (no-op). | `UserDomain.cs:107` | Not ported; NEW persists `FirmName` from the trimmed input (`ExternalSignupAppService.cs:543-545`). |
| B-06-03 | Unused `FailedCount` -- client sends/stores `failedCount` in localStorage but the server never reads it; `User` table has no FailedCount column. No actual lockout. | `login.component.ts:81-82,106`; `User.cs` (no column) | Not ported; NEW uses real ABP lockout (G-06-07). |
| B-06-04 | Dead token-mismatch branch -- `PutEmailVerification` re-queries by `VerificationCode == guid && UserId` AFTER `PutEmailVerificationValidation` already proved the code matches; the mismatch branch can never fire and an un-found user would null-ref. | `UserAuthenticationDomain.cs:272-274,285-293` | Not ported; NEW uses ABP `ConfirmEmailAsync` token validation. |
| B-06-05 | Forgot-password account-enumeration leak -- distinct messages for not-found / unverified / inactive expose account state. | `UserAuthenticationDomain.cs:157-179` | Not ported; NEW returns a single generic message (G-06-03). The PasswordResetGate even documents this as the OLD-bug-fix it replaces. |

Additional OLD oddities observed (context, not in the prompt's list):
- `UserAuthorizationController.Post` passes `ClaimTypes.Role` (the RoleId) into
  `GetAccessModules(applicationModuleId, userId)` whose second param is NAMED `userId`
  but is actually used as a role key throughout `UserAuthorization.cs` -- a misleading
  name, not a bug. NEW's role-keyed grants are correct.
- `_local\fix-permissions.sql` replaces `spPermissions` with a grant-EVERYTHING stub
  returning 150 modules all-true. This is a DEV convenience in the OLD worktree, NOT
  production behavior; the real OLD matrix is data-driven from `RolePermissions` rows.
  Do not treat the stub as the OLD permission model.

## Open questions

1. **Single-session policy (G-06-06):** Is one-concurrent-session a deliberate requirement,
   or an accidental side effect of OLD's single-token-row storage? If required, OpenIddict
   token revocation on login must be configured; if not, document as intentionally dropped.
2. **Adjuster enum cleanup (G-06-05):** Confirm Adjuster and Claim Examiner are the same
   role and remove the duplicate enum value (or remap `ToRoleName`). Which name is canonical
   for the live UI -- "Claim Examiner" (current seeder) is assumed.
3. **Patient demographic placeholders (G-06-08):** Should the registration-time Patient row
   use `null` demographics instead of `Gender.Male` / `DateTime.MinValue`? Confirm the
   booking flow always overwrites before any read path surfaces them (HIPAA-adjacent).
4. **Accessor home variant (G-06-02):** Defer to the area-01 accessor flow, but confirm the
   accessor-only (search-only) home layout is still desired for visual parity.
5. **Role x type configurability (G-06-01):** If appointment types become tenant-configurable
   in Phase 2, decide between porting a real RoleAppointmentType join vs. a
   `RequiresAttorney` boolean on AppointmentType.
6. **Implicit login gates (G-06-04):** Add an integration test pinning that inactive +
   unverified users cannot obtain a token, since the gate is now framework-implicit.
