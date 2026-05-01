using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

public interface IDashboardAppService : IApplicationService
{
    Task<DashboardCountersDto> GetAsync();
}
