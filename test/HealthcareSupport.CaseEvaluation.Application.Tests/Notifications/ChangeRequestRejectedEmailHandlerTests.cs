using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Unit coverage for <see cref="ChangeRequestRejectedEmailHandler"/>: the email every appointment
/// party receives when staff reject a cancellation or reschedule request.
///
/// <para><b>WHAT IS PINNED.</b> The template follows the request type (cancel vs reschedule), the
/// staff member's rejection notes reach the email verbatim, and blank recipient addresses are dropped
/// -- with nobody left, nothing is sent.</para>
///
/// <para><b>WHY THE POSITIVE CONTROL IS NOT OPTIONAL.</b> An unconfigured substitute makes the
/// handler bail out early, which also dispatches nothing. The negative Facts mean something only
/// because <see cref="HandleEventAsync_CancelRequestRejected_EmailsTheParties_PositiveControl"/> shows
/// this fixture reaches the dispatcher. Same rule as <c>PatientPacketEmailKindGateTests</c>.</para>
///
/// <para><b>NO MAIL CAN LEAVE.</b> <see cref="INotificationDispatcher"/> is fully substituted and the
/// handler is built with <c>new</c> (its <c>[UnitOfWork]</c> is inert). No database is touched.</para>
///
/// <para>Synthetic data only (HIPAA).</para>
/// </summary>
public class ChangeRequestRejectedEmailHandlerTests
{
    private const string RejectionNotes = "TEST-notes: no slot is available that week";

    private sealed class Rig
    {
        public INotificationDispatcher Dispatcher { get; } = Substitute.For<INotificationDispatcher>();

        /// <summary>
        /// The ten nulls are never dereferenced: <c>ResolveAsync</c> is <c>virtual</c> and is
        /// configured below, so the real body never runs.
        /// </summary>
        public DocumentEmailContextResolver ContextResolver { get; } =
            Substitute.For<DocumentEmailContextResolver>(null, null, null, null, null, null, null, null, null, null);

        public IAppointmentRecipientResolver RecipientResolver { get; } = Substitute.For<IAppointmentRecipientResolver>();
        public ICurrentTenant CurrentTenant { get; } = Substitute.For<ICurrentTenant>();

        public DocumentEmailContext Context { get; } = new()
        {
            AppointmentId = Guid.NewGuid(),
            RequestConfirmationNumber = "TEST-A0004",
            AppointmentDate = new DateTime(2026, 10, 7, 11, 0, 0),
            PatientFirstName = "TEST-First",
            PatientLastName = "TEST-Last",
            PortalBaseUrl = "https://tenant.portal.test.local",
        };

        public List<SendAppointmentEmailArgs> Parties { get; } = new()
        {
            Party("TEST-applicant-attorney@test.local", RecipientRole.ApplicantAttorney),
            Party("TEST-patient@test.local", RecipientRole.Patient),
        };

        public Rig()
        {
            ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(_ => Context);
            // List<T> is concrete, so an unconfigured resolver would hand back null and throw at .Where.
            RecipientResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>()).Returns(_ => Parties);
            CurrentTenant.Name.Returns("TEST-clinic");
        }

