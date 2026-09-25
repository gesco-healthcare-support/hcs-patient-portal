using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The missing-intake report (#944) against a real (SQLite) EF context: its queries run on the database, and --
/// the guarantee that matters -- neither the report nor the weekly job writes, changes or queues an outbox row.
///
/// <para>A negative guarantee needs the thing PRESENT: the office holds Pending, Sent and Failed intake rows
/// before the report runs (LOAD-BEARING -- against an empty outbox "nothing changed" would prove nothing), and
/// every field a write could move is compared before and after.</para>
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class EfCoreCaseTrackerMissingIntakeReporterTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private static readonly DateTime FirstRowAt = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private readonly ICurrentTenant _currentTenant;
    private readonly IIntegrationOutboxRepository _outboxRepository;

    public EfCoreCaseTrackerMissingIntakeReporterTests()
    {
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _outboxRepository = GetRequiredService<IIntegrationOutboxRepository>();
    }

    [Fact]
    public async Task TheReportAndTheWeeklyJob_ReadTheOffice_AndLeaveEveryOutboxRowExactlyAsItWas()
    {
        // Office B: the rig seeds the Approved appointment (Appointment2) there.
        var office = TenantsTestData.TenantBRef;
        await SeedIntakeRowsAsync(office);
        var hasIntake = await InOfficeAsync(office, () => _outboxRepository.HasIntakeAsync(AppointmentsTestData.Appointment2Id));
        var before = await SnapshotAsync(office);

        List<CaseTrackerMissingIntakeOffice> offices = [];
        await WithUnitOfWorkAsync(async () =>
            offices = await GetRequiredService<CaseTrackerMissingIntakeReporter>().BuildAsync());
        await WithUnitOfWorkAsync(() => GetRequiredService<CaseTrackerMissingIntakeReportJob>().ExecuteAsync());

        (await SnapshotAsync(office)).ShouldBe(before);

        var section = offices.Where(o => o.OfficeId == office).ShouldHaveSingleItem();
        section.Failed.ShouldBeFalse();
        section.FirstIntakeRowAt.ShouldBe(FirstRowAt);

        // The seeded Approved appointment is reported exactly when it has no intake row of its own.
        var listed = section.LikelyLost.Concat(section.Settling).Concat(section.BeforeIntegration)
            .Any(i => i.AppointmentId == AppointmentsTestData.Appointment2Id);
        listed.ShouldBe(!hasIntake);
    }

    /// <summary>Intake rows in three states, for appointments that are not the seeded ones.</summary>
    private Task SeedIntakeRowsAsync(Guid office) => InOfficeAsync(office, async () =>
    {
        var pending = NewIntake(office, FirstRowAt);
        var sent = NewIntake(office, FirstRowAt.AddDays(1));
        sent.MarkSent(FirstRowAt.AddDays(1));
        var failed = NewIntake(office, FirstRowAt.AddDays(2));
        failed.MarkFatal(FirstRowAt.AddDays(2), "Case Tracker responded 401.");

        await _outboxRepository.InsertManyAsync([pending, sent, failed], autoSave: true);
        return true;
    });

    private static IntegrationOutboxItem NewIntake(Guid office, DateTime createdAt)
    {
        var id = Guid.NewGuid();
        return new IntegrationOutboxItem(
            id, office, IntegrationMessageType.Intake, CaseTrackerEndpoints.Intake, Guid.NewGuid(),
            "{\"data\":{}}", "key-" + id.ToString("N"))
        {
            CreationTime = createdAt,
        };
    }

    /// <summary>Every field of every row in the office that a write could move, one line per row, by id.</summary>
    private Task<List<string>> SnapshotAsync(Guid office) => InOfficeAsync(office, async () =>
    {
        var rows = (await _outboxRepository.GetQueryableAsync()).OrderBy(r => r.Id).ToList();
        return rows.Select(r => string.Join('|',
            r.Id, r.Status, r.AttemptCount, r.MessageType, r.AppointmentId, r.IdempotencyKey, r.Payload,
            r.NextAttemptAt, r.LockedUntil, r.SentAt, r.FirstFailedAt, r.AlertedAt, r.EarlyWarnedAt,
            r.LastError, r.LastModificationTime, r.IsDeleted)).ToList();
    });

    private Task<T> InOfficeAsync<T>(Guid office, Func<Task<T>> work) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(office))
            {
                return await work();
            }
        });
}
