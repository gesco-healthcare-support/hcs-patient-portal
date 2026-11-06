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
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
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
            Substitute.For<DocumentEmailContextResolver>(null, null, null, null, null, null, null, null, null, null, null);

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
            LinkHolders.ResolveEmailsAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
                .Returns(Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>()));
            ChangeRequests.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(new AppointmentChangeRequest(
                    Guid.NewGuid(), null, Guid.NewGuid(), ChangeRequestType.Cancel,
                    cancellationReason: "TEST-reason: moving away", reScheduleReason: null, newDoctorAvailabilityId: null));
        }

        public FakeOfficeRecipients Office { get; } = new();

        /// <summary>The submitting account the handler looks up; null = the account is not found.</summary>
        public IRepository<IdentityUser, Guid> Users { get; } = Substitute.For<IRepository<IdentityUser, Guid>>();

        public IRepository<DoctorAvailability, Guid> Slots { get; } = Substitute.For<IRepository<DoctorAvailability, Guid>>();

        /// <summary>The parties the resolver returns; a test adds the ones it needs.</summary>
        public List<SendAppointmentEmailArgs> Parties { get; } = new()
        {
            new() { To = "TEST-aa@test.local", Role = RecipientRole.ApplicantAttorney, IsRegistered = true },
        };

        /// <summary>The consent-link holders; empty unless a test lists them. The ctor args are never dereferenced.</summary>
        public ConsentLinkHolders LinkHolders { get; } = Substitute.For<ConsentLinkHolders>(null, null, null);

        public ChangeRequestSubmittedEmailHandler Build()
        {
            var recipients = Substitute.For<IAppointmentRecipientResolver>();
            recipients.ResolveAsync(Arg.Any<Guid>(), Arg.Any<NotificationKind>()).Returns(Parties);
            return new ChangeRequestSubmittedEmailHandler(
                Dispatcher,
                ContextResolver,
                recipients,
                ChangeRequests,
                Office,
                LinkHolders,
                Users,
                Slots,
                Substitute.For<ICurrentTenant>(),
                NullLogger<ChangeRequestSubmittedEmailHandler>.Instance);
        }
    }

    private static AppointmentChangeRequestSubmittedEto RescheduleSubmitted() => new()
    {
        AppointmentId = Guid.NewGuid(),
        ChangeRequestId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        ChangeRequestType = ChangeRequestType.Reschedule,
        SubmittedByUserId = Guid.NewGuid(),
        OccurredAt = new DateTime(2026, 11, 3, 17, 0, 0, DateTimeKind.Utc),
    };

    private static Rig RescheduleRig(Guid? newSlotId = null)
    {
        var rig = new Rig();
        rig.ChangeRequests.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(new AppointmentChangeRequest(
                Guid.NewGuid(), null, Guid.NewGuid(), ChangeRequestType.Reschedule,
                cancellationReason: null, reScheduleReason: "TEST-reason: conflict at work", newDoctorAvailabilityId: newSlotId));
        return rig;
    }

    private static (string Template, IReadOnlyCollection<NotificationRecipient> Recipients, IReadOnlyDictionary<string, object?> Variables)
        TheDispatch(Rig rig)
    {
        var args = rig.Dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .ShouldHaveSingleItem().GetArguments().Skip(1).ToArray();
        return ((string)args[0]!, (IReadOnlyCollection<NotificationRecipient>)args[1]!, (IReadOnlyDictionary<string, object?>)args[2]!);
    }

    /// <summary>
    /// Row 7. An attorney's reschedule request goes to the parties and the Intake Staff with the reschedule
    /// template. Nobody holds a consent link at submit (consent opens when staff set a date), so the
    /// link-holder lookup is not consulted.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_AReschedule_SendsTheRescheduleNoticeToThePartiesAndIntake()
    {
        var rig = RescheduleRig();
        rig.Office.Intake.Add("TEST-intake-1@test.local");
        var evt = RescheduleSubmitted();

        await rig.Build().HandleEventAsync(evt);

        var (template, recipients, _) = TheDispatch(rig);
        template.ShouldBe(NotificationTemplateConsts.Codes.AppointmentRescheduleRequest);
        recipients.Select(r => r.Email).ShouldBe(
            new[] { "TEST-aa@test.local", "TEST-intake-1@test.local" }, ignoreOrder: true);
        await rig.LinkHolders.DidNotReceive().ResolveEmailsAsync(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    /// <summary>Row 7: the patient is left off a reschedule request somebody else filed.</summary>
    [Fact]
    public async Task HandleEventAsync_AReschedule_LeavesThePatientOffWhenSomeoneElseAsked()
    {
        var rig = RescheduleRig();
        rig.Parties.Add(new() { To = "TEST-patient@test.local", Role = RecipientRole.Patient, IsRegistered = true });
        rig.ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(ContextWithPatient());

        await rig.Build().HandleEventAsync(RescheduleSubmitted());

        TheDispatch(rig).Recipients.Select(r => r.Email).ShouldBe(new[] { "TEST-aa@test.local" });
    }

    /// <summary>Row 7: the patient who filed the reschedule request (confirmed email) is told.</summary>
    [Fact]
    public async Task HandleEventAsync_AReschedule_TellsThePatientWhoAsked()
    {
        var rig = RescheduleRig();
        rig.Parties.Add(new() { To = "TEST-patient@test.local", Role = RecipientRole.Patient, IsRegistered = true });
        rig.ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(ContextWithPatient());
        var evt = RescheduleSubmitted();
        var patientLogin = new IdentityUser(evt.SubmittedByUserId, "TEST-patient", "TEST-patient@test.local");
        patientLogin.SetEmailConfirmed(true);
        rig.Users.FindAsync(evt.SubmittedByUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(patientLogin);

        await rig.Build().HandleEventAsync(evt);

        TheDispatch(rig).Recipients.Select(r => r.Email).ShouldBe(
            new[] { "TEST-aa@test.local", "TEST-patient@test.local" }, ignoreOrder: true);
    }

    /// <summary>An UNCONFIRMED address proves nothing about who filed it: the patient is not told.</summary>
    [Fact]
    public async Task HandleEventAsync_AReschedule_DoesNotTrustAnUnconfirmedPatientAddress()
    {
        var rig = RescheduleRig();
        rig.Parties.Add(new() { To = "TEST-patient@test.local", Role = RecipientRole.Patient, IsRegistered = true });
        rig.ContextResolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(ContextWithPatient());
        var evt = RescheduleSubmitted();
        var impostor = new IdentityUser(evt.SubmittedByUserId, "TEST-impostor", "TEST-patient@test.local");
        impostor.SetEmailConfirmed(false);
        rig.Users.FindAsync(evt.SubmittedByUserId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(impostor);

        await rig.Build().HandleEventAsync(evt);

        TheDispatch(rig).Recipients.Select(r => r.Email).ShouldBe(new[] { "TEST-aa@test.local" });
    }

    /// <summary>
    /// A request that arrives with NO proposed date (the external path) renders the reason and no date
    /// line; one that carries a slot also shows the date.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_AReschedule_WithoutADate_ShowsOnlyTheReason()
    {
        var rig = RescheduleRig(newSlotId: null);

        await rig.Build().HandleEventAsync(RescheduleSubmitted());

        var block = (string)TheDispatch(rig).Variables["ConsentDetailsBlock"]!;
        block.ShouldContain("TEST-reason: conflict at work");
        block.ShouldNotContain("Requested new date");
    }

    [Fact]
    public async Task HandleEventAsync_AReschedule_WithADate_ShowsTheDate()
    {
        var slotId = Guid.NewGuid();
        var rig = RescheduleRig(newSlotId: slotId);
        rig.Slots.FindAsync(slotId, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(
            new DoctorAvailability(slotId, Guid.NewGuid(), new DateTime(2026, 12, 3), new TimeOnly(9, 30), new TimeOnly(10, 0), HealthcareSupport.CaseEvaluation.Enums.BookingStatus.Available));

        await rig.Build().HandleEventAsync(RescheduleSubmitted());

        ((string)TheDispatch(rig).Variables["ConsentDetailsBlock"]!).ShouldContain("Dec 3, 2026 at 9:30 AM");
    }

    /// <summary>
    /// A missing or deactivated template must not fail the request that was just submitted: the email is a
    /// side effect. (Surfaced by the full EF suite, where a reschedule submit ran without the template seeded.)
    /// </summary>
    [Theory]
    [InlineData(ChangeRequestType.Reschedule)]
    [InlineData(ChangeRequestType.Cancel)]
    public async Task HandleEventAsync_AMissingTemplate_DoesNotFailTheSubmit(ChangeRequestType type)
    {
        var rig = type == ChangeRequestType.Reschedule ? RescheduleRig() : new Rig();
        rig.Dispatcher
            .DispatchAsync(
                Arg.Any<NotificationCase>(),
                Arg.Any<string>(), Arg.Any<IReadOnlyCollection<NotificationRecipient>>(),
                Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<string>(),
                Arg.Any<PacketAttachmentRef?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Volo.Abp.BusinessException(CaseEvaluationDomainErrorCodes.NotificationTemplateNotFound)));
        var evt = type == ChangeRequestType.Reschedule ? RescheduleSubmitted() : CancelSubmitted();

        await Should.NotThrowAsync(() => rig.Build().HandleEventAsync(evt));
    }

    private static DocumentEmailContext ContextWithPatient() => new()
    {
        AppointmentId = Guid.NewGuid(),
        RequestConfirmationNumber = "TEST-CS0001",
        AppointmentDate = new DateTime(2026, 10, 21, 9, 0, 0),
        PatientFirstName = "TEST-First",
        PatientLastName = "TEST-Last",
        PatientAddresses = new[] { "TEST-patient@test.local" },
    };

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
            .ShouldHaveSingleItem().GetArguments().Skip(1).ToArray();
        args[0].ShouldBe(NotificationTemplateConsts.Codes.AppointmentCancelledRequest);
        ((IReadOnlyDictionary<string, object?>)args[2]!)["CancellationReason"].ShouldBe("TEST-reason: moving away");
    }

    /// <summary>2026-11-02: the office's Intake Staff are copied on a cancel request, once per address.</summary>
    [Fact]
    public async Task HandleEventAsync_ASubmittedCancellation_AlsoGoesToTheIntakeStaff()
    {
        var rig = new Rig();
        rig.Office.Intake.Add("TEST-intake-1@test.local");
        rig.Office.Intake.Add("TEST-AA@test.local");

        await rig.Build().HandleEventAsync(CancelSubmitted());

        var recipients = (IReadOnlyCollection<NotificationRecipient>)rig.Dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .ShouldHaveSingleItem().GetArguments().Skip(1).ToArray()[1]!;
        recipients.Select(r => r.Email).ShouldBe(new[] { "TEST-aa@test.local", "TEST-intake-1@test.local" });
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

    /// <summary>
    /// 2026-11-03: one email per person per event. The opposing side's representative holds a consent
    /// link, so the consent email is their email for this cancel request and they are left off this
    /// information notice; everyone else (the other parties, Intake Staff, the inbox) still gets it.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_TheConsentLinkHolder_IsLeftOffTheInformationNotice()
    {
        var rig = new Rig();
        rig.Office.Intake.Add("TEST-intake-1@test.local");
        rig.LinkHolders.ResolveEmailsAsync(Arg.Any<Guid>(), Arg.Any<Guid>())
            .Returns(Task.FromResult<IReadOnlyList<string>>(new[] { "TEST-AA@test.local" }));

        await rig.Build().HandleEventAsync(CancelSubmitted());

        var recipients = (IReadOnlyCollection<NotificationRecipient>)rig.Dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .ShouldHaveSingleItem().GetArguments().Skip(1).ToArray()[1]!;
        recipients.Select(r => r.Email).ShouldBe(new[] { "TEST-intake-1@test.local" });
    }

    [Fact]
    public async Task HandleEventAsync_TheSubmitterAndAppointmentAreHandedToTheLinkHolderLookup()
    {
        var rig = new Rig();
        var evt = CancelSubmitted();

        await rig.Build().HandleEventAsync(evt);

        await rig.LinkHolders.Received(1).ResolveEmailsAsync(evt.AppointmentId, evt.SubmittedByUserId);
    }
}
