---
feature: external-user-registration
status: draft
audited: 2026-05-04
old-source:
  - patientappointment-portal/src/app/components/user/users/add/user-add.component.html
  - patientappointment-portal/src/app/components/user/users/add/user-add.component.ts
  - patientappointment-portal/src/app/components/login/verify-email/verify-email.component.html
  - patientappointment-portal/src/app/components/login/verify-email/verify-email.component.ts
  - patientappointment-portal/src/app/components/term-and-condition/term-and-condition.component.ts
  - PatientAppointment.Domain/UserModule/UserDomain.cs
  - PatientAppointment.Domain/Core/UserAuthenticationDomain.cs
  - PatientAppointment.DbEntities/Constants/RegexConstant.cs
new-source: []
parity-audit: docs/parity/external-user-registration.md
shell-variant: 1-unauthenticated
strict-parity: true
---

# external-user-registration -- design

Public self-registration form for the four external roles (Patient,
Adjuster, Applicant Attorney, Defense Attorney). Sits in shell variant
1 (unauthenticated auth-shell). Submits to a new
`IExternalUserRegistrationAppService` (per the parity audit -- ABP's
default `RegisterAsync` lacks the role + attorney-specific fields).
After submit, the user lands on a verification screen via the email
link.

## 1. Routes

- OLD: `/registration` -> NEW: `/account/register` (NEW Angular routes)
- OLD: `/verify-email/:userId?query=:guid` -> NEW: `/account/verify-email/:userId?confirmationToken=:token`

The OLD verify URL uses int `userId` + GUID `query` param. NEW uses
ABP's Guid `userId` + signed-token `confirmationToken` (cryptographic,
not stored). The Angular `/account/verify-email/...` component reads
both and forwards to ABP `IAccountAppService.VerifyEmailAsync`.

## 2. Screen layout

### Registration form (default state, role not yet selected)

```
+--------------------------------------------------------+
| login-bg.jpg image (full viewport)                     |
| + dark scrim (rgba(0,0,0,.25))                          |
|                                                        |
|        +----------------------------------+            |
|        |   [Doctor.png logo, centered]    |            |
|        |                                   |            |
|        |   [User Type select v]            |            |
|        |                                   |            |
|        |   [Sign Up - btn-secondary,       |            |
|        |    btn-block - DISABLED]          |            |
|        |                                   |            |
|        |   By clicking "Sign Up" you       |            |
|        |   agree to our terms of service   |            |
|        |   and privacy policy. We'll       |            |
|        |   occasionally send you account   |            |
|        |   related emails.                 |            |
|        |                                   |            |
|        |   ----- card-footer -----         |            |
|        |   Already have an account?        |            |
|        |   Sign In                         |            |
|        +----------------------------------+            |
|                                                        |
+--------------------------------------------------------+

OLD: ./screenshots/old/external-user-registration/01-empty-form.png
NEW: NEW UI not yet built.
```

### Registration form (role = Patient | Adjuster -- non-attorney)

```
+----------------------------------+
|   [Doctor.png logo, centered]    |
|   [User Type: Patient        v]  |
|   [First Name]   [Last Name]     |   <- form-row, 50/50 split
|   [Email]                        |
|   [Password]                     |
|   [Confirm Password]             |
|   [Sign Up - enabled when valid] |
|   By clicking "Sign Up" ...      |
|   Already have an account?       |
|   Sign In                         |
+----------------------------------+

OLD: ./screenshots/old/external-user-registration/02-patient-role.png
```

### Registration form (role = Applicant Attorney | Defense Attorney)

```
+----------------------------------+
|   [Doctor.png logo, centered]    |
|   [User Type: Applicant Atty  v] |
|   [Firm Name]                    |   <- replaces First/Last row
|   [Email]                        |
|   [Password]                     |
|   [Confirm Password]             |
|   [Sign Up - enabled when valid] |
|   ...                            |
+----------------------------------+

OLD: ./screenshots/old/external-user-registration/03-attorney-role.png
```

