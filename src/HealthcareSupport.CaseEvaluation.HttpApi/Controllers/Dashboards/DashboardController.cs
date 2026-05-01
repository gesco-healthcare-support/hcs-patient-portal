using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.Dashboards;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Dashboards;

[RemoteService(Name = "Default")]
[Area("app")]
[ControllerName("Dashboard")]
[Route("api/app/dashboard")]
[ApiVersion("1.0")]
public class DashboardController : AbpController, IDashboardAppService
{
    private readonly IDashboardAppService _appService;

    public DashboardController(IDashboardAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    public Task<DashboardCountersDto> GetAsync()
    {
        return _appService.GetAsync();
    }
}
