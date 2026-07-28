using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>
/// Unit tests for the reconcile endpoint's HTTP contract, exercised through the REAL token validator
/// and reconcile service over substituted repositories. Wiring them for real is the point: the
/// valuable assertions are that a bad token is rejected BEFORE any office is opened, and that the 200
/// body is byte-for-byte what the outbound push would send.
///
/// <para>No web host is needed -- the controller is driven directly with a fake
/// <see cref="HttpContext"/>. All fixture data is synthetic and the token is an arbitrary string.</para>
/// </summary>
public class CaseTrackerReconcileControllerTests
{
    private const string Token = "sample-integration-token-value";

    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");

    private sealed class Harness
    {
        public CaseTrackerReconcileController Controller { get; init; } = null!;
        public IIntakePayloadBuilder Builder { get; init; } = null!;
        public IntakeEnvelope Envelope { get; init; } = null!;
    }

    private static Harness Build(string? presentedToken, bool officeEnabled = true, bool appointmentExists = true)
    {
        var envelope = new IntakeEnvelope
        {
            Data = new IntakePayload
            {
                AppointmentId = AppointmentId,
                ConfirmationNumber = "A00065",
                Status = "Approved",
                UpdatedAt = "2026-07-28T11:30:00.0000000Z",
            },
            Meta = new IntakeMeta
            {
                RequestId = new Guid("c3d4e5f6-a7b8-49ca-8bdc-ed2143658709"),
                Timestamp = "2026-07-28T11:30:05.0000000Z",
            },
        };

        var builder = Substitute.For<IIntakePayloadBuilder>();
        builder.BuildAsync(AppointmentId, Arg.Any<CancellationToken>())
            .Returns(appointmentExists
                ? Task.FromResult(envelope)
                : Task.FromException<IntakeEnvelope>(new InvalidOperationException("no such appointment")));

        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled)
            .Returns(Task.FromResult<string?>(officeEnabled ? "true" : "false"));

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(Substitute.For<IDisposable>());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CaseTrackerIntegrationConsts.TokenConfigurationKey] = Token,
            })
            .Build();

        var controller = new CaseTrackerReconcileController(
            new IntegrationTokenValidator(configuration),
            new CaseTrackerReconcileService(
                builder,
                settingProvider,
                currentTenant,
                NullLogger<CaseTrackerReconcileService>.Instance));

        var httpContext = new DefaultHttpContext();
        if (presentedToken != null)
        {
            httpContext.Request.Headers[CaseTrackerIntegrationConsts.IntegrationTokenHeaderName] = presentedToken;
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return new Harness { Controller = controller, Builder = builder, Envelope = envelope };
    }

    private static Task<IActionResult> ActAsync(Harness h) =>
        h.Controller.GetAppointmentAsync(TenantId, AppointmentId, CancellationToken.None);

    [Fact]
    public async Task WithTheCorrectToken_TheEnvelopeIsReturnedAsJson()
    {
        var h = Build(Token);

        var result = await ActAsync(h);

        var content = result.ShouldBeOfType<ContentResult>();
        content.ContentType.ShouldBe("application/json");
        content.Content.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task TheBodyIsByteIdenticalToWhatThePushWouldSend()
    {
        // The contract promises one shape for push and pull so the receiver runs ONE deserializer.
        var h = Build(Token);

        var result = await ActAsync(h);

        var content = result.ShouldBeOfType<ContentResult>();
        content.Content.ShouldBe(IntakePayloadSerializer.Serialize(h.Envelope));
    }

    [Theory]
    [InlineData(null)]          // header absent
    [InlineData("")]            // header present but empty
    [InlineData("wrong-token")]
    [InlineData("Sample-integration-token-value")] // differs only by case
    public async Task WithoutTheCorrectToken_TheRequestIsUnauthorized(string? presented)
    {
        var h = Build(presented);

        var result = await ActAsync(h);

        result.ShouldBeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AnUnauthorizedRequest_NeverTouchesAnOfficeDatabase()
    {
        // The token check must gate the DB work, not merely the response body.
        var h = Build("wrong-token");

        await ActAsync(h);

        await h.Builder.DidNotReceiveWithAnyArgs().BuildAsync(default, default);
    }

    [Fact]
    public async Task WhenTheOfficeHasTheIntegrationDisabled_TheAnswerIsNotFound()
    {
        var h = Build(Token, officeEnabled: false);

        var result = await ActAsync(h);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task WhenTheAppointmentIsUnknown_TheAnswerIsNotFound()
    {
        var h = Build(Token, appointmentExists: false);

        var result = await ActAsync(h);

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DisabledOfficeAndUnknownAppointment_AreIndistinguishable()
    {
        // A holder of the token must not be able to tell whether an appointment or office exists.
        var disabled = await ActAsync(Build(Token, officeEnabled: false));
        var unknown = await ActAsync(Build(Token, appointmentExists: false));

        disabled.ShouldBeOfType<NotFoundResult>();
        unknown.ShouldBeOfType<NotFoundResult>();
        disabled.ShouldBeOfType(unknown.GetType());
    }
}
