using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.ClaimExaminers;

namespace HealthcareSupport.CaseEvaluation.Controllers.ClaimExaminers;

[RemoteService]
[Area("app")]
[ControllerName("ClaimExaminer")]
[Route("api/app/claim-examiners")]
public class ClaimExaminerController : AbpController, IClaimExaminersAppService
{
    protected IClaimExaminersAppService _claimExaminersAppService;

    public ClaimExaminerController(IClaimExaminersAppService claimExaminersAppService)
    {
        _claimExaminersAppService = claimExaminersAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ClaimExaminerWithNavigationPropertiesDto>> GetListAsync(GetClaimExaminersInput input)
    {
        return _claimExaminersAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<ClaimExaminerWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _claimExaminersAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<ClaimExaminerDto> GetAsync(Guid id)
    {
        return _claimExaminersAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("state-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        return _claimExaminersAppService.GetStateLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _claimExaminersAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<ClaimExaminerDto> CreateAsync(ClaimExaminerCreateDto input)
    {
        return _claimExaminersAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<ClaimExaminerDto> UpdateAsync(Guid id, ClaimExaminerUpdateDto input)
    {
        return _claimExaminersAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _claimExaminersAppService.DeleteAsync(id);
    }
}
