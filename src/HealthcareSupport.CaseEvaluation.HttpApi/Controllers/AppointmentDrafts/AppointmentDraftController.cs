using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.AppointmentDrafts;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentDrafts;

/// <summary>
/// The single HTTP surface for <see cref="IAppointmentDraftAppService"/>. The service is not auto-exposed;
/// these routes are the ones the generated Angular client already calls, so the client needs no regeneration.
/// Authorization lives on the app service.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("AppointmentDraft")]
[Route("api/app/appointment-draft")]
public class AppointmentDraftController : AbpController, IAppointmentDraftAppService
{
    private readonly IAppointmentDraftAppService _service;

    public AppointmentDraftController(IAppointmentDraftAppService service)
    {
        _service = service;
    }

    [HttpGet]
    [Route("mine")]
    public virtual Task<AppointmentDraftDto?> GetMineAsync() => _service.GetMineAsync();

    [HttpPost]
    [Route("upsert")]
    public virtual Task<AppointmentDraftDto> UpsertAsync([FromBody] UpsertAppointmentDraftInput input) =>
        _service.UpsertAsync(input);

    [HttpPost]
    [Route("discard-mine")]
    public virtual Task DiscardMineAsync() => _service.DiscardMineAsync();
}
