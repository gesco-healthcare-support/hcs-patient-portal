using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// The feed endpoint's HTTP contract (#927), driven directly with a fake <see cref="HttpContext"/>: the order of
/// its gates (token, then the office's allowance, then the feed) and the status and code for every outcome. The
/// REAL token validator and allowance are used, so a wrong token provably spends nothing. Fixture data is
/// synthetic and the tokens are arbitrary strings.
/// </summary>
public class CaseTrackerFeedControllerTests
{
    private const string FeedToken = "sample-feed-token-value";
    private const string IntegrationToken = "sample-integration-token-value";

    private static readonly Guid OfficeId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");

    private sealed class Harness
    {
        public required CaseTrackerFeedController Controller { get; init; }
        public required CaseTrackerFeedService Service { get; init; }
        public required CaseTrackerFeedAllowance Allowance { get; init; }
        public required DefaultHttpContext Http { get; init; }
    }

    private static Harness Build(string? presentedToken, CaseTrackerFeedResult? result = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CaseTrackerFeedConsts.FeedTokenConfigurationKey] = FeedToken,
                [CaseTrackerIntegrationConsts.TokenConfigurationKey] = IntegrationToken,
            })
            .Build();

        // A class substitute: ReadAsync is virtual, so only the outcome is scripted here. The service's own
        // rules are covered by CaseTrackerFeedServiceTests.
        var feedStateRepository = Substitute.For<ICaseTrackerFeedStateRepository>();
        var service = Substitute.For<CaseTrackerFeedService>(
            Substitute.For<ICurrentTenant>(),
            feedStateRepository,
            Substitute.For<ICaseTrackerFeedStore>(),
            new CaseTrackerFeedAlertPublisher(Substitute.For<ILocalEventBus>(), Substitute.For<ITenantStore>()),
            new CaseTrackerDeliveryModeReader(Substitute.For<Volo.Abp.Settings.ISettingProvider>(), feedStateRepository),
            Substitute.For<IClock>(),
            NullLogger<CaseTrackerFeedService>.Instance);
        service.ReadAsync(OfficeId, Arg.Any<string?>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result ?? CaseTrackerFeedResult.Served([], 100, false)));

        var clock = Substitute.For<IClock>();
        clock.Now.Returns(new DateTime(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc));
        var allowance = new CaseTrackerFeedAllowance();

        var controller = new CaseTrackerFeedController(new FeedTokenValidator(configuration), allowance, service, clock);
        var http = new DefaultHttpContext();
        if (presentedToken != null)
        {
            http.Request.Headers[CaseTrackerFeedConsts.FeedTokenHeaderName] = presentedToken;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return new Harness { Controller = controller, Service = service, Allowance = allowance, Http = http };
    }

    private static Task<IActionResult> ActAsync(Harness h, string? cursor = null) =>
        h.Controller.GetFeedAsync(OfficeId, cursor, null, CancellationToken.None);

    private static (int Status, JsonDocument Body) Read(IActionResult result)
    {
        var content = result.ShouldBeOfType<ContentResult>();
        content.ContentType.ShouldBe("application/json");
        return (content.StatusCode!.Value, JsonDocument.Parse(content.Content!));
    }

    private static string ErrorCode(JsonDocument body) =>
        body.RootElement.GetProperty("errors")[0].GetProperty("code").GetString()!;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong")]
    [InlineData(IntegrationToken)] // the reconcile/attendance token must not open the feed
    public async Task AWrongToken_Is403_BeforeTheFeedOrTheAllowanceIsTouched(string? token)
    {
        var h = Build(token);

        var (status, body) = Read(await ActAsync(h));

        status.ShouldBe(StatusCodes.Status403Forbidden);
        ErrorCode(body).ShouldBe("forbidden");
        await h.Service.DidNotReceiveWithAnyArgs().ReadAsync(default, default, default, default);
    }

    [Fact]
    public async Task WrongTokenRequests_DoNotSpendTheOfficesAllowance()
    {
        var h = Build("wrong");
        for (var i = 0; i < CaseTrackerFeedConsts.RequestsPerHourPerOffice + 10; i++)
        {
            await ActAsync(h);
        }

        h.Allowance.TryAcquire(OfficeId).ShouldBeTrue();
    }

    [Fact]
    public async Task OnceTheOfficesAllowanceIsSpent_Is403AllowanceExceeded_WithoutReadingTheFeed()
    {
        var h = Build(FeedToken);
        for (var i = 0; i < CaseTrackerFeedConsts.RequestsPerHourPerOffice; i++)
        {
            h.Allowance.TryAcquire(OfficeId).ShouldBeTrue();
        }

        var (status, body) = Read(await ActAsync(h));

        status.ShouldBe(StatusCodes.Status403Forbidden);
        ErrorCode(body).ShouldBe("allowance_exceeded");
        await h.Service.DidNotReceiveWithAnyArgs().ReadAsync(default, default, default, default);
    }

    [Fact]
    public async Task APage_Is200_AndEachBodyDecodesToTheStoredPayload()
    {
        const string payload = "{\"data\":{\"confirmationNumber\":\"A00065\"},\"meta\":{},\"errors\":[]}";
        var page = CaseTrackerFeedResult.Served(
            new[] { new CaseTrackerFeedRow(2003, IntegrationMessageType.Intake, AppointmentId, payload) }, 2003, false);
        var h = Build(FeedToken, page);

        var (status, body) = Read(await ActAsync(h, "00000000000007D2"));

        status.ShouldBe(StatusCodes.Status200OK);
        body.RootElement.GetProperty("data").GetProperty("rows")[0].GetProperty("body").GetString().ShouldBe(payload);
        await h.Service.Received(1).ReadAsync(OfficeId, "00000000000007D2", Arg.Any<IReadOnlyList<string>?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(CaseTrackerFeedOutcome.FeedNotEnabled, StatusCodes.Status403Forbidden, "feed_not_enabled")]
    [InlineData(CaseTrackerFeedOutcome.CursorInvalid, StatusCodes.Status400BadRequest, "cursor_invalid")]
    [InlineData(CaseTrackerFeedOutcome.CursorBelowFloor, StatusCodes.Status400BadRequest, "cursor_below_floor")]
    [InlineData(CaseTrackerFeedOutcome.CursorAhead, StatusCodes.Status400BadRequest, "cursor_ahead")]
    [InlineData(CaseTrackerFeedOutcome.SkipInvalid, StatusCodes.Status400BadRequest, "skip_invalid")]
    public async Task EachRefusal_HasItsAgreedStatusAndCode_AndNeverA401Or429(
        CaseTrackerFeedOutcome outcome, int expectedStatus, string expectedCode)
    {
        var h = Build(FeedToken, CaseTrackerFeedResult.Refused(outcome));

        var (status, body) = Read(await ActAsync(h));

        status.ShouldBe(expectedStatus);
        ErrorCode(body).ShouldBe(expectedCode);
        body.RootElement.GetProperty("data").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
