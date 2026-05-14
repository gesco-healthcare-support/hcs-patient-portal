# Userflow testing findings -- 2026-05-13

**Session:** main-worktree userflow testing per
`docs/runbooks/MAIN-WORKTREE-USERFLOW-TESTING.md`.

**Stack under test:** `W:\patient-portal\main` on branch `main` at tip
`b740b01` (post-PR #189 promotion).

**Running on alternate ports** (Adrian's call 2026-05-13) so the
`replicate-old-app` fix-session stack can keep running on the canonical
ports. Active alt-port map:

| Service | Alt port (this session) | Canonical port (fix session) |
| --- | --- | --- |
| SQL Server | 1435 | 1434 |
| Redis | 6380 | 6379 |
| MinIO API / Console | 9002 / 9003 | 9000 / 9001 |
| Gotenberg | 3001 | 3000 |
| AuthServer | 44369 | 44368 |
| API | 44328 | 44327 |
| Angular SPA | 4201 | 4200 |
| DB name | `CaseEvaluationTesting` | `CaseEvaluation` |

When the hand-off doc cites canonical ports (e.g.
`http://falkinstein.localhost:44368/Account/Register`), substitute the
alt port (`44369`).

**Pre-flight state captured:**

- Falkinstein tenant id: `2b40625d-d6b9-519c-e15f-3a213589d782`.
- All 16 seeded users present with the role assignments documented in
  Part 4 of the hand-off doc. Synthetic users (`@falkinstein.test`,
  `@evaluators.com`, `it.admin@hcs.test`) seeded with
  `EmailConfirmed = 1`; Gmail inbox users (`@gesco.com`) seeded with
  `EmailConfirmed = 0` so the verify-email flow can be exercised
  against a real inbox. `admin@abp.io` has both a host-scope row and a
  tenant-scope row (expected).

---

## Ticket template (Part 11 of the hand-off doc)

```
[BUG-{NNN}] {short title <= 70 chars>}

Severity: blocker | high | medium | low
Role: Patient | Applicant Attorney | Defense Attorney | Claim Examiner |
      Clinic Staff | Staff Supervisor | admin | IT Admin
Flow: {feature name from docs/parity/wave-1-parity/}
Component: {NEW source file path that contains the bug, best guess}

Steps to reproduce:
  1. As {role}, navigate to {URL}
  2. {Action}
  3. {Observation}

Expected (per OLD parity):
  {What OLD does in the same situation, with OLD source ref}

Actual (current NEW behaviour):
  {What NEW does, with NEW source ref if known}

Evidence:
  - Screenshot: {path to PNG saved under tests/screenshots/}
  - Network request: {URL + method + status + relevant headers/body}
  - Console error: {verbatim error message + line}
  - DB state: {SQL query + result, if relevant}

OLD source: P:\PatientPortalOld\{path}:{line}
NEW source: src\{path}:{line}
Parity doc: docs/parity/wave-1-parity/{slug}.md (or NONE)

Suggested fix scope:
  {Best guess. Optional. Fix worktree decides.}
```

Severity bands: blocker / high / medium / low (see Part 11).

---

## Findings

### [BUG-001] Register form leaks user enumeration: duplicate-email error confirms account existence

```
Severity: high
Role: Patient (also affects Applicant Attorney / Defense Attorney / Claim Examiner)
Flow: external-user-registration
Component: AuthServer Razor Account/Register + AbpUserManager / ExternalSignup app service

Steps to reproduce:
  1. Navigate to http://falkinstein.localhost:44369/Account/Register
  2. Type any existing email -- e.g. SoftwareThree@gesco.com (seeded with EmailConfirmed=0)
  3. Fill First Name "Software", Last Name "Three", Password "1q2w3E*r", Confirm "1q2w3E*r"
  4. Click "Sign Up"

Expected (per OWASP A07:2021 + general healthcare-domain caution):
  Registration response should NOT confirm or deny whether an email is
  already registered. Industry practice: return a generic acknowledgement
  like "If this email is new, you will receive a verification message
  shortly. If it is already registered, sign in instead." Per OWASP
  Identification & Authentication guidance the registration endpoint
  must not be an enumeration oracle. In a healthcare context
  (HCS Patient Portal handles PII associated with workers'-comp
  evaluations), confirming an email maps to an existing Patient is
  doubly sensitive -- it tells an attacker that the person is a patient
  at this practice.

  OLD parity: not directly comparable -- OLD's database does not contain
  SoftwareThree@gesco.com, so OLD's register succeeded. We could not
  confirm whether OLD also leaks enumeration without seeding the same
  email into OLD's DB. Per the parity audit (UserDomain.AddValidation):
  OLD also returns a duplicate-email message in the error path, so this
  is likely a long-standing leak inherited verbatim. Per the project
  CLAUDE.md "Bug and deviation policy", this is a clear security bug --
  fix in NEW silently rather than replicate.

Actual (current NEW behaviour):
  - Server returns HTTP 403 with JSON body
    {"error":{"code":null,"message":"Email address is already used:
    SoftwareThree@gesco.com","details":null,"data":{},
    "validationErrors":null}}
  - Browser renders a native window.alert() dialog with text
    "Email address is already used: SoftwareThree@gesco.com"
  - The literal email is echoed back -- no rate limiting observed.

Evidence:
  - Screenshot: tests/screenshots/2026-05-13/patient-register-verify-login/06-new-register-duplicate-alert.png (alert dialog could not be screenshotted -- replay via the steps above to reproduce)
  - Network request: POST /api/public/external-signup/register -> 403
    Forbidden, response-body shown above
  - Correlation ID: 68469a17cc0a4032aa9c84cd456cd769

OLD source: P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs (AddValidation)
NEW source: AuthServer Account/Register page model + Application/ExternalSignups/* (specific file TBD by fix worktree)
Parity doc: docs/parity/wave-1-parity/external-user-registration.md

Suggested fix scope:
  Either (a) collapse all register-error responses to a generic 202
  Accepted message "If this email is new, you will receive a
  verification email shortly", regardless of whether the email already
  exists, OR (b) treat duplicate-email-for-unverified-account as a
  "resend verification" trigger silently. Add rate limiting (e.g. 5
  /register POSTs per IP per minute) so the enumeration oracle isn't
  brute-forceable via timing or burst. Track in
  docs/security/SESSION-AND-TOKENS.md once the fix lands.
```

### [BUG-002] Register-error uses native window.alert() instead of in-app message

```
Severity: medium
Role: Patient (also affects all external-user registration)
Flow: external-user-registration
Component: AuthServer Razor Account/Register page (likely global-scripts.js or page-specific JS)

Steps to reproduce:
  1. Trigger any register error (e.g. submit with duplicate email per BUG-001 steps).
  2. Observe error UI.

Expected (per OLD parity + modern UX):
  OLD shows a styled in-page banner / toast: "Your registration is
  successfully done, please verify your email to login." (success case)
  -- error case in OLD likely uses the same styled banner pattern (not
  observable from this session without an existing OLD user). Either
  way, browsers' native alert() is jarring, blocks the UI thread, and
  is not styleable / localizable.

Actual (current NEW behaviour):
  Native window.alert() dialog with the raw server message.

Evidence:
  - Playwright trapped the dialog as
    Modal state: ["alert" dialog with message "Email address is already
    used: SoftwareThree@gesco.com"]
  - No styled in-page error appears after the alert is dismissed; the
    form just re-enables.

OLD source: P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\add\user-add.component.ts (toast / banner wiring)
NEW source: AuthServer Account/Register page + JS (likely needs an
  in-page error placeholder + form-submit JS that writes to it on
  non-2xx).
Parity doc: docs/parity/wave-1-parity/external-user-registration.md

Suggested fix scope:
  Replace alert() with an in-page error placeholder. LeptonX has alert
  components (.lpx-alert-error or similar); pattern-match against
  existing /Account/Login error rendering for consistency.
```

### [BUG-003] Register endpoint returns HTTP 403 for duplicate email (should be 4xx client error)

```
Severity: medium
Role: Patient (also affects all external-user registration)
Flow: external-user-registration
Component: AuthServer / Application/ExternalSignups/ExternalSignupAppService.cs (likely)

Steps to reproduce:
  1. Same as BUG-001 -- POST a duplicate email to /api/public/external-signup/register.

Expected (per REST conventions + OpenAPI guidance):
  Duplicate-email is a validation failure -> 400 Bad Request or 409
  Conflict. 422 Unprocessable Entity is also defensible. 403 Forbidden
  semantically means "you do not have permission to perform this
  action" -- it would be appropriate for a permission gate but not a
  data-validation error. Returning 403 here will mislead client error
  handlers (which often special-case 403 for re-auth flows).

Actual (current NEW behaviour):
  HTTP 403 Forbidden, body
  {"error":{"code":null,"message":"Email address is already used: ..."}}.

Evidence:
  - Network request: POST /api/public/external-signup/register -> 403
    Forbidden, _abperrorformat: true header set, correlation ID
    68469a17cc0a4032aa9c84cd456cd769.

OLD source: (n/a -- OLD uses a different status-code convention in its custom error envelope)
NEW source: ExternalSignupAppService or AbpUserManager wrap-throw
  (Volo.Abp.BusinessException with a 403 default? confirm with ABP team)

Suggested fix scope:
  Throw a Volo.Abp.UserFriendlyException or a custom DomainException
  that ABP maps to 400 / 409. If using BusinessException, set the
  ErrorCode to a value that maps to 400 via
  AbpExceptionHttpStatusCodeOptions. Confirm with the ABP error-handling
  module: Volo.Abp.AspNetCore.ExceptionHandling.AbpExceptionHttpStatusCodeOptions.
```

### [BUG-004] User Type pre-selects Patient on NEW; OLD requires explicit selection

```
Severity: low
Role: external (Patient / AA / DA / CE)
Flow: external-user-registration
Component: AuthServer Account/Register.cshtml (User Type select rendering)

Steps to reproduce:
  1. Navigate fresh to http://falkinstein.localhost:44369/Account/Register
  2. Observe the User Type dropdown.

Expected (per OLD parity):
  OLD shows a disabled "Select" placeholder option first, requiring the
  user to consciously pick a role (Patient / Adjuster / PatientAttorney
  / DefenseAttorney). Forces deliberate selection.

Actual (current NEW behaviour):
  Patient is pre-selected. A user who clicks Sign Up without changing
  it will silently register as a Patient even if they meant Applicant
  Attorney etc. The role gates downstream (e.g. AA-only JDF upload,
  DA-only doc paths) make the choice non-trivial -- silently defaulting
  is a UX trap.

OLD source: P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\add\user-add.component.html (User Type select)
NEW source: src\HealthcareSupport.CaseEvaluation.AuthServer\Pages\Account\Register.cshtml or equivalent.
Parity doc: docs/parity/wave-1-parity/external-user-registration.md

Suggested fix scope:
  Add a leading "Select a user type" disabled option, default-selected.
  Client-side + server-side validation should reject submission when
  this option is still active.
```

### [BUG-005] Sign Up button enabled before form is valid; OLD disables until fields filled

```
Severity: low
Role: external (Patient / AA / DA / CE)
Flow: external-user-registration
Component: AuthServer Account/Register page form (button disabled-state binding)

Steps to reproduce:
  1. Navigate fresh to http://falkinstein.localhost:44369/Account/Register
  2. Without typing anything, hover Sign Up.

Expected (per OLD parity):
  OLD's Sign Up button is `disabled` until First/Last/Email/Password/
  ConfirmPassword are all populated and User Type is selected.

Actual (current NEW behaviour):
  Sign Up button is `enabled` from page load. Clicking it with empty
  fields shows... TBD (not exercised yet).

OLD source: user-add.component.html / .ts (form-valid binding)
NEW source: Account/Register.cshtml form
Parity doc: docs/parity/wave-1-parity/external-user-registration.md

Suggested fix scope:
  Wire button disabled-binding to form validity. Razor-page native
  approach: use ASP.NET Core unobtrusive validation; LeptonX styling
  should respect .disabled state.
```

### [BUG-006] BLOCKER: Email-verification API returns 405 Method Not Allowed; SPA misreports as "link may have expired"

```
Severity: blocker
Role: Patient (also affects every external user that must verify email before first login -- Applicant Attorney / Defense Attorney / Claim Examiner)
Flow: external-user-registration -> email confirmation -> first login
Component: API /api/account/verify-email endpoint AND
           angular/src/app/.../email-confirmation.component (SPA error reporting)

Steps to reproduce:
  1. Register a new Patient via the AuthServer Register page (per BUG-001 fresh-register flow):
     - Navigate to http://falkinstein.localhost:44369/Account/Register
     - Fill First Name / Last Name / Email / Password / Confirm
     - Click Sign Up -> success banner appears
  2. Open the verification email in the recipient inbox. The URL has
     the form
     http://falkinstein.localhost:4200/account/email-confirmation?userId=<u>&confirmationToken=<t>
     (this session: substitute port 4201 because the testing stack is
     on alt ports; that is not part of this bug -- the bug reproduces
     on canonical 4200 too).
  3. Navigate to the URL in the browser.
  4. SPA shows "We could not verify your email. The link may have
     expired. Use the button below to request a new verification email."
     (in fact the link is fresh -- generated ~30 min before, well
     within token TTL.)

Expected:
  - API POST /api/account/verify-email with body
    {"userId":"<u>","token":"<t>"} should hit ABP's
    AccountAppService.VerifyEmailAsync and return 204 No Content (or
    200 with a confirmation DTO).
  - SPA shows "Email verified -- proceed to sign in" and offers a
    "Sign In" CTA.

Actual:
  - POST /api/account/verify-email returns 405 Method Not Allowed.
    The route exists (otherwise it would be 404) but does not accept
    POST. ABP's AccountAppService.VerifyEmailAsync should be wired up
    as POST by default. Possible causes:
      a) The API host module isn't depending on AbpAccountHttpApiModule.
      b) The endpoint is wired as PUT in this ABP version and the SPA
         is still calling POST.
      c) A custom controller is shadowing the stock route with
         GET-only.
    Need fix-worktree investigation.
  - SPA's error UI says "The link may have expired" regardless of why
    the call failed. A 405 should not be funnelled into a TTL-expired
    message.

Evidence:
  - Network: POST http://falkinstein.localhost:44328/api/account/verify-email
    -> 405 Method Not Allowed
    request body: {"userId":"97e54a2b-8a17-6dc3-6fe0-3a2135b49b46","token":"CfDJ8LV2clo2fEFItEBBANXeMNY7iiyN9Nr+uJF4oxP0WJXyvD/9zrdIlt/5qXVab+6zDSJsQpmX5Pxh8yKLZ5Zjq/HCVU2HhUIe1ptVnU2xLgUi/JypCALqHxVBPasOIQ2waUmiy7QZCUKY4LFmLOjHGUyVCNaLw3d2Kxmjk3MQ1OFiwG0DTYWJi8+oZO7H0l84aseiLUYjl8qlmZyIPRq0stFu9rln0KrOKb5jtpplyhLgJp8A3AwWRoR/YhqsElnEKg=="}
    response status: 405
  - Screenshot: tests/screenshots/2026-05-13/patient-register-verify-login/09-new-verify-failed.png
  - API logs: api-1 [23:30:15 INF] Request finished HTTP/1.1 POST
    http://falkinstein.localhost:44328/api/account/verify-email - 405 0
    null 1144ms

OLD source: P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\Core\UserAuthenticationController.cs (PutEmailVerification PUT endpoint)
NEW source: ABP stock AccountAppService.VerifyEmailAsync; verify it is
  registered as POST in the HttpApi module + that the proxy /
  account-public service is calling the right verb. SPA component:
  angular/src/app/account/email-confirmation* (TBD).
Parity doc: docs/parity/wave-1-parity/external-user-registration.md

Suggested fix scope:
  - Backend: confirm AbpAccountHttpApiModule is in DependsOn for
    HealthcareSupport.CaseEvaluation.HttpApi; confirm
    AccountAppService.VerifyEmailAsync still maps to
    [HttpPost("verify-email")] in this ABP version. If ABP renamed the
    verb to PUT in 10.0.2, regen proxy + update the SPA to PUT.
  - SPA: in the email-confirmation component, branch the error message
    on the HTTP status. 4xx-other-than-410 should say "We could not
    verify your email -- please try again or request a new link" with
    the request id surfaced for support, not a TTL-expired message.
  - Until this is fixed, no external user can complete first-login. This
    blocks every downstream flow tied to a freshly-registered user.

Test plan once fixed:
  - Register a fresh user, click the email link, expect EmailConfirmed
    flips to 1 + SPA shows a success banner with Sign In CTA + clicking
    Sign In lands on /home for a Patient role.
```

---

## SEED-1: SoftwareThree/Four/Five/Six pre-seeded but should not be (per Adrian 2026-05-13)

Per Adrian (2026-05-13): "I never wanted them to be seeded by default;
they are for real users tests." The current
`DemoExternalUsersDataSeedContributor.cs:49-55` defines
`InboxedExternalUsers` and `:134+` seeds them into every tenant on
every fresh DB. Introduced in PR #186 / Issue #119.

Confirmed: the four
`Software{Three|Four|Five|Six}@gesco.com` users are created with
`EmailConfirmed=0` on every `docker compose down -v && up` cycle.

Recommended fix scope (for the replicate-old-app fix worktree):
- Remove the `InboxedExternalUsers` foreach loop from
  `DemoExternalUsersDataSeedContributor.SeedTenantUsersAsync`. The
  `InboxedExternalUsers` array can stay as a constant for any test or
  doc that needs the canonical email-role mapping. (Or move it to a
  separate `InboxedExternalUsers.cs` data file and import where the
  mapping is needed.)
- Update `docs/runbooks/MAIN-WORKTREE-USERFLOW-TESTING.md` Part 4 to
  drop the @gesco.com rows from the seeded-user table; clarify they
  are intended for fresh self-register tests (i.e. you register them
  yourself when you need a real-inbox test, not via seed).
- Verify the testing-session preflight SQL query in
  `docs/runbooks/findings/2026-05-13-userflow-findings.md` -- after the
  fix, only the 11 synthetic + extra-admin + IT-Admin rows should be
  expected from a fresh seed; the Gmail rows will not.

This change is small but high-value: it removes the "user already
exists" trap that biased the BUG-001 / BUG-002 / BUG-003 register
walk, and matches Adrian's testing intent.

---

### [OBS-1] Field inventory comparison (no bug, for parity audit doc update)

OLD `/users/add` form fields visible after selecting User Type=Patient:
- User Type (combobox, options Select(disabled)/Patient/Adjuster/PatientAttorney/DefenseAttorney)
- First Name (text)
- Last Name (text)
- Email (text)
- Password (text)
- Confirm Password (text)
- terms-and-conditions link
- "Already have an account? Sign In" link (BOTTOM)

NEW `/Account/Register` form fields with User Type=Patient pre-selected:
- User Type (combobox, options Patient/Claim Examiner/Applicant Attorney/Defense Attorney; no placeholder)
- First Name (text)
- Last Name (text)
- Email address (text, placeholder name@example.com)
- Password (text, with Show password toggle)
- Confirm Password (text)
- terms-and-conditions link
- "Already have an account? Sign In" link (TOP of card, as a heading)
- Language selector (English) -- NEW only

Role-name reconciliations (verified intentional per
`docs/parity/wave-1-parity/_old-docs-index.md` naming overrides):
- OLD "Adjuster" -> NEW "Claim Examiner"
- OLD "PatientAttorney" -> NEW "Applicant Attorney"
- OLD "DefenseAttorney" -> NEW "Defense Attorney"

DateOfBirth + PhoneNumber: the parity audit doc
`external-user-registration.md` lists these as required form fields
for Patient registration, but NEITHER OLD nor NEW shows them on the
register page. The audit doc may describe the BACKEND model (where
these fields exist on the User entity) rather than the FORM the user
fills out. Recommend updating the audit doc to clarify which fields
are form-required vs entity-required, and where the missing fields
get captured (probably post-login onboarding profile page).

---

## Related parity-audit docs written this session

None yet. The OBS-1 inventory above is candidate content for updating
`docs/parity/wave-1-parity/external-user-registration.md` once Adrian
confirms whether the DateOfBirth / PhoneNumber gap is a doc error or a
real missing-form-field bug.