The First/Last row hides via `*ngIf="!isAttorney"`; the Firm Name field
shows via `*ngIf="isAttorney"`. `isAttorney` is true when
`roleId == ExternalUserRoleTypeEnum.PatientAttorney || roleId == ExternalUserRoleTypeEnum.DefenseAttorney`.
Cite: `user-add.component.ts`:84-101.

### Post-submit: redirect to login

After successful POST, OLD shows a green toast "Your registration is
successfully done, please verify your email to login." and after a
1000ms delay routes to `/login`. Cite: `user-add.component.ts`:73-76.

NEW preserves the toast copy verbatim and the 1000ms delay-then-redirect.

### Verify-email screen (success state)

```
+------------------------+
|  [Doctor.png logo]     |
|                        |
|  Your account has      |
|  been successfully     |
|  verified.             |
|                        |
|  Thank you             |
|                        |
|  [Click Here To Login] |
+------------------------+

OLD: ./screenshots/old/external-user-registration/04-verify-success.png
```

### Verify-email screen (failure state)

```
+----------------------------+
|  [Doctor.png logo]         |
|                            |
|  Sorry, we couldn't verify |
|  your account.             |
|                            |
|  Thank you                 |
+----------------------------+

(No retry button; OLD just shows the error. The user has to log in,
which triggers the auto-resend flow per parity audit Section
"Login auto-resends verification".)

OLD: ./screenshots/old/external-user-registration/05-verify-failure.png
```

## 3. Form fields

OLD's form uses Bootstrap 4 `placeholder=` attributes for the visible
field hint; the `<label>` element exists but is `sr-only` (screen-reader
only). NEW preserves the placeholders verbatim and keeps `sr-only`
labels for accessibility.

| Label (sr-only) | Placeholder | Field name | Type | Validation | Default | Conditional visibility | OLD citation |
|---|---|---|---|---|---|---|---|
| User Type | "Select" (disabled, selected) | `roleId` | select | required (form invalid until non-null) | `null` (placeholder option) | always | `user-add.component.html`:14-22 |
| First name | "First Name" | `firstName` | text | required + maxLength 50 (when non-attorney) | -- | `*ngIf="!isAttorney"` (in form-row col-md-6) | `user-add.component.html`:23-27, `user-add.component.ts`:96 |
| Last name | "Last Name" | `lastName` | text | required + maxLength 50 (when non-attorney) | -- | `*ngIf="!isAttorney"` (in form-row col-md-6) | `user-add.component.html`:28-31 |
| Firm name | "Firm Name" | `firmName` | text | required + maxLength 50 (when attorney) | -- | `*ngIf="isAttorney"` | `user-add.component.html`:33-36, `user-add.component.ts`:87-89 |
| Email | "Email" | `emailId` | text (no `type="email"` in OLD) | required + email format (server-side) + uniqueness | -- | always | `user-add.component.html`:37-40 |
| Password | "Password" | `userPassword` | password | required + matches `RegexConstant.systemPasswordPattern` | -- | always | `user-add.component.html`:41-44 |
| Confirm Password | "Confirm Password" | `confirmPassword` | password | required + must equal `userPassword` | -- | always | `user-add.component.html`:45-48 |

### Hidden / system-set fields (set in `setDefaultValues`, not user-editable)

Cite: `user-add.component.ts`:57-68.

| Field | Default | Reason |
|---|---|---|
| `applicationTimeZoneId` | 1 | OLD uses a custom timezone table; NEW drops -- ABP/NodaTime handles TZ. **Do not port.** |
| `userTypeId` | `UserTypeEnum.ExternalUser` | NEW: `IsExternalUser` extension property on `IdentityUser` set true. |
| `createdBy` | 1 | OLD self-references; NEW: ABP audit fields set automatically. |
| `createdOn` | `new Date()` | NEW: ABP `ICreationAuditedObject`. |
| `statusId` | `StatusEnum.Active` | NEW: ABP `IsActive = true`. |
| `phoneNumber` | 0 | OLD's quirk: phone defaults to 0 because the field is required at DB level but absent from the public registration form. NEW: keep optional / nullable. **Strict-parity exception** -- see Section 10. |
| `oldPassword` | `"null"` (literal string) | OLD's stub for the User shared model. NEW: irrelevant (registration creates a new user). **Do not port.** |
| `isActive` | `StatusEnum.Active` | Redundant with `statusId`; NEW: ABP `IsActive`. |

