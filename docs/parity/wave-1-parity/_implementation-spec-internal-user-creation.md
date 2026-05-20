---
title: Implementation spec — Internal user creation (IT Admin)
date: 2026-05-15
status: ready-for-implementation
audience: implementing session (currently W:\patient-portal\replicate-old-app)
parity-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs (AddInternalUser, lines 281-312)
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\User\UsersController.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\
audit-doc: docs/parity/wave-1-parity/it-admin-user-management.md
---

# Implementation spec — Internal user creation (IT Admin → adds Clinic Staff / Staff Supervisor)

Self-contained, ready-to-implement spec for the **Internal User Creation** feature
that is currently **entirely missing** in the NEW app. This doc replaces the need to
re-read the original audit; everything you need is here. Paths assume repo root
(works in both `W:\patient-portal\main` and `W:\patient-portal\replicate-old-app`).

---

## 1. Mission

Restore OLD's `IT Admin → Users → Add` flow on the NEW ABP stack. Allows the IT
Admin role to create accounts for **Clinic Staff** and **Staff Supervisor** with
auto-generated passwords emailed to the new user. New internal users are
auto-verified (no email-confirm step required) and can log in immediately.

Out of scope (separate work, not part of this ticket):
- External user invitation (already implemented as `InviteExternalUserComponent`).
- Internal user **editing** (use ABP Identity admin UI for now).
- Internal user **deletion** (use ABP Identity admin UI for now).
- Internal user **block / unblock** (use ABP Identity `IsActive` toggle in ABP admin UI; can be added later).
- IT Admin self-creation (per OLD spec — IT Admin accounts are seeded only).

---

## 2. OLD behavior (verbatim source-cited)

**OLD code: `P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs:281-312`**

```csharp
private User AddInternalUser(User user)
{
    var randomPassword = Guid.NewGuid().ToString().Substring(0, 8);
    randomPassword = randomPassword.Substring(0, 4) + "@" + randomPassword.Substring(4, 4);
    var newPassword = this.PasswordHash.Encrypt(randomPassword);
    user.UserPassword = randomPassword;
    user.Password = newPassword.Signature;
    user.Salt = newPassword.Salt;
    user.CreatedBy = UserClaim.UserId;
    user.CreatedOn = DateTime.Now;
    user.IsVerified = true;                       // ← auto-verified, skip email confirmation
    user.EmailId = user.EmailId.ToLower();
    UserUow.RegisterNew<User>(user);
    UserUow.Commit();

    vEmailSenderViewModel vm = new vEmailSenderViewModel()
    {
        UserName = user.FirstName + " " + user.LastName,
        LoginUserName = user.EmailId,
        Password = randomPassword                  // ← plaintext password in email
    };
    string emailBody = ApplicationUtility.GetEmailTemplateFromHTML(
        EmailTemplate.AddInternalUser, vm, "");
    SendMail.SendSMTPMail(user.EmailId, "Welcome to socal", emailBody);
    return user;
}
```

**OLD password format**: first 8 hex chars of a `Guid.NewGuid()`, split into two
4-char halves with literal `@` between them. Total = **9 characters**. Example:
`a3f9@k2b5`. (OLD password regex per `RegexConstant.systemPasswordPattern`
requires digit + letter + special — the `@` satisfies special; the GUID chars
satisfy digit + letter.)

**OLD email**:
- Subject: literal string `"Welcome to socal"` (lowercase "socal" — OLD bug; in NEW use the localizable `{ClinicName}` token instead).
- Template: `EmailTemplate.AddInternalUser` (resolves to `wwwroot/EmailTemplates/Add-Internal-User.html`).
- Body variables substituted: `##UserName##`, `##LoginUserName##`, `##Password##`.

**OLD controller: `P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\User\UsersController.cs`**

`POST /api/Users` is shared between external self-signup AND IT-Admin internal-user-add. The body's `UserTypeId` discriminates:
- `UserTypeId = InternalUser (6)` → goes to `AddInternalUser` path (random password, auto-verified, welcome email).
- `UserTypeId = ExternalUser (7)` → goes to the regular signup path (password from input, verification email).

In NEW we will **not** reuse this discrimination; external signup already lives in
`ExternalSignupAppService`. Internal creation gets its own AppService.

