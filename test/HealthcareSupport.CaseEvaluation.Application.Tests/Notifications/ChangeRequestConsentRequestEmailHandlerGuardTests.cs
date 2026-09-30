using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Unit coverage for the guards of <see cref="ChangeRequestConsentRequestEmailHandler"/>, which asks
/// the OPPOSING party to consent to a cancel or reschedule request.
///
/// <para><b>WHAT IS PINNED.</b> No email without an opposing address or without the change request.
/// A MISSING TEMPLATE is swallowed -- the consent round must not fail because an office has not set
/// the template up -- but ANY OTHER business error still reaches the caller, so a real failure is
/// never hidden behind that catch.</para>
///
/// <para><b>The results asserted are the dispatch made, and the error thrown or not.</b> The
/// dispatcher is a substitute, so no mail can leave. The first Fact is the positive control for the
/// "sends nothing" Facts. Synthetic data only (HIPAA).</para>
/// </summary>
public class ChangeRequestConsentRequestEmailHandlerGuardTests
{
    private sealed class Rig
    {
        public INotificationDispatcher Dispatcher { get; } = Substitute.For<INotificationDispatcher>();
        public IRepository<AppointmentChangeRequest, Guid> ChangeRequests { get; } = Substitute.For<IRepository<AppointmentChangeRequest, Guid>>();

        public Rig()
        {
            ChangeRequests.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(new AppointmentChangeRequest(
                    Guid.NewGuid(), null, Guid.NewGuid(), ChangeRequestType.Cancel,
                    cancellationReason: "TEST-reason: relocating", reScheduleReason: null, newDoctorAvailabilityId: null));
        }

        public ChangeRequestConsentRequestEmailHandler Build() => new(
            Dispatcher,
            ChangeRequests,
            Substitute.For<IRepository<Appointment, Guid>>(),
            Substitute.For<IRepository<DoctorAvailability, Guid>>(),
            Substitute.For<ICurrentTenant>(),
            NullLogger<ChangeRequestConsentRequestEmailHandler>.Instance);

        public List<object?[]> Dispatches() =>
            Dispatcher.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
                .Select(c => c.GetArguments())
                .ToList();
    }

    private static ChangeRequestConsentRequestedEto ConsentRequested(string opposingEmail = "TEST-da@test.local") => new()
    {
        AppointmentId = Guid.NewGuid(),
        ChangeRequestId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ChangeRequestType = ChangeRequestType.Cancel,
        OpposingRecipientEmail = opposingEmail,
        OpposingRecipientRole = RecipientRole.DefenseAttorney,
        ConsentUrl = "https://tenant.portal.test.local/consent/TEST-token",
        RoundNumber = 1,
        SendAttempt = 1,
        OccurredAt = new DateTime(2026, 9, 23, 17, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>
    /// <b>POSITIVE CONTROL.</b> A consent request is emailed to the opposing party, unregistered, with
    /// the consent template.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_EmailsTheOpposingPartyWithTheConsentTemplate_PositiveControl()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(ConsentRequested());

        var args = rig.Dispatches().ShouldHaveSingleItem();
        args[0].ShouldBe(NotificationTemplateConsts.Codes.ChangeRequestConsentRequest);
        var recipient = ((IReadOnlyCollection<NotificationRecipient>)args[1]!).ShouldHaveSingleItem();
        recipient.Email.ShouldBe("TEST-da@test.local");
        recipient.IsRegistered.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleEventAsync_NoOpposingAddress_SendsNothing(string opposingEmail)
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(ConsentRequested(opposingEmail));

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleEventAsync_NullEvent_SendsNothing()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(null!);

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleEventAsync_ChangeRequestNotFound_SendsNothing()
    {
        var rig = new Rig();
        rig.ChangeRequests.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((AppointmentChangeRequest?)null);

        await rig.Build().HandleEventAsync(ConsentRequested());

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>
    /// A missing consent template is logged and skipped: the handler returns normally after trying to
    /// dispatch, so the consent round is not failed by an office's unfinished template setup.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_TheTemplateIsMissing_IsSwallowedAfterTheAttempt()
    {
        var rig = new Rig();
        rig.Dispatcher.DispatchAsync(default!, default!, default!, default!, default, default)
            .ReturnsForAnyArgs(Task.FromException(new BusinessException(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)));

        await Should.NotThrowAsync(() => rig.Build().HandleEventAsync(ConsentRequested()));

        rig.Dispatches().Count.ShouldBe(1, "the dispatch was attempted before the missing template was swallowed");
    }

    /// <summary>
    /// The catch is filtered to the missing-template code. Any other business error must still reach
    /// the caller -- otherwise a genuine failure would look like a successful send.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_AnyOtherBusinessError_StillPropagates()
    {
        var rig = new Rig();
        rig.Dispatcher.DispatchAsync(default!, default!, default!, default!, default, default)
            .ReturnsForAnyArgs(Task.FromException(new BusinessException("TEST:SomeOtherFailure")));

        var thrown = await Should.ThrowAsync<BusinessException>(() => rig.Build().HandleEventAsync(ConsentRequested()));

        thrown.Code.ShouldBe("TEST:SomeOtherFailure");
    }
}
