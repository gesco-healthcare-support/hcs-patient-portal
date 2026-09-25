using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The feed alert email (#927): who receives it, that an empty list is loud rather than silent, and what each
/// kind says. Addresses are synthetic.
/// </summary>
public class CaseTrackerFeedAlertEmailHandlerTests
{
    private static readonly Guid OfficeId = new("0ff1ce00-0000-4000-8000-000000000001");
    private static readonly DateTime Now = new(2026, 9, 24, 18, 0, 0, DateTimeKind.Utc);

    private static (CaseTrackerFeedAlertEmailHandler Handler, INotificationDispatcher Dispatcher) Build(string? recipients)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CaseTrackerFeedConsts.AlertRecipientsConfigurationKey] = recipients,
            })
            .Build();
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var handler = new CaseTrackerFeedAlertEmailHandler(
            dispatcher, Substitute.For<ICurrentTenant>(), configuration, NullLogger<CaseTrackerFeedAlertEmailHandler>.Instance);
        return (handler, dispatcher);
    }

    private static CaseTrackerFeedAlertEto Alert(CaseTrackerFeedAlertKind kind) => new()
    {
        Kind = kind,
        TenantId = OfficeId,
        OfficeName = "Sample Medical Group",
        OccurredAt = Now,
        OutstandingCount = 7,
        Cursor = "00000000000007D3",
        AppointmentId = new Guid("ada5e3c5-0034-ebde-253c-3a2293631dee"),
        MessageType = nameof(IntegrationMessageType.Intake),
    };

    [Fact]
    public async Task ItEmailsEveryConfiguredAddress_WithTheFeedTemplate_InOneDispatch()
    {
        var (handler, dispatcher) = Build("ops@example.test; dev@example.test, ops@example.test");

        await handler.HandleEventAsync(Alert(CaseTrackerFeedAlertKind.SilenceStarted));

        await dispatcher.Received(1).DispatchAsync(
            NotificationTemplateConsts.Codes.CaseTrackerFeedAlert,
            Arg.Is<IReadOnlyCollection<NotificationRecipient>>(r =>
                r.Count == 2 && r.All(x => !x.IsRegistered)),
            Arg.Is<IReadOnlyDictionary<string, object?>>(v =>
                (string)v["OfficeName"]! == "Sample Medical Group" && (string)v["FeedAlertTitle"]! == "no requests"),
            Arg.Any<string>(),
            Arg.Any<PacketAttachmentRef?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ; , ")]
    public async Task WithNoRecipientsConfigured_NothingIsDispatched(string? recipients)
    {
        // Logged as an error instead; an alert nobody receives must not look like one that was sent.
        var (handler, dispatcher) = Build(recipients);

        await handler.HandleEventAsync(Alert(CaseTrackerFeedAlertKind.StallStarted));

        await dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(default!, default!, default!, default!);
    }

    [Fact]
    public void ParseRecipients_SplitsOnSemicolonsAndCommas_TrimsAndDropsRepeats()
    {
        CaseTrackerFeedAlertEmailHandler.ParseRecipients(" a@example.test;b@example.test , A@example.test ;; ")
            .ShouldBe(new[] { "a@example.test", "b@example.test" });
    }

    [Theory]
    [InlineData(CaseTrackerFeedAlertKind.SilenceStarted, "no requests")]
    [InlineData(CaseTrackerFeedAlertKind.SilenceCleared, "requests arriving again")]
    [InlineData(CaseTrackerFeedAlertKind.StallStarted, "position not advancing")]
    [InlineData(CaseTrackerFeedAlertKind.StallCleared, "position advancing again")]
    [InlineData(CaseTrackerFeedAlertKind.CursorAhead, "cursor refused")]
    [InlineData(CaseTrackerFeedAlertKind.SkipReported, "row skipped")]
    public void EachKind_HasItsOwnHeadline(CaseTrackerFeedAlertKind kind, string expected)
    {
        CaseTrackerFeedAlertEmailHandler.TitleFor(kind).ShouldBe(expected);
    }

    [Fact]
    public void AnUnknownKind_Throws_RatherThanMailingAMisleadingHeadline()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => CaseTrackerFeedAlertEmailHandler.TitleFor((CaseTrackerFeedAlertKind)99));
    }

    [Fact]
    public void TheStallDetail_NamesAHeldTransactionAsAPossibleCause()
    {
        // Research 3.2: a long-running or abandoned transaction pins MIN_ACTIVE_ROWVERSION() and stalls the
        // feed with a cause neither side would otherwise guess.
        var detail = CaseTrackerFeedAlertEmailHandler.DetailFor(Alert(CaseTrackerFeedAlertKind.StallStarted));

        detail.ShouldContain("7 change(s) are waiting");
        detail.ShouldContain("transaction");
    }

    [Fact]
    public void TheSkipDetail_NamesTheRowByAppointmentTypeAndCursor()
    {
        var detail = CaseTrackerFeedAlertEmailHandler.DetailFor(Alert(CaseTrackerFeedAlertKind.SkipReported));

        detail.ShouldContain("ada5e3c5-0034-ebde-253c-3a2293631dee");
        detail.ShouldContain("Intake");
        detail.ShouldContain("00000000000007D3");
    }
}
