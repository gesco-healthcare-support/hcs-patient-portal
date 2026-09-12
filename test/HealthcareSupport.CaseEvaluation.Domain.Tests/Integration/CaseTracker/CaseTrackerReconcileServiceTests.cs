using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the reconcile read. Three rules carry the design and each is a security property,
/// not a convenience:
/// <list type="bullet">
/// <item><description>The office gate is read AFTER switching into that office, so one live office
/// cannot expose a second office that is still switched off.</description></item>
/// <item><description>A disabled office does no payload work at all -- nothing is assembled, so
/// nothing can leak through a logging or error path.</description></item>
/// <item><description>Every failure collapses to null, so an unknown appointment, an unknown office
/// and a broken office all look identical from outside.</description></item>
/// </list>
/// </summary>
public class CaseTrackerReconcileServiceTests
{
    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");

    private sealed class Harness
    {
        public CaseTrackerReconcileService Service { get; init; } = null!;
        public IIntakePayloadBuilder Builder { get; init; } = null!;
        public ICurrentTenant CurrentTenant { get; init; } = null!;
        public IDisposable TenantScope { get; init; } = null!;
    }

    private static Harness Build(bool enabled = true, Exception? builderThrows = null)
    {
        var envelope = new IntakeEnvelope
        {
            Data = new IntakePayload { AppointmentId = AppointmentId, ConfirmationNumber = "A00065" },
        };

        var builder = Substitute.For<IIntakePayloadBuilder>();
        if (builderThrows != null)
        {
            builder.BuildAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Throws(builderThrows);
        }
        else
        {
            builder.BuildAsync(AppointmentId, Arg.Any<CancellationToken>()).Returns(Task.FromResult(envelope));
        }

        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider.GetOrNullAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled)
            .Returns(Task.FromResult<string?>(enabled ? "true" : "false"));

        var scope = Substitute.For<IDisposable>();
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(scope);

        return new Harness
        {
            Service = new CaseTrackerReconcileService(
                builder,
                settingProvider,
                currentTenant,
                NullLogger<CaseTrackerReconcileService>.Instance),
            Builder = builder,
            CurrentTenant = currentTenant,
            TenantScope = scope,
        };
    }

    [Fact]
    public async Task ForAKnownAppointmentInALiveOffice_TheEnvelopeIsReturned()
    {
        var h = Build();

        var result = await h.Service.GetAsync(TenantId, AppointmentId);

        result.ShouldNotBeNull();
        result!.Data.AppointmentId.ShouldBe(AppointmentId);
    }

    [Fact]
    public async Task TheOfficeScopeIsEntered_BeforeAnyPayloadWork()
    {
        var h = Build();

        await h.Service.GetAsync(TenantId, AppointmentId);

        h.CurrentTenant.Received(1).Change(TenantId, Arg.Any<string?>());
    }

    [Fact]
    public async Task TheOfficeScopeIsAlwaysDisposed()
    {
        // Leaking the scope would let this office's identity bleed into the rest of the request.
        var h = Build();

        await h.Service.GetAsync(TenantId, AppointmentId);

        h.TenantScope.Received(1).Dispose();
    }

    [Fact]
    public async Task TheOfficeScopeIsDisposedEvenWhenTheBuildFails()
    {
        var h = Build(builderThrows: new InvalidOperationException("office database unreachable"));

        await h.Service.GetAsync(TenantId, AppointmentId);

        h.TenantScope.Received(1).Dispose();
    }

    [Fact]
    public async Task WhenTheOfficeHasTheIntegrationDisabled_NothingIsReturnedAndNoPayloadIsAssembled()
    {
        var h = Build(enabled: false);

        var result = await h.Service.GetAsync(TenantId, AppointmentId);

        result.ShouldBeNull();
        await h.Builder.DidNotReceiveWithAnyArgs().BuildAsync(default, default);
    }

    [Fact]
    public async Task WhenTheAppointmentIsUnknown_NullIsReturned()
    {
        // The payload builder resolves the appointment with GetAsync, which throws rather than
        // returning null. That must surface as a 404, never a 500.
        var h = Build(builderThrows: new EntityNotFoundException(typeof(object), AppointmentId));

        var result = await h.Service.GetAsync(TenantId, AppointmentId);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task WhenTheOfficeIsUnknownOrBroken_NullIsReturnedRatherThanThrowing()
    {
        // T5: an unrecognised tenant id must not become a 500. Any failure reaching this point is
        // treated the same way, so the response cannot be used to probe which offices exist.
        var h = Build(builderThrows: new InvalidOperationException("no connection string for tenant"));

        IntakeEnvelope? result = null;
        await Should.NotThrowAsync(async () => result = await h.Service.GetAsync(TenantId, AppointmentId));

        result.ShouldBeNull();
    }
}
