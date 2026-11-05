using System;
using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.EmailStops;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.EmailStops;

/// <summary>
/// 2026-11-05 -- manual controller forwarding to <see cref="IEmailStopsAppService"/> (the app service carries
/// <c>[RemoteService(IsEnabled = false)]</c>, so these are its only routes). Staff stop and restart emails to
/// external people, for one case or for all cases.
///
/// <para>The class-level <c>[Authorize]</c> repeats the app service's permission on purpose: this surface lists
/// every address a case emails, so it must never be reachable without the staff permission, even if the
/// service attribute is changed (and CodeQL's function-level access-control check can see it here).</para>
/// </summary>
[Authorize(CaseEvaluationPermissions.EmailStops.Default)]
[RemoteService]
[Area("app")]
[ControllerName("EmailStop")]
[Route("api/app")]
public class EmailStopsController : AbpController, IEmailStopsAppService
{
    private readonly IEmailStopsAppService _service;

    public EmailStopsController(IEmailStopsAppService service)
    {
        _service = service;
    }

    [HttpGet("appointments/{appointmentId}/email-recipients")]
    public virtual Task<AppointmentEmailRecipientsDto> GetAppointmentRecipientsAsync(Guid appointmentId)
    {
        return _service.GetAppointmentRecipientsAsync(appointmentId);
    }

    [HttpPost("appointments/{appointmentId}/email-stops")]
    public virtual Task<EmailStopStatusDto> StopForAppointmentAsync(
        Guid appointmentId,
        [FromBody] StopAppointmentEmailsInput input)
    {
        return _service.StopForAppointmentAsync(appointmentId, input);
    }

    [HttpDelete("appointments/{appointmentId}/email-stops")]
    public virtual Task ResumeForAppointmentAsync(Guid appointmentId, [FromQuery] string email)
    {
        return _service.ResumeForAppointmentAsync(appointmentId, email);
    }

    [HttpGet("appointments/{appointmentId}/email-stops/consent-impact")]
    public virtual Task<EmailStopConsentImpactDto> GetConsentImpactAsync(
        Guid appointmentId,
        [FromQuery] string email,
        [FromQuery] EmailStopScope scope)
    {
        return _service.GetConsentImpactAsync(appointmentId, email, scope);
    }

    [HttpGet("email-stops")]
    public virtual Task<PagedResultDto<GlobalEmailStopDto>> GetListAsync([FromQuery] GetGlobalEmailStopsInput input)
    {
        return _service.GetListAsync(input);
    }

    [HttpPost("email-stops")]
    public virtual Task<GlobalEmailStopDto> CreateAsync([FromBody] CreateGlobalEmailStopInput input)
    {
        return _service.CreateAsync(input);
    }

    [HttpDelete("email-stops/{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _service.DeleteAsync(id);
    }

    [HttpGet("email-stops/consent-impact")]
    public virtual Task<EmailStopConsentImpactDto> GetGlobalConsentImpactAsync([FromQuery] string email)
    {
        return _service.GetGlobalConsentImpactAsync(email);
    }
}
