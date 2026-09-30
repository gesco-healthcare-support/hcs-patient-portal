using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
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
/// The weekly missing-intake email (#944): one host-scope email to the technical list, dated so each week's
/// repeat is sent, loud when nobody can receive it, and carrying ids and confirmation numbers only. Synthetic data.
/// </summary>
public class CaseTrackerMissingIntakesEmailHandlerTests
{
    private static readonly DateTime Monday = new(2026, 9, 28, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");

    private static (CaseTrackerMissingIntakesEmailHandler Handler, INotificationDispatcher Dispatcher, ICurrentTenant Tenant) Build(
        string? recipients)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [CaseTrackerFeedConsts.AlertRecipientsConfigurationKey] = recipients,
            })
            .Build();
        var dispatcher = Substitute.For<INotificationDispatcher>();
        var tenant = Substitute.For<ICurrentTenant>();
        var handler = new CaseTrackerMissingIntakesEmailHandler(
            dispatcher, tenant, configuration, NullLogger<CaseTrackerMissingIntakesEmailHandler>.Instance);
        return (handler, dispatcher, tenant);
    }

    private static CaseTrackerMissingIntakesEto Report(DateTime runAt) => new()
    {
        RunAt = runAt,
        Offices =
        [
            new CaseTrackerMissingIntakesOfficeEto
            {
                TenantId = new Guid("0ff1ce00-0000-4000-8000-000000000001"),
                OfficeName = "Sample Medical Group",
                FirstIntakeRowAt = new DateTime(2026, 7, 30, 18, 0, 0, DateTimeKind.Utc),
                Items =
                [
                    new CaseTrackerMissingIntakeLineEto
                    {
                        AppointmentId = AppointmentId,
                        ConfirmationNumber = "A00104",
                        Status = AppointmentStatusType.Approved,
                        ApprovedAt = new DateTime(2026, 8, 4, 18, 0, 0, DateTimeKind.Utc),
                    },
                ],
            },
        ],
        FailedOfficeNames = ["Unreadable Office"],
    };

    [Fact]
    public async Task ItSendsOneHostScopeEmail_ToEveryConfiguredAddress_WithTheMissingIntakeTemplate()
    {
        var (handler, dispatcher, tenant) = Build("ops@example.test; dev@example.test, ops@example.test");

        await handler.HandleEventAsync(Report(Monday));

        tenant.Received(1).Change(null, Arg.Any<string?>());
        await dispatcher.Received(1).DispatchAsync(
            NotificationTemplateConsts.Codes.CaseTrackerMissingIntakes,
            Arg.Is<IReadOnlyCollection<NotificationRecipient>>(r => r.Count == 2 && r.All(x => !x.IsRegistered)),
            Arg.Is<IReadOnlyDictionary<string, object?>>(v => (string)v["MissingIntakeCount"]! == "1"),
            "CaseTrackerMissingIntakes/2026-09-28",
            Arg.Any<PacketAttachmentRef?>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ; , ")]
    public async Task WithNoRecipientsConfigured_NothingIsSent(string? recipients)
    {
        var (handler, dispatcher, _) = Build(recipients);

        await handler.HandleEventAsync(Report(Monday));

        await dispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(
            default!, default!, default!, default!, default, default);
    }

    [Fact]
    public void TheContextTag_ChangesWeekToWeek_SoARepeatIsNotSwallowedByTheOutbox()
    {
        var thisWeek = CaseTrackerMissingIntakesEmailHandler.ContextTagFor(Report(Monday));
        var nextWeek = CaseTrackerMissingIntakesEmailHandler.ContextTagFor(Report(Monday.AddDays(7)));

        thisWeek.ShouldBe("CaseTrackerMissingIntakes/2026-09-28");
        nextWeek.ShouldNotBe(thisWeek);
    }

    [Fact]
    public void TheDetail_NamesTheOfficeTheAppointmentAndTheUnreadableOffice_AndNothingElse()
    {
        var detail = CaseTrackerMissingIntakesEmailHandler.DetailFor(Report(Monday));

        detail.ShouldContain("Sample Medical Group (first intake row 2026-07-30 18:00:00Z)");
        detail.ShouldContain("A00104  Approved  approved 2026-08-04 18:00:00Z  appointment " + AppointmentId.ToString("D"));
        detail.ShouldContain("Nothing has been queued.");
        detail.ShouldContain("could not be read this run: Unreadable Office.");
    }
}
