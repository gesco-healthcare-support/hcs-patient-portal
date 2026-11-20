using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Net.Http.Headers;
using NSubstitute;
using NSubstitute.Extensions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// The blank forms' and office list's HTTP contract, driven directly with a fake <see cref="HttpContext"/>: the
/// order of the gates (token, then the allowance, then the work), the status and code for each outcome, the ETag and
/// 304. The REAL token validator and allowance are used, so a wrong token provably spends nothing. Synthetic data.
/// </summary>
public class CaseTrackerFormsControllerTests
{
    private const string FeedToken = "sample-feed-token-value";
    private const string IntegrationToken = "sample-integration-token-value";
    private static readonly Guid OfficeId = new("0ff1ce00-0000-4000-8000-000000000003");
    private static readonly byte[] Pdf = [37, 80, 68, 70];

    private sealed class Harness
    {
        public required CaseTrackerFormsController Controller { get; init; }
        public required CaseTrackerFormsAllowance Allowance { get; init; }
        public required IHtmlPacketRenderer Renderer { get; init; }
        public required CaseTrackerDeliveryModeReader Mode { get; init; }
        public required ITenantWorkRunner Runner { get; init; }
        public required DefaultHttpContext Http { get; init; }
    }

    private static Harness Build(
        string? presentedToken, bool switchOn = true, string? ifNoneMatch = null, BlankPacketFormResult? forcedForm = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CaseTrackerFeedConsts.FeedTokenConfigurationKey] = FeedToken,
                [CaseTrackerIntegrationConsts.TokenConfigurationKey] = IntegrationToken,
            })
            .Build();

        var mode = Substitute.For<CaseTrackerDeliveryModeReader>(
            Substitute.For<ISettingProvider>(), Substitute.For<ICaseTrackerFeedStateRepository>());
        mode.IsPushSwitchOnAsync().Returns(Task.FromResult(switchOn));
        var renderer = Substitute.For<IHtmlPacketRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Pdf));
        renderer.GetTemplateVersionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult("v1"));
        var letterhead = Substitute.For<OfficeLetterheadResolver>(
            Substitute.For<IReadOnlyRepository<Doctor, Guid>>(),
            Substitute.For<IReadOnlyRepository<OfficeBranding, Guid>>(),
            Substitute.For<ICurrentTenant>());
        letterhead.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OfficeLetterhead { PracticeName = "Sample Practice", PhysicianName = "Dr. Sample Doctor" }));
        var store = Substitute.For<ITenantStore>();
        store.FindAsync(OfficeId).Returns(Task.FromResult<TenantConfiguration?>(new TenantConfiguration(OfficeId, "sample")));
        var formService = Substitute.ForPartsOf<CaseTrackerBlankFormService>(
            store, Substitute.For<ICurrentTenant>(), mode, letterhead, renderer, new BlankPacketFormCache(),
            NullLogger<CaseTrackerBlankFormService>.Instance);
        if (forcedForm != null)
        {
            formService.Configure()
                .GetAsync(Arg.Any<Guid>(), Arg.Any<BlankPacketKind>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(forcedForm));
        }

        var runner = Substitute.For<ITenantWorkRunner>();
        runner.AggregateAcrossOfficesAsync(Arg.Any<Func<Guid, Task<CaseTrackerOfficeEntry>>>())
            .Returns(async call => new List<CaseTrackerOfficeEntry> { await call.Arg<Func<Guid, Task<CaseTrackerOfficeEntry>>>()(OfficeId) });
        var listService = new CaseTrackerOfficeListService(
            runner, letterhead, store, NullLogger<CaseTrackerOfficeListService>.Instance);

        var clock = Substitute.For<IClock>();
        clock.Now.Returns(new DateTime(2026, 11, 19, 18, 0, 0, DateTimeKind.Utc));
        var allowance = new CaseTrackerFormsAllowance();
        var controller = new CaseTrackerFormsController(new FeedTokenValidator(configuration), allowance, formService, listService, clock);
        var http = new DefaultHttpContext();
        if (presentedToken != null)
        {
            http.Request.Headers[CaseTrackerFeedConsts.FeedTokenHeaderName] = presentedToken;
        }

        if (ifNoneMatch != null)
        {
            http.Request.Headers[HeaderNames.IfNoneMatch] = ifNoneMatch;
        }

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return new Harness
        {
            Controller = controller,
            Allowance = allowance,
            Renderer = renderer,
            Mode = mode,
            Runner = runner,
            Http = http,
        };
    }

    private static (int Status, JsonDocument Body) ReadJson(IActionResult result)
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
    [InlineData(IntegrationToken)] // the reconcile/attendance token must not open the forms
    public async Task AWrongToken_Is403Forbidden_OnEveryRoute_BeforeAnythingIsReadOrCounted(string? token)
    {
        var h = Build(token);

        var results = new[]
        {
            await h.Controller.GetPatientPacketAsync(OfficeId, null, CancellationToken.None),
            await h.Controller.GetDoctorPacketAsync(OfficeId, null, CancellationToken.None),
            await h.Controller.GetOfficesAsync(CancellationToken.None),
        };

        foreach (var result in results)
        {
            var (status, body) = ReadJson(result);
            status.ShouldBe(StatusCodes.Status403Forbidden);
            ErrorCode(body).ShouldBe("forbidden");
        }

        await h.Mode.DidNotReceive().IsPushSwitchOnAsync();
        await h.Renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
        await h.Runner.DidNotReceiveWithAnyArgs().AggregateAcrossOfficesAsync<CaseTrackerOfficeEntry>(default!);
    }

    [Fact]
    public async Task WrongTokenRequests_DoNotSpendTheOfficesAllowance()
    {
        var h = Build("wrong");
        for (var i = 0; i < CaseTrackerFormsConsts.FormRequestsPerHourPerOffice + 5; i++)
        {
            await h.Controller.GetPatientPacketAsync(OfficeId, null, CancellationToken.None);
        }

        h.Allowance.TryAcquireForm(OfficeId).ShouldBeTrue();
    }

    [Fact]
    public async Task TheFormsAllowance_IsItsOwn_AndTheFeedsIsUntouched()
    {
        var h = Build(FeedToken);
        for (var i = 0; i < CaseTrackerFormsConsts.FormRequestsPerHourPerOffice; i++)
        {
            h.Allowance.TryAcquireForm(OfficeId).ShouldBeTrue();
        }

        var (status, body) = ReadJson(await h.Controller.GetDoctorPacketAsync(OfficeId, null, CancellationToken.None));

        status.ShouldBe(StatusCodes.Status403Forbidden);
        ErrorCode(body).ShouldBe("allowance_exceeded");
        await h.Renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
        new CaseTrackerFeedAllowance().TryAcquire(OfficeId).ShouldBeTrue();
    }

    [Fact]
    public async Task TheOfficeListAllowance_IsSeparateFromTheFormsAllowance()
    {
        var h = Build(FeedToken);
        for (var i = 0; i < CaseTrackerFormsConsts.OfficeListRequestsPerHour; i++)
        {
            h.Allowance.TryAcquireOfficeList().ShouldBeTrue();
        }

        var (status, body) = ReadJson(await h.Controller.GetOfficesAsync(CancellationToken.None));

        status.ShouldBe(StatusCodes.Status403Forbidden);
        ErrorCode(body).ShouldBe("allowance_exceeded");
        h.Allowance.TryAcquireForm(OfficeId).ShouldBeTrue();
    }

    [Fact]
    public async Task AnOfficeNotSwitchedOn_Is403FeedNotEnabled_LikeTheFeed()
    {
        var h = Build(FeedToken, switchOn: false);

        var (status, body) = ReadJson(await h.Controller.GetPatientPacketAsync(OfficeId, null, CancellationToken.None));

        status.ShouldBe(StatusCodes.Status403Forbidden);
        ErrorCode(body).ShouldBe("feed_not_enabled");
    }

    [Theory]
    [InlineData(true, "patient-packet-blank.pdf")]
    [InlineData(false, "doctor-packet-blank.pdf")]
    public async Task AGoodRequest_Is200Pdf_WithAnETag_ANeutralFileName_AndPrivateCaching(bool patient, string fileName)
    {
        var h = Build(FeedToken);

        var result = patient
            ? await h.Controller.GetPatientPacketAsync(OfficeId, null, CancellationToken.None)
            : await h.Controller.GetDoctorPacketAsync(OfficeId, null, CancellationToken.None);

        var file = result.ShouldBeOfType<FileContentResult>();
        file.ContentType.ShouldBe("application/pdf");
        file.FileDownloadName.ShouldBe(fileName);
        file.FileContents.ShouldBe(Pdf);
        h.Http.Response.Headers[HeaderNames.ETag].ToString().ShouldStartWith("\"");
        h.Http.Response.Headers[HeaderNames.CacheControl].ToString().ShouldContain("private");
    }

    [Fact]
    public async Task AnIfNoneMatchOfTheCurrentETag_Is304_WithTheETag_AndNoBody()
    {
        var first = Build(FeedToken);
        await first.Controller.GetPatientPacketAsync(OfficeId, null, CancellationToken.None);
        var eTag = first.Http.Response.Headers[HeaderNames.ETag].ToString();

        var second = Build(FeedToken, ifNoneMatch: eTag);
        var result = await second.Controller.GetPatientPacketAsync(OfficeId, eTag, CancellationToken.None);

        result.ShouldBeOfType<StatusCodeResult>().StatusCode.ShouldBe(StatusCodes.Status304NotModified);
        second.Http.Response.Headers[HeaderNames.ETag].ToString().ShouldBe(eTag);
        await second.Renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
    }

    [Fact]
    public async Task TheOfficeList_Is200_WithIdPracticeAndDoctorOnly()
    {
        var h = Build(FeedToken);

        var (status, body) = ReadJson(await h.Controller.GetOfficesAsync(CancellationToken.None));

        status.ShouldBe(StatusCodes.Status200OK);
        var office = body.RootElement.GetProperty("data").GetProperty("offices").EnumerateArray().Single();
        office.EnumerateObject().Select(p => p.Name).ShouldBe(["id", "practiceName", "doctorName"]);
        office.GetProperty("id").GetString().ShouldBe(OfficeId.ToString("D"));
        office.GetProperty("practiceName").GetString().ShouldBe("Sample Practice");
        office.GetProperty("doctorName").GetString().ShouldBe("Dr. Sample Doctor");
        body.RootElement.GetProperty("errors").GetArrayLength().ShouldBe(0);
    }

    // Fail closed (2026-11-20): an outcome the action does not handle, or a PDF outcome without bytes, must
    // throw (a 500), never serve a guessed body. A future fourth outcome used to fall through to File(null).
    [Theory]
    [InlineData(99, true)]
    [InlineData((int)BlankPacketFormOutcome.Pdf, false)]
    public async Task AnUnhandledOutcome_OrAPdfOutcomeWithoutBytes_Throws_AndServesNoFile(int outcome, bool withBytes)
    {
        var h = Build(FeedToken, forcedForm: new BlankPacketFormResult(
            (BlankPacketFormOutcome)outcome, "\"e\"", withBytes ? Pdf : null));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => h.Controller.GetPatientPacketAsync(OfficeId, null, CancellationToken.None));

        thrown.Message.ShouldContain(((BlankPacketFormOutcome)outcome).ToString());
        h.Http.Response.Headers.ContainsKey(HeaderNames.ETag).ShouldBeFalse();
    }
}