**OLD Angular UI** (`P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\`):
- `users.module.ts` + `users.routing.ts` → lazy module at `/users`
- `list/user-list.component.ts` → grid with search, sort, paginate; shows "Add" button calling `UserViewComponent` modal with `userTypeId = InternalUser`
- `add/user-add.component.ts` → reactive form: First Name, Last Name, Email, Role (dropdown), Phone Number
- `edit/user-edit.component.ts` → ABP-equivalent already provided via ABP Identity admin
- `view/user-view.component.ts` → modal that switches between Add/Edit based on `operationTypeId`
- `delete/user-delete.component.ts` → confirmation
- `users.service.ts` → CRUD wrapping `api/Users`

---

## 3. NEW current state (what to reuse vs avoid)

### Reusable patterns

| Asset | Path | Why |
|---|---|---|
| `ExternalSignupAppService` | `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs` | Reference for how to create IdentityUser + assign role + tenant context |
| `InviteExternalUserComponent` | `angular/src/app/users/invite/invite-external-user.component.ts` | Reference for an admin invite form in NEW Angular (similar shape, different target roles) |
| `InternalUserRoleDataSeedContributor` | `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs` | Already seeds the three internal roles + their permission grants. Just extend with the new `InternalUsers.*` grants below. |
| `IdentityUserManager` / `IdentityRoleManager` | ABP built-ins | Standard ABP API surface for user creation |
| `INotificationDispatcher` | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Notifications/INotificationDispatcher.cs` | Reuse for the welcome email |
| `NotificationTemplate` entity + seeding | `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/` + `NotificationTemplateDataSeedContributor.cs` | Add a new template row keyed by code |
| `BookingSubmissionEmailHandler` | `src/HealthcareSupport.CaseEvaluation.Application/Notifications/Handlers/` | Reference for how to wire a handler to an Eto |
| Riok.Mapperly mappers | `src/HealthcareSupport.CaseEvaluation.Application/CaseEvaluationApplicationMappers.cs` | For mapping the new DTOs |

### What NOT to do

- Do NOT extend `ExternalSignupAppService` — keep concerns separate. New AppService.
- Do NOT use `ObjectMapper.Map<>` or AutoMapper. Use Riok.Mapperly.
- Do NOT use ABP's `IIdentityUserAppService.CreateAsync` directly from the controller — wrap it in our own AppService so we can enforce the random-password + auto-verified + welcome-email semantics atomically.
- Do NOT use `[RemoteService(IsEnabled = true)]` — every AppService in this project is `IsEnabled = false`; the manual controller is the route entry point.
- Do NOT ship plaintext password in the response DTO — the email is the only channel. (OLD did echo it via the user object; NEW should not.)

---

## 4. The gap (4 things missing — confirmed via grep 2026-05-15)

| What | Confirmation |
|---|---|
| No `InternalUserAppService` / no `CreateInternalUserAsync` | `grep "Internal[A-Z][a-zA-Z]*UserCreate\|InternalUserManager\|CreateInternalUserAsync\|InternalUserAppService"` returned no files in `src/` |
| No internal-user create permission | `CaseEvaluationPermissions.cs` has no `InternalUsers` static class |
| No welcome-email template handler | `NotificationTemplateConsts.cs` references `UserQuery` (for a different feature) but no `InternalUserCreated` / `AddInternalUser` template code |
| No Angular `/users` admin route | `app.routes.ts` has only `/users/invite` (external user invite); `angular/src/app/users/` directory contains only the `invite/` subfolder |

---

## 5. Implementation — file-by-file

### 5.1 Backend permissions

**Edit** `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs` — add a new static class at the end of the file (above the closing brace of `CaseEvaluationPermissions`):

```csharp
/// <summary>
/// Phase ? (2026-05-?) — IT Admin can create new internal users (Clinic Staff,
/// Staff Supervisor). External user creation is self-service via ExternalSignup;
/// this gate is internal-only. ABP Identity admin pages handle edit/delete via
/// the existing AbpIdentity.Users.* permission set. Block/unblock is a future
/// follow-up.
/// </summary>
public static class InternalUsers
{
    public const string Default = GroupName + ".InternalUsers";
    public const string Create = Default + ".Create";
    // Future: Block / Unblock children if/when the dedicated UI is built.
}
```

**Edit** `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs` — append registration after the existing permission definitions (near the end of `Define`):

```csharp
var internalUsersPermission = myGroup.AddPermission(
    CaseEvaluationPermissions.InternalUsers.Default,
    L("Permission:InternalUsers"),
    MultiTenancySides.Host);   // IT Admin is host-scoped per role seed
internalUsersPermission.AddChild(
    CaseEvaluationPermissions.InternalUsers.Create,
    L("Permission:InternalUsers.Create"));
```

**Edit** `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` — add two new keys (alphabetical):

```json
"Permission:InternalUsers": "Internal users",
"Permission:InternalUsers.Create": "Create internal user",
```

**Edit** `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs` — inside `ItAdminGrants()` (around line 240), add **before** the `UserSignatures` block:

```csharp
// 2026-05-? — IT Admin can create new internal users.
yield return Default("InternalUsers");
yield return $"{Group}.InternalUsers.Create";
```

(Note: the literal `Group + ".InternalUsers.Create"` is needed because the layered architecture prevents `Domain` from referencing `Application.Contracts`.)

### 5.2 DTOs

**Create** `src/HealthcareSupport.CaseEvaluation.Application.Contracts/InternalUsers/CreateInternalUserDto.cs`:

```csharp
using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

public class CreateInternalUserDto
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string FirstName { get; set; } = null!;

    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string LastName { get; set; } = null!;

    /// <summary>
    /// Role assignment. Must be "Clinic Staff" or "Staff Supervisor". IT Admin
    /// self-creation is rejected (parity with OLD: IT Admin accounts are
    /// seeded only, not user-creatable).
    /// </summary>
    [Required]
    public string RoleName { get; set; } = null!;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }
}
```

**Create** `src/HealthcareSupport.CaseEvaluation.Application.Contracts/InternalUsers/InternalUserCreatedDto.cs`:

```csharp
using System;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

public class InternalUserCreatedDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string RoleName { get; set; } = null!;
    public bool WelcomeEmailQueued { get; set; }
}
```

**Note**: do NOT include the generated password in this DTO. Plaintext-password
must only flow through the email channel.

**Create** `src/HealthcareSupport.CaseEvaluation.Application.Contracts/InternalUsers/IInternalUsersAppService.cs`:

```csharp
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

public interface IInternalUsersAppService : IApplicationService
{
    Task<InternalUserCreatedDto> CreateAsync(CreateInternalUserDto input);
}
```

### 5.3 AppService implementation

**Create** `src/HealthcareSupport.CaseEvaluation.Application/InternalUsers/InternalUsersAppService.cs`:

```csharp
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

[Authorize(CaseEvaluationPermissions.InternalUsers.Default)]
[Volo.Abp.RemoteService(IsEnabled = false)]
public class InternalUsersAppService : CaseEvaluationAppService, IInternalUsersAppService
{
    // The two roles an IT Admin is allowed to create. IT Admin self-creation
    // is rejected (OLD parity); external roles are created via ExternalSignup.
    private static readonly string[] CreatableRoleNames =
    {
        "Clinic Staff",
        "Staff Supervisor",
    };

    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly INotificationDispatcher _notificationDispatcher;

    public InternalUsersAppService(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        IRepository<IdentityUser, Guid> userRepository,
        INotificationDispatcher notificationDispatcher)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _userRepository = userRepository;
        _notificationDispatcher = notificationDispatcher;
    }

    [Authorize(CaseEvaluationPermissions.InternalUsers.Create)]
    public virtual async Task<InternalUserCreatedDto> CreateAsync(CreateInternalUserDto input)
    {
        // 1. Validate role assignment is allowed.
        if (!CreatableRoleNames.Contains(input.RoleName))
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.InternalUserInvalidRole)
                .WithData("AllowedRoles", string.Join(", ", CreatableRoleNames))
                .WithData("AttemptedRole", input.RoleName);
        }

        // 2. Confirm the role exists (it should — seeded by
        //    InternalUserRoleDataSeedContributor).
        var role = await _roleManager.FindByNameAsync(input.RoleName);
        if (role == null)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.InternalUserRoleMissing)
                .WithData("RoleName", input.RoleName);
        }

        // 3. Reject duplicate email (HIPAA-safe: do NOT echo the email in the
        //    exception message; use error code only, like ExternalSignup).
        var existing = await _userManager.FindByEmailAsync(input.Email);
        if (existing != null)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.InternalUserDuplicateEmail);
        }

        // 4. Generate parity-compatible password: {4chars}@{4chars}, where
        //    each 4-char block contains a mix that satisfies ABP's password
        //    complexity (uppercase, lowercase, digit, plus the '@' covers
        //    the non-alphanumeric requirement). The OLD used hex GUID chars
        //    which fail ABP's default uppercase rule — see Notes below.
        var generatedPassword = GenerateParityPassword();

        // 5. Create the IdentityUser. Use CurrentTenant — IT Admin host-side
        //    creates against the resolved tenant; for the seeded demo there
        //    is exactly one tenant.
        var user = new IdentityUser(
            id: GuidGenerator.Create(),
            userName: input.Email,
            email: input.Email,
            tenantId: CurrentTenant.Id);

        user.Name = input.FirstName;
        user.Surname = input.LastName;
        if (!string.IsNullOrWhiteSpace(input.PhoneNumber))
        {
            user.SetPhoneNumber(input.PhoneNumber, confirmed: false);
        }

        var createResult = await _userManager.CreateAsync(user, generatedPassword);
        if (!createResult.Succeeded)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.InternalUserCreateFailed)
                .WithData("Errors", string.Join("; ", createResult.Errors.Select(e => e.Description)));
        }

        // 6. Auto-verify (OLD parity: IsVerified = true; NEW = EmailConfirmed).
        user.SetEmailConfirmed(true);
        await _userManager.UpdateAsync(user);

        // 7. Assign role.
        var roleResult = await _userManager.AddToRoleAsync(user, input.RoleName);
        if (!roleResult.Succeeded)
        {
            // Rollback: remove the user so we don't leave an orphan.
            await _userManager.DeleteAsync(user);
            throw new BusinessException(CaseEvaluationDomainErrorCodes.InternalUserRoleAssignFailed)
                .WithData("Errors", string.Join("; ", roleResult.Errors.Select(e => e.Description)));
        }

        // 8. Send welcome email with credentials. Use the notification
        //    dispatcher (background queue) — NEVER block the response on
        //    SMTP success.
        bool welcomeQueued = await TrySendWelcomeEmailAsync(user, input, generatedPassword);

        return new InternalUserCreatedDto
        {
            UserId = user.Id,
            Email = user.Email,
            FirstName = user.Name,
            LastName = user.Surname,
            RoleName = input.RoleName,
            WelcomeEmailQueued = welcomeQueued,
        };
    }

    private static string GenerateParityPassword()
    {
        // Format: {4chars}@{4chars}, total 9 chars, OLD parity.
        // Composition guaranteed to satisfy ABP defaults (1 upper, 1 lower,
        // 1 digit, 1 non-alphanumeric):
        //   - block 1 = 1 uppercase + 1 lowercase + 2 cryptographically random
        //     alphanumeric chars
        //   - block 2 = 1 digit + 3 cryptographically random alphanumeric chars
        //   - '@' between blocks supplies the non-alphanumeric requirement
        const string lowers = "abcdefghijkmnpqrstuvwxyz";    // exclude l,o
        const string uppers = "ABCDEFGHJKLMNPQRSTUVWXYZ";    // exclude I,O
        const string digits = "23456789";                    // exclude 0,1
        const string mix = lowers + uppers + digits;

        var p1 = $"{Pick(uppers)}{Pick(lowers)}{Pick(mix)}{Pick(mix)}";
        var p2 = $"{Pick(digits)}{Pick(mix)}{Pick(mix)}{Pick(mix)}";
        return $"{p1}@{p2}";
    }

    private static char Pick(string source)
    {
        var idx = RandomNumberGenerator.GetInt32(source.Length);
        return source[idx];
    }

    private async Task<bool> TrySendWelcomeEmailAsync(
        IdentityUser user,
        CreateInternalUserDto input,
        string generatedPassword)
    {
        try
        {
            await _notificationDispatcher.DispatchAsync(new NotificationRequest
            {
                TemplateCode = NotificationTemplateConsts.InternalUserCreated,
                Recipient = new NotificationRecipient { EmailAddress = user.Email },
                Variables = new Dictionary<string, string?>
                {
                    ["UserName"] = $"{input.FirstName} {input.LastName}",
                    ["LoginUserName"] = user.Email,
                    ["Password"] = generatedPassword,
                    ["RoleName"] = input.RoleName,
                },
            });
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Internal user {UserId} created but welcome email failed to queue. " +
                "IT Admin must reset password manually.", user.Id);
            return false;
        }
    }
}
```

**Note on `NotificationRequest` / `INotificationDispatcher`**: this matches the
existing dispatcher signature used by other handlers in
`src/HealthcareSupport.CaseEvaluation.Application/Notifications/`. If the exact
property names differ from what you find there, adjust to match. The point is:
**queue the email via the existing dispatcher; do not call `IEmailSender` directly**.

### 5.4 Error codes

**Edit** `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs` — add four new constants:

```csharp
public const string InternalUserInvalidRole = "CaseEvaluation:InternalUser:InvalidRole";
public const string InternalUserRoleMissing = "CaseEvaluation:InternalUser:RoleMissing";
public const string InternalUserDuplicateEmail = "CaseEvaluation:InternalUser:DuplicateEmail";
public const string InternalUserCreateFailed = "CaseEvaluation:InternalUser:CreateFailed";
public const string InternalUserRoleAssignFailed = "CaseEvaluation:InternalUser:RoleAssignFailed";
```

**Edit** `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` — extend the existing `AbpExceptionHttpStatusCodeOptions` block (where `ExternalSignupDuplicateEmail` is mapped):

```csharp
options.Map(CaseEvaluationDomainErrorCodes.InternalUserInvalidRole, HttpStatusCode.BadRequest);
options.Map(CaseEvaluationDomainErrorCodes.InternalUserRoleMissing, HttpStatusCode.BadRequest);
options.Map(CaseEvaluationDomainErrorCodes.InternalUserDuplicateEmail, HttpStatusCode.BadRequest);
options.Map(CaseEvaluationDomainErrorCodes.InternalUserCreateFailed, HttpStatusCode.BadRequest);
options.Map(CaseEvaluationDomainErrorCodes.InternalUserRoleAssignFailed, HttpStatusCode.BadRequest);
```

This avoids the BUG-003 / BUG-023 / BUG-024 pattern where validation errors
return 403 instead of 400.

**Localization for the error codes** — add to `en.json`:

```json
"CaseEvaluation:InternalUser:InvalidRole": "Role '{AttemptedRole}' is not allowed. Allowed roles: {AllowedRoles}.",
"CaseEvaluation:InternalUser:RoleMissing": "Role '{RoleName}' does not exist. Run the data seed.",
"CaseEvaluation:InternalUser:DuplicateEmail": "An account with this email already exists.",
"CaseEvaluation:InternalUser:CreateFailed": "Could not create user: {Errors}",
"CaseEvaluation:InternalUser:RoleAssignFailed": "Could not assign role: {Errors}",
```

### 5.5 Notification template (welcome email)

**Edit** `src/HealthcareSupport.CaseEvaluation.Domain.Shared/NotificationTemplates/NotificationTemplateConsts.cs` — add the new template code constant:

```csharp
public const string InternalUserCreated = "Notification:InternalUserCreated";
```

**Edit** `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/NotificationTemplateDataSeedContributor.cs` — add a new seed row (follow the pattern of existing rows). The body should mirror OLD's `Add-Internal-User.html` token set. Suggested:

```csharp
new SeedTemplate(
    code: NotificationTemplateConsts.InternalUserCreated,
    subject: "Welcome to {ClinicName}",
    bodyHtml: """
        <p>Hello {UserName},</p>
        <p>An account has been created for you on the {ClinicName} Patient Portal.</p>
        <p>Your login credentials:</p>
        <ul>
          <li><strong>Email:</strong> {LoginUserName}</li>
          <li><strong>Temporary password:</strong> {Password}</li>
          <li><strong>Role:</strong> {RoleName}</li>
        </ul>
        <p>Please log in at <a href="{PortalUrl}">{PortalUrl}</a> and change
        your password from the profile page.</p>
        <p>If you did not expect this account, contact your IT administrator.</p>
        """),
```

The OLD subject was the lowercase typo `"Welcome to socal"`. Fix to
`"Welcome to {ClinicName}"` and let the branding token resolve per tenant
(matches the existing branding pattern documented in
`docs/parity/wave-1-parity/_branding.md`).

### 5.6 HttpApi controller

**Create** `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/InternalUsers/InternalUsersController.cs`:

```csharp
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.InternalUsers;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.HttpApi.Controllers.InternalUsers;

[RemoteService]
[Route("api/app/internal-users")]
public class InternalUsersController : AbpController, IInternalUsersAppService
{
    private readonly IInternalUsersAppService _service;

    public InternalUsersController(IInternalUsersAppService service)
    {
        _service = service;
    }

    [HttpPost]
    public Task<InternalUserCreatedDto> CreateAsync(CreateInternalUserDto input)
        => _service.CreateAsync(input);
}
```

### 5.7 Module registration

Both `IInternalUsersAppService` (interface) and `InternalUsersAppService` (impl)
auto-register via ABP convention (the project module
`CaseEvaluationApplicationModule` already includes
`[DependsOn(typeof(CaseEvaluationApplicationContractsModule))]`). No manual DI
registration needed unless you hit a wiring issue, in which case add to
`CaseEvaluationApplicationModule.cs` `ConfigureServices`.

### 5.8 Frontend — Angular admin UI

Create a new feature folder `angular/src/app/users/internal-users/`. Pattern matches the existing `users/invite/` folder.

**Create** `angular/src/app/users/internal-users/internal-users-routes.ts`:

```typescript
import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@abp/ng.core';
import { InternalUsersListComponent } from './components/internal-users-list.component';

export const INTERNAL_USERS_ROUTES: Routes = [
  {
    path: '',
    component: InternalUsersListComponent,
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: 'CaseEvaluation.InternalUsers' },
  },
];
```

**Edit** `angular/src/app/app.routes.ts` — add a lazy-loaded route after the existing `/users/invite` block:

```typescript
{
  path: 'users/internal',
  loadChildren: () =>
    import('./users/internal-users/internal-users-routes')
      .then(m => m.INTERNAL_USERS_ROUTES),
},
```

**Create** `angular/src/app/users/internal-users/services/internal-users.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { Observable } from 'rxjs';

export interface CreateInternalUserDto {
  email: string;
  firstName: string;
  lastName: string;
  roleName: string;
  phoneNumber?: string;
}

export interface InternalUserCreatedDto {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  roleName: string;
  welcomeEmailQueued: boolean;
}

@Injectable({ providedIn: 'root' })
export class InternalUsersService {
  apiName = 'Default';
  constructor(private rest: RestService) {}

  create(input: CreateInternalUserDto): Observable<InternalUserCreatedDto> {
    return this.rest.request<CreateInternalUserDto, InternalUserCreatedDto>(
      { method: 'POST', url: '/api/app/internal-users', body: input },
      { apiName: this.apiName },
    );
  }
}
```

(We deliberately bypass the auto-proxy because a single endpoint isn't worth running
`abp generate-proxy`; if you choose to regenerate the proxy, replace with the
generated service.)

**Create** `angular/src/app/users/internal-users/components/internal-users-list.component.ts`:

```typescript
import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup } from '@angular/forms';
import { ToasterService } from '@abp/ng.theme.shared';
import { LocalizationModule } from '@abp/ng.core';
import { InternalUsersService } from '../services/internal-users.service';

@Component({
  selector: 'app-internal-users-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, LocalizationModule],
  templateUrl: './internal-users-list.component.html',
})
export class InternalUsersListComponent {
  private fb = inject(FormBuilder);
  private service = inject(InternalUsersService);
  private toaster = inject(ToasterService);

  // Allowed roles match backend's CreatableRoleNames.
  readonly roles = ['Clinic Staff', 'Staff Supervisor'];

  form: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    firstName: ['', [Validators.required, Validators.maxLength(64)]],
    lastName: ['', [Validators.required, Validators.maxLength(64)]],
    roleName: ['', [Validators.required]],
    phoneNumber: ['', [Validators.maxLength(20)]],
  });

  submitting = false;
  lastCreated: { email: string; role: string; emailQueued: boolean } | null = null;

  submit(): void {
    if (this.form.invalid || this.submitting) { return; }
    this.submitting = true;
    this.service.create(this.form.value).subscribe({
      next: (res) => {
        this.lastCreated = {
          email: res.email,
          role: res.roleName,
          emailQueued: res.welcomeEmailQueued,
        };
        this.toaster.success(
          res.welcomeEmailQueued
            ? 'User created. Welcome email queued.'
            : 'User created. Welcome email failed to queue — reset password manually.',
        );
        this.form.reset();
        this.submitting = false;
      },
      error: (err) => {
        this.toaster.error(err?.error?.error?.message ?? 'Could not create user.');
        this.submitting = false;
      },
    });
  }
}
```

**Create** `angular/src/app/users/internal-users/components/internal-users-list.component.html`:

```html
<div class="card">
  <div class="card-header">
    <h5>{{ 'CaseEvaluation::CreateInternalUser' | abpLocalization }}</h5>
  </div>
  <div class="card-body">
    <form [formGroup]="form" (ngSubmit)="submit()" class="row g-3">
      <div class="col-md-6">
        <label class="form-label">Email</label>
        <input formControlName="email" type="email" class="form-control" autocomplete="off" />
      </div>
      <div class="col-md-6">
        <label class="form-label">Role</label>
        <select formControlName="roleName" class="form-select">
          <option value="" disabled>Select a role</option>
          <option *ngFor="let r of roles" [value]="r">{{ r }}</option>
        </select>
      </div>
      <div class="col-md-6">
        <label class="form-label">First name</label>
        <input formControlName="firstName" class="form-control" />
      </div>
      <div class="col-md-6">
        <label class="form-label">Last name</label>
        <input formControlName="lastName" class="form-control" />
      </div>
      <div class="col-md-6">
        <label class="form-label">Phone (optional)</label>
        <input formControlName="phoneNumber" class="form-control" />
      </div>
      <div class="col-12 d-flex justify-content-end">
        <button type="submit" class="btn btn-primary" [disabled]="form.invalid || submitting">
          {{ submitting ? 'Creating...' : 'Create user' }}
        </button>
      </div>
    </form>

    <div *ngIf="lastCreated" class="alert alert-success mt-3">
      Created <strong>{{ lastCreated.email }}</strong> as <strong>{{ lastCreated.role }}</strong>.
      <span *ngIf="lastCreated.emailQueued">Welcome email queued.</span>
      <span *ngIf="!lastCreated.emailQueued" class="text-danger">
        Welcome email failed to queue — reset the password manually.
      </span>
    </div>
  </div>
</div>
```

**Localization** — add to `angular/src/.../shared` or the existing localization file:

```json
"CreateInternalUser": "Create internal user"
```

(Match the project's existing localization pattern.)

### 5.9 Sidebar / nav entry

The NEW project uses LeptonX. Adding the entry to the admin sidebar is typically
done in `angular/src/app/route.provider.ts` or `app.config.ts` (whichever wires
`@volo/abp.ng.theme.lepton-x.layouts` route configs). Add the new menu item
gated on `CaseEvaluation.InternalUsers`:

```typescript
{
  identifier: 'CaseEvaluation.InternalUsers',
  name: '::Menu:InternalUsers',
  parentName: 'AbpIdentity::Menu:UserManagement',
  layout: eLayoutType.application,
  iconClass: 'fa fa-users-cog',
  order: 1,
  requiredPolicy: 'CaseEvaluation.InternalUsers.Create',
  url: '/users/internal',
},
```

Check the existing `/users/invite` registration first to confirm the pattern and
parent menu key.

---

## 6. Tests

### 6.1 Unit tests (backend)

**Create** `test/HealthcareSupport.CaseEvaluation.Application.Tests/InternalUsers/InternalUsersAppService_Tests.cs`:

Required cases (each `[Fact]`):

1. **CreateAsync_creates_user_with_clinic_staff_role**
   - Arrange: log in as IT Admin (seed identity for test or use `WithCurrentUserAsync(itAdminId)`).
   - Act: call `CreateAsync` with role `"Clinic Staff"`.
   - Assert: return `InternalUserCreatedDto` with non-empty `UserId`; `_userManager.IsInRoleAsync(user, "Clinic Staff")` is `true`; `user.EmailConfirmed` is `true`.

2. **CreateAsync_creates_user_with_staff_supervisor_role**
   - Same shape, role `"Staff Supervisor"`.

3. **CreateAsync_rejects_invalid_role**
   - Pass role `"IT Admin"` or `"Patient"` → should throw `BusinessException` with code `CaseEvaluationDomainErrorCodes.InternalUserInvalidRole`.

4. **CreateAsync_rejects_duplicate_email**
   - Pre-seed a user with the same email → should throw with code `InternalUserDuplicateEmail`.

5. **CreateAsync_rejects_invalid_email_format**
   - DTO validation should kick in via DataAnnotations; the ASP.NET model binder
     gives the 400. (Optional unit test — covered by integration test below.)

6. **CreateAsync_generates_password_matching_parity_format**
   - Mock the `IdentityUserManager` to capture the password argument; assert
     it matches `^[A-Za-z0-9]{4}@[A-Za-z0-9]{4}$` regex.

7. **CreateAsync_assigns_role_after_user_create**
   - Verify call order: `CreateAsync(user, password)` → `SetEmailConfirmed(true)` → `UpdateAsync(user)` → `AddToRoleAsync(user, roleName)`. Order matters for rollback.

8. **CreateAsync_rolls_back_user_when_role_assign_fails**
   - Mock `AddToRoleAsync` to fail → user must be deleted, exception thrown.

9. **CreateAsync_queues_welcome_email_with_credentials**
   - Mock `INotificationDispatcher`; assert one call with template code
     `Notification:InternalUserCreated` and variables containing `Password`,
     `LoginUserName`, `UserName`, `RoleName`.

10. **CreateAsync_returns_emailQueued_false_when_dispatch_throws**
    - Mock dispatcher to throw → response DTO has `WelcomeEmailQueued = false`,
      user creation still succeeds.

11. **CreateAsync_unauthorized_without_permission**
    - Log in as a user without `CaseEvaluation.InternalUsers.Create` →
      `AbpAuthorizationException` thrown.

12. **CreateAsync_returns_400_status_via_status_code_map** (integration test, see below)

### 6.2 Integration test (backend)

**Create** `test/HealthcareSupport.CaseEvaluation.HttpApi.Host.Tests/InternalUsers/InternalUsersController_Tests.cs`:

Required cases:
- **POST /api/app/internal-users returns 201 with expected DTO** when called by IT Admin.
- **POST returns 401** when called anonymously.
- **POST returns 403** when called by a Patient or Clinic Staff user.
- **POST returns 400 with error code InternalUserDuplicateEmail** when email already exists.
- **POST returns 400 with error code InternalUserInvalidRole** when role is `"Patient"`.

### 6.3 UI hardening scenario (Playwright via HARDENING-TEST-SUITE)

Add a new scenario block to `docs/runbooks/HARDENING-TEST-SUITE.md`. Suggested ID
range: `HRD-R1.9.{1..3}` (matches existing convention).

```
### HRD-R1.9.1 — IT Admin creates a Clinic Staff user
Pre: logged in as IT Admin at falkinstein.localhost:4200
Steps:
  1. Navigate to /users/internal
  2. Fill: email "clinic.test+20260515@example.test", firstName "Clinic", lastName "Test", role "Clinic Staff"
  3. Click "Create user"
Pass:
  - Toast: "User created. Welcome email queued."
  - Success alert shows the email + role
  - DB query: SELECT TOP 1 EmailConfirmed FROM AbpUsers WHERE Email = '...' → 1
  - DB query: user has UserRole linking to "Clinic Staff" role
  - Inbox: welcome email arrives with subject "Welcome to West Coast Spine Institute" + Username + Password

### HRD-R1.9.2 — IT Admin creates a Staff Supervisor user
(Same as 1, role "Staff Supervisor")

### HRD-R1.9.3 — Newly created user can log in
Pre: HRD-R1.9.1 succeeded, password captured from email
Steps:
  1. Logout
  2. Login as the new user with the emailed credentials
  3. Confirm redirect to /dashboard
  4. Confirm role-appropriate menu items visible (Appointments, Patients, etc.)

### HRD-R2.7.1 — Duplicate email returns 400 with no echo
Steps: re-submit the same email → expect 400 with generic "already exists" message; no email echo in response body.

### HRD-R2.7.2 — Role `"Patient"` rejected
Steps: post via API with role "Patient" → expect 400 with error code CaseEvaluation:InternalUser:InvalidRole.

### HRD-R2.7.3 — Non-IT-Admin denied
Steps: as Clinic Staff, POST to /api/app/internal-users → expect 403.
```

### 6.4 Smoke test (manual)

After build + deploy:

```bash
# Verify the endpoint exists
curl -s -o /dev/null -w "%{http_code}" -X POST \
  -H "Content-Type: application/json" \
  -d '{}' \
  https://api.falkinstein.localhost:44327/api/app/internal-users
# Expect 401 (no auth) — confirms route is wired

# Authenticated request (replace <TOKEN> with an IT Admin JWT)
curl -X POST \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <TOKEN>" \
  -d '{"email":"smoke.test@example.test","firstName":"Smoke","lastName":"Test","roleName":"Clinic Staff"}' \
  https://api.falkinstein.localhost:44327/api/app/internal-users
# Expect 200 + DTO
```

---

## 7. Acceptance criteria

A reviewer should be able to verify each in 30 seconds:

- [ ] `CaseEvaluationPermissions.InternalUsers.{Default, Create}` constants exist and are registered in the permission provider.
- [ ] IT Admin role data seed grants both permissions.
- [ ] `POST /api/app/internal-users` exists and is gated on `CaseEvaluationPermissions.InternalUsers.Create`.
- [ ] Endpoint creates an `IdentityUser` with `EmailConfirmed = true` and the requested role assigned.
- [ ] Password emailed matches regex `^[A-Za-z0-9]{4}@[A-Za-z0-9]{4}$` and satisfies ABP default complexity (1 upper / 1 lower / 1 digit / 1 non-alphanumeric).
- [ ] Password is **never** returned in the response body.
- [ ] Welcome email template seeded; dispatch goes through `INotificationDispatcher`.
- [ ] Duplicate email returns HTTP 400 with code `CaseEvaluation:InternalUser:DuplicateEmail` (no email echoed).
- [ ] Role outside the allowed set (`Clinic Staff` / `Staff Supervisor`) returns HTTP 400 with code `CaseEvaluation:InternalUser:InvalidRole`.
- [ ] Non-IT-Admin caller receives HTTP 403.
- [ ] Angular route `/users/internal` exists, gated on `CaseEvaluation.InternalUsers`.
- [ ] Sidebar menu item appears for IT Admin and is hidden for other roles.
- [ ] New Clinic Staff user can log in with emailed credentials and lands on `/dashboard`.
- [ ] All 12 unit tests + 5 integration tests pass.
- [ ] Playwright `HRD-R1.9.{1,2,3}` and `HRD-R2.7.{1,2,3}` pass.

---

## 8. Verification procedure (Docker — both worktrees)

```bash
# 1. Build + restart relevant containers
docker compose -f docker-compose.yml -f docker-compose.testing.yml build api authserver angular
docker compose -f docker-compose.yml -f docker-compose.testing.yml up -d
docker compose ps  # all should be healthy

# 2. Run backend tests
docker exec main-api-1 dotnet test \
  /workspace/test/HealthcareSupport.CaseEvaluation.Application.Tests \
  --filter "FullyQualifiedName~InternalUsers"

docker exec main-api-1 dotnet test \
  /workspace/test/HealthcareSupport.CaseEvaluation.HttpApi.Host.Tests \
  --filter "FullyQualifiedName~InternalUsers"

# 3. Re-seed the demo data (picks up new permission grants)
docker exec main-api-1 dotnet run --project /workspace/src/HealthcareSupport.CaseEvaluation.DbMigrator

# 4. Smoke test via Playwright (HRD-R1.9 scenarios)
# Follow docs/runbooks/HARDENING-TEST-SUITE.md "Start prompt"
```

If the implementing worktree is `W:\patient-portal\replicate-old-app`, replace
container names (`main-*` → `replicate-old-app-*`) accordingly.

---

## 9. Decisions already made (do not re-litigate)

| Decision | Rationale |
|---|---|
| Plaintext password in welcome email | OLD parity. Documented as accepted security debt per audit `it-admin-user-management.md`. Document explicitly in the AppService XML doc comment. |
| Fix subject typo `"socal"` → `"{ClinicName}"` | The OLD subject was a literal-string bug; NEW substitutes the branding token. Matches the branding migration plan in `_branding.md`. |
| Only two creatable roles (`Clinic Staff`, `Staff Supervisor`) | OLD spec lines 551-553: IT Admin accounts are seeded, not user-creatable. Doctor role removed per Phase 0 cleanup task. |
| Random password format `{4chars}@{4chars}` (9 chars) | OLD parity for visual consistency. Composition guaranteed to satisfy ABP's default complexity rules. |
| No edit / delete endpoint | ABP Identity admin UI at `/identity/users` handles these. Block/unblock follow-up if needed. |
| Auto-verify (skip email confirmation) | OLD `IsVerified = true` parity; matches NEW `EmailConfirmed = true`. |
| Welcome email via `INotificationDispatcher`, not direct `IEmailSender` | Consistent with all other notification flows in NEW. Failure to queue does not roll back the user creation. |

---

## 10. Open questions / decisions needed from Adrian (flag if blocking)

1. **Sidebar menu placement** — under "User Management" (ABP Identity submenu) or as a top-level "Internal Users" entry? Pick whichever matches the existing site information architecture.
2. **Tenant scope on creation** — IT Admin is host-scoped. The new user should be created in *which* tenant context? For Phase 1 (single demo tenant), use `CurrentTenant.Id` (the resolved tenant from the request). Phase 2 will need a tenant-picker.
3. **First-login password reset enforcement** — OLD did NOT require change-on-first-login; NEW could add this via ABP's `ShouldChangePasswordOnNextLogin` flag. If yes, set it on `CreateAsync` after `UpdateAsync`. If not, document as a follow-up.

If any of these block implementation, surface them and proceed with the choice
flagged here; do not stall.

---

## 11. Files to be created / edited (summary)

**New files** (10):
1. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/InternalUsers/CreateInternalUserDto.cs`
2. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/InternalUsers/InternalUserCreatedDto.cs`
3. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/InternalUsers/IInternalUsersAppService.cs`
4. `src/HealthcareSupport.CaseEvaluation.Application/InternalUsers/InternalUsersAppService.cs`
5. `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/InternalUsers/InternalUsersController.cs`
6. `test/HealthcareSupport.CaseEvaluation.Application.Tests/InternalUsers/InternalUsersAppService_Tests.cs`
7. `test/HealthcareSupport.CaseEvaluation.HttpApi.Host.Tests/InternalUsers/InternalUsersController_Tests.cs`
8. `angular/src/app/users/internal-users/internal-users-routes.ts`
9. `angular/src/app/users/internal-users/services/internal-users.service.ts`
10. `angular/src/app/users/internal-users/components/internal-users-list.component.ts` (+ .html)

**Edited files** (7):
1. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissions.cs` — add `InternalUsers` static class
2. `src/HealthcareSupport.CaseEvaluation.Application.Contracts/Permissions/CaseEvaluationPermissionDefinitionProvider.cs` — register permissions
3. `src/HealthcareSupport.CaseEvaluation.Domain/Identity/InternalUserRoleDataSeedContributor.cs` — grant to IT Admin
4. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/CaseEvaluationDomainErrorCodes.cs` — 5 new error code constants
5. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/NotificationTemplates/NotificationTemplateConsts.cs` — add `InternalUserCreated` code
6. `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/NotificationTemplateDataSeedContributor.cs` — seed welcome-email template
7. `src/HealthcareSupport.CaseEvaluation.HttpApi.Host/CaseEvaluationHttpApiHostModule.cs` — map 5 new error codes → HTTP 400
8. `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Localization/CaseEvaluation/en.json` — permission display names + error messages
9. `angular/src/app/app.routes.ts` — add lazy `/users/internal` route
10. `angular/src/app/route.provider.ts` (or sidebar menu registration file) — add menu entry
11. `docs/runbooks/HARDENING-TEST-SUITE.md` — add HRD-R1.9.{1,2,3} + HRD-R2.7.{1,2,3} scenarios

---

## 12. Commit + PR plan

Suggested commit cadence (per `commit-format.md`):

1. `feat(internal-users): add permissions + error codes`
2. `feat(internal-users): add CreateInternalUserDto + IInternalUsersAppService`
3. `feat(internal-users): add InternalUsersAppService with auto-password generation`
4. `feat(internal-users): wire welcome email template + dispatcher`
5. `feat(internal-users): add HTTP API controller + 400 status mapping`
6. `feat(internal-users): add Angular admin UI at /users/internal`
7. `test(internal-users): unit + integration tests`
8. `docs(hardening): add HRD-R1.9 scenarios for internal-user creation`

PR title: `feat(users): IT Admin internal-user creation (OLD parity)`

PR body sections (per `pr-format.md`): Summary, Motivation, Changes (grouped by
file), Test Plan, Risk/Rollback, Screenshots (N/A — admin UI; screenshot the
form for proof), Dependencies (none new), Breaking change (None), HIPAA/PHI
Impact (no patient data; the emailed password is internal-user credentials,
log redaction is on), Closes (no GH issue; reference this spec).

---

End of spec. The implementing session should be able to execute this end-to-end
in one focused work block without re-investigating OLD code.
