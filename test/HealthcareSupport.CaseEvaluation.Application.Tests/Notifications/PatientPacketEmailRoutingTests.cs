using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Unit coverage for WHO receives the patient packet email (<see cref="PatientPacketEmailHandler"/>).
/// <c>PatientPacketEmailKindGateTests</c> pins the packet-kind gate; these pin the routing behind it.
///
/// <para><b>WHAT IS PINNED.</b> The packet goes to the patient; when the patient has no email it goes
/// to the applicant attorney instead ("all the communication for the patient is sent to the AA");
/// with neither address, or no appointment, nothing is sent.</para>
///
/// <para><b>The result asserted is the dispatched recipient and template</b>, read back from a
/// substituted <see cref="INotificationDispatcher"/>, so no mail can leave. The first Fact is the
/// positive control for the two "sends nothing" Facts. Synthetic data only (HIPAA).</para>
/// </summary>
public class PatientPacketEmailRoutingTests
{
    private static DocumentEmailContext Context(string? patientEmail, string? applicantAttorneyEmail) => new()
    {
        AppointmentId = Guid.NewGuid(),
        RequestConfirmationNumber = "TEST-PP0001",
        AppointmentDate = new DateTime(2026, 10, 12, 9, 0, 0),
        PatientFirstName = "TEST-First",
        PatientLastName = "TEST-Last",
        PatientEmail = patientEmail,
        ApplicantAttorneyEmail = applicantAttorneyEmail,
        PortalBaseUrl = "https://tenant.portal.test.local",
    };

    /// <summary>
    /// The ten nulls are never dereferenced: <c>ResolveAsync</c> is virtual and configured here.
    /// </summary>
    private static PatientPacketEmailHandler Build(INotificationDispatcher dispatcher, DocumentEmailContext? ctx)
    {
        var resolver = Substitute.For<DocumentEmailContextResolver>(null, null, null, null, null, null, null, null, null, null, null);
        resolver.ResolveAsync(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(ctx);
        return new PatientPacketEmailHandler(
            dispatcher,
            resolver,
            Substitute.For<ICurrentTenant>(),
            NullLogger<PatientPacketEmailHandler>.Instance,
            Substitute.For<IAccountUrlBuilder>());
    }

    private static PacketGeneratedEto PatientPacket() => new()
    {
        AppointmentId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        PacketId = Guid.NewGuid(),
        Kind = PacketKind.Patient,
        OccurredAt = new DateTime(2026, 9, 23, 17, 0, 0, DateTimeKind.Utc),
    };

    private static (string Template, IReadOnlyCollection<NotificationRecipient> Recipients) SingleDispatch(INotificationDispatcher dispatcher)
    {
        var calls = dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .ToList();
        calls.Count.ShouldBe(1, "one patient packet sends exactly one email");
        var args = calls[0].GetArguments().Skip(1).ToArray();
        return ((string)args[0]!, (IReadOnlyCollection<NotificationRecipient>)args[1]!);
    }

    /// <summary><b>POSITIVE CONTROL.</b> A patient with an email receives their own packet.</summary>
    [Fact]
    public async Task HandleEventAsync_PatientHasAnEmail_SendsThePacketToThePatient_PositiveControl()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher, Context("TEST-patient@test.local", "TEST-aa@test.local")).HandleEventAsync(PatientPacket());

        var (template, recipients) = SingleDispatch(dispatcher);
        template.ShouldBe(NotificationTemplateConsts.Codes.AppointmentDocumentAddWithAttachment);
        recipients.ShouldHaveSingleItem().Email.ShouldBe("TEST-patient@test.local");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task HandleEventAsync_PatientHasNoEmail_SendsThePacketToTheApplicantAttorney(string? patientEmail)
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher, Context(patientEmail, "TEST-aa@test.local")).HandleEventAsync(PatientPacket());

        SingleDispatch(dispatcher).Recipients.ShouldHaveSingleItem().Email.ShouldBe("TEST-aa@test.local");
    }

    // 2026-11-04 (office decision 2): with the applicant attorney's mail redirected, the context hides the
    // lawyer's address, so a patient with no email has their packet sent to the applicant paralegal(s).
    private static CaseFollower Follower(string email, params string[] roles) =>
        new(email, Guid.NewGuid(), IsBooker: false, IsPatientRole: false, Role: null, RoleNames: roles);

    [Fact]
    public async Task HandleEventAsync_NoPatientEmail_SideARedirect_SendsThePacketToTheApplicantParalegals()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var ctx = Context(null, null);
        ctx.RedirectSide = AppointmentChangeRequests.ChangeRequestSide.SideA;
        ctx.Followers = new[]
        {
            Follower("TEST-ap-1@test.local", "Applicant Paralegal"),
            Follower("test-ap-1@TEST.local", "Applicant Paralegal"),
            Follower("TEST-ap-2@test.local", "Applicant Paralegal"),
            Follower("TEST-dp@test.local", "Defense Paralegal"),
            Follower("TEST-aa-accessor@test.local", "Applicant Attorney"),
        };

        await Build(dispatcher, ctx).HandleEventAsync(PatientPacket());

        SingleDispatch(dispatcher).Recipients.Select(r => r.Email)
            .ShouldBe(new[] { "TEST-ap-1@test.local", "TEST-ap-2@test.local" }, ignoreOrder: true);
    }

    [Fact]
    public async Task HandleEventAsync_NoPatientEmail_SideBRedirect_ChangesNothing()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var ctx = Context(null, "TEST-aa@test.local");
        ctx.RedirectSide = AppointmentChangeRequests.ChangeRequestSide.SideB;
        ctx.Followers = new[] { Follower("TEST-ap-1@test.local", "Applicant Paralegal") };

        await Build(dispatcher, ctx).HandleEventAsync(PatientPacket());

        SingleDispatch(dispatcher).Recipients.ShouldHaveSingleItem().Email.ShouldBe("TEST-aa@test.local");
    }

    [Fact]
    public async Task HandleEventAsync_NoPatientEmail_SideARedirect_NoParalegal_SendsNothing()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var ctx = Context(null, null);
        ctx.RedirectSide = AppointmentChangeRequests.ChangeRequestSide.SideA;

        await Build(dispatcher, ctx).HandleEventAsync(PatientPacket());

        dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleEventAsync_NeitherPatientNorAttorneyHasAnEmail_SendsNothing()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher, Context(null, "  ")).HandleEventAsync(PatientPacket());

        dispatcher.ReceivedCalls().ShouldBeEmpty("with no address at all there is nobody to send the packet to");
    }

    [Fact]
    public async Task HandleEventAsync_AppointmentNotFound_SendsNothing()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher, ctx: null).HandleEventAsync(PatientPacket());

        dispatcher.ReceivedCalls().ShouldBeEmpty("with no appointment context the handler must skip, not throw");
    }
}
