using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Unit coverage for <see cref="ClinicalStaffCancellationEmailHandler"/>: the separate notice sent
/// to the office's intake inbox when an external user submits a CANCELLATION request.
///
/// <para><b>WHAT IS PINNED.</b> It fires for cancellations only (reschedule submissions go through a
/// different handler), it goes to the configured office address and nobody else, it is skipped when
/// no office address is configured, and its link is the HOST portal root -- staff open staff notices
/// on the host site, not on the tenant's external portal -- never the tenant URL in the context.</para>
///
/// <para><b>WHY THE POSITIVE CONTROL IS NOT OPTIONAL.</b> An unconfigured substitute makes the
/// handler bail out early, which also dispatches nothing. The negative Facts mean something only
/// because <see cref="HandleEventAsync_CancellationSubmitted_EmailsTheOffice_PositiveControl"/> shows
/// this fixture reaches the dispatcher. Same rule as <c>PatientPacketEmailKindGateTests</c>.</para>
///
/// <para><b>NO MAIL CAN LEAVE.</b> <see cref="INotificationDispatcher"/> is fully substituted and the
/// handler is built with <c>new</c> (its <c>[UnitOfWork]</c> is inert). The change-request repository
/// is a substitute; no database is touched.</para>
///
/// <para>Synthetic data only (HIPAA).</para>
/// </summary>
public class ClinicalStaffCancellationEmailHandlerTests
{
    private const string OfficeEmail = "TEST-intake-office@test.local";
    private const string HostPortalRoot = "https://admin.portal.test.local";
    private const string CancellationReason = "TEST-reason: the patient has moved out of state";

    private sealed class Rig
    {
        public INotificationDispatcher Dispatcher { get; } = Substitute.For<INotificationDispatcher>();

        /// <summary>
        /// The ten nulls are never dereferenced: <c>ResolveAsync</c> is <c>virtual</c> and is
        /// configured below, so the real body never runs.
        /// </summary>
        public DocumentEmailContextResolver ContextResolver { get; } =
            Substitute.For<DocumentEmailContextResolver>(null, null, null, null, null, null, null, null, null, null);

        public IRepository<AppointmentChangeRequest, Guid> ChangeRequests { get; } =
            Substitute.For<IRepository<AppointmentChangeRequest, Guid>>();

        public ISettingProvider Settings { get; } = Substitute.For<ISettingProvider>();
        public ICurrentTenant CurrentTenant { get; } = Substitute.For<ICurrentTenant>();
        public IAccountUrlBuilder UrlBuilder { get; } = Substitute.For<IAccountUrlBuilder>();

        public DocumentEmailContext Context { get; } = new()
        {
            AppointmentId = Guid.NewGuid(),
            RequestConfirmationNumber = "TEST-A0005",
            AppointmentDate = new DateTime(2026, 10, 8, 13, 0, 0),
            PatientFirstName = "TEST-First",
            PatientLastName = "TEST-Last",
            PortalBaseUrl = "https://tenant.portal.test.local",
        };

        public AppointmentChangeRequest ChangeRequest { get; } = new(
            id: Guid.NewGuid(),
            tenantId: null,
            appointmentId: Guid.NewGuid(),
            changeRequestType: ChangeRequestType.Cancel,
            cancellationReason: CancellationReason,
            reScheduleReason: null,
            newDoctorAvailabilityId: null);

        public Rig()
        {
            Settings.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.OfficeEmail).Returns(OfficeEmail);
            ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(_ => Context);
            ChangeRequests.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(_ => ChangeRequest);
            CurrentTenant.Name.Returns("TEST-clinic");
            // Any tenant id gets a TENANT root; only a null id (the host) gets the host root. So a
            // Fact reading the host root proves the handler asked for the host, not a tenant.
            UrlBuilder.BuildPortalRootUrlAsync(Arg.Any<Guid?>()).Returns("https://tenant-root.portal.test.local");
            UrlBuilder.BuildPortalRootUrlAsync(null).Returns(HostPortalRoot);
        }

