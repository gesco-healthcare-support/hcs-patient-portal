using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

namespace HealthcareSupport.CaseEvaluation.Controllers.DoctorAvailabilities;

[RemoteService]
[Area("app")]
[ControllerName("DoctorAvailability")]
[Route("api/app/doctor-availabilities")]
public class DoctorAvailabilityController : AbpController, IDoctorAvailabilitiesAppService
{
    protected IDoctorAvailabilitiesAppService _doctorAvailabilitiesAppService;

    public DoctorAvailabilityController(IDoctorAvailabilitiesAppService doctorAvailabilitiesAppService)
    {
        _doctorAvailabilitiesAppService = doctorAvailabilitiesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<DoctorAvailabilityWithNavigationPropertiesDto>> GetListAsync(GetDoctorAvailabilitiesInput input)
    {
        return _doctorAvailabilitiesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id:guid}")]
    public virtual Task<DoctorAvailabilityWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _doctorAvailabilitiesAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id:guid}")]
    public virtual Task<DoctorAvailabilityDto> GetAsync(Guid id)
    {
        return _doctorAvailabilitiesAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("location-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetLocationLookupAsync(LookupRequestDto input)
    {
        return _doctorAvailabilitiesAppService.GetLocationLookupAsync(input);
    }

    [HttpGet]
    [Route("appointment-type-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentTypeLookupAsync(LookupRequestDto input)
    {
        return _doctorAvailabilitiesAppService.GetAppointmentTypeLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<DoctorAvailabilityDto> CreateAsync(DoctorAvailabilityCreateDto input)
    {
        return _doctorAvailabilitiesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id:guid}")]
    public virtual Task<DoctorAvailabilityDto> UpdateAsync(Guid id, DoctorAvailabilityUpdateDto input)
    {
        return _doctorAvailabilitiesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id:guid}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _doctorAvailabilitiesAppService.DeleteAsync(id);
    }

    [HttpDelete]
    [Route("by-slot")]
    public virtual Task DeleteBySlotAsync([FromQuery] DoctorAvailabilityDeleteBySlotInputDto input)
    {
        return _doctorAvailabilitiesAppService.DeleteBySlotAsync(input);
    }

    [HttpDelete]
    [Route("by-date")]
    public virtual Task DeleteByDateAsync([FromQuery] DoctorAvailabilityDeleteByDateInputDto input)
    {
        return _doctorAvailabilitiesAppService.DeleteByDateAsync(input);
    }

    [HttpPost]
    [Route("preview")]
    public virtual Task<List<DoctorAvailabilitySlotsPreviewDto>> GeneratePreviewAsync(List<DoctorAvailabilityGenerateInputDto> input)
    {
        return _doctorAvailabilitiesAppService.GeneratePreviewAsync(input);
    }

    [HttpGet]
    [Route("lookup")]
    public virtual Task<List<DoctorAvailabilityDto>> GetDoctorAvailabilityLookupAsync([FromQuery] GetDoctorAvailabilityLookupInput input)
    {
        return _doctorAvailabilitiesAppService.GetDoctorAvailabilityLookupAsync(input);
    }
}