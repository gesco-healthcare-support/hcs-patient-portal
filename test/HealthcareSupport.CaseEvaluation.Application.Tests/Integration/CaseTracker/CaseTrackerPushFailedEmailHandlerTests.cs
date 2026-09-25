using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for <see cref="CaseTrackerPushFailedEmailHandler"/>, the email that tells a staff member
/// Case Tracker pushes for their office have dead-lettered or are still retrying.
///
/// <para><b>The template choice (#917).</b> The two emails ask staff for different things, so the
/// mapping must be exact and must refuse an unknown kind rather than guess.</para>
///
/// <para><b>The email.</b> It goes to that staff member only, links the HOST portal (where staff
/// work), and lists each failure by confirmation number -- or by appointment id when there is none.
/// When only some failures are listed, the email SAYS there are more, so the list never reads as the
/// whole story; only a dead-letter email points at the portal for the rest. With no staff address,
/// nothing is sent.</para>
///
/// <para>The dispatched email is read back from a substituted <see cref="INotificationDispatcher"/>,
/// so no mail can leave. Synthetic data only (HIPAA).</para>
/// </summary>
public class CaseTrackerPushFailedEmailHandlerTests
{
    [Theory]
    [InlineData(CaseTrackerPushAlertKind.DeadLettered, NotificationTemplateConsts.Codes.CaseTrackerPushFailed)]
    [InlineData(CaseTrackerPushAlertKind.StillRetrying, NotificationTemplateConsts.Codes.CaseTrackerPushRetrying)]
    public void EachAlertKind_UsesItsOwnTemplate(CaseTrackerPushAlertKind kind, string expectedCode)
    {
        CaseTrackerPushFailedEmailHandler.TemplateCodeFor(kind).ShouldBe(expectedCode);
    }

    [Fact]
    public void AnUnknownKind_Throws_RatherThanSendingTheWrongEmail()
    {
        Should.Throw<ArgumentOutOfRangeException>(
            () => CaseTrackerPushFailedEmailHandler.TemplateCodeFor((CaseTrackerPushAlertKind)99));
    }

    [Fact]
    public void AnEventBuiltWithoutAKind_IsADeadLetterAlert()
    {
        // Every event raised before #917 meant a dead letter; the default keeps that meaning.
        new CaseTrackerPushFailedEto().Kind.ShouldBe(CaseTrackerPushAlertKind.DeadLettered);
    }

    /// <summary>
    /// <b>POSITIVE CONTROL</b> for the "sends nothing" tests. The alert goes to the staff member alone,
    /// on the push-failed template, linking the HOST portal.
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
        list.ShouldNotContain("more.");
    }

    /// <summary>
    /// When a dead-letter alert lists fewer failures than the office has, the email says how many more
    /// there are and points at the portal, whose failures screen lists them all.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_ADeadLetterAlertListingOnlySome_SaysHowManyMore_AndPointsAtThePortal()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher).HandleEventAsync(Alert(7, Failure("TEST-CT0004"), Failure("TEST-CT0005")));

        ((string)SingleSend(dispatcher).Variables["FailureList"]!)
            .ShouldEndWith("... and 5 more. Open the portal to see all of them.");
    }

    /// <summary>
    /// A still-retrying alert also says how many more, but does NOT point at the portal: the failures
    /// screen lists nothing that is still retrying. The Fact above is its control.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_AStillRetryingAlertListingOnlySome_SaysHowManyMore_WithoutPointingAtThePortal()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var alert = Alert(7, Failure("TEST-CT0006"), Failure("TEST-CT0007"));
        alert.Kind = CaseTrackerPushAlertKind.StillRetrying;

        await Build(dispatcher).HandleEventAsync(alert);

        var (template, _, variables) = SingleSend(dispatcher);
        template.ShouldBe(NotificationTemplateConsts.Codes.CaseTrackerPushRetrying);
        var list = (string)variables["FailureList"]!;
        list.ShouldEndWith("... and 5 more.");
        list.ShouldNotContain("Open the portal");
    }

    [Fact]
    public async Task HandleEventAsync_NoFailuresListed_SendsAnEmptyList()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher).HandleEventAsync(Alert(0));

        SingleSend(dispatcher).Variables["FailureList"].ShouldBe(string.Empty);
    }

    // ------------------------------------------------------------------------

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
}
