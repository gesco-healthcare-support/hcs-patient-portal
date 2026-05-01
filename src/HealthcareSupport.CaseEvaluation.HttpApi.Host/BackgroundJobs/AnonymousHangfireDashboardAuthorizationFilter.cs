using Hangfire.Dashboard;

namespace HealthcareSupport.CaseEvaluation.BackgroundJobs;

/// <summary>
/// Allow-all dashboard auth filter for the Hangfire UI in Wave 0 dev mode. Hangfire's
/// default <c>LocalRequestsOnlyAuthorizationFilter</c> blocks remote access; we want the
/// dashboard reachable from the developer's browser without needing extra plumbing.
/// Production hardening (require ABP admin permission) lands in the deferred Wave 0
/// hardening tail per the approved plan.
/// </summary>
public class AnonymousHangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => true;
}
