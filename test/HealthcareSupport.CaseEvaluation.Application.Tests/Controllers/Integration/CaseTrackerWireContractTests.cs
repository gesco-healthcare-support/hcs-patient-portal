using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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
    /// off on <c>allowance_exceeded</c>. An unrecognised code falls through to "never halt".
    ///
    /// <para>So a renamed code DEMOTES rather than silences, which they corrected me on: the office stops
    /// halting and warns every tick instead. That is worse than it sounds, not better. A halt is a stopped
    /// office an operator must clear; a warning every minute against an office their screen still shows as
    /// RUNNING is the exact noise that trains people past warnings. The fault is a lost demand for action,
    /// not a lost signal, so more logging would not address it.</para>
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
            + "unrecognised one falls through to 'never halt'. So a rename does not fail on their side -- it "
            + "demotes a halt to a warning every tick against an office that still reads as RUNNING.");
    }

    /// <summary>
    /// The COMPLETE set of codes the feed emits. Pinning each code individually closes a rename; it cannot
    /// close an ADDITION, and an addition is the more dangerous of the two.
    ///
    /// <para>A new code is legitimate, arrives unrecognised by construction, and lands entirely on the
    /// consumer's fall-through. Their mapping treats an unrecognised code as "never halt", which is the right
    /// default -- <c>feed_not_enabled</c> must not halt, and a new benign code stranding every office would be
    /// worse. But it means an eighth code is safe only because of a decision made on their side, and no
    /// assertion here could cover it.</para>
    ///
    /// <para>They are closing their half too: the unknown code's name now reaches their warning and their
    /// stored failure behind a strict allow-list, and the first sighting of each unknown code is raised once
    /// per process at ERROR, which their post-deploy check already counts. So an addition is caught by
    /// whichever side notices first -- this test before it ships, their ERROR if it ships anyway.</para>
    ///
    /// <para>So this test does the one thing that IS in our power: it fails when the set changes, which puts a
    /// human in front of the decision rather than leaving it to a fall-through neither side chose. Adding a code
    /// is not forbidden. It requires telling them first, because it is the single change on this side that
    /// their end cannot be made safe against in advance, however well either of us pins things.</para>
    /// </summary>
    [Fact]
    public void TheFeedEmitsExactlyTheSevenAgreedCodes()
    {
        var emitted = Regex
            .Matches(ControllerSourceOf(typeof(CaseTrackerFeedController)), @"Status\d{3}\w+,\s*""([a-z_]+)""")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        emitted.ShouldNotBeEmpty(
            "Parsed no error codes from the feed controller. The shape the parser matches has changed, which "
            + "would leave this guard permanently and silently green.");

        emitted.ShouldBe(
            [
                "allowance_exceeded",
                "cursor_ahead",
                "cursor_below_floor",
                "cursor_invalid",
                "feed_not_enabled",
                "forbidden",
                "skip_invalid",
            ],
            "The feed's error-code set has changed. Either way the new or renamed code is unrecognised on "
            + "their side by construction and falls through to 'never halt', so an office that should stop "
            + "does not -- it warns every tick while still reading as RUNNING. Tell the Case Tracker before "
            + "this ships, then update this list.");
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
