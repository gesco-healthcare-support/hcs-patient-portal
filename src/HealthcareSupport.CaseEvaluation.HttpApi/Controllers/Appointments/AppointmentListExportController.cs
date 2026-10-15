using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Appointments;

/// <summary>2026-10-14 -- records an internal appointment list export in the PHI access log.</summary>
[RemoteService]
[Area("app")]
[ControllerName("AppointmentListExport")]
[Route("api/app/appointment-list-exports")]
[Authorize]
public class AppointmentListExportController : AbpController, IAppointmentListExportAppService
{
    private readonly IAppointmentListExportAppService _service;

    public AppointmentListExportController(IAppointmentListExportAppService service)
    {
        _service = service;
    }

    [HttpPost]
    [Route("csv")]
    public virtual Task RecordCsvExportAsync([FromBody] RecordAppointmentListExportInput input)
    {
        return _service.RecordCsvExportAsync(input);
    }
}
