---
research: stage-0-gates
date: 2026-05-04
status: ready-for-design
covers: [G1, G2, G3, G4]
---

# Stage 0 Foundation Gates -- Research

Research for four foundation tasks that block downstream parity work. Each
section: OLD source, NEW current state, implementation plan, acceptance,
gotchas. Path:line citations next to every claim.

---

## G1 -- Restore Angular toolchain (npm install)

### NEW current state

- `angular/package.json:19-49` declares 18 production dependencies
  including `@angular/* ~20.3.19`, `@abp/ng.* ~10.0.2`,
  `@volo/abp.commercial.ng.ui ~10.0.2`, `@volosoft/abp.ng.theme.lepton-x
  ~5.0.2`, `rxjs ~7.8.0`, `tslib ^2.0.0`, `zone.js ~0.15.0`.
- `package.json:50-62` `resolutions` block pins vite, rollup, tar, qs,
  etc. to security-patched ranges (npm honors via `overrides` only --
  yarn-style `resolutions` is ignored by npm; flagged below).
- `package.json:76-104` declares 22 devDependencies incl.
  `@angular/build ~20.3.24`, `@angular/cli ~20.3.24`, eslint 8.x,
  husky 9.x, prettier 3.x.
- `package-lock.json` -- **MISSING** (verified: file does not exist).
- `angular/.gitignore:12` ignores `/node_modules`. lockfile NOT
  ignored, so absence is real (not gitignore-hidden).
- Scripts (`package.json:4-17`) include `start: "ng serve"` -- must
  NEVER run (CLAUDE.md "Critical constraints"). Use
  `npx ng build --configuration development` then
  `npx serve -s dist/CaseEvaluation/browser -p 4200`.

### Stale-proxy gap

- `angular/src/app/appointments/appointment-add.component.ts:34`
  imports `LookupRequestDto` from `../proxy/shared/models`.
- `angular/src/app/proxy/shared/models.ts:22` defines:
  `export interface LookupRequestDto extends PagedResultRequestDto`
  -- which DOES inherit `skipCount` + `maxResultCount` from ABP's
  `PagedResultRequestDto`. So the import path is fine; the reported
  "missing skipCount/maxResultCount" is a TypeScript-resolution
  failure caused by `node_modules` absence, not a true proxy gap.
  Once `npm install` runs and `@abp/ng.core` resolves, the inherited
  fields will be visible. **No proxy regen needed for G1.**
- The component DOES inline a `AppointmentTypeFieldConfigDto` type at
  `appointment-add.component.ts:46-54` "until the auto-generated proxy
  is regenerated" -- separate concern, tracked under W2-5. Not part of G1.

### Implementation plan

| Step | Command / file | Notes |
|------|----------------|-------|
| 1 | `cd angular && npm install` | Generates `package-lock.json`; expect peer-dep warnings on @ngx-validate/core. |
| 2 | Commit `angular/package-lock.json` | Lockfile is required for reproducible CI builds; not in `.gitignore`. |
| 3 | Verify build: `npx ng build --configuration development` from `angular/` | Confirms all unresolved imports clear. |
| 4 | Confirm proxy still type-checks: `tsc --noEmit -p angular/tsconfig.json` | LookupRequestDto inheritance path resolves once @abp/ng.core is on disk. |
| 5 | If `@ngx-validate/core` missing in `package.json`, add explicit dep | `appointment-add.component.ts:14` imports it -- confirm it ships transitively via @volo/abp.commercial.ng.ui or add it. |

### Proxy regen command (for future use, NOT this gate)

- `cd angular && abp generate-proxy -t ng` per
  `docs/frontend/PROXY-SERVICES.md:200`.
- Config: `angular/src/app/proxy/generate-proxy.json` (1.8MB,
  gitignored at `.claudeignore:42-43`).
- Requires HttpApi.Host running on port 44327.

### Acceptance criteria

- `angular/node_modules/` populated.
- `angular/package-lock.json` committed.
- `npx ng build --configuration development` exits 0.
- `appointment-add.component.ts` reports zero `LookupRequestDto`-related
  errors in `tsc --noEmit`.