### Password regex (server-side validation)

OLD's `RegexConstant.systemPasswordPattern`:

```regex
^(?=.*[0-9])(?=.*[a-zA-Z])(?=.*[-.!@#$%^&*()_=+/\\'])([a-zA-Z0-9-.!@#$%^&*()_=+/\\']+)$
```

Cite: parity audit Section "Password policy reconciliation"
(`docs/parity/external-user-registration.md` lines 292-322).

NEW maps this to ABP `IdentitySettingNames.Password`:

| ABP setting | Value | Reason |
|---|---|---|
| `RequireDigit` | `true` | OLD requires `(?=.*[0-9])` |
| `RequireNonAlphanumeric` | `true` | OLD requires `(?=.*[-.!@#$%^&*()_=+/\\'])` |
| `RequireLowercase` | `false` | OLD allows any letter |
| `RequireUppercase` | `false` | OLD allows any letter |
| `RequiredLength` | `8` | OLD has none; NEW picks sane minimum |

## 4. Tables / grids

None. This is a single form.

## 5. Modals + interactions

| Trigger | Modal title | Body | Primary action | Secondary action | OLD citation |
|---|---|---|---|---|---|
| Click "terms of service and privacy policy" link | (none) | T&C HTML rendered via `<rx-popup>` -> `TermAndConditionComponent` | (close icon `x`) | -- | `user-add.component.html`:51-52, `user-add.component.ts`:127-129 |
| Submit error | "Validation" (default `dialog.validation` title) | Server-side error messages from the `RxDialog.validation(error)` helper -- shown in the `<rx-popup>` style validation modal | "OK" | -- | `user-add.component.ts`:78-80 |

The T&C modal opens from `openTermaAndConidition()` (OLD typo
`Conidition` -- NEW spells it `openTermsAndCondition`; **strict-parity
exception, OLD typo, fixed for correctness**, see Section 10).

The T&C content is per-tenant (see `docs/parity/terms-and-conditions.md`
+ `_branding.md` -- `branding.termsAndConditions` config field).

## 6. Buttons + actions

| Label | Variant | Permission gate | Pre-action confirm? | Success toast | Error toast | OLD citation |
|---|---|---|---|---|---|---|
| Sign Up | primary CTA (`btn btn-secondary btn-block` in OLD) | anonymous (no permission) | N | "Your registration is successfully done, please verify your email to login." | shown via `dialog.validation(error)` -- contents server-side | `user-add.component.html`:49, `user-add.component.ts`:70-82 |
| Sign In (card-footer link) | link | anonymous | N | n/a | n/a | `user-add.component.html`:56-58 |
| terms of service and privacy policy (inline link) | link | anonymous | N | n/a | n/a | `user-add.component.html`:51-52 |

The Sign Up button is `[disabled]="!userFormGroup.valid"`. NEW: same
gate via reactive form's `valid` flag.

NOTE on variant naming: OLD's "btn btn-secondary" CTA is the primary
visual button (the B4 theme repurposes secondary for the brand-blue
fill -- documented in `_components.md` Buttons matrix). NEW maps it to
the `primary` variant in `<app-button>`.

### Verify-email screen buttons

| Label | Variant | OLD citation |
|---|---|---|
| Click Here To Login | primary (`btn btn-primary btn-block font-lg`) | `verify-email.component.html`:20 |

Shown only on success (`*ngIf="isVerified"`). On failure (`notVerified`)
no button is shown.

## 7. Role visibility matrix

Registration is anonymous: any visitor can choose one of the 4 external
roles. The 3 internal roles do NOT appear in the role dropdown -- the
lookup `userLookupGroup.externalUserRoleLookUps` filters to externals
only. Cite: `user-add.component.html`:17-20 (the `*ngFor` over
`externalUserRoleLookUps`).

