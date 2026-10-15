using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Unit coverage for the skip guards of <see cref="ChangeRequestSubmittedEmailHandler"/>, the email
/// every party gets when a CANCELLATION request is submitted.
///
/// <para><b>WHAT IS PINNED.</b> A null event, a missing appointment, or a missing change request sends
/// nothing; a submitted cancellation is emailed with its reason. The result asserted is the dispatch,
/// read back from a substituted <see cref="INotificationDispatcher"/> -- no mail can leave. The first
/// Fact is the positive control for the three "sends nothing" Facts. Synthetic data only (HIPAA).</para>
/// </summary>
public class ChangeRequestSubmittedEmailHandlerGuardTests
{
    private sealed class Rig
    {
        public INotificationDispatcher Dispatcher { get; } = Substitute.For<INotificationDispatcher>();

        /// <summary>The ten nulls are never dereferenced: <c>ResolveAsync</c> is virtual and configured.</summary>
        public DocumentEmailContextResolver ContextResolver { get; } =
            Substitute.For<DocumentEmailContextResolver>(null, null, null, null, null, null, null, null, null, null);

        public IRepository<AppointmentChangeRequest, Guid> ChangeRequests { get; } = Substitute.For<IRepository<AppointmentChangeRequest, Guid>>();

        public Rig()
        {
            ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(new DocumentEmailContext
            {
                AppointmentId = Guid.NewGuid(),
                RequestConfirmationNumber = "TEST-CS0001",
                AppointmentDate = new DateTime(2026, 10, 21, 9, 0, 0),
                PatientFirstName = "TEST-First",
                PatientLastName = "TEST-Last",
            });
            ChangeRequests.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(new AppointmentChangeRequest(
                    Guid.NewGuid(), null, Guid.NewGuid(), ChangeRequestType.Cancel,
                    cancellationReason: "TEST-reason: moving away", reScheduleReason: null, newDoctorAvailabilityId: null));
        }

        public ChangeRequestSubmittedEmailHandler Build()
        {
            var recipients = Substitute.For<IAppointmentRecipientResolver>();
            recipients.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>()).Returns(new List<SendAppointmentEmailArgs>
            {
                new() { To = "TEST-aa@test.local", Role = RecipientRole.ApplicantAttorney, IsRegistered = true },
            });
            return new ChangeRequestSubmittedEmailHandler(
                Dispatcher,
                ContextResolver,
                recipients,
                ChangeRequests,
                Substitute.For<ICurrentTenant>(),
                NullLogger<ChangeRequestSubmittedEmailHandler>.Instance);
        }
    }

    private static AppointmentChangeRequestSubmittedEto CancelSubmitted() => new()
    {
        AppointmentId = Guid.NewGuid(),
        ChangeRequestId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ChangeRequestType = ChangeRequestType.Cancel,
        SubmittedByUserId = Guid.NewGuid(),
        OccurredAt = new DateTime(2026, 9, 23, 17, 0, 0, DateTimeKind.Utc),
    };

    /// <summary><b>POSITIVE CONTROL.</b> A submitted cancellation is emailed, carrying its reason.</summary>
    [Fact]
    public async Task HandleEventAsync_ASubmittedCancellation_IsEmailedWithItsReason_PositiveControl()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(CancelSubmitted());

        var args = rig.Dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .ShouldHaveSingleItem().GetArguments();
        args[0].ShouldBe(NotificationTemplateConsts.Codes.AppointmentCancelledRequest);
        ((IReadOnlyDictionary<string, object?>)args[2]!)["CancellationReason"].ShouldBe("TEST-reason: moving away");
    }

    [Fact]
    public async Task HandleEventAsync_NullEvent_SendsNothing()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(null!);

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleEventAsync_AppointmentNotFound_SendsNothing()
    {
        var rig = new Rig();
        rig.ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns((DocumentEmailContext?)null);

        await rig.Build().HandleEventAsync(CancelSubmitted());

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleEventAsync_ChangeRequestNotFound_SendsNothing()
    {
        var rig = new Rig();
        rig.ChangeRequests.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((AppointmentChangeRequest?)null);

        await rig.Build().HandleEventAsync(CancelSubmitted());

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty();
    }
}
