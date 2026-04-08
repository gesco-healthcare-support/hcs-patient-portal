using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

namespace HealthcareSupport.CaseEvaluation.Controllers.ApplicantAttorneys;

[RemoteService]
[Area("app")]
[ControllerName("ApplicantAttorney")]
[Route("api/app/applicant-attorneys")]
public class ApplicantAttorneyController : AbpController, IApplicantAttorneysAppService
{
    protected IApplicantAttorneysAppService _applicantAttorneysAppService;

    public ApplicantAttorneyController(IApplicantAttorneysAppService applicantAttorneysAppService)
    {
        _applicantAttorneysAppService = applicantAttorneysAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ApplicantAttorneyWithNavigationPropertiesDto>> GetListAsync(GetApplicantAttorneysInput input)
    {
        return _applicantAttorneysAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<ApplicantAttorneyWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _applicantAttorneysAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<ApplicantAttorneyDto> GetAsync(Guid id)
    {
        return _applicantAttorneysAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("state-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        return _applicantAttorneysAppService.GetStateLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _applicantAttorneysAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<ApplicantAttorneyDto> CreateAsync(ApplicantAttorneyCreateDto input)
    {
        return _applicantAttorneysAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<ApplicantAttorneyDto> UpdateAsync(Guid id, ApplicantAttorneyUpdateDto input)
    {
        return _applicantAttorneysAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _applicantAttorneysAppService.DeleteAsync(id);
    }
}