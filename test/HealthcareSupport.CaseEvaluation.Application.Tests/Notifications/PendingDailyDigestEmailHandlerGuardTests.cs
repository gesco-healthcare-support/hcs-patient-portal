using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Handlers;

/// <summary>
/// Unit coverage for the skip guards of <see cref="PendingDailyDigestEmailHandler"/>, which turns the
/// daily pending-request digest into one email to the office's intake inbox.
///
/// <para><b>WHAT IS PINNED.</b> A null or empty digest sends nothing, and so does an office with no
/// intake inbox configured; otherwise one email goes to that inbox only. The result asserted is the
/// dispatch, read back from a substituted <see cref="INotificationDispatcher"/> -- no mail can leave.
/// The first Fact is the positive control for the "sends nothing" Facts. Synthetic data only (HIPAA).</para>
/// </summary>
public class PendingDailyDigestEmailHandlerGuardTests
{
    private static PendingDailyDigestEmailHandler Build(INotificationDispatcher dispatcher, string? officeEmail)
    {
        var settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.OfficeEmail).Returns(officeEmail);
        var systemParameters = Substitute.For<ISystemParameterRepository>();
        systemParameters.GetCurrentTenantAsync(Arg.Any<CancellationToken>()).Returns((SystemParameter?)null);
        return new PendingDailyDigestEmailHandler(
            dispatcher,
            settings,
            systemParameters,
            Substitute.For<ICurrentTenant>(),
            NullLogger<PendingDailyDigestEmailHandler>.Instance,
            Substitute.For<IAccountUrlBuilder>());
    }

    private static PendingDailyDigestEto Digest(int rowCount) => new()
    {
        TenantId = Guid.NewGuid(),
        OccurredAt = new DateTime(2026, 9, 23, 16, 0, 0, DateTimeKind.Utc),
        Rows = Enumerable.Range(1, rowCount).Select(i => new PendingDailyDigestRow
        {
            RequestConfirmationNumber = $"TEST-DG000{i}",
            PatientName = "TEST-First TEST-Last",
            AppointmentDate = new DateTime(2026, 10, 1 + i),
            RequestedAt = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc),
        }).ToList(),
    };

    /// <summary><b>POSITIVE CONTROL.</b> A digest with a row is emailed to the intake inbox only.</summary>
    [Fact]
    public async Task HandleEventAsync_ADigestWithRows_IsEmailedToTheIntakeInbox_PositiveControl()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher, "TEST-intake@test.local").HandleEventAsync(Digest(1));

        var args = dispatcher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(INotificationDispatcher.DispatchAsync))
            .ShouldHaveSingleItem().GetArguments();
        args[0].ShouldBe(NotificationTemplateConsts.Codes.PendingAppointmentDailyNotification);
        var recipient = ((IReadOnlyCollection<NotificationRecipient>)args[1]!).ShouldHaveSingleItem();
        recipient.Email.ShouldBe("TEST-intake@test.local");
        recipient.Role.ShouldBe(RecipientRole.OfficeAdmin);
    }

    [Fact]
    public async Task HandleEventAsync_NullOrEmptyDigest_SendsNothing()
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var handler = Build(dispatcher, "TEST-intake@test.local");

        await handler.HandleEventAsync(null!);
        await handler.HandleEventAsync(Digest(0));

        dispatcher.ReceivedCalls().ShouldBeEmpty("an empty digest must not produce an empty email");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleEventAsync_NoIntakeInboxConfigured_SendsNothing(string? officeEmail)
    {
        var dispatcher = Substitute.For<INotificationDispatcher>();

        await Build(dispatcher, officeEmail).HandleEventAsync(Digest(2));

        dispatcher.ReceivedCalls().ShouldBeEmpty("with no intake inbox configured there is nobody to send the digest to");
    }
}
