using System;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Controllers;

/// <summary>
/// The Case Tracker admin services have exactly one HTTP surface: the hand-written controllers under
/// <c>api/app/case-tracker</c>.
/// </summary>
/// <remarks>
/// <para>
/// While these services were auto-exposed, ABP published a second route for every method beside the
/// controller's, and a guard written on the controller did not reach it. The old push toggle was the
/// case in point: the controller bound a body whose <c>Enabled</c> was <c>[JsonRequired]</c>, while the
/// auto route bound the service's <c>bool enabled</c> directly. Since 2026-10-26 an office is connected
/// or disconnected instead, and the toggle and the two feed routes are gone.
/// </para>
/// <para>
/// The admin screens call the controller routes as literal strings through <c>RestService</c>
/// (<c>admin/case-tracker-offices.component.ts</c>, <c>admin/integration-failures.component.ts</c>), so
/// each of those routes is pinned here against the values the screens send. The pass-through half is
/// covered for every action by <see cref="ControllerDelegationTests"/>.
/// </para>
/// </remarks>
public class CaseTrackerRouteSurfaceTests
{
    public static TheoryData<Type> Services() => new()
    {
        typeof(CaseTrackerPushAppService),
        typeof(CaseTrackerPushSettingsAppService),
        typeof(CaseTrackerDeadLetterAppService),
    };

    [Theory]
    [MemberData(nameof(Services))]
    public void The_service_is_not_auto_exposed(Type service)
    {
        var attribute = service.GetCustomAttribute<RemoteServiceAttribute>(inherit: false);

        attribute.ShouldNotBeNull("without it ABP publishes a second, auto-generated route for every public method");
        attribute!.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Every controller route the admin screens call, plus the per-appointment push.</summary>
    public static TheoryData<string, string> ControllerRoutes() => new()
    {
        { "GET", "api/app/case-tracker/offices" },
        { "POST", "api/app/case-tracker/offices/{officeId}/connect" },
        { "POST", "api/app/case-tracker/offices/{officeId}/disconnect" },
        { "GET", "api/app/case-tracker/dead-letters" },
        { "POST", "api/app/case-tracker/offices/{officeId}/dead-letters/{id}/retry" },
        { "POST", "api/app/case-tracker/offices/{officeId}/dead-letters/retry-all" },
        { "POST", "api/app/case-tracker/appointments/{id}/push" },
    };

    [Theory]
    [MemberData(nameof(ControllerRoutes))]
    public void Each_route_is_answered_by_exactly_one_action(string verb, string path)
    {
        AttributeRouteTable.Matching(verb, path)
            .ShouldHaveSingleItem($"{verb} {path} must be answered by exactly one hand-written action");
    }

    /// <summary>
    /// 2026-10-26: switching an office on without its feed, starting the feed separately and returning an
    /// office to push are no longer offered. Nothing answers those routes, so a stale screen or script gets a
    /// 404 instead of leaving an office half connected.
    /// </summary>
    [Theory]
    [InlineData("PUT", "api/app/case-tracker/offices/{officeId}/push")]
    [InlineData("POST", "api/app/case-tracker/offices/{officeId}/feed/start")]
    [InlineData("POST", "api/app/case-tracker/offices/{officeId}/feed/return-to-push")]
    public void The_removed_switch_and_feed_routes_are_not_answered(string verb, string path)
    {
        AttributeRouteTable.Matching(verb, path).ShouldBeEmpty($"{verb} {path} must no longer be answered");
    }
}