        public ClinicalStaffCancellationEmailHandler Build() => new(
            Dispatcher,
            ContextResolver,
            ChangeRequests,
            Settings,
            CurrentTenant,
            NullLogger<ClinicalStaffCancellationEmailHandler>.Instance,
            UrlBuilder);
    }

    /// <summary>The arguments of one <c>DispatchAsync</c> call, read back from the substitute.</summary>
    private sealed record SentEmail(
        string TemplateCode,
        IReadOnlyCollection<NotificationRecipient> Recipients,
        IReadOnlyDictionary<string, object?> Variables,
        string ContextTag);

    private static AppointmentChangeRequestSubmittedEto SubmittedEvent(ChangeRequestType type = ChangeRequestType.Cancel) => new()
    {
        AppointmentId = Guid.NewGuid(),
        ChangeRequestId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ChangeRequestType = type,
        SubmittedByUserId = Guid.NewGuid(),
        OccurredAt = new DateTime(2026, 9, 23, 17, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>Reads the ONE <c>DispatchAsync</c> call, asserting the count first for a readable failure.</summary>
    private static SentEmail SingleSend(INotificationDispatcher dispatcher)
    {
        var calls = dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .ToList();
        calls.Count.ShouldBe(1, "one submitted cancellation sends exactly one office notice");
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
    public async Task HandleEventAsync_CancellationSubmitted_EmailsTheOffice_PositiveControl()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(SubmittedEvent());

        SingleSend(rig.Dispatcher).TemplateCode.ShouldBe(NotificationTemplateConsts.Codes.ClinicalStaffCancellation);
    }

    [Fact]
    public async Task HandleEventAsync_NullEvent_DispatchesNothing()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(null!);

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty(
            "a null event must be ignored; the positive control proves this fixture otherwise dispatches");
    }

    /// <summary>
    /// Reschedule submissions are announced by a different handler to every party (office included),
    /// so this one must stay silent -- and must not even read the office setting.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_RescheduleSubmitted_DispatchesNothingAndReadsNoSetting()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(SubmittedEvent(ChangeRequestType.Reschedule));

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty("only a CANCELLATION request goes to the intake inbox");
        await rig.Settings.DidNotReceive().GetOrNullAsync(Arg.Any<string>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleEventAsync_NoOfficeEmailConfigured_DispatchesNothing(string? officeEmail)
    {
        var rig = new Rig();
        rig.Settings.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.OfficeEmail).Returns(officeEmail);

        await rig.Build().HandleEventAsync(SubmittedEvent());

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty(
            "with no intake inbox configured for the tenant there is nobody to notify");
    }

    [Fact]
    public async Task HandleEventAsync_AppointmentNotFound_DispatchesNothing()
    {
        var rig = new Rig();
        rig.ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns((DocumentEmailContext?)null);

        await rig.Build().HandleEventAsync(SubmittedEvent());

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty("with no appointment context the handler must skip, not throw");
    }

    [Fact]
    public async Task HandleEventAsync_ChangeRequestNotFound_DispatchesNothing()
    {
        var rig = new Rig();
        rig.ChangeRequests.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((AppointmentChangeRequest?)null);

        await rig.Build().HandleEventAsync(SubmittedEvent());

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty("with no change request there is no reason to report");
    }

    /// <summary>
    /// The link in a STAFF notice is the host portal root, asked for with a null tenant id -- not the
    /// tenant's external portal URL carried in the context.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_LinksToTheHostPortalRootNotTheTenantPortal()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(SubmittedEvent());

        SingleSend(rig.Dispatcher).Variables["PortalUrl"].ShouldBe(HostPortalRoot);
        await rig.UrlBuilder.Received(1).BuildPortalRootUrlAsync(null);
    }

    /// <summary>
    /// The requester's reason is passed through; a reason cleared after the request was created
    /// renders as empty text rather than failing the notice.
    /// </summary>
    [Theory]
    [InlineData(CancellationReason, CancellationReason)]
    [InlineData(null, "")]
    public async Task HandleEventAsync_PassesTheCancellationReasonThrough(string? storedReason, string expected)
    {
        var rig = new Rig();
        rig.ChangeRequest.CancellationReason = storedReason;

        await rig.Build().HandleEventAsync(SubmittedEvent());

        SingleSend(rig.Dispatcher).Variables["CancellationReason"].ShouldBe(expected);
    }

    [Fact]
    public async Task HandleEventAsync_SendsOnlyToTheOfficeInboxAndTagsTheSend()
    {
        var rig = new Rig();
        var evt = SubmittedEvent();

        await rig.Build().HandleEventAsync(evt);

        var sent = SingleSend(rig.Dispatcher);
        var recipient = sent.Recipients.ShouldHaveSingleItem();
        recipient.Email.ShouldBe(OfficeEmail);
        recipient.Role.ShouldBe(RecipientRole.OfficeAdmin);
        recipient.IsRegistered.ShouldBeFalse();
        sent.ContextTag.ShouldBe($"ClinicalStaffCancellation/{evt.ChangeRequestId}");
    }
}