| UI element | (anonymous visitor) | Patient | Adjuster | Applicant Atty | Defense Atty | Clinic Staff | Staff Supervisor | IT Admin |
|---|---|---|---|---|---|---|---|---|
| Registration form access | Y | (N/A) | (N/A) | (N/A) | (N/A) | (N/A) | (N/A) | (N/A) |
| User Type select | Y -- 4 external options only | -- | -- | -- | -- | -- | -- | -- |
| First/Last Name fields | Y when role in {Patient, Adjuster} | -- | -- | -- | -- | -- | -- | -- |
| Firm Name field | Y when role in {Applicant Atty, Defense Atty} | -- | -- | -- | -- | -- | -- | -- |

After registration the user lands on `/login`, then post-login on
`/home` per shell variant 2. Internal-user creation goes through IT
Admin -- see `it-admin-user-management-design.md`.

## 8. Branding tokens used

Per `_design-tokens.md` + `_branding.md`:

- Auth shell wrapper background -- `--brand-login-bg-url` (replaces OLD's inline `style="background-image: url('./assets/images/login-bg.jpg')"`)
- Auth scrim -- `--scrim-image` (rgba(0,0,0,.25))
- Logo -- `--brand-logo-url` (replaces OLD's `<img src="./assets/images/Doctor.png">`)
- Card -- `--bg-card`, `--border-light`, `--shadow-login-form`, `--space-login-form`
- Form fields -- `--radius-sm`, `--font-family-base`, `--text-body`, `--shadow-input-focus`, `--brand-primary-focus-border`
- Buttons -- `--brand-primary` (Sign Up bg + Click Here To Login bg), `#fff` text, `--fs-button-login`, `--fw-bold`
- Validation invalid bottom border -- `--color-danger`
- Card footer link -- `--brand-primary-text`
- Inline T&C link -- `--brand-primary-text`
- Footnote text "By clicking ..." -- `--text-muted` (OLD: `text-light small`)

Email subject + body branding -- per `_branding.md` Section
"Localization keys": `L["Email:Subject:Registration", clinicShortName]`
and `EmailTemplate.UserRegistered` -> NEW Razor template.

## 9. NEW current-state delta

**NEW UI not yet built.** No `account/register` Angular component
exists; only background SVGs at `angular/src/assets/images/login/`
(per `docs/parity/external-user-registration.md` Section "NEW current
state -> Frontend"). Backend `IExternalUserRegistrationAppService` not
yet built either.

This design.md drives the future build. The implementer should:

1. Scaffold `angular/src/app/account/register/register.component.{ts,html,scss}` using the auth-shell variant 1 chrome (per `_shell-layout.md`).
2. Wire to a new backend `IExternalUserRegistrationAppService` (per the parity audit "Replication notes -> ABP-specific wiring" section).
3. Use `IObjectExtensionManager` to add `IsExternalUser`, `FirmName`, `FirmEmail` properties on `IdentityUser` (per parity audit Q3).
4. Configure password policy per Section 3 above.
5. Customize the ABP email confirmation template (subject + body) to match `_branding.md` Section E.
6. Build `angular/src/app/account/verify-email/...` to handle the link-click + success/failure states above.

## 10. Strict-parity exceptions

1. **Password regex -> ABP password policy.** OLD's exact regex cannot be expressed exactly via stock ABP toggles (no "any letter" flag). NEW gets close (digit + non-alphanumeric required; case-insensitive letter; min length 8). Cite parity audit Section "Password policy reconciliation". If we want exact regex parity later, add a custom `IPasswordValidator<IdentityUser>`. **Practical gap is small** -- both accept the same simple test passwords.
2. **`VerificationCode` GUID -> ABP cryptographic token.** OLD stores the GUID in the user row; ABP issues a signed token on demand (no DB column). URL changes from `?query=:guid` to `?confirmationToken=:signed`. Visible delta: a verification link cannot be replayed forever (ABP tokens have a TTL).
3. **No "auto-resend on login attempt".** OLD's `UserAuthenticationDomain.PostLogin` lines 124-145 auto-resends verification email when an unverified user attempts login. NEW switches to an explicit "Resend verification email" link on the login page (per parity audit decision). Simpler, more standard UX.
4. **Email subject typo fixed.** OLD ships `"Your have registered successfully"` -- NEW ships `"You have registered successfully - {ClinicName}"` (per parity audit Q1 confirmation 2026-05-01). This is an OLD bug, fixed for correctness.
5. **Method name typo fixed.** OLD's `openTermaAndConidition()` -- NEW's `openTermsAndCondition()`. OLD typo, fixed for correctness.
6. **`phoneNumber` default 0** -- OLD writes literal 0 because the DB column is non-null + form omits it. NEW makes the column nullable; the field stays absent from registration. **Strict-parity exception, OLD bug, fixed for correctness.** Phone becomes a required field on first profile edit (TO CONFIRM with Adrian during impl).
7. **`btn btn-secondary` repurposing.** OLD uses Bootstrap 4's secondary class as the primary CTA fill (the theme's brand-blue color is bound to `.btn-secondary`). NEW maps to the `primary` variant of `<app-button>` per `_components.md`. Visible pixels identical (both render brand-blue + white text).
8. **Logo file `Doctor.png` -> `var(--brand-logo-url)`.** Per `_branding.md` Phase 1 implementation order item 3. Per-tenant override is one declaration in `_brand.scss`.

## 11. OLD source citations (consolidated)

```
- patientappointment-portal/src/app/components/login/verify-email/verify-email.component.html
- patientappointment-portal/src/app/components/login/verify-email/verify-email.component.ts
- patientappointment-portal/src/app/components/term-and-condition/term-and-condition.component.ts
- patientappointment-portal/src/app/components/user/users/add/user-add.component.html
- patientappointment-portal/src/app/components/user/users/add/user-add.component.ts
- patientappointment-portal/src/app/const/external-user-role-type.ts
- patientappointment-portal/src/app/enums/user-type.enum.ts
- PatientAppointment.DbEntities/Constants/RegexConstant.cs
- PatientAppointment.Domain/Core/UserAuthenticationDomain.cs
- PatientAppointment.Domain/UserModule/UserDomain.cs
```

## 12. Verification (post-implementation)

- [ ] Visit `/account/register` -- the role select shows exactly 4
      options (Patient, Adjuster, Applicant Attorney, Defense Attorney);
      no internal roles listed.
- [ ] Choose Patient -- First / Last name fields show, Firm Name hidden.
- [ ] Choose Applicant Attorney -- First / Last hidden, Firm Name shows.
- [ ] Sign Up button is disabled until form is valid (all required
      fields + matching passwords + password meets policy).
- [ ] Submit valid Patient registration -- success toast renders
      `"Your registration is successfully done, please verify your email to login."`,
      after 1000ms route to `/account/login`.
- [ ] Verification email arrives with subject
      `"You have registered successfully - {ClinicName}"` and a link to
      `/account/verify-email/:userId?confirmationToken=:token`.
- [ ] Click link -- the success state renders the "successfully verified"
      copy + a "Click Here To Login" button.
- [ ] Click an invalid link -- the failure state renders "Sorry, we
      couldn't verify your account."
- [ ] Submit duplicate email -- the validation modal renders the
      duplicate-email message; row is not persisted.
- [ ] Submit weak password -- the validation modal renders the
      password-policy message.
- [ ] Submit attorney role with empty Firm Name -- the validation modal
      renders the firm-name message.
- [ ] T&C link in the form opens the T&C modal with content from
      `branding.termsAndConditions` (per-tenant).
- [ ] After successful verification, login + land on `/home` (shell
      variant 2 per `_shell-layout.md`).
- [ ] Per-tenant theme: swap brand-primary -- the Sign Up button +
      verify-screen "Click Here To Login" button + card-footer link
      reflect the new color end-to-end.
