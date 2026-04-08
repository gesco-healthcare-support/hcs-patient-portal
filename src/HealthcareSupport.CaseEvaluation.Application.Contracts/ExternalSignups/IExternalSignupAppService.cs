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
}
