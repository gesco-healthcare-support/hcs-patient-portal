using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.ExternalLogins;

/// <summary>
/// 2026-11-13 -- an office's staff supervisor lists the practice's outside logins (patients, attorneys,
/// paralegals, claim examiners) and switches one off or back on. Requires
/// <c>CaseEvaluation.UserManagement.SwitchOffExternalUser</c>. Routes are on the hand-written
/// <c>ExternalLoginsController</c>.
/// </summary>
public interface IExternalLoginsAppService : IApplicationService
{
    /// <summary>The practice's outside logins, filtered and paged. Never lists staff or administrators.</summary>
    Task<PagedResultDto<ExternalLoginDto>> GetListAsync(GetExternalLoginsInput input);

    /// <summary>Switches one outside login off or on. Idempotent.</summary>
    Task<ExternalLoginDto> SetActiveAsync(Guid id, ExternalLoginSetActiveDto input);
}
