using System;
using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.HostOperators;

/// <summary>
/// The single HTTP surface for <see cref="IIntakeAssignmentsAppService"/>. The service is not auto-exposed;
/// these are the routes the generated Angular client already calls. Authorization lives on the app service,
/// per method.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("IntakeAssignments")]
[Route("api/app/intake-assignments")]
public class IntakeAssignmentsController : AbpController, IIntakeAssignmentsAppService
{
    private readonly IIntakeAssignmentsAppService _service;

    public IntakeAssignmentsController(IIntakeAssignmentsAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public virtual Task<ListResultDto<IntakeOfficeAssignmentDto>> GetListAsync() => _service.GetListAsync();

    [HttpGet]
    [Route("paged-list")]
    public virtual Task<PagedResultDto<IntakeOfficeAssignmentDto>> GetPagedListAsync(
        [FromQuery] GetIntakeAssignmentsInput input) =>
        _service.GetPagedListAsync(input);

    [HttpPost]
    [Route("assign")]
    public virtual Task AssignAsync([FromBody] AssignIntakeOfficeDto input) => _service.AssignAsync(input);

    [HttpPost]
    [Route("unassign")]
    public virtual Task UnassignAsync([FromQuery] Guid operatorUserId, [FromQuery] Guid officeId) =>
        _service.UnassignAsync(operatorUserId, officeId);

    [HttpGet]
    [Route("assignable-operators")]
    public virtual Task<ListResultDto<LookupDto<Guid>>> GetAssignableOperatorsAsync() =>
        _service.GetAssignableOperatorsAsync();

    [HttpGet]
    [Route("office-options")]
    public virtual Task<ListResultDto<LookupDto<Guid>>> GetOfficeOptionsAsync() =>
        _service.GetOfficeOptionsAsync();

    [HttpGet]
    [Route("my-offices")]
    public virtual Task<ListResultDto<LookupDto<Guid>>> GetMyOfficesAsync() => _service.GetMyOfficesAsync();

    [HttpGet]
    [Route("my-office-metrics")]
    public virtual Task<ListResultDto<IntakeOfficeMetricsDto>> GetMyOfficeMetricsAsync() =>
        _service.GetMyOfficeMetricsAsync();

    [HttpGet]
    [Route("switchable-offices")]
    public virtual Task<ListResultDto<LookupDto<Guid>>> GetSwitchableOfficesAsync() =>
        _service.GetSwitchableOfficesAsync();

    [HttpGet]
    [Route("impersonator-info")]
    public virtual Task<ImpersonatorInfoDto> GetImpersonatorInfoAsync() => _service.GetImpersonatorInfoAsync();
}
