using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

public interface IDashboardAppService : IApplicationService
{
    Task<DashboardCountersDto> GetAsync();

    /// <summary>Rich composite payload for the redesigned internal dashboard.</summary>
    Task<DashboardDto> GetDashboardAsync(DashboardRange range);
}
