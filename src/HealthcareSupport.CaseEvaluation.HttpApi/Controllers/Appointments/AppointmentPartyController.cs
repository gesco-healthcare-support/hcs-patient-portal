using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Appointments;

/// <summary>
/// Staff change the parties on an appointment (2026-10-21). Thin passthrough; the permission and
/// the staff-only rule are enforced in <see cref="IAppointmentPartiesAppService"/>.
/// </summary>
[RemoteService]
[Area("app")]
[Authorize]
[Route("api/app/appointments/{appointmentId}/parties")]
public class AppointmentPartyController : AbpController, IAppointmentPartiesAppService
{
    private readonly IAppointmentPartiesAppService _service;

    public AppointmentPartyController(IAppointmentPartiesAppService service)
    {
        _service = service;
    }

    [HttpPut]
    public virtual Task<AppointmentDto> UpdateAsync(Guid appointmentId, [FromBody] AppointmentPartiesUpdateDto input)
        => _service.UpdateAsync(appointmentId, input);
}
