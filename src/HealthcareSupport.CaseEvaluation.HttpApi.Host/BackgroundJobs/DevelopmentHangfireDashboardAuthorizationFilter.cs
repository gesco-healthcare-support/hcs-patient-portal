using Hangfire.Dashboard;

namespace HealthcareSupport.CaseEvaluation.BackgroundJobs;

/// <summary>
/// Lets any request open the Hangfire dashboard. Mounted in Development ONLY, by
/// <c>CaseEvaluationHttpApiHostModule.CreateHangfireDashboardOptions</c>, so a developer's browser
/// reaches the dashboard through the local stack's published port. Hangfire's default
/// <c>LocalRequestsOnlyAuthorizationFilter</c> cannot serve that case: behind the container port
/// mapping the request does not arrive from a loopback address. Every other environment uses ABP's
/// permission filter instead; never mount this one there.
/// </summary>
public class DevelopmentHangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <summary>Allows the request; see the class summary for where this filter may be used.</summary>
    public bool Authorize(DashboardContext context) => true;
}