### Gotchas

- **Never run `npm start` / `ng serve`** -- Vite duplicates
  `CORE_OPTIONS` InjectionToken (CLAUDE.md "Critical constraints",
  ADR-005).
- **Path length**: worktree at
  `W:\patient-portal\replicate-old-app\` is short (40 chars), node_modules
  trees up to ~180 chars stay under the 260-char Windows limit.
- **`resolutions` is yarn-syntax**: npm 11.x ignores it. If a
  vulnerability re-surfaces, migrate to `overrides` in package.json.
- Husky `prepare` script (`package.json:16`) runs after install; needs
  `cd .. && husky angular/.husky` to succeed -- verify hooks dir exists.

---

## G2 -- Merge identity branch into domain branch

### Branch state (verified via git)

3 commits on `feat/replicate-old-app-track-identity` ahead of
`feat/replicate-old-app-track-domain`:

| SHA | Phase | Subject |
|-----|-------|---------|
| `6d9ce4b` | Phase 4 | NotificationTemplates AppService + 59-code OLD parity |
| `140aae7` | Phase 6 | IT Admin CustomFields catalog AppService |
| `284fdf0` | Phase 8 | ExternalSignupAppService OLD-parity surface |

### Files added/modified per commit

**`6d9ce4b` (Phase 4)** -- NotificationTemplates surface (19 files):
- `src/.../Application.Contracts/NotificationTemplates/{GetNotificationTemplatesInput,INotificationTemplatesAppService,NotificationTemplateDto,NotificationTemplateTypeDto,NotificationTemplateUpdateDto,NotificationTemplateWithNavigationPropertiesDto}.cs`
- `src/.../Application.Contracts/Notifications/INotificationTemplateRenderer.cs`
- `src/.../Application/CaseEvaluationApplicationMappers.NotificationTemplates.cs`
- `src/.../Application/NotificationTemplates/NotificationTemplatesAppService.cs`
- `src/.../Application/Notifications/NotificationTemplateRenderer.cs`
- `src/.../Domain.Shared/NotificationTemplates/NotificationTemplateConsts.cs` **(EXPANDED 23 -> 59 codes)**
- `src/.../Domain/NotificationTemplates/{INotificationTemplateRepository,NotificationTemplateDataSeedContributor,NotificationTemplateWithNavigationProperties}.cs`
- `src/.../EntityFrameworkCore/NotificationTemplates/EfCoreNotificationTemplateRepository.cs`
- `src/.../HttpApi/Controllers/NotificationTemplates/NotificationTemplatesController.cs`
- `test/.../Application.Tests/NotificationTemplates/{NotificationTemplatesAppServiceTests,NotificationTemplatesValidatorUnitTests}.cs`
- `test/.../EntityFrameworkCore.Tests/.../EfCoreNotificationTemplatesAppServiceTests.cs`

**`140aae7` (Phase 6)** -- CustomFields (23 files, 7294 insertions):
- Domain.Shared: `Enums/CustomFieldType.cs`, `CustomFields/CustomFieldConsts.cs`,
  `CaseEvaluationDomainErrorCodes.cs` (extended), `Localization/.../en.json` (+7 keys).
- Domain: `CustomFields/{CustomField,CustomFieldValue,ICustomFieldRepository}.cs`.
- EntityFrameworkCore: `CustomFields/EfCoreCustomFieldRepository.cs`,
  `CaseEvaluationDbContext.cs` (+39), `CaseEvaluationTenantDbContext.cs` (+32),
  migration `20260503230345_Phase6_Add_CustomFields.cs` + designer + snapshot.
- Application.Contracts: `CustomFields/{CustomFieldCreateDto,CustomFieldDto,CustomFieldUpdateDto,GetCustomFieldsInput,ICustomFieldsAppService}.cs`.
- Application: `CaseEvaluationApplicationMappers.CustomFields.cs`,
  `CustomFields/CustomFieldsAppService.cs`.
- HttpApi: `Controllers/CustomFields/CustomFieldsController.cs`.
- Tests: `Application.Tests/CustomFields/CustomFieldsAppServiceUnitTests.cs`.

**`284fdf0` (Phase 8)** -- ExternalSignup (5 files):
- `src/.../Application.Contracts/ExternalSignups/{ExternalUserSignUpDto,ExternalUserType}.cs`.
- `src/.../Application/ExternalSignups/ExternalSignupAppService.cs`.
- `test/.../Application.Tests/ExternalSignups/ExternalSignupValidatorUnitTests.cs`.
- `docs/parity/external-user-registration.md` (re-verification update).

### Conflict prediction

| File | Domain branch | Identity branch | Resolution |
|------|---------------|-----------------|------------|
| `src/.../Domain.Shared/NotificationTemplates/NotificationTemplateConsts.cs` | 23 invented codes (Phase 1.3 placeholder) | 59 OLD-verified codes (typo fixes) | **Take identity** -- closes parity gap, audit-blessed (commit body cites OLD source files + line numbers). |
| `src/.../Application.Contracts/ExternalSignups/ExternalUserType.cs` | 4 values (Patient, ClaimExaminer, ApplicantAttorney, DefenseAttorney) | 5 values (+Adjuster=5) | **Take identity** -- locked memory `project_role-model.md` says Adjuster IS a role, ClaimExaminer is metadata not a role; identity branch flags ClaimExaminer for cleanup. |
| `src/.../Application.Contracts/ExternalSignups/ExternalUserSignUpDto.cs` | minimal (email, password, name, userType, tenantId) | adds ConfirmPassword, FirmName, FirmEmail | **Take identity** -- closes G2/G3 audit gaps. |
| `src/.../Application/ExternalSignups/ExternalSignupAppService.cs` | basic register | adds validators, FirmName-required gates, IsExternalUser flag | **Take identity** -- adds OLD-bug-fix for DefenseAttorney FirmName check. |
| `src/.../Domain.Shared/Localization/CaseEvaluation/en.json` | base + 23 NT keys | base + 59 NT keys + 7 CustomField keys + Registration:* + Login:* keys | **Manual merge** -- additive only; take superset. |
| `src/.../Domain.Shared/CaseEvaluationDomainErrorCodes.cs` | base | +CustomField + Registration codes | **Take identity** (additive). |
| `src/.../EntityFrameworkCore/CaseEvaluationDbContext.cs` | latest entity registrations | +CustomField + CustomFieldValue | **Manual merge** -- additive entity registrations; both sides may have touched the file. |

### Implementation plan

| Step | Command | Notes |
|------|---------|-------|
| 1 | `git -C W:\patient-portal\replicate-old-app status` | Confirm clean working tree on `feat/replicate-old-app-track-domain`. |
| 2 | `git fetch origin` (skip if local-only) | Both branches local per CLAUDE.md "do NOT push". |
| 3 | `git merge --no-ff feat/replicate-old-app-track-identity` | `--no-ff` preserves merge boundary for archaeology. |
| 4 | Resolve `NotificationTemplateConsts.cs`: `git checkout --theirs` | Identity carries the 59 codes. |
| 5 | Resolve `ExternalUserType.cs`: `git checkout --theirs` | Identity has 5-role version. |
| 6 | Resolve `ExternalUserSignUpDto.cs` + `ExternalSignupAppService.cs`: `git checkout --theirs` | Identity has audit-resolved version. |
| 7 | Resolve `en.json` manually | Take union of keys; preserve order where both touched same blocks. |
| 8 | Resolve `CaseEvaluationDomainErrorCodes.cs` manually | Additive; take both sides' new codes. |
| 9 | Resolve `CaseEvaluationDbContext.cs` + `CaseEvaluationTenantDbContext.cs` manually | Additive entity config blocks; merge by hand. |
| 10 | Run `dotnet build` from worktree root | Expect 0 warnings, 0 errors. |
| 11 | Run `dotnet test test/HealthcareSupport.CaseEvaluation.Application.Tests` | Expect 69/69 pass per identity branch's commit body. |
| 12 | `git commit -m "merge: integrate identity branch (Phases 4, 6, 8)"` | Drop AI footer per `commit-format.md`. |

### Acceptance criteria

- `dotnet build` clean.
- `dotnet test test/HealthcareSupport.CaseEvaluation.Application.Tests`
  shows 69/69 passing (Phase 3+4+6+8 unit tests merged).
- `git log --oneline feat/replicate-old-app-track-domain | head -5`
  shows merge commit + the 3 ported commits.
- `NotificationTemplateConsts.Codes` exposes 59 string constants.
- `ExternalUserType` enum exposes 5 values.

### Gotchas

- **EF migration timestamps from BOTH branches must coexist**:
  identity adds `20260503230345_Phase6_Add_CustomFields.cs`. Verify
  this is the chronologically latest migration after merge; if the
  domain branch added a later one, EF will accept both since they are
  independent migrations on the same chain.
- **No identity-side EF migration on the domain side** -- if domain
  has migrations after `20260503230345`, the merge is fine. Latest
  migration on domain branch (working tree) is
  `20260504170956_Phase11f_AppointmentConfirmationNumberUniqueIndex.cs`,
  which sorts AFTER the Phase6 migration -- no migration ordering issue.
- **ABP Pro license blocker** for EFCore tests per
  `docs/handoffs/2026-05-03-test-host-license-blocker.md` -- only
  Application.Tests will run cleanly.
- **`commit-format.md` enforcement**: do NOT include any
  `Co-Authored-By: Claude` line in the merge commit (hook blocks).

---

## G3 -- Extend CustomFieldType from 3 to 7 values

### OLD source (verbatim)

`P:\PatientPortalOld\PatientAppointment.DbEntities\Enums\CustomFieldType.cs:1-13`:

```
namespace PatientAppointment.DbEntities.Enums
{
    public enum CustomFieldType
    {
        Alphanumeric = 12,
        Numeric = 13,
        Picklist = 14,
        Tickbox = 15,
        Date = 16,
        Radio = 17,
        Time = 18,
    }
}
```

### OLD UI rendering (validators per type)

`P:\PatientPortalOld\patientappointment-portal\src\app\components\appointment-request\appointments\domain\appointment.domain.ts:835-892`:

| OLD enum value | Validator(s) applied | Notes |
|----------------|----------------------|-------|
| Alphanumeric (12) | `alphaNumericValidator()`, `requiredValidator` if `isMandatory`, `Validators.maxLength(fieldLength)` if `fieldLength` set | Text input. |
| Numeric (13) | `numericValidator()`, `requiredValidator`, `maxLength` | Numeric text input (string). |
| Picklist (14) | (not validated in domain.ts; rendered as select) | Uses `MultipleValues` CSV. |
| Tickbox (15) | (not validated in domain.ts; rendered as checkbox) | Bool. |
| Date (16) | `requiredValidator` if mandatory, datepicker | ISO 8601 in storage. |
| Radio (17) | (not validated in domain.ts; rendered as radio group) | Uses `MultipleValues` CSV. |
| Time (18) | (no specific validator) | Time input. |

OLD references:
- Lookup population: `custom-field-add.component.ts:44`,
  `custom-field-edit.component.ts:44` -- via `customFieldTypeLookUps`.
- Type guard for date toggle: `custom-field-add.component.ts:97`,
  `custom-field-edit.component.ts:97` -- `if (... == CustomFieldTypeEnum.Date)`.
- Booking-form binding: `appointment-add.component.ts:111, 352` and
  `appointment-edit.component.ts:182` -- bind enum to `customeFieldsEnums`.
- Field column in OLD: `FieldTypeId` (int) per spm.CustomFields.

### NEW current state

`src/.../Domain.Shared/Enums/CustomFieldType.cs:15-20`:

```
public enum CustomFieldType
{
    Date = 1,
    Text = 2,
    Number = 3,
}
```

- 3 values; integer codes diverge from OLD.
- Strict-parity violation per CLAUDE.md "Match: entities, names, ...".

Touchpoints to update:
- Entity: `src/.../Domain/CustomFields/CustomField.cs:45` -- field
  typed as `CustomFieldType` enum.
- DTOs: `Application.Contracts/CustomFields/{CustomFieldCreateDto,CustomFieldDto,CustomFieldUpdateDto}.cs`.
- AppService validator:
  `src/.../Application/CustomFields/CustomFieldsAppService.cs:176-188`
  (`EnsureNoDuplicateLabelAndTypeAsync`) -- composite uniqueness on
  `(FieldLabel, FieldType)`; widening enum is safe (no rule depends
  on the 3-value set).
- Mapperly mapper:
  `src/.../Application/CaseEvaluationApplicationMappers.CustomFields.cs`
  -- enum-to-enum copy; auto-handles new values.
- Localization: `Domain.Shared/Localization/CaseEvaluation/en.json`
  -- needs 7 keys (CustomFieldType:Alphanumeric, ...:Numeric, etc.).
- Migration: `20260503230345_Phase6_Add_CustomFields.cs` -- column
  type is `int`. Existing migration stays valid; no DB schema change.
  But seed-data callers passing `CustomFieldType.Text` (=2 NEW) would
  now resolve to `Numeric` (=2 not used in OLD; collision-free since
  Text was invented, but any seed using the literal `2` becomes wrong).

### Implementation plan

| Step | Action | Files |
|------|--------|-------|
| 1 | Replace enum body with OLD's 7 values + verbatim ints | `Domain.Shared/Enums/CustomFieldType.cs` |
| 2 | Add 7 localization keys `CustomFieldType:Alphanumeric` ... `CustomFieldType:Time` | `Domain.Shared/Localization/CaseEvaluation/en.json` |
| 3 | Audit AppService for any literal usage | `Application/CustomFields/CustomFieldsAppService.cs` -- search for `CustomFieldType.` references; expect zero non-validator usage. |
| 4 | Audit unit tests | `test/.../Application.Tests/CustomFields/CustomFieldsAppServiceUnitTests.cs` -- update any test data that names `Text` / `Number` to OLD names. |
| 5 | No EF migration required | Column is already `int`. Re-run `dotnet ef migrations list` to confirm; expect `20260504170956` is latest. |
| 6 | Update Angular proxy after backend change | `cd angular && abp generate-proxy -t ng` -- regenerates `proxy/custom-fields/...` and `proxy/enums/custom-field-type.enum.ts`. |
| 7 | Add booking-form rendering switch (downstream task B1) | NOT in G3; tracked as B1 "Render all 7 CustomField types". |

### Acceptance criteria

- `Enums/CustomFieldType.cs` lists 7 values with int codes 12-18 in
  the same order as OLD source.
- `dotnet build` clean.
- `Application.Tests` pass.
- `en.json` exposes 7 `CustomFieldType:<Name>` keys.
- Regenerated proxy `enums/custom-field-type.enum.ts` mirrors the
  7 values + `customFieldTypeOptions` lookup array.

### Gotchas

- **DO NOT** rename OLD's `Alphanumeric` to `Text` even though
  it functions as text -- strict parity is on names.
- **Existing data risk**: if any seed / test row already wrote the
  value `1` (NEW Date) to DB, after the change `1` is invalid (no
  enum member exists for `1`). Phase 6 migration only runs against
  empty `AppCustomFields` table; CI is safe. Local dev DBs need
  truncation: `DELETE FROM AppCustomFields; DELETE FROM AppCustomFieldValues;`
  before next `dotnet run`.
- **DisplayOrder auto-assignment** stays unchanged
  (`CustomFieldsAppService.cs:204-207`).
- Cap rule `>= 10` per type stays (`CustomFieldsAppService.cs:215-218`).

---

## G4 -- AuthServer Razor login override + email-verify gate + localization

### OLD source -- 4 verbatim error strings

`P:\PatientPortalOld\PatientAppointment.Domain\Core\UserAuthenticationDomain.cs`:

| Line | OLD literal | Trigger |
|------|-------------|---------|
| 73, 152 | `"User not exist"` (`ValidationFailedCode.UserNotExist`) | User not found by email OR `StatusId == Status.Delete`. |
| 120 | `"Invalid username or password"` (`ValidationFailedCode.InvalidUserNamePassword`) | Verified user, password mismatch. |
| 65 | `"Your account is not activated"` (`ValidationFailedCode.UserInactivated`) | `StatusId == InActive` or `IsActive == false`. |
| 143 | `"We have sent a verification link to your registered email id, please verify your email address to login."` (hardcoded) | `IsVerified == false`; OLD also auto-resends the verification email at this point (line 126-139). |

### OLD resend-verification UX (frontend)

- `P:\PatientPortalOld\patientappointment-portal\src\app\components\login\login\` -- login form.
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\login\verify-email\` -- verify-email landing page consumes
  `userId + ?query={verificationCode}` from email link.
- `P:\PatientPortalOld\patientappointment-portal\src\app\components\login\login.service.ts` -- POST `/api/UserAuthentication/login`.
- OLD does NOT expose a manual "resend" button on the login form;
  resend is automatic on every failed login while `IsVerified=false`.

### NEW current state

`src/.../AuthServer/Pages/`:
- `_ViewImports.cshtml`, `Index.cshtml`, `Index.cshtml.cs` -- ONLY 3
  files. **No `Account/Login.cshtml` override exists.**

`src/.../Application/ExternalSignups/LoginErrorMapper.cs:47-50` -- 4
localization keys already mapped:

| Constant | Localization key |
|----------|------------------|
| `LoginErrorMapper.UserNotExist` | `"Login:UserNotExist"` |
| `LoginErrorMapper.InvalidUsernameOrPassword` | `"Login:InvalidUsernameOrPassword"` |
| `LoginErrorMapper.AccountNotActivated` | `"Login:AccountNotActivated"` |
| `LoginErrorMapper.VerificationLinkSent` | `"Login:VerificationLinkSent"` |

`Domain.Shared/Localization/CaseEvaluation/en.json:449-454` -- 6 keys
**already present** (verified):

```
"Login:UserNotExist": "User not exist",
"Login:InvalidUsernameOrPassword": "Invalid username or password",
"Login:AccountNotActivated": "Your account is not activated",
"Login:VerificationLinkSent": "We have sent a verification link to your registered email id, please verify your email address to login.",
"Login:ResendConfirmationEmail": "Resend confirmation email",
"Login:ResendConfirmationEmailSent": "Confirmation email re-sent. Please check your inbox.",
```

`src/.../Domain/Settings/CaseEvaluationSettingDefinitionProvider.cs:14-54` --
defines 27 CaseEvaluation settings via `Define(...)`. **No
`IsEmailConfirmationRequiredForLogin` setting**. ABP's
`AccountOptions` setting `Abp.Account.IsEmailConfirmationRequiredForLogin`
is the canonical lever -- needs to be set globally (default `false`).

### Implementation plan

| Step | Action | Files |
|------|--------|-------|
| 1 | Set ABP setting at module init | Override `OnApplicationInitializationAsync` in `CaseEvaluationDomainModule.cs` to call `ISettingManager.SetGlobalAsync("Abp.Account.IsEmailConfirmationRequiredForLogin", "true")`. Idempotent. |
| 2 | Copy ABP Pro Account login Razor source | `cd src/HealthcareSupport.CaseEvaluation.AuthServer && abp get-source Volo.Abp.Account.Pro.Public.Web` -- downloads obfuscated module source. Copy `Pages/Account/Login.cshtml` + `Login.cshtml.cs` into AuthServer's `Pages/Account/` folder. ABP convention: matching paths under host's `Pages/` override module pages. |
| 3 | Modify `Login.cshtml.cs` `OnPostAsync` | After `SignInResult` returns failure, call `LoginErrorMapper.MapAbpErrorToLocalizationKey(error.Code)` to set `ModelState.AddModelError("", L[key])`. If `LoginErrorMapper.ShouldShowResendLink(error.Code)` is true, set `ViewData["ShowResend"] = true`. |
| 4 | Modify `Login.cshtml` Razor | Below the credential error block, render `<a asp-page-handler="ResendEmailConfirmation">@L["Login:ResendConfirmationEmail"]</a>` only when `ViewData["ShowResend"]`. |
| 5 | Add `OnPostResendEmailConfirmationAsync` page handler | Call ABP's `IAccountAppService.SendEmailConfirmationCodeAsync(new SendEmailConfirmationCodeDto { Email = Input.UserNameOrEmailAddress, AppName = "MVC", ReturnUrl = ReturnUrl })`. Show success toast `L["Login:ResendConfirmationEmailSent"]`. |
| 6 | Add the 7 missing localization keys for any runtime messages NOT yet seeded | Verify `en.json` already covers; only 6 of 7 expected -- check at runtime. |
| 7 | Wire `LoginErrorMapper` into AuthServer | Currently `internal static` in Application; AuthServer references Application via `HealthcareSupport.CaseEvaluation.AuthServer.csproj`. Verify reference; promote to `public` if AuthServer cannot see internals (no `InternalsVisibleTo` for AuthServer today). |
| 8 | Test: integration test that submits unverified-user login | Asserts response body contains `Login:VerificationLinkSent` localized text + the "Resend confirmation email" link is rendered. |

### Acceptance criteria

- `Pages/Account/Login.cshtml` + `Login.cshtml.cs` exist under
  AuthServer; ABP login UI uses them (verify by inspecting served page
  source for a marker comment).
- Login with unknown email shows `"User not exist"`.
- Login with valid email + wrong password shows
  `"Invalid username or password"`.
- Login with `IsActive=false` user shows `"Your account is not activated"`.
- Login with unverified user shows
  `"We have sent a verification link to your registered email id, please verify your email address to login."` AND
  a `"Resend confirmation email"` link.
- Clicking resend re-sends the confirmation email and surfaces
  `"Confirmation email re-sent. Please check your inbox."`.
- ABP setting `Abp.Account.IsEmailConfirmationRequiredForLogin` = `"true"`
  globally (verifiable via Settings UI under Identity Management).

### Gotchas

- **ABP Pro Razor pages are obfuscated** in the binary
  `Volo.Abp.Account.Pro.Public.Web.dll`. `abp get-source` is the
  ONLY supported way to view + override them. Requires valid ABP
  Commercial license in `appsettings.secrets.json`.
- **Login override path must be exact**: `Pages/Account/Login.cshtml`
  -- mismatched casing or directory structure means ABP serves the
  module's stock page.
- **Settings precedence**: tenant-level overrides win over global. If
  any tenant has `Abp.Account.IsEmailConfirmationRequiredForLogin=false`
  set, that tenant bypasses the gate. Acceptable for Phase 1
  (single-tenant target).
- **`LoginErrorMapper` is `internal static`**: `LoginErrorMapper.cs:45`.
  AuthServer is a separate assembly; promote to `public` OR add
  `[assembly: InternalsVisibleTo("HealthcareSupport.CaseEvaluation.AuthServer")]`
  to `Application/AssemblyInfo.cs`.
- **OLD's resend was automatic** on every failed login. NEW prefers an
  explicit user-clicked link to avoid email spam from credential-stuffing
  attempts -- documented deviation from strict parity (security >
  parity per CLAUDE.md unaudited-features protocol).
- **JWT vs cookie**: NEW uses OpenIddict cookie session via the
  AuthServer Razor flow; OLD used a custom JWT in `localStorage`.
  Out of scope for G4; tracked under R1.
- **OLD's 4-byte newline literal `"\n"` after "Your have"** typo (line 129)
  is the registration template, not the login error -- not affected here.

---

## Cross-cutting risks

- All 4 gates are pre-requisites for Phase R1+ (Angular external
  registration form). G2 must land before G3 (CustomFieldType ints
  collide with the 3-value enum that lives only on identity branch).
- G1 unblocks all Angular-side work (proxy regen, build verification).
- G4 unblocks the post-login auto-redirect work in Phase 9 (R1 down-
  stream).