        public ChangeRequestRejectedEmailHandler Build() => new(
            Dispatcher,
            ContextResolver,
            RecipientResolver,
            CurrentTenant,
            NullLogger<ChangeRequestRejectedEmailHandler>.Instance);
    }

    /// <summary>The arguments of one <c>DispatchAsync</c> call, read back from the substitute.</summary>
    private sealed record SentEmail(
        string TemplateCode,
        IReadOnlyCollection<NotificationRecipient> Recipients,
        IReadOnlyDictionary<string, object?> Variables,
        string ContextTag);

    private static SendAppointmentEmailArgs Party(string email, RecipientRole? role) => new()
    {
        To = email,
        Role = role,
        IsRegistered = true,
    };

    private static AppointmentChangeRequestRejectedEto RejectedEvent(ChangeRequestType type = ChangeRequestType.Cancel) => new()
    {
        AppointmentId = Guid.NewGuid(),
        ChangeRequestId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ChangeRequestType = type,
        RejectionNotes = RejectionNotes,
        RejectedByUserId = Guid.NewGuid(),
        OccurredAt = new DateTime(2026, 9, 23, 17, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>Reads the ONE <c>DispatchAsync</c> call, asserting the count first for a readable failure.</summary>
    private static SentEmail SingleSend(INotificationDispatcher dispatcher)
    {
        var calls = dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .ToList();
        calls.Count.ShouldBe(1, "one rejected change request sends exactly one email");
        var args = calls[0].GetArguments();
        return new SentEmail(
            (string)args[0]!,
            (IReadOnlyCollection<NotificationRecipient>)args[1]!,
            (IReadOnlyDictionary<string, object?>)args[2]!,
            (string)args[3]!);
    }

    /// <summary>
    /// <b>POSITIVE CONTROL -- load-bearing.</b> Proves this fixture reaches the dispatcher at all.
    /// Every "dispatches nothing" Fact below depends on it.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_CancelRequestRejected_EmailsTheParties_PositiveControl()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(RejectedEvent());

        var sent = SingleSend(rig.Dispatcher);
        sent.TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentCancelledRequestRejected);
        sent.Recipients.Select(r => r.Email).ShouldBe(
            new[] { "TEST-applicant-attorney@test.local", "TEST-patient@test.local" },
            ignoreOrder: true);
    }

    [Fact]
    public async Task HandleEventAsync_NullEvent_DispatchesNothing()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(null!);

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty(
            "a null event must be ignored; the positive control proves this fixture otherwise dispatches");
    }

    [Fact]
    public async Task HandleEventAsync_AppointmentNotFound_DispatchesNothing()
    {
        var rig = new Rig();
        rig.ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns((DocumentEmailContext?)null);

        await rig.Build().HandleEventAsync(RejectedEvent());

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty(
            "with no appointment context the handler must skip, not throw");
    }

    [Fact]
    public async Task HandleEventAsync_EveryRecipientAddressBlank_DispatchesNothing()
    {
        var rig = new Rig();
        rig.Parties.Clear();
        rig.Parties.Add(Party("", RecipientRole.ApplicantAttorney));
        rig.Parties.Add(Party("   ", RecipientRole.Patient));

        await rig.Build().HandleEventAsync(RejectedEvent());

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty(
            "blank addresses are dropped, and with none left there is nobody to send to");
    }

    [Theory]
    [InlineData(ChangeRequestType.Cancel, NotificationTemplateConsts.Codes.AppointmentCancelledRequestRejected)]
    [InlineData(ChangeRequestType.Reschedule, NotificationTemplateConsts.Codes.AppointmentRescheduleRequestRejected)]
    public async Task HandleEventAsync_PicksTheTemplateByRequestType(ChangeRequestType type, string expectedTemplate)
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(RejectedEvent(type));

        SingleSend(rig.Dispatcher).TemplateCode.ShouldBe(expectedTemplate);
    }

    /// <summary>
    /// A defensive default: the enum has only Cancel (1) and Reschedule (2), but an out-of-range
    /// value still sends the cancellation-rejected email rather than throwing.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_AnUnknownRequestType_FallsBackToTheCancellationTemplate()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(RejectedEvent((ChangeRequestType)99));

        SingleSend(rig.Dispatcher).TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.AppointmentCancelledRequestRejected);
    }

    [Fact]
    public async Task HandleEventAsync_PassesTheRejectionNotesThroughAndTagsTheSend()
    {
        var rig = new Rig();
        var evt = RejectedEvent(ChangeRequestType.Reschedule);

        await rig.Build().HandleEventAsync(evt);

        var sent = SingleSend(rig.Dispatcher);
        sent.Variables["RejectionNotes"].ShouldBe(RejectionNotes);
        sent.ContextTag.ShouldBe($"ChangeRequestRejected/Reschedule/{evt.ChangeRequestId}");
        await rig.RecipientResolver.Received(1).ResolveAsync(evt.AppointmentId, NotificationKind.Rejected);
    }
}
