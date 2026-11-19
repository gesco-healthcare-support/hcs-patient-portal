using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The blank forms' rules with the renderer and the letterhead substituted: the office gate, the ETag, the 304 and
/// the cache, and that no case data is ever sent. Fixture data is synthetic.
/// </summary>
public class CaseTrackerBlankFormServiceTests
{
    private static readonly Guid OfficeId = new("0ff1ce00-0000-4000-8000-000000000002");
    private static readonly byte[] Pdf = [37, 80, 68, 70];

    private sealed class Harness
    {
        public required CaseTrackerBlankFormService Service { get; init; }
        public required IHtmlPacketRenderer Renderer { get; init; }
        public required CaseTrackerDeliveryModeReader Mode { get; init; }
        public required OfficeLetterheadResolver Letterhead { get; init; }
        public required ICurrentTenant Tenant { get; init; }
        public required ITenantStore Store { get; init; }
        public string Version { get; set; } = "version-one";
    }

    private static OfficeLetterheadResolver LetterheadResolver(string practice)
    {
        var resolver = Substitute.For<OfficeLetterheadResolver>(
            Substitute.For<IReadOnlyRepository<Doctor, Guid>>(),
            Substitute.For<IReadOnlyRepository<OfficeBranding, Guid>>(),
            Substitute.For<ICurrentTenant>());
        resolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new OfficeLetterhead { PracticeName = practice, SignatureImage = "data:image/png;base64,AAAA" }));
        return resolver;
    }

    private static Harness Build(bool switchOn = true, string practice = "Sample Practice")
    {
        var mode = Substitute.For<CaseTrackerDeliveryModeReader>(
            Substitute.For<ISettingProvider>(), Substitute.For<ICaseTrackerFeedStateRepository>());
        mode.IsPushSwitchOnAsync().Returns(Task.FromResult(switchOn));

        var renderer = Substitute.For<IHtmlPacketRenderer>();
        renderer.RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Pdf));

        var letterhead = LetterheadResolver(practice);
        var tenant = Substitute.For<ICurrentTenant>();
        var store = Substitute.For<ITenantStore>();
        store.FindAsync(OfficeId).Returns(Task.FromResult<TenantConfiguration?>(new TenantConfiguration(OfficeId, "sample")));
        renderer.GetTemplateVersionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult("version-one"));
        return new Harness
        {
            Service = new CaseTrackerBlankFormService(
                store, tenant, mode, letterhead, renderer, new BlankPacketFormCache(),
                NullLogger<CaseTrackerBlankFormService>.Instance),
            Renderer = renderer,
            Mode = mode,
            Letterhead = letterhead,
            Tenant = tenant,
            Store = store,
        };
    }

    [Theory]
    [InlineData(BlankPacketKind.PatientPacket, "patient-blank")]
    [InlineData(BlankPacketKind.DoctorPacket, "doctor-blank")]
    public async Task ARequest_RendersTheBlankTemplateOfThatKind_InsideTheOfficeScope(BlankPacketKind kind, string template)
    {
        var h = Build();

        var result = await h.Service.GetAsync(OfficeId, kind, null);

        result.Outcome.ShouldBe(BlankPacketFormOutcome.Pdf);
        result.Pdf.ShouldBe(Pdf);
        result.ETag.ShouldStartWith("\"");
        h.Tenant.Received().Change(OfficeId);
        await h.Renderer.Received(1).RenderAsync(template, Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnOfficeNotSwitchedOn_IsNotEnabled_AndNothingIsRendered()
    {
        var h = Build(switchOn: false);

        var result = await h.Service.GetAsync(OfficeId, BlankPacketKind.PatientPacket, null);

        result.Outcome.ShouldBe(BlankPacketFormOutcome.NotEnabled);
        result.Pdf.ShouldBeNull();
        await h.Renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
    }

    [Fact]
    public async Task AnUnknownOffice_IsNotEnabled_WithoutEverEnteringItsScopeOrReadingTheSwitch()
    {
        var h = Build();
        var unknown = Guid.NewGuid();
        h.Store.FindAsync(unknown).Returns(Task.FromResult<TenantConfiguration?>(null));

        var result = await h.Service.GetAsync(unknown, BlankPacketKind.PatientPacket, null);

        result.Outcome.ShouldBe(BlankPacketFormOutcome.NotEnabled);
        h.Tenant.DidNotReceiveWithAnyArgs().Change(default);
        await h.Mode.DidNotReceive().IsPushSwitchOnAsync();
        await h.Renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
    }

    [Fact]
    public async Task AFailedOfficeLookup_IsNotEnabled_NeverAnException()
    {
        var h = Build();
        h.Store.FindAsync(OfficeId).Throws(new InvalidOperationException("registry down"));

        var result = await h.Service.GetAsync(OfficeId, BlankPacketKind.DoctorPacket, null);

        result.Outcome.ShouldBe(BlankPacketFormOutcome.NotEnabled);
    }

    [Fact]
    public async Task TwoOfficesWithTheSameLetterhead_GetDifferentETags()
    {
        var h = Build();
        var other = Guid.NewGuid();
        h.Store.FindAsync(other).Returns(Task.FromResult<TenantConfiguration?>(new TenantConfiguration(other, "other")));

        var first = await h.Service.GetAsync(OfficeId, BlankPacketKind.PatientPacket, null);
        var second = await h.Service.GetAsync(other, BlankPacketKind.PatientPacket, null);

        second.ETag.ShouldNotBe(first.ETag);
    }

    [Fact]
    public async Task AnOfficeWhoseSwitchCannotBeRead_IsNotEnabled()
    {
        var h = Build();
        h.Mode.IsPushSwitchOnAsync().Throws(new InvalidOperationException("no such office"));

        var result = await h.Service.GetAsync(OfficeId, BlankPacketKind.DoctorPacket, null);

        result.Outcome.ShouldBe(BlankPacketFormOutcome.NotEnabled);
    }

    [Fact]
    public async Task OnlyTheOfficeTokensCarryAValue_AndNoSignatureIsSent()
    {
        var h = Build();
        IReadOnlyDictionary<string, string>? sent = null;
        h.Renderer.RenderAsync(Arg.Any<string>(), Arg.Do<IReadOnlyDictionary<string, string>>(t => sent = t), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Pdf));

        await h.Service.GetAsync(OfficeId, BlankPacketKind.PatientPacket, null);

        sent.ShouldNotBeNull();
        sent["##Office.PracticeName##"].ShouldBe("Sample Practice");
        sent["##Office.SignatureImage##"].ShouldBe(string.Empty);
        sent.Where(t => !t.Key.StartsWith("##Office.", StringComparison.Ordinal))
            .Select(t => t.Value).ShouldAllBe(v => v == string.Empty);
    }

    [Fact]
    public async Task AnIfNoneMatchOfTheCurrentVersion_IsNotModified_WithNoBody_AndNoRender()
    {
        var h = Build();
        var first = await h.Service.GetAsync(OfficeId, BlankPacketKind.PatientPacket, null);
        h.Renderer.ClearReceivedCalls();

        var second = await h.Service.GetAsync(OfficeId, BlankPacketKind.PatientPacket, first.ETag);

        second.Outcome.ShouldBe(BlankPacketFormOutcome.NotModified);
        second.Pdf.ShouldBeNull();
        second.ETag.ShouldBe(first.ETag);
        await h.Renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
    }

    [Fact]
    public async Task AChangedTemplateVersion_ChangesTheETag_SoAnOldOneGetsTheNewPdf()
    {
        var h = Build();
        var first = await h.Service.GetAsync(OfficeId, BlankPacketKind.PatientPacket, null);
        h.Renderer.GetTemplateVersionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("version-two"));

        var second = await h.Service.GetAsync(OfficeId, BlankPacketKind.PatientPacket, first.ETag);

        second.Outcome.ShouldBe(BlankPacketFormOutcome.Pdf);
        second.ETag.ShouldNotBe(first.ETag);
    }

    [Fact]
    public async Task ASecondRequestForTheSameVersion_IsServedFromTheCache()
    {
        var h = Build();

        await h.Service.GetAsync(OfficeId, BlankPacketKind.DoctorPacket, null);
        await h.Service.GetAsync(OfficeId, BlankPacketKind.DoctorPacket, null);

        await h.Renderer.Received(1).RenderAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void TheETag_DependsOnTheTemplateTheVersionAndEveryToken()
    {
        var tokens = new Dictionary<string, string> { ["##Office.PracticeName##"] = "A" };
        var baseline = CaseTrackerBlankFormService.ComputeETag(OfficeId, "patient-blank", "v1", tokens);

        baseline.ShouldBe(CaseTrackerBlankFormService.ComputeETag(OfficeId, "patient-blank", "v1", new Dictionary<string, string>(tokens)));
        baseline.ShouldNotBe(CaseTrackerBlankFormService.ComputeETag(OfficeId, "doctor-blank", "v1", tokens));
        baseline.ShouldNotBe(CaseTrackerBlankFormService.ComputeETag(OfficeId, "patient-blank", "v2", tokens));
        baseline.ShouldNotBe(CaseTrackerBlankFormService.ComputeETag(
            OfficeId, "patient-blank", "v1", new Dictionary<string, string> { ["##Office.PracticeName##"] = "B" }));
        baseline.ShouldNotBe(CaseTrackerBlankFormService.ComputeETag(Guid.NewGuid(), "patient-blank", "v1", tokens));
    }

    [Theory]
    [InlineData("\"abc\"", "\"abc\"", true)]
    [InlineData("W/\"abc\"", "\"abc\"", true)]
    [InlineData("\"x\", \"abc\"", "\"abc\"", true)]
    [InlineData("*", "\"abc\"", true)]
    [InlineData("\"x\"", "\"abc\"", false)]
    [InlineData("", "\"abc\"", false)]
    [InlineData(null, "\"abc\"", false)]
    public void IfNoneMatch_IsMatchedAsHttpDefinesIt(string? header, string eTag, bool expected) =>
        CaseTrackerBlankFormService.IfNoneMatchContains(header, eTag).ShouldBe(expected);

    [Fact]
    public void AnUnknownKind_IsRefused() =>
        Should.Throw<ArgumentOutOfRangeException>(() => CaseTrackerBlankFormService.TemplateFor((BlankPacketKind)99));

    [Fact]
    public async Task TheCache_DropsEverythingWhenFull_AndKeepsTheNewEntry()
    {
        var cache = new BlankPacketFormCache();
        for (var i = 0; i < CaseTrackerFormsConsts.MaxCachedForms; i++)
        {
            cache.Store($"k{i}", Pdf);
        }

        cache.Store("new", Pdf);

        cache.Find("new").ShouldBe(Pdf);
        cache.Find("k0").ShouldBeNull();
        await Task.CompletedTask;
    }

    [Fact]
    public async Task TheOfficeList_NamesEachOfficeLikeTheLetterhead_AndNeverListsOneBlank()
    {
        var runner = Substitute.For<ITenantWorkRunner>();
        var named = Guid.NewGuid();
        var bare = Guid.NewGuid();
        runner.AggregateAcrossOfficesAsync(Arg.Any<Func<Guid, Task<CaseTrackerOfficeEntry>>>())
            .Returns(async call =>
            {
                var selector = call.Arg<Func<Guid, Task<CaseTrackerOfficeEntry>>>();
                return new List<CaseTrackerOfficeEntry> { await selector(bare), await selector(named) };
            });
        var store = Substitute.For<ITenantStore>();
        store.FindAsync(bare).Returns(Task.FromResult<TenantConfiguration?>(new TenantConfiguration(bare, "zeta")));
        store.FindAsync(named).Returns(Task.FromResult<TenantConfiguration?>(new TenantConfiguration(named, "alpha")));
        var letterhead = Substitute.For<OfficeLetterheadResolver>(
            Substitute.For<IReadOnlyRepository<Doctor, Guid>>(),
            Substitute.For<IReadOnlyRepository<OfficeBranding, Guid>>(),
            Substitute.For<ICurrentTenant>());
        letterhead.ResolveAsync(Arg.Any<CancellationToken>()).Returns(
            Task.FromResult(new OfficeLetterhead()),
            Task.FromResult(new OfficeLetterhead { PhysicianName = "Dr. Sample Doctor", PracticeName = "Sample Practice" }));
        var service = new CaseTrackerOfficeListService(runner, letterhead, store, NullLogger<CaseTrackerOfficeListService>.Instance);

        var offices = await service.ListAsync();

        offices.Select(o => o.PracticeName).ShouldBe(["Sample Practice", "zeta"]);
        offices.Single(o => o.Id == named).DoctorName.ShouldBe("Dr. Sample Doctor");
        offices.Single(o => o.Id == bare).DoctorName.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task AnOfficeWhoseNamesCannotBeRead_IsStillListed_UnderItsSlug()
    {
        var runner = Substitute.For<ITenantWorkRunner>();
        var id = Guid.NewGuid();
        runner.AggregateAcrossOfficesAsync(Arg.Any<Func<Guid, Task<CaseTrackerOfficeEntry>>>())
            .Returns(async call => new List<CaseTrackerOfficeEntry> { await call.Arg<Func<Guid, Task<CaseTrackerOfficeEntry>>>()(id) });
        var store = Substitute.For<ITenantStore>();
        store.FindAsync(id).Returns(Task.FromResult<TenantConfiguration?>(new TenantConfiguration(id, "broken")));
        var letterhead = Substitute.For<OfficeLetterheadResolver>(
            Substitute.For<IReadOnlyRepository<Doctor, Guid>>(),
            Substitute.For<IReadOnlyRepository<OfficeBranding, Guid>>(),
            Substitute.For<ICurrentTenant>());
        letterhead.ResolveAsync(Arg.Any<CancellationToken>()).Throws(new InvalidOperationException("db down"));
        var service = new CaseTrackerOfficeListService(runner, letterhead, store, NullLogger<CaseTrackerOfficeListService>.Instance);

        var offices = await service.ListAsync();

        offices.Single().PracticeName.ShouldBe("broken");
        offices.Single().DoctorName.ShouldBe(string.Empty);
    }
}
