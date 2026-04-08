using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

public interface IApplicantAttorneysAppService : IApplicationService
{
    Task<PagedResultDto<ApplicantAttorneyWithNavigationPropertiesDto>> GetListAsync(GetApplicantAttorneysInput input);
    Task<ApplicantAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<ApplicantAttorneyDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<ApplicantAttorneyDto> CreateAsync(ApplicantAttorneyCreateDto input);
    Task<ApplicantAttorneyDto> UpdateAsync(Guid id, ApplicantAttorneyUpdateDto input);
}