using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Shared;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// IT-Admin-only surface for creating new internal users (Clinic Staff,
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
    /// Returns the active tenants for the form's tenant-picker
    /// dropdown. Runs in host context regardless of the caller's
    /// tenant cookie (IT Admin is host-scoped). Optional case-
    /// insensitive substring filter on tenant <c>Name</c>.
    /// </summary>
    Task<ListResultDto<LookupDto<Guid>>> GetTenantOptionsAsync(string? filter = null);
}
