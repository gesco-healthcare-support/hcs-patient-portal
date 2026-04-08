using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.Doctors;

namespace HealthcareSupport.CaseEvaluation.Controllers.Doctors;

[RemoteService]
[Area("app")]
[ControllerName("Doctor")]
[Route("api/app/doctors")]
public class DoctorController : AbpController, IDoctorsAppService
{
    protected IDoctorsAppService _doctorsAppService;

    public DoctorController(IDoctorsAppService doctorsAppService)
    {
        _doctorsAppService = doctorsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<DoctorWithNavigationPropertiesDto>> GetListAsync(GetDoctorsInput input)
    {
        return _doctorsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<DoctorWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _doctorsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<DoctorDto> GetAsync(Guid id)
    {
        return _doctorsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _doctorsAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpGet]
    [Route("tenant-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetTenantLookupAsync(LookupRequestDto input)
    {
        return _doctorsAppService.GetTenantLookupAsync(input);
    }

    [HttpGet]
    [Route("appointment-type-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentTypeLookupAsync(LookupRequestDto input)
    {
        return _doctorsAppService.GetAppointmentTypeLookupAsync(input);
    }

    [HttpGet]
    [Route("location-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetLocationLookupAsync(LookupRequestDto input)
    {
        return _doctorsAppService.GetLocationLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<DoctorDto> CreateAsync(DoctorCreateDto input)
    {
        return _doctorsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<DoctorDto> UpdateAsync(Guid id, DoctorUpdateDto input)
    {
        return _doctorsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _doctorsAppService.DeleteAsync(id);
    }
}