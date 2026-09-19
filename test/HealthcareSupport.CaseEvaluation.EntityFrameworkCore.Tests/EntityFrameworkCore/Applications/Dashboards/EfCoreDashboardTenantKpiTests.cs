using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// EF Core runner for <c>DashboardTenantKpiTests</c>. The test body lives in the
/// Application.Tests assembly because the dashboard family reads types that are
/// internal to the Application assembly; only the module binding belongs here.
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreDashboardTenantKpiTests
    : DashboardTenantKpiTests<CaseEvaluationEntityFrameworkCoreTestModule>
{
}
