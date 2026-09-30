using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// T11 test for the reconciliation sweep's per-office isolation: because
/// <see cref="ITenantWorkRunner.ForEachOfficeAsync"/> aborts the whole run when a
/// delegate throws, the job must swallow one office's failure and still reconcile
/// the rest.
/// </summary>
public class ApprovalReconciliationJobTests
{
    private static readonly Guid OfficeA = new("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OfficeB = new("bbbbbbbb-0000-0000-0000-000000000002");

    [Fact]
    public async Task ExecuteAsync_WhenOneOfficeThrows_StillReconcilesTheOthers()
    {
        var tenantRunner = Substitute.For<ITenantWorkRunner>();
        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        var packetRepo = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        var jobs = Substitute.For<IBackgroundJobManager>();

        // Office A blows up on its appointment query; office B returns none (so it
        // reaches the outbox-drain enqueue cleanly).
        appointmentRepo.GetQueryableAsync().Returns(
            _ => throw new InvalidOperationException("office A DB error"),
            _ => new List<Appointment>().AsQueryable());

        // ForEachOfficeAsync runs the delegate for A then B (it propagates, so the
        // job's own try/catch is what must keep B alive after A throws).
        tenantRunner
            .ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>())
            .Returns(ci => RunOffices(ci.Arg<Func<Guid, Task>>()));

        var job = new ApprovalReconciliationJob(
            tenantRunner, appointmentRepo, packetRepo, jobs,
            NullLogger<ApprovalReconciliationJob>.Instance);

        // Must not throw despite office A failing.
        await job.ExecuteAsync();

        // Both offices were attempted (A threw in the query, B queried empty).
        await appointmentRepo.Received(2).GetQueryableAsync();
        // Office B reached the drain; office A did not.
        await jobs.Received(1).EnqueueAsync(
            Arg.Is<OutboxDrainArgs>(a => a.TenantId == OfficeB),
            Arg.Any<BackgroundJobPriority>(),
            Arg.Any<TimeSpan?>());
        await jobs.DidNotReceive().EnqueueAsync(
            Arg.Is<OutboxDrainArgs>(a => a.TenantId == OfficeA),
            Arg.Any<BackgroundJobPriority>(),
            Arg.Any<TimeSpan?>());
    }

    private static async Task RunOffices(Func<Guid, Task> work)
    {
        await work(OfficeA);
        await work(OfficeB);
    }
}
