using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

public interface IExternalSignupAppService : IApplicationService
{
    Task<ListResultDto<LookupDto<Guid>>> GetTenantOptionsAsync(string? filter = null);
    Task<ListResultDto<ExternalUserLookupDto>> GetExternalUserLookupAsync(string? filter = null);
    Task<ExternalUserProfileDto> GetMyProfileAsync();

    Task RegisterAsync(ExternalUserSignUpDto input);

    /// <summary>
    /// 1.6 (2026-04-30): host-context tenant lookup by name; returns null on
    /// miss. Used by the AuthServer JS overlay to resolve `?__tenant=&lt;Name&gt;`
    /// invite-link query strings to the GUID required by the registration
    /// payload.
    /// </summary>
    Task<LookupDto<Guid>?> ResolveTenantByNameAsync(string name);

    /// <summary>
    /// D.2 (2026-04-30): admin-side external-user invite. Builds a
    /// tenant-specific `/Account/Register` URL and enqueues an invite email
    /// via Hangfire. Returns the URL in the response so the admin can copy
    /// + paste it manually (the dev-stack NullEmailSender swallows email
    /// silently until ACS credentials land per S-5.7).
    /// </summary>
    Task<InviteExternalUserResultDto> InviteExternalUserAsync(InviteExternalUserDto input);
}
