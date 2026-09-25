using HealthcareSupport.CaseEvaluation.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

public interface IClaimExaminersAppService : IApplicationService
{
    Task<PagedResultDto<ClaimExaminerWithNavigationPropertiesDto>> GetListAsync(GetClaimExaminersInput input);
    Task<ClaimExaminerWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<ClaimExaminerDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<ClaimExaminerDto> CreateAsync(ClaimExaminerCreateDto input);
    Task<ClaimExaminerDto> UpdateAsync(Guid id, ClaimExaminerUpdateDto input);
}
