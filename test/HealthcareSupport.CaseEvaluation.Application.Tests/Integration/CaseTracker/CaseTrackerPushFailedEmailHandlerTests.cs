using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;

/// <summary>
/// Unit coverage for <see cref="CaseTrackerPushFailedEmailHandler"/>, the email that tells a staff
/// member Case Tracker pushes for their office have dead-lettered.
///
/// <para><b>WHAT IS PINNED.</b> It goes to that staff member only, links the HOST portal (where staff
/// work), and lists each failure by confirmation number -- or by appointment id when there is none.
/// When only some failures are listed, the email SAYS there are more, so the list never reads as the
/// whole story. With no staff address, nothing is sent.</para>
///
/// <para><b>The result asserted is the dispatched email</b>, read back from a substituted
/// <see cref="INotificationDispatcher"/>, so no mail can leave. The first Fact is the positive control
/// for the "sends nothing" Fact. Synthetic data only (HIPAA).</para>
/// </summary>
public class CaseTrackerPushFailedEmailHandlerTests
{
    private static CaseTrackerPushFailedEmailHandler Build(INotificationDispatcher dispatcher)
    {
        var urls = Substitute.For<IAccountUrlBuilder>();
        urls.BuildPortalRootUrlAsync(Arg.Any<Guid?>()).Returns("https://tenant-root.portal.test.local");
        urls.BuildPortalRootUrlAsync(null).Returns("https://admin.portal.test.local");
        return new CaseTrackerPushFailedEmailHandler(
            dispatcher,
            Substitute.For<ICurrentTenant>(),
            urls,
            NullLogger<CaseTrackerPushFailedEmailHandler>.Instance);
    }

    private static CaseTrackerPushFailureSummary Failure(string confirmation, int attempts = 5) => new()
    {
        AppointmentId = Guid.NewGuid(),
        ConfirmationNumber = confirmation,
        MessageType = "TEST-Intake",
        AttemptCount = attempts,
        LastError = "TEST-error: 503 from Case Tracker",
    };

    private static CaseTrackerPushFailedEto Alert(int failureCount, params CaseTrackerPushFailureSummary[] listed) => new()
    {
        TenantId = Guid.NewGuid(),
        OfficeName = "TEST-Office",
        StaffUserId = Guid.NewGuid(),
        StaffEmail = "TEST-staff@test.local",
        StaffFirstName = "TEST-Staffer",
        FailureCount = failureCount,
        Failures = listed.ToList(),
        OccurredAt = new DateTime(2026, 9, 23, 17, 0, 0, DateTimeKind.Utc),
    };

    private static (string Template, IReadOnlyCollection<NotificationRecipient> Recipients, IReadOnlyDictionary<string, object?> Variables) SingleSend(INotificationDispatcher dispatcher)
    {
        var args = dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .ShouldHaveSingleItem().GetArguments();
        return ((string)args[0]!, (IReadOnlyCollection<NotificationRecipient>)args[1]!, (IReadOnlyDictionary<string, object?>)args[2]!);
    }

    /// <summary>
    /// <b>POSITIVE CONTROL.</b> The alert goes to the staff member alone, on the push-failed template,
    /// linking the HOST portal.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_EmailsTheStaffMemberLinkingTheHostPortal_PositiveControl()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher).HandleEventAsync(Alert(1, Failure("TEST-CT0001")));

        var (template, recipients, variables) = SingleSend(dispatcher);
        template.ShouldBe(NotificationTemplateConsts.Codes.CaseTrackerPushFailed);
        var recipient = recipients.ShouldHaveSingleItem();
        recipient.Email.ShouldBe("TEST-staff@test.local");
        recipient.Role.ShouldBe(RecipientRole.OfficeAdmin);
        variables["PortalUrl"].ShouldBe("https://admin.portal.test.local");
        variables["FailureCount"].ShouldBe(1);
        variables["OfficeName"].ShouldBe("TEST-Office");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleEventAsync_NoStaffAddress_SendsNothing(string staffEmail)
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var alert = Alert(1, Failure("TEST-CT0002"));
        alert.StaffEmail = staffEmail;

        await Build(dispatcher).HandleEventAsync(alert);

        dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleEventAsync_NullEvent_SendsNothing()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher).HandleEventAsync(null!);

        dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    /// <summary>
    /// Each failure is listed by confirmation number; one without a confirmation number is listed by its
    /// appointment id instead, so no failure is anonymous.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_ListsEachFailureByConfirmationNumberElseAppointmentId()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var withoutNumber = Failure(string.Empty, attempts: 3);

        await Build(dispatcher).HandleEventAsync(Alert(2, Failure("TEST-CT0003"), withoutNumber));

        var list = (string)SingleSend(dispatcher).Variables["FailureList"]!;
        list.ShouldContain("TEST-CT0003  (TEST-Intake, attempt 5)  TEST-error: 503 from Case Tracker");
        list.ShouldContain($"{withoutNumber.AppointmentId:D}  (TEST-Intake, attempt 3)");
        list.ShouldNotContain("more. Open the portal");
    }

    /// <summary>
    /// When the alert lists fewer failures than the office has, the email says how many more there are.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_OnlySomeFailuresListed_SaysHowManyMoreThereAre()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher).HandleEventAsync(Alert(7, Failure("TEST-CT0004"), Failure("TEST-CT0005")));

        ((string)SingleSend(dispatcher).Variables["FailureList"]!)
            .ShouldEndWith("... and 5 more. Open the portal to see all of them.");
    }

    [Fact]
    public async Task HandleEventAsync_NoFailuresListed_SendsAnEmptyList()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher).HandleEventAsync(Alert(0));

        SingleSend(dispatcher).Variables["FailureList"].ShouldBe(string.Empty);
    }
}
