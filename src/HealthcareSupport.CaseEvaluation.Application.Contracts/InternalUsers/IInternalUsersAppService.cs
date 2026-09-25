using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// IT-Admin-only surface for creating new internal users (Intake Staff,
/// Staff Supervisor). Mirrors OLD's <c>AddInternalUser</c> path
/// (<c>UserDomain.cs:281-312</c>) on the NEW ABP stack with three
/// material upgrades:
///
/// <list type="bullet">
///   <item>Cryptographically random password (32-byte
///         <c>RandomNumberGenerator</c> source, not 8 hex chars from a
///         <c>Guid.NewGuid()</c>).</item>
///   <item>Force change on first login (OLD did not).</item>
///   <item>Per-tenant editable email template via
///         <c>INotificationDispatcher</c> (OLD inlined HTML).</item>
/// </list>
///
/// <para>Authorize at the AppService class level via
/// <c>CaseEvaluationPermissions.InternalUsers.Default</c>; per-method
/// gates kick in via the <c>Create</c> child permission. Permission is
/// declared <c>MultiTenancySides.Host</c> because IT Admin is host-
/// scoped; the new user is placed inside the tenant carried on the
/// input DTO.</para>
/// </summary>
public interface IInternalUsersAppService : IApplicationService
{
    /// <summary>
    /// Creates a new <c>IdentityUser</c> in the target tenant with
    /// <c>EmailConfirmed = true</c> (auto-verified, OLD parity),
    /// <c>ShouldChangePasswordOnNextLogin = true</c> (force change),
    /// the requested role assigned, and a welcome email queued through
    /// <c>INotificationDispatcher</c>. Throws
    /// <c>BusinessException</c> with one of the
    /// <c>CaseEvaluationDomainErrorCodes.InternalUser*</c> codes on
    /// validation failure -- all map to HTTP 400 via the host module's
    /// status-code map.
    /// </summary>
    Task<InternalUserCreatedDto> CreateAsync(CreateInternalUserDto input);

    /// <summary>
    /// 2026-06-16 (Prompt 16, A-B3) -- sends a password-reset email to an
    /// existing internal user (admin-triggered). Generates an ABP Identity
    /// reset token, builds the tenant-aware reset URL, and dispatches the
    /// per-tenant <c>ResetPassword</c> template. Gated by
    /// <c>CaseEvaluation.InternalUsers.Edit</c>. Throws
    /// <c>InternalUserNotFound</c> when the id resolves to no user in the
    /// caller's tenant scope.
    /// </summary>
    Task SendPasswordResetEmailAsync(Guid userId);

    /// <summary>
    /// Returns the active tenants for the form's tenant-picker
    /// dropdown. Runs in host context regardless of the caller's
    /// tenant cookie (IT Admin is host-scoped). Optional case-
    /// insensitive substring filter on tenant <c>Name</c>.
    /// </summary>
    Task<ListResultDto<LookupDto<Guid>>> GetTenantOptionsAsync(string? filter = null);

    /// <summary>
    /// 2026-06-30 (QA item B) -- paged, internal-role-scoped user list for the
    /// Staff table. Runs in HOST context (internal operators are host logins) and
    /// returns only members of the three internal roles (IT Admin, Staff
    /// Supervisor, Intake Staff), with a server-side <c>Filter</c> (name / email)
    /// plus <c>Sorting</c> and offset paging. Replaces the client-side
    /// load-500-then-filter, which silently truncated past 500 identity users.
    /// </summary>
    Task<PagedResultDto<InternalUserListDto>> GetInternalUsersAsync(GetInternalUsersInput input);
}
