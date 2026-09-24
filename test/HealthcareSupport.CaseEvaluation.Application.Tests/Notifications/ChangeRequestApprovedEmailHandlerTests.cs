using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
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
/// Unit coverage for the branches of <see cref="ChangeRequestApprovedEmailHandler"/> that the
/// existing approval-flow tests do not reach: the four skip guards, the cancellation template, the
/// staff-override wording, and a reschedule whose new slot cannot be found.
///
/// <para><b>WHAT IS PINNED.</b> With no appointment, no change request, or no addressable recipient,
/// nothing is sent. A cancel approval uses the cancellation template, as does an unknown type. When
/// staff changed the slot themselves the email says so and quotes their reason HTML-encoded. A
/// reschedule with no resolvable slot renders a blank date rather than failing.</para>
///
/// <para><b>The result asserted is the dispatched template and variables</b>, read back from a
/// substituted <see cref="INotificationDispatcher"/>; no mail can leave and no database is touched.
/// The first Fact is the positive control for every "sends nothing" Fact. Synthetic data only (HIPAA).</para>
/// </summary>
public class ChangeRequestApprovedEmailHandlerTests
{
    private sealed class Rig
    {
        public INotificationDispatcher Dispatcher { get; } = Substitute.For<INotificationDispatcher>();

        /// <summary>The ten nulls are never dereferenced: <c>ResolveAsync</c> is virtual and configured.</summary>
        public DocumentEmailContextResolver ContextResolver { get; } =
            Substitute.For<DocumentEmailContextResolver>(null, null, null, null, null, null, null, null, null, null);

        public IAppointmentRecipientResolver RecipientResolver { get; } = Substitute.For<IAppointmentRecipientResolver>();
        public IRepository<AppointmentChangeRequest, Guid> ChangeRequests { get; } = Substitute.For<IRepository<AppointmentChangeRequest, Guid>>();
        public IRepository<DoctorAvailability, Guid> Slots { get; } = Substitute.For<IRepository<DoctorAvailability, Guid>>();
        public ICurrentTenant CurrentTenant { get; } = Substitute.For<ICurrentTenant>();

        public DocumentEmailContext Context { get; } = new()
        {
            AppointmentId = Guid.NewGuid(),
            RequestConfirmationNumber = "TEST-CA0001",
            AppointmentDate = new DateTime(2026, 10, 20, 9, 0, 0),
            PatientFirstName = "TEST-First",
            PatientLastName = "TEST-Last",
            PortalBaseUrl = "https://tenant.portal.test.local",
        };

        public List<SendAppointmentEmailArgs> Parties { get; } = new()
        {
            new SendAppointmentEmailArgs { To = "TEST-aa@test.local", Role = RecipientRole.ApplicantAttorney, IsRegistered = true },
        };

        public AppointmentChangeRequest ChangeRequest { get; set; } = new(
            Guid.NewGuid(), null, Guid.NewGuid(), ChangeRequestType.Reschedule,
            cancellationReason: null, reScheduleReason: "TEST-reason: travel", newDoctorAvailabilityId: null);

