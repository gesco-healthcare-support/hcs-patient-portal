# Probe log: account-self-service

**Timestamp (local):** 2026-04-24T12:46:00
**Purpose:** Confirm ABP Account Module endpoints + Razor pages are wired on
AuthServer (44368) and HttpApi.Host (44327); confirm no handwritten
wrapper is needed on Angular side beyond `@volo/abp.ng.account/public`.

## Probe 1 -- HttpApi.Host swagger enumeration

### Command

```
curl -sk -o .tmp/acct-swagger.json \
  -w "HTTP_STATUS=%{http_code}\nSIZE=%{size_download}\n" \
  https://localhost:44327/swagger/v1/swagger.json
```

### Response

Status: 200
Size: 2,607,985 bytes
Total paths: 317
Account paths: 58

Selected relevant paths (redacted to the self-service surface):
```
/api/account/register                             [POST]
/api/account/send-password-reset-code             [POST]
/api/account/verify-password-reset-token          [POST]
/api/account/reset-password                       [POST]
/api/account/send-email-confirmation-token        [POST]
/api/account/verify-email-confirmation-token      [POST]
/api/account/confirm-email                        [POST]
/api/account/confirmation-state                   [GET]
/api/account/send-email-confirmation-code         [POST]
/api/account/email-confirmation-code-limit        [GET]
/api/account/my-profile                           [GET,PUT]
/api/account/my-profile/change-password           [POST]
```

Plus 46 additional account endpoints (2FA providers, external login,
user delegation, link-user, sessions, profile picture, security logs). Full
dump in `.tmp/acct-swagger.json` on the research worktree; not committed.

### Interpretation

Confirms all ABP Account Public controllers are registered on HttpApi.Host
exactly as the NuGet package ships them. Resolves 5-G13 and 5-G14 from
"ABP provides; not verified wired" to "wired".

## Probe 2 -- AuthServer Razor /Account/Login

### Command

```
curl -sk -o .tmp/acct-login.html \
  -w "HTTP_STATUS=%{http_code}\nSIZE=%{size_download}\nCTYPE=%{content_type}\n" \
  https://localhost:44368/Account/Login
```

### Response

Status: 200
Size: 17,206 bytes
Content-Type: text/html; charset=utf-8
Title: `Login | CaseEvaluation`
Form fields present (verified via `grep -Eio 'name="...'`):
`LoginInput.UserNameOrEmailAddress`, `LoginInput.Password`,
`LoginInput.RememberMe`, `__RequestVerificationToken`.

### Interpretation

AuthServer is serving the ABP Account Public Razor pages, not just the API.
LeptonX theme layout is applied. Antiforgery is active.

## Probe 3 -- AuthServer Razor /Account/ForgotPassword

### Command

```
curl -sk -o .tmp/acct-forgot.html \
  -w "HTTP_STATUS=%{http_code}\nSIZE=%{size_download}\nCTYPE=%{content_type}\n" \
  https://localhost:44368/Account/ForgotPassword
```

### Response

Status: 200
Size: 15,365 bytes
Content-Type: text/html; charset=utf-8
Title: `Forgot password? | CaseEvaluation`
Form fields: `Email`, `__RequestVerificationToken`.

### Interpretation

The Razor forgot-password page is reachable. Submitting the form POSTs to the
same page, which invokes `IAccountAppService.SendPasswordResetCodeAsync`
inside the module -- equivalent to `POST /api/account/send-password-reset-code`.

## Probe 4 -- AuthServer Razor /Account/ResetPassword (parameterless)

### Command

```
curl -sk -o .tmp/acct-rp.html \
  -w "HTTP_STATUS=%{http_code}\nSIZE=%{size_download}\nCTYPE=%{content_type}\n" \
  https://localhost:44368/Account/ResetPassword
```

### Response

Status: 500
Size: 6,878 bytes
Content-Type: text/plain; charset=utf-8
Body head:
```
Volo.Abp.Validation.AbpValidationException: ModelState is not valid!
See ValidationErrors for details.
   at Volo.Abp.AspNetCore.Mvc.Validation.ModelStateValidator.Validate(ModelStateDictionary modelState)
   at Volo.Abp.AspNetCore.Mvc.UI.RazorPages.AbpPageModel.ValidateModel()
```

### Interpretation

Expected failure: the page requires `?userId=<guid>&resetToken=<base64>&tenantId=<guid>`
supplied by the emailed link (Volo.Abp.Account email template substitutes the
`PasswordReset` `AppUrlOptions` key wired at
`CaseEvaluationAuthServerModule.cs:194`). 500 proves the page is registered
and model-bound. A real flow supplies the params via the emailed link.

## Probe 5 -- AuthServer Razor /Account/EmailConfirmation (parameterless)

### Command

```
curl -sk -o .tmp/acct-ec.html \
  -w "HTTP_STATUS=%{http_code}\nSIZE=%{size_download}\nCTYPE=%{content_type}\n" \
  https://localhost:44368/Account/EmailConfirmation
```

### Response

Status: 500
Size: 7,375 bytes
Content-Type: text/plain; charset=utf-8
Body head:
```
Volo.Abp.Domain.Entities.EntityNotFoundException: There is no such an entity.
Entity type: Volo.Abp.Identity.IdentityUser, id: 00000000-0000-0000-0000-000000000000
   at Volo.Abp.Identity.IdentityUserManager.GetByIdAsync(Guid id)
```

### Interpretation

Expected failure: the page requires `?userId=<guid>&confirmationToken=...&tenantId=<guid>`.
With empty query, `userId` defaults to `Guid.Empty`, and the module correctly
looks up that Guid in `IdentityUser` and fails. 500 proves the page is
registered and reached the identity lookup step.

## Probes deliberately skipped (state-mutating)

- `POST /api/account/send-password-reset-code` -- would write to
  `AbpAccountSecurityLogs` + update rate-limit counters. `NullEmailSender`
  suppresses delivery but persistent state still mutates. Out of scope per
  the Live Verification Protocol for a non-SEC capability.
- `POST /api/account/register` -- creates an `IdentityUser` row, triggers
  `ExternalUserRoleDataSeedContributor`-style external role assignment per
  tenant. Persistent. Skipped.
- `POST /api/account/send-email-confirmation-token` -- same rate-limit +
  security-log mutation concern. Skipped.

## Cleanup

No mutating probes executed. No cleanup required.

## Artefacts

- `.tmp/acct-swagger.json` -- 2.6 MB, 317 paths (gitignored).
- `.tmp/acct-login.html` -- 17.2 KB Razor response.
- `.tmp/acct-forgot.html` -- 15.4 KB Razor response.
- `.tmp/acct-rp.html` -- 6.9 KB error dump.
- `.tmp/acct-ec.html` -- 7.4 KB error dump.

Artifact files live only in the worktree `.tmp/` folder (not committed); this
log is the permanent record.
```

---
