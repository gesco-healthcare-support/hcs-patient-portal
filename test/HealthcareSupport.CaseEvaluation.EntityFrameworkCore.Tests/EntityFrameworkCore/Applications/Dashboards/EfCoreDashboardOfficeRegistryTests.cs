using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// EF Core runner for <c>DashboardOfficeRegistryTests</c>. The test body lives in the
/// Application.Tests assembly because the dashboard family reads types that are
/// internal to the Application assembly; only the module binding belongs here.
/// </summary>
public class EfCoreDashboardOfficeRegistryTests
    : DashboardOfficeRegistryTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
