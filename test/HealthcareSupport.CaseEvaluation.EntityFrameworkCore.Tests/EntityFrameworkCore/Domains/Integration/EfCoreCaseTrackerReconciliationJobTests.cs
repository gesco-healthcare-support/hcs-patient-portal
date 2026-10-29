using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// <see cref="CaseTrackerReconciliationJob"/>'s stalled-set query on the real rig. The Domain tests pin
/// which sets are chosen, against in-memory lists; only a real provider shows the grouped query
/// (2026-10-29) translates rather than throwing or quietly evaluating on the client. The publisher is
/// substituted, so nothing is pushed. Synthetic data only (HIPAA).
/// </summary>
public class EfCoreCaseTrackerReconciliationJobTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly CaseTrackerPacketPublishService _publisher =
        Substitute.For<CaseTrackerPacketPublishService>(null, null, null, null, null);

    private readonly IBackgroundJobManager _jobs = Substitute.For<IBackgroundJobManager>();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _publisher.PublishSettledPacketsAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>()).Returns(true);
        services.Replace(ServiceDescriptor.Transient(_ => _publisher));
        services.Replace(ServiceDescriptor.Singleton(_jobs));
    }

    [Fact]
    public async Task AStalledSet_IsReleasedThroughTheTranslatedQuery()
    {
        // Appointment2 is seeded Approved in office B: one packet rendered, one failed, both idle
        // for two hours -- well past the 30-minute settle cutoff.
        await SeedPacketsAsync(TenantsTestData.TenantBRef, AppointmentsTestData.Appointment2Id, DateTime.UtcNow.AddHours(-2));

        await WithUnitOfWorkAsync(() => GetRequiredService<CaseTrackerReconciliationJob>().ExecuteAsync());

        PublishedIds().ShouldBe(new[] { AppointmentsTestData.Appointment2Id });
    }

    [Fact]
    public async Task ASetThatChangedRecently_IsNotReleased()
    {
        // Positive control: AStalledSet_IsReleasedThroughTheTranslatedQuery, identical but idle.
        await SeedPacketsAsync(TenantsTestData.TenantBRef, AppointmentsTestData.Appointment2Id, DateTime.UtcNow.AddMinutes(-5));

        await WithUnitOfWorkAsync(() => GetRequiredService<CaseTrackerReconciliationJob>().ExecuteAsync());

        PublishedIds().ShouldBeEmpty();
    }

    private Guid[] PublishedIds() =>
        _publisher.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(CaseTrackerPacketPublishService.PublishSettledPacketsAsync))
            .Select(c => ((Appointment)c.GetArguments()[0]!).Id)
            .ToArray();

    private Task SeedPacketsAsync(Guid officeId, Guid appointmentId, DateTime lastChangedAt) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(officeId))
            {
                var repository = GetRequiredService<IRepository<AppointmentPacket, Guid>>();
                foreach (var (kind, status) in new[]
                {
                    (PacketKind.Patient, PacketGenerationStatus.Generated),
                    (PacketKind.Doctor, PacketGenerationStatus.Failed),
                })
                {
                    var packet = new AppointmentPacket(Guid.NewGuid(), officeId, appointmentId, kind, "TEST-blob", status)
                    {
                        LastModificationTime = lastChangedAt,
                    };
                    await repository.InsertAsync(packet, autoSave: true);
                }
            }
        });
}
