# Invitations

Tokenized, time-limited invitations issued by internal staff (IT Admin / Staff Supervisor /
Intake Staff) so an external user (Patient / Applicant Attorney / Defense Attorney / Claim
Examiner) can self-register on a specific tenant portal. The recipient receives an emailed invite
URL carrying a one-time random token; clicking it lands on the AuthServer register page with the
email + role locked, and registration atomically marks the invite as accepted.

## Key files (non-obvious placement only)

- `Invitation.cs` -- aggregate root (`FullAuditedAggregateRoot<Guid>` + `IMultiTenant`); stores
  the SHA256 hex of the token, never the raw token.
- `InvitationManager.cs` -- `IssueAsync` (returns the raw token exactly once), `ValidateAsync`
  (non-mutating lookup by hash), `AcceptAsync` (atomic, concurrency-stamp guarded).
- `Domain.Shared/Invitations/InvitationConsts.cs` -- token byte length (32 = 256-bit entropy),
  hash storage length, default TTL (7 days).
- The invite DTOs (`InvitationValidationDto`, `InviteExternalUserDto`, `InviteExternalUserResultDto`)
  live under `Application.Contracts/ExternalSignups/`, NOT here; `ExternalSignupAppService` hosts
  the endpoints and calls `AcceptAsync` during external signup.

## Entity shape

See `Invitation.cs`. Non-obvious facts: `TokenHash` is the SHA256 hex (required, UNIQUE) -- the
raw token is never persisted; `UserType` is `ExternalUserType`; `Email` is locked on the register
page; state is derived (Active = `AcceptedAt` null AND `ExpiresAt` > now; else Accepted / Expired).

## Token security model

- Raw token: 32 cryptographic random bytes -> URL-safe Base64 (~43 chars). Returned to the caller
  exactly once, embedded in the invite URL.
- Storage: only the SHA256 hex hash. A DB breach therefore cannot leak active invite URLs
  (reversing SHA256 of a 256-bit input is infeasible).
- Validation: caller hashes the URL-supplied token and looks up the row. Constant-time comparison
  is unnecessary because the hash itself is the lookup key.
- Concurrency: simultaneous accept attempts race on the aggregate root's concurrency stamp. The
  first wins; the loser sees `AbpDbConcurrencyException`, which the AppService surfaces as
  `InviteAlreadyAccepted`.

## Lifecycle

1. **Issue** (`InvitationManager.IssueAsync`): staff triggers `InviteExternalUserAsync`; manager
   generates token + hash, persists the row with `ExpiresAt = now + 7d`, returns the raw token.
2. **Send**: AppService builds the invite URL with the raw token and queues the `InviteExternalUser`
   email through the standard notification pipeline.
3. **Validate** (`InvitationManager.ValidateAsync`): the AuthServer register page hashes the
   `?token=` value and looks it up; throws `BusinessException` for `InviteNotFound`,
   `InviteExpired`, or `InviteAlreadyAccepted`.
4. **Accept** (`InvitationManager.AcceptAsync`): during external signup, after the `IdentityUser`
   is created, the AppService atomically sets `AcceptedAt` + `AcceptedByUserId`.

## Permissions

`CaseEvaluation.UserManagement.InviteExternalUser` gates the staff-facing invite form AND the
invite-management endpoints (list / resend / revoke, added 2026-06-16); granted to IT Admin /
Staff Supervisor / Intake Staff. See `CaseEvaluationPermissions.cs` +
`CaseEvaluationPermissionDefinitionProvider.cs`.

## Multi-tenancy

- `Invitation` is `IMultiTenant`; ABP's automatic filter isolates per-tenant. The register page
  resolves tenant from the subdomain before token lookup, so the recipient's URL implies the
  tenant context.
- The `AppInvitations` table is configured on BOTH `CaseEvaluationDbContext` and
  `CaseEvaluationTenantDbContext` so host and tenant migrations both create it.

## Gotchas

- The raw token leaves the server exactly once (in the invite URL); it cannot be retrieved later.
  "Resend" (2026-06-16) re-issues the SAME row in place via `InvitationManager.ResendAsync`: a fresh
  token + hash overwrite the old ones and `ExpiresAt` resets to now + 7d, so the old URL stops
  validating immediately and the list keeps one row per recipient. "Revoke" soft-deletes the row;
  `GetInvitesAsync` disables the `ISoftDelete` filter so revoked rows still surface (Status = Revoked).
- Email casing: callers must lowercase the email before `IssueAsync`. The current AppService does;
  future callers must too, or duplicate-email checks at register time will miss collisions.
- Soft-delete is enabled (inherited), so a future "revoke invite" is a soft-delete; queries filter
  it out by default.

## Related

- src/HealthcareSupport.CaseEvaluation.Application/ExternalSignups/CLAUDE.md