        public Rig()
        {
            ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(_ => Context);
            RecipientResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>()).Returns(_ => Parties);
            ChangeRequests.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_ => ChangeRequest);
            Slots.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns((DoctorAvailability?)null);
        }

        public ChangeRequestApprovedEmailHandler Build() => new(
            Dispatcher,
            ContextResolver,
            RecipientResolver,
            ChangeRequests,
            Slots,
            CurrentTenant,
            NullLogger<ChangeRequestApprovedEmailHandler>.Instance);

        public (string Template, IReadOnlyDictionary<string, object?> Variables) SingleSend()
        {
            var calls = Dispatcher.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
                .ToList();
            calls.Count.ShouldBe(1, "one approval sends exactly one email");
            var args = calls[0].GetArguments();
            return ((string)args[0]!, (IReadOnlyDictionary<string, object?>)args[2]!);
        }
    }

    private static AppointmentChangeRequestApprovedEto Approved(ChangeRequestType type, bool isAdminOverride = false) => new()
    {
        AppointmentId = Guid.NewGuid(),
        ChangeRequestId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ChangeRequestType = type,
        Outcome = AppointmentStatusType.Approved,
        IsAdminOverride = isAdminOverride,
        ApprovedByUserId = Guid.NewGuid(),
        OccurredAt = new DateTime(2026, 9, 23, 17, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>
    /// <b>POSITIVE CONTROL.</b> An approved cancellation is emailed with the cancellation template.
    /// Every "sends nothing" Fact below depends on this fixture reaching the dispatcher.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_ApprovedCancellation_UsesTheCancellationTemplate_PositiveControl()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(Approved(ChangeRequestType.Cancel));

        rig.SingleSend().Template.ShouldBe(NotificationTemplateConsts.Codes.AppointmentCancelledRequestApproved);
    }

    /// <summary>A defensive default: an out-of-range type still sends the cancellation template.</summary>
    [Fact]
    public async Task HandleEventAsync_AnUnknownRequestType_FallsBackToTheCancellationTemplate()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(Approved((ChangeRequestType)99));

        rig.SingleSend().Template.ShouldBe(NotificationTemplateConsts.Codes.AppointmentCancelledRequestApproved);
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

        await rig.Build().HandleEventAsync(Approved(ChangeRequestType.Cancel));

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleEventAsync_ChangeRequestNotFound_SendsNothing()
    {
        var rig = new Rig();
        rig.ChangeRequests.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((AppointmentChangeRequest?)null);

        await rig.Build().HandleEventAsync(Approved(ChangeRequestType.Cancel));

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleEventAsync_EveryRecipientAddressBlank_SendsNothing()
    {
        var rig = new Rig();
        rig.Parties.Clear();
        rig.Parties.Add(new SendAppointmentEmailArgs { To = "  ", Role = RecipientRole.Patient });

        await rig.Build().HandleEventAsync(Approved(ChangeRequestType.Cancel));

        rig.Dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>
    /// When staff changed the appointment themselves, the email says so and quotes the staff reason --
    /// HTML-encoded, so text a staff member typed cannot inject markup. With no reason, the block is empty.
    /// </summary>
    [Theory]
    [InlineData("<b>TEST-doctor unavailable</b>", "<strong>Reason for change:</strong> &lt;b&gt;TEST-doctor unavailable&lt;/b&gt;")]
    [InlineData(null, "")]
    public async Task HandleEventAsync_StaffOverride_SaysStaffChangedItAndQuotesTheirReasonEncoded(string? adminReason, string expectedReasonBlock)
    {
        var rig = new Rig();
        rig.ChangeRequest.AdminReScheduleReason = adminReason;

        await rig.Build().HandleEventAsync(Approved(ChangeRequestType.Reschedule, isAdminOverride: true));

        var variables = rig.SingleSend().Variables;
        variables["ApprovedSubjectQualifier"].ShouldBe("Reschedule request has been changed by our team");
        variables["ApprovedHeadline"].ShouldBe("Our clinic staff has changed your appointment to the date and time below.");
        variables["ReasonBlock"].ShouldBe(expectedReasonBlock);
    }

    /// <summary>
    /// A reschedule with no slot recorded, or a slot id that no longer resolves, renders a BLANK new
    /// date and time rather than failing the approval email.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleEventAsync_RescheduleWithNoResolvableSlot_RendersABlankNewDate(bool slotIdRecorded)
    {
        var rig = new Rig();
        rig.ChangeRequest.AdminOverrideSlotId = slotIdRecorded ? Guid.NewGuid() : null;

        await rig.Build().HandleEventAsync(Approved(ChangeRequestType.Reschedule));

        var variables = rig.SingleSend().Variables;
        variables["NewAppointmentDate"].ShouldBe(string.Empty);
        variables["NewAppointmentFromTime"].ShouldBe(string.Empty);
    }
}
