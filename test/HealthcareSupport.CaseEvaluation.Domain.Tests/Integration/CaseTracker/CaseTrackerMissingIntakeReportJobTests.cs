using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The weekly missing-intake job (#944): silent when nothing is likely lost, one event when something is, and the
/// event carries ids, confirmation numbers, statuses and dates only. Synthetic data.
/// </summary>
public class CaseTrackerMissingIntakeReportJobTests
{
    private static readonly DateTime Now = new(2026, 9, 28, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FirstRowAt = new(2026, 7, 30, 18, 0, 0, DateTimeKind.Utc);

    /// <summary>A reporter whose BuildAsync returns exactly <paramref name="offices"/>.</summary>
    private static CaseTrackerMissingIntakeReporter ReporterReturning(List<CaseTrackerMissingIntakeOffice> offices)
    {
        var reporter = Substitute.For<CaseTrackerMissingIntakeReporter>(
            Substitute.For<ITenantWorkRunner>(),
            Substitute.For<ITenantStore>(),
            Substitute.For<IRepository<Appointment, Guid>>(),
            Substitute.For<IRepository<AppointmentPacket, Guid>>(),
            Substitute.For<IIntegrationOutboxRepository>(),
            Substitute.For<IClock>(),
            NullLogger<CaseTrackerMissingIntakeReporter>.Instance);
        reporter.BuildAsync(Arg.Any<CancellationToken>()).Returns(offices);
        return reporter;
    }

    private static (CaseTrackerMissingIntakeReportJob Job, ILocalEventBus Bus) Build(List<CaseTrackerMissingIntakeOffice> offices)
    {
        var bus = Substitute.For<ILocalEventBus>();
        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);
        return (
            new CaseTrackerMissingIntakeReportJob(
                ReporterReturning(offices), bus, clock, NullLogger<CaseTrackerMissingIntakeReportJob>.Instance),
            bus);
    }

    private static CaseTrackerMissingIntakeItem Item(string confirmation) => new()
    {
        AppointmentId = Guid.NewGuid(),
        ConfirmationNumber = confirmation,
        Status = AppointmentStatusType.Approved,
        ApprovedAt = FirstRowAt.AddDays(5),
    };

    [Fact]
    public async Task WhenNothingIsLikelyLost_NoEventIsRaised_EvenWithOldHistoryAndSettlingRows()
    {
        var (job, bus) = Build(
        [
            new CaseTrackerMissingIntakeOffice
            {
                OfficeId = Guid.NewGuid(),
                OfficeName = "Quiet Office",
                FirstIntakeRowAt = FirstRowAt,
                Settling = [Item("A00200")],
                BeforeIntegration = [Item("A00001")],
                BeforeIntegrationCount = 1,
            },
        ]);

        await job.ExecuteAsync();

        await bus.DidNotReceiveWithAnyArgs().PublishAsync(default(CaseTrackerMissingIntakesEto)!);
    }

    [Fact]
    public async Task WhenAnOfficeHasALikelyLostIntake_OneEventListsOnlyThoseOffices_AndNamesTheUnreadableOnes()
    {
        var lostOffice = Guid.NewGuid();
        var (job, bus) = Build(
        [
            new CaseTrackerMissingIntakeOffice
            {
                OfficeId = lostOffice,
                OfficeName = "Lossy Office",
                FirstIntakeRowAt = FirstRowAt,
                LikelyLost = [Item("A00104"), Item("A00105")],
            },
            new CaseTrackerMissingIntakeOffice { OfficeId = Guid.NewGuid(), OfficeName = "Quiet Office", FirstIntakeRowAt = FirstRowAt },
            new CaseTrackerMissingIntakeOffice { OfficeId = Guid.NewGuid(), OfficeName = "Unreadable Office", Failed = true },
        ]);

        await job.ExecuteAsync();

        await bus.Received(1).PublishAsync(Arg.Is<CaseTrackerMissingIntakesEto>(e =>
            e.RunAt == Now
            && e.Offices.Count == 1
            && e.Offices[0].TenantId == lostOffice
            && e.Offices[0].OfficeName == "Lossy Office"
            && e.Offices[0].FirstIntakeRowAt == FirstRowAt
            && e.Offices[0].Items.Count == 2
            && e.Offices[0].Items[0].ConfirmationNumber == "A00104"
            && e.FailedOfficeNames.Count == 1
            && e.FailedOfficeNames[0] == "Unreadable Office"));
    }
}
