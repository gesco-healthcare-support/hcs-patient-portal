using System;
using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.Controllers.Integration;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// Pins the Case Tracker's wire contract as LITERALS, so renaming a constant fails here rather than silently
/// breaking a system we cannot compile against.
///
/// <para><b>Why literals, when the repo otherwise prefers constants.</b> Every other test reads these through
/// <see cref="CaseTrackerFeedConsts"/> or the route attributes, which means it agrees with whatever value the
/// source happens to hold. Rename <c>FeedTokenHeaderName</c> from <c>X-Feed-Token</c> to anything at all and the
/// entire suite still passes, while the Case Tracker's client -- which sends the old spelling -- gets a 403 on
/// every office at once. A test that reads the same constant as the code it tests cannot disagree with it.</para>
///
/// <para><b>This is not hypothetical.</b> The Case Tracker hit exactly this in September 2026: their adapter and
/// their end-to-end stub both encoded their own draft spelling of the cursor parameter, so every test agreed
/// with itself while disagreeing with us. Reverting the parameter name failed two adapter tests and no
/// end-to-end test. They fixed it by spelling OUR names as literals read from this source; this is the same
/// guard from the other side.</para>
///
/// <para><b>What to do when one of these fails.</b> Not update the literal. A failure here means a change that
/// a deployed consumer cannot see coming, so it needs a coordinated release: agree the change, let them ship
/// support for it, then change it here. The literal is the thing under protection, not the thing to fix.</para>
///
/// <para>Confirmed against their implementation on 2026-09-25.</para>
/// </summary>
public sealed class CaseTrackerWireContractTests
{
    /// <summary>
    /// The feed's own credential, deliberately NOT the integration token: the feed is read-only, high volume and
    /// permanent, while the integration token also authorises attendance, which closes an appointment
    /// irreversibly. One leak must not grant both, and each must rotate alone.
    /// </summary>
    [Fact]
    public void TheFeedTokenHeaderIsNamed_XFeedToken()
        => CaseTrackerFeedConsts.FeedTokenHeaderName.ShouldBe("X-Feed-Token");

    /// <summary>Gates reconcile and attendance. A different credential from the feed's, by design.</summary>
    [Fact]
    public void TheIntegrationTokenHeaderIsNamed_XIntegrationToken()
        => CaseTrackerIntegrationConsts.IntegrationTokenHeaderName.ShouldBe("X-Integration-Token");

    /// <summary>
    /// All three machine-to-machine endpoints share this prefix, which is also what the rate limiter matches on,
    /// so moving it silently changes which bucket these requests are counted in as well as where they live.
    /// </summary>
    [Theory]
    [InlineData(typeof(CaseTrackerFeedController))]
    [InlineData(typeof(CaseTrackerAttendanceController))]
    [InlineData(typeof(CaseTrackerReconcileController))]
    public void EveryIntegrationControllerIsRootedAt_ApiIntegration(Type controller)
        => RouteOn(controller).ShouldBe("api/integration");

    [Fact]
    public void TheFeedRouteIs_OfficesTenantIdFeed()
        => RouteOn(typeof(CaseTrackerFeedController), nameof(CaseTrackerFeedController.GetFeedAsync))
            .ShouldBe("offices/{tenantId}/feed");

    [Fact]
    public void TheAttendanceRouteIs_OfficesTenantIdAppointmentsAppointmentIdAttendance()
        => RouteOn(typeof(CaseTrackerAttendanceController), nameof(CaseTrackerAttendanceController.RecordAttendanceAsync))
            .ShouldBe("offices/{tenantId}/appointments/{appointmentId}/attendance");

    [Fact]
    public void TheReconcileRouteIs_OfficesTenantIdAppointmentsAppointmentId()
        => RouteOn(typeof(CaseTrackerReconcileController), nameof(CaseTrackerReconcileController.GetAppointmentAsync))
            .ShouldBe("offices/{tenantId}/appointments/{appointmentId}");

