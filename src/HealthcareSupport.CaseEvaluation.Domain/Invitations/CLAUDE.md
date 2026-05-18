# Invitations

Tokenized, time-limited invitations issued by internal staff (IT Admin / Staff Supervisor / Clinic Staff) so an external user (Patient / Applicant Attorney / Defense Attorney / Claim Examiner) can self-register on a specific tenant portal. The recipient receives an emailed invite URL carrying a one-time random token; clicking the URL lands on the AuthServer register page with the email + role locked, and registration atomically marks the invite as accepted.

## File Map

| Layer | File | Purpose |
|---|---|---|
| Domain.Shared | `src/HealthcareSupport.CaseEvaluation.Domain.Shared/Invitations/InvitationConsts.cs` | Token byte length (32 = 256 bits entropy), encoded max length (64), hash storage length (64 hex), default TTL (7 days), email max length (256) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Invitations/Invitation.cs` | Aggregate root, `FullAuditedAggregateRoot<Guid>` + `IMultiTenant`. State: Active (AcceptedAt null AND ExpiresAt > now) / Accepted / Expired. Stores SHA256 hex of the token, never the raw token |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Invitations/InvitationManager.cs` | Domain service. `IssueAsync` (generate + hash + persist, returns raw token once), `ValidateAsync` (non-mutating lookup by hash), `AcceptAsync` (atomic accept; concurrency-stamp guarded) |
| Domain | `src/HealthcareSupport.CaseEvaluation.Domain/Invitations/IInvitationRepository.cs` | Custom repo interface; adds `FindByTokenHashAsync(hash)`. CRUD inherited from `IRepository<Invitation, Guid>` |
| EF Core | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Invitations/EfCoreInvitationRepository.cs` | EF impl of `IInvitationRepository` |
| EF Core | `src/HealthcareSupport.CaseEvaluation.EntityFrameworkCore/Migrations/20260515183211_Added_Invitations.cs` | Migration adding the `AppInvitations` table on both `CaseEvaluationDbContext` (host + tenant) and `CaseEvaluationTenantDbContext` (tenant-only) |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/InvitationValidationDto.cs` | Result of `ValidateInvitationAsync` -- email, role, expiry, validation status |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/InviteExternalUserDto.cs` | Input for `InviteExternalUserAsync` -- email, `ExternalUserType` |
| Contracts | `src/HealthcareSupport.CaseEvaluation.Application.Contracts/ExternalSignups/InviteExternalUserResultDto.cs` | Result -- invite URL containing the raw token (only returned once) |
| Application | `src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/ExternalSignupAppService.cs` | Hosts `InviteExternalUserAsync`, `ValidateInvitationAsync`, plus the register-flow integration that calls `InvitationManager.AcceptAsync` during external signup |
| HttpApi | `src/HealthcareSupport.CaseEvaluation.HttpApi/Controllers/ExternalSignups/ExternalSignupController.cs` | Manual controller; routes invite endpoints under `api/app/external-signup` |
| Notification | `src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/InviteExternalUser.html` | Email body template; tokens substituted at send time |
| Angular | `angular/src/app/external-users/components/invite-external-user.component.{ts,html}` | Staff-facing invite form (gated by permission) |
| Angular | `angular/src/app/proxy/external-signups/*` | Auto-generated REST client + DTO shapes (do not edit; regenerate via `abp generate-proxy`) |

## Entity Shape

```
Invitation : FullAuditedAggregateRoot<Guid>, IMultiTenant
  TenantId           : Guid?
  Email              : string  [required, max 256]   recipient address; locked on register page
  UserType           : ExternalUserType                Patient | ApplicantAttorney | DefenseAttorney | ClaimExaminer
  TokenHash          : string  [required, max 64]    SHA256 hex of the raw token; UNIQUE
  ExpiresAt          : DateTime  (UTC)               IssuedAt + DefaultTtlDays
  AcceptedAt         : DateTime? (UTC)               set by AcceptAsync
  AcceptedByUserId   : Guid?                         IdentityUser id of the registered account
  InvitedByUserId    : Guid                          internal staff who issued
```

## Token security model

- Raw token: 32 cryptographic random bytes -> URL-safe Base64 (~43 chars). Returned to the caller exactly once, embedded in the invite URL.
- Storage: only the SHA256 hex hash. A DB breach therefore cannot leak active invite URLs (reversing SHA256 of a 256-bit input is infeasible).
- Validation: caller hashes the URL-supplied token and looks up the row. Constant-time comparison is unnecessary because the hash itself is the lookup key.
- Concurrency: simultaneous accept attempts race on the aggregate root's concurrency stamp. The first wins; the loser sees `AbpDbConcurrencyException`, which the AppService catches and surfaces as `InviteAlreadyAccepted`.

## Lifecycle

1. **Issue** (`InvitationManager.IssueAsync`): staff member triggers `InviteExternalUserAsync`. Manager generates token + hash, persists the row with `ExpiresAt = now + 7d`, returns raw token.
2. **Send**: AppService builds the invite URL with the raw token and queues the `InviteExternalUser` email through the standard notification pipeline.
3. **Validate** (`InvitationManager.ValidateAsync`): when the recipient lands on the AuthServer register page with `?token=<raw>`, the page hashes + looks up. Returns the entity or throws a `BusinessException` for `InviteNotFound`, `InviteExpired`, or `InviteAlreadyAccepted`.
4. **Accept** (`InvitationManager.AcceptAsync`): during external signup, after the `IdentityUser` is created, the AppService calls `AcceptAsync` to atomically set `AcceptedAt` + `AcceptedByUserId`.

## Permissions

- `CaseEvaluation.Invitations.Create` (added in PR #202) -- gates the staff-facing invite form. Granted to IT Admin / Staff Supervisor / Clinic Staff per OLD parity. See `CaseEvaluationPermissions.cs` and `CaseEvaluationPermissionDefinitionProvider.cs`.

## Multi-tenancy

- `Invitation` is `IMultiTenant`. ABP's automatic filter ensures one tenant cannot see another tenant's invites. The AuthServer register page resolves tenant from the subdomain before token lookup -- the recipient's URL therefore implies the tenant context.
- The `AppInvitations` table is added to BOTH `CaseEvaluationDbContext` (Both sides) and `CaseEvaluationTenantDbContext` (tenant side) so host migrations and tenant migrations both create it.

## Gotchas

- The raw token leaves the server exactly once -- in the invite URL. There is no "resend" that returns the same token. Resend = issue a new invitation (and the old hash row stays around as audit history; expiry handles cleanup).
- Email casing: callers should lowercase the email before passing to `IssueAsync`. The current AppService does this; future callers must too or duplicate-email checks at register time will miss collisions.
- Soft-delete is enabled (inherited from `FullAuditedAggregateRoot`) so future admin-side "revoke invite" is a soft-delete; queries already filter it out by default.
