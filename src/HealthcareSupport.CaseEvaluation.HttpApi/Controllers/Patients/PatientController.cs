using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.Patients;

namespace HealthcareSupport.CaseEvaluation.Controllers.Patients;

[RemoteService]
[Area("app")]
[ControllerName("Patient")]
[Route("api/app/patients")]
public class PatientController : AbpController, IPatientsAppService
{
    protected IPatientsAppService _patientsAppService;

    public PatientController(IPatientsAppService patientsAppService)
    {
        _patientsAppService = patientsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<PatientWithNavigationPropertiesDto>> GetListAsync(GetPatientsInput input)
    {
        return _patientsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<PatientWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _patientsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("for-appointment-booking/{id}")]
    public virtual Task<PatientWithNavigationPropertiesDto> GetPatientForAppointmentBookingAsync(Guid id)
    {
        return _patientsAppService.GetPatientForAppointmentBookingAsync(id);
    }

    [HttpGet]
    [Route("for-appointment-booking/by-email")]
    public virtual Task<PatientWithNavigationPropertiesDto?> GetPatientByEmailForAppointmentBookingAsync([FromQuery] string email)
    {
        return _patientsAppService.GetPatientByEmailForAppointmentBookingAsync(email);
    }

    [HttpPost]
    [Route("for-appointment-booking/get-or-create")]
    public virtual Task<PatientWithNavigationPropertiesDto> GetOrCreatePatientForAppointmentBookingAsync(CreatePatientForAppointmentBookingInput input)
    {
        return _patientsAppService.GetOrCreatePatientForAppointmentBookingAsync(input);
    }

    [HttpPut]
    [Route("for-appointment-booking/{id}")]
    public virtual Task<PatientDto> UpdatePatientForAppointmentBookingAsync(Guid id, PatientUpdateDto input)
    {
        return _patientsAppService.UpdatePatientForAppointmentBookingAsync(id, input);
    }

    [HttpGet]
    [Route("me")]
    public virtual Task<PatientWithNavigationPropertiesDto> GetMyProfileAsync()
    {
        return _patientsAppService.GetMyProfileAsync();
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<PatientDto> GetAsync(Guid id)
    {
        return _patientsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("state-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetStateLookupAsync(LookupRequestDto input)
    {
        return _patientsAppService.GetStateLookupAsync(input);
    }

    [HttpGet]
    [Route("appointment-language-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentLanguageLookupAsync(LookupRequestDto input)
    {
        return _patientsAppService.GetAppointmentLanguageLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _patientsAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpGet]
    [Route("tenant-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetTenantLookupAsync(LookupRequestDto input)
    {
        return _patientsAppService.GetTenantLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<PatientDto> CreateAsync(PatientCreateDto input)
    {
        return _patientsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<PatientDto> UpdateAsync(Guid id, PatientUpdateDto input)
    {
        return _patientsAppService.UpdateAsync(id, input);
    }

    [HttpPut]
    [Route("me")]
    public virtual Task<PatientDto> UpdateMyProfileAsync(PatientUpdateDto input)
    {
        return _patientsAppService.UpdateMyProfileAsync(input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _patientsAppService.DeleteAsync(id);
    }
}