    /// <summary>
    /// The query parameter names, which are the ones a consumer gets wrong most cheaply. A request carrying an
    /// unrecognised name binds to null rather than failing, so the wrong spelling reads as "no cursor sent" --
    /// the portal then resumes from its own acknowledged position and the consumer is silently re-served the
    /// same page forever, with 200s throughout. That is precisely what nearly shipped in September 2026.
    /// </summary>
    [Fact]
    public void TheFeedsQueryParametersAreNamed_CursorAndSkipped()
    {
        var parameters = typeof(CaseTrackerFeedController)
            .GetMethod(nameof(CaseTrackerFeedController.GetFeedAsync))!
            .GetParameters()
            .Where(p => p.GetCustomAttribute<FromQueryAttribute>() != null)
            .Select(p => p.Name)
            .ToList();

        parameters.ShouldBe(["cursor", "skipped"]);
    }

    /// <summary>
    /// <c>skipped</c> is a repeated parameter, not a comma-joined one. An array binds repeated keys and does NOT
    /// split on commas, so a consumer sending <c>skipped=a,b</c> delivers one element "a,b", which fails to
    /// decode as a cursor. Changing this to a scalar would make that spelling start working and the correct one
    /// stop.
    /// </summary>
    [Fact]
    public void TheSkippedParameterIsRepeatedRatherThanCommaJoined()
        => typeof(CaseTrackerFeedController)
            .GetMethod(nameof(CaseTrackerFeedController.GetFeedAsync))!
            .GetParameters()
            .Single(p => p.Name == "skipped")
            .ParameterType.ShouldBe(typeof(string[]));

    /// <summary>
    /// Page size is fixed server-side and is not a request parameter. The consumer paces its drains against this
    /// number multiplied by its tick rate, so raising it raises their worst-case request count in one of our
    /// rate-limit windows without them changing anything.
    /// </summary>
    [Fact]
    public void ThePageSizeIs200()
        => CaseTrackerFeedConsts.PageSize.ShouldBe(200);

    /// <summary>
    /// The consumer branches on these codes rather than on the status, so the STRING is the contract. Their
    /// mapping halts an office on the cursor and skip codes, never halts on <c>feed_not_enabled</c>, and backs
    /// off on <c>allowance_exceeded</c>. An unrecognised code falls through to "never halt", so a renamed code
    /// does not fail loudly on their side -- it quietly stops halting.
    /// </summary>
    [Theory]
    [InlineData("forbidden")]
    [InlineData("allowance_exceeded")]
    [InlineData("feed_not_enabled")]
    [InlineData("cursor_invalid")]
    [InlineData("cursor_below_floor")]
    [InlineData("cursor_ahead")]
    [InlineData("skip_invalid")]
    public void TheFeedsErrorCodesAreEmittedVerbatim(string code)
    {
        var source = ControllerSourceOf(typeof(CaseTrackerFeedController));
        source.Contains($"\"{code}\"", StringComparison.Ordinal).ShouldBeTrue(
            $"The feed no longer emits the literal '{code}'. Their client branches on the code string, and an "
            + "unrecognised one falls through to 'never halt' -- so a rename does not fail on their side, it "
            + "silently stops halting an office that should stop.");
    }

    private static string RouteOn(Type controller)
        => controller.GetCustomAttribute<RouteAttribute>()!.Template;

    private static string RouteOn(Type controller, string method)
        => controller.GetMethod(method)!.GetCustomAttribute<RouteAttribute>()!.Template;

    /// <summary>
    /// The controller's own source, located from this file rather than from the build output, so the assertion
    /// reads what a developer would edit. Throws rather than skipping when it cannot be found: a guard that
    /// passes because its input went missing is the failure this repo has shipped before.
    /// </summary>
    private static string ControllerSourceOf(Type controller, [System.Runtime.CompilerServices.CallerFilePath] string callerPath = "")
    {
        var directory = System.IO.Directory.GetParent(callerPath);

        while (directory != null)
        {
            var candidate = System.IO.Path.Combine(
                directory.FullName,
                "src",
                "HealthcareSupport.CaseEvaluation.HttpApi.Host",
                "Controllers",
                "Integration",
                controller.Name + ".cs");

            if (System.IO.File.Exists(candidate))
            {
                return System.IO.File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new ShouldAssertException(
            $"Could not find the source for {controller.Name} walking up from '{callerPath}'. This test pins a "
            + "wire contract and must fail loudly when it cannot read its input.");
    }
}
