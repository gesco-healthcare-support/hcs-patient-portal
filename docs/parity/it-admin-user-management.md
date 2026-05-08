---
feature: it-admin-user-management
old-source:
  - P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs (AddInternalUser, Delete)
  - P:\PatientPortalOld\PatientAppointment.Api\Controllers\Api\User\UsersController.cs
  - P:\PatientPortalOld\patientappointment-portal\src\app\components\user\users\
old-docs:
  - socal-project-overview.md (lines 551-553)
audited: 2026-05-01
status: audit-only
priority: 3
strict-parity: true
internal-user-role: ITAdmin
depends-on:
  - external-user-registration   # extends Users CRUD with internal-user creation path
required-by: []
---

# IT Admin -- User management

## Purpose

IT Admin can add and remove internal users (Clinic Staff, Staff Supervisor). IT Admin can also block (deactivate) external users. The OLD spec excludes IT Admin self-creation -- IT Admin accounts are seeded.

**Strict parity with OLD.**

## OLD behavior (binding)

### Add internal user (per `UserDomain.AddInternalUser` lines 281-312)

Triggered when `POST /api/Users` body has `UserTypeId = InternalUser`:

1. Generate random 8-char password: `{4chars}@{4chars}` (e.g., `a3F9@k2P`).
2. Hash password.
3. Insert user with `IsVerified = true` (skips email verification flow).
4. Set `CreatedBy = current IT Admin user`, `EmailId = lowercased`.
5. Send email via `EmailTemplate.AddInternalUser` template with subject `"Welcome to socal"` (lowercase "socal" preserved). Body includes username + password.

### Block / unblock external user

Per spec line 553: IT Admin can block external user's access. Likely sets `User.IsActive = false` and/or `StatusId = InActive`. (TO VERIFY in OLD `UpdateValidation`.)

### Delete user (`UserDomain.Delete`)

Soft delete via `StatusId = Status.Delete`. User records preserved for audit trail; user can no longer log in.

### Critical OLD behaviors

- **Internal users get auto-verified** (`IsVerified = true` set immediately) -- no email-verify step.
- **Random password emailed in plaintext** -- security weakness; user must change on first login (per spec line 553-557 not explicit, but typical pattern).
- **Strict parity:** match OLD's plaintext-emailed password approach. Document as known security debt.
- **No edit endpoint for users** at IT Admin level (TO VERIFY); typically email + role + active flag only.
- **Block external user:** flip `IsActive` -> external user cannot log in, but their data + appointments remain.

## OLD code map

| File | Purpose |
|------|---------|
| `PatientAppointment.Domain/UserModule/UserDomain.cs` AddInternalUser, UpdateValidation, Delete | Internal user creation + soft delete |
| `PatientAppointment.Api/Controllers/Api/User/UsersController.cs` | API: shared with external-user registration (POST /api/Users) |
| `patientappointment-portal/.../user/users/{add,edit,list,delete,view}/...` | IT Admin user management UI |
| `Models.Enums.EmailTemplate.AddInternalUser` | Welcome-email template |

## NEW current state

- ABP Commercial provides `IIdentityUserAppService` with full user CRUD at `/api/identity/users` (admin-only).
- ABP's identity admin UI in LeptonX theme handles add/edit/delete/lock-out.
- `InternalUsersDataSeedContributor.cs` already seeds internal users in dev environment (per role-model audit).

## Gap analysis (strict parity)

| Aspect | OLD | NEW | Action | Sev |
|--------|-----|-----|--------|-----|
| Add internal user (random password + email) | OLD: custom `AddInternalUser` | NEW: ABP's `IdentityUserAppService.CreateAsync` accepts password, but no auto-generation | **Add `IInternalUsersAppService.CreateInternalUserAsync(CreateInternalUserDto { Email, FirstName, LastName, RoleId })`** -- generates random `{4chars}@{4chars}` password, calls `UserManager.CreateAsync`, sets `EmailConfirmed = true`, assigns role, sends welcome email | B |
| Random password format `{4chars}@{4chars}` | OLD pattern | -- | **Strict parity:** match the format. (Could use stronger generation, but parity wins.) | I |
| Welcome email with credentials | OLD: `EmailTemplate.AddInternalUser` template | -- | **Add subscriber for `InternalUserCreatedEto`** that sends the welcome email | I |
| Auto-verified internal users | OLD `IsVerified = true` | ABP: set `EmailConfirmed = true` on create | **Set in CreateInternalUserAsync** | I |
| Block external user | OLD: flip `IsActive` | ABP: `IIdentityUserAppService.UpdateAsync` with `IsActive = false`, OR `LockoutEnabled = true` + `LockoutEnd` | **Use ABP's IsActive flag** -- maps cleanly to OLD's behavior | I |
| Permission gate | OLD: IT Admin only | -- | **`CaseEvaluation.InternalUsers.{Create, Delete, Block}`** + ABP's existing `AbpIdentity.Users.*` permissions | I |
| List internal users | OLD: standard list | NEW: ABP IdentityUser list filtered by IsExternalUser=false | **Add filter to existing ABP list endpoint** OR custom list method | I |
| Edit internal user | OLD: standard | NEW: ABP IdentityUser update | None -- use ABP's | -- |
| Soft delete | OLD: StatusId = Delete | ABP `ISoftDelete` | None | -- |
| Self-service password change | OLD: via Manage Profile (separate audit -- not in scope here) | ABP: built-in | None | -- |

## Internal dependencies surfaced

- `IsExternalUser` extension property on `IdentityUser` (already required for registration).
- Welcome email template (covered in notification-templates audit).

## Branding/theming touchpoints

- IT Admin user management UI.
- Welcome email template.

## Replication notes

### ABP wiring

- New `IInternalUsersAppService` extends ABP's `IdentityUserAppService` patterns.
- `CreateInternalUserAsync` flow:
  1. Generate `{4}{at}{4}` password via `RandomNumberGenerator`.
  2. `UserManager.CreateAsync(user, password)`.
  3. Set `IsExternalUser = false`, `EmailConfirmed = true`.
  4. `UserManager.AddToRoleAsync(user, roleName)`.
  5. Publish `InternalUserCreatedEto { UserId, Email, GeneratedPassword }`.
  6. Email handler sends welcome email.

### Things NOT to port

- Custom `IPasswordHash` -- use ABP's password hashing.
- Lowercase "socal" email subject -- replace with `{ClinicName}` token.

### Verification

1. IT Admin creates a Clinic Staff user -> success; welcome email arrives with credentials
2. New Clinic Staff user logs in with emailed password -> success, no email-verify step
3. IT Admin blocks an external user -> user can no longer log in
4. IT Admin unblocks -> user can log in again
5. IT Admin deletes a user -> soft-deleted; audit trail preserved
6. Non-IT-Admin tries -> 403
