using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

public interface IDefenseAttorneysAppService : IApplicationService
{
    Task<PagedResultDto<DefenseAttorneyWithNavigationPropertiesDto>> GetListAsync(GetDefenseAttorneysInput input);
    Task<DefenseAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<DefenseAttorneyDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<DefenseAttorneyDto> CreateAsync(DefenseAttorneyCreateDto input);
    Task<DefenseAttorneyDto> UpdateAsync(Guid id, DefenseAttorneyUpdateDto input);
}
