using HealthcareSupport.CaseEvaluation.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.Controllers.Appointments;

[RemoteService]
[Area("app")]
[ControllerName("Appointment")]
[Route("api/app/appointments")]
public class AppointmentController : AbpController, IAppointmentsAppService
{
    protected IAppointmentsAppService _appointmentsAppService;

    public AppointmentController(IAppointmentsAppService appointmentsAppService)
    {
        _appointmentsAppService = appointmentsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentWithNavigationPropertiesDto>> GetListAsync(GetAppointmentsInput input)
    {
        return _appointmentsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<AppointmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _appointmentsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<AppointmentDto> GetAsync(Guid id)
    {
        return _appointmentsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("patient-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetPatientLookupAsync(LookupRequestDto input)
    {
        return _appointmentsAppService.GetPatientLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _appointmentsAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpGet]
    [Route("appointment-type-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetAppointmentTypeLookupAsync(LookupRequestDto input)
    {
        return _appointmentsAppService.GetAppointmentTypeLookupAsync(input);
    }

    [HttpGet]
    [Route("location-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetLocationLookupAsync(LookupRequestDto input)
    {
        return _appointmentsAppService.GetLocationLookupAsync(input);
    }

    [HttpGet]
    [Route("doctor-availability-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetDoctorAvailabilityLookupAsync(LookupRequestDto input)
    {
        return _appointmentsAppService.GetDoctorAvailabilityLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<AppointmentDto> CreateAsync(AppointmentCreateDto input)
    {
        return _appointmentsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<AppointmentDto> UpdateAsync(Guid id, AppointmentUpdateDto input)
    {
        return _appointmentsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _appointmentsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("applicant-attorney-details-for-booking")]
    public virtual Task<ApplicantAttorneyDetailsDto?> GetApplicantAttorneyDetailsForBookingAsync([FromQuery] Guid? identityUserId = null, [FromQuery] string? email = null)
    {
        return _appointmentsAppService.GetApplicantAttorneyDetailsForBookingAsync(identityUserId, email);
    }

    [HttpGet]
    [Route("{appointmentId}/applicant-attorney")]
    public virtual Task<ApplicantAttorneyDetailsDto?> GetAppointmentApplicantAttorneyAsync(Guid appointmentId)
    {
        return _appointmentsAppService.GetAppointmentApplicantAttorneyAsync(appointmentId);
    }

    [HttpPost]
    [Route("{appointmentId}/applicant-attorney")]
    public virtual Task UpsertApplicantAttorneyForAppointmentAsync(Guid appointmentId, [FromBody] ApplicantAttorneyDetailsDto input)
    {
        return _appointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync(appointmentId, input);
    }

    [HttpGet]
    [Route("defense-attorney-details-for-booking")]
    public virtual Task<DefenseAttorneyDetailsDto?> GetDefenseAttorneyDetailsForBookingAsync([FromQuery] Guid? identityUserId = null, [FromQuery] string? email = null)
    {
        return _appointmentsAppService.GetDefenseAttorneyDetailsForBookingAsync(identityUserId, email);
    }

    [HttpGet]
    [Route("{appointmentId}/defense-attorney")]
    public virtual Task<DefenseAttorneyDetailsDto?> GetAppointmentDefenseAttorneyAsync(Guid appointmentId)
    {
        return _appointmentsAppService.GetAppointmentDefenseAttorneyAsync(appointmentId);
    }

    [HttpPost]
    [Route("{appointmentId}/defense-attorney")]
    public virtual Task UpsertDefenseAttorneyForAppointmentAsync(Guid appointmentId, [FromBody] DefenseAttorneyDetailsDto input)
    {
        return _appointmentsAppService.UpsertDefenseAttorneyForAppointmentAsync(appointmentId, input);
    }

    [HttpPost]
    [Route("{id}/approve")]
    public virtual Task<AppointmentDto> ApproveAsync(Guid id)
    {
        return _appointmentsAppService.ApproveAsync(id);
    }

    [HttpPost]
    [Route("{id}/reject")]
    public virtual Task<AppointmentDto> RejectAsync(Guid id, [FromBody] RejectAppointmentInput input)
    {
        return _appointmentsAppService.RejectAsync(id, input);
    }

    /// <summary>
    /// Phase 13 (2026-05-04) -- look up an appointment by user-facing
    /// confirmation number. Same access policy as the by-Id variant.
    /// </summary>
    [HttpGet]
    [Route("by-confirmation-number/{requestConfirmationNumber}")]
    public virtual Task<AppointmentWithNavigationPropertiesDto?> GetByConfirmationNumberAsync(string requestConfirmationNumber)
    {
        return _appointmentsAppService.GetByConfirmationNumberAsync(requestConfirmationNumber);
    }

    /// <summary>
    /// Phase 11g (2026-05-04) -- Re-Submit (OLD <c>IsReRequestForm</c>).
    /// Source confirmation number flows in the route (uppercase A##### is
    /// always URL-safe). Body carries the new appointment's intake DTO.
    /// </summary>
    [HttpPost]
    [Route("re-submit/{sourceConfirmationNumber}")]
    public virtual Task<AppointmentDto> ReSubmitAsync(string sourceConfirmationNumber, [FromBody] AppointmentCreateDto input)
    {
        return _appointmentsAppService.ReSubmitAsync(sourceConfirmationNumber, input);
    }

    /// <summary>
    /// Phase 11g (2026-05-04) -- Reval (OLD <c>IsRevolutionForm</c>).
    /// </summary>
    [HttpPost]
    [Route("create-reval/{sourceConfirmationNumber}")]
    public virtual Task<AppointmentDto> CreateRevalAsync(string sourceConfirmationNumber, [FromBody] AppointmentCreateDto input)
    {
        return _appointmentsAppService.CreateRevalAsync(sourceConfirmationNumber, input);
    }

}