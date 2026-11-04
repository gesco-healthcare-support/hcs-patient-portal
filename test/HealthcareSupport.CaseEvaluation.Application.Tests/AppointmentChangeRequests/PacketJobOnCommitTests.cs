using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Unit coverage for <see cref="PacketJobOnCommit"/> (2026-11-03): the packet job for a reschedule's
/// replacement appointment is enqueued only once the unit of work completes. The commit-timing proof on the
/// real rig is <c>MultiOfficeConsentCompletionTests</c> (a rolled-back finalize enqueues nothing); these pin
/// the branches the rig cannot reach: no ambient unit of work, and a disposed job store at completion.
/// Synthetic ids only.
/// </summary>
public class PacketJobOnCommitTests
{
    private static readonly Guid OfficeId = Guid.Parse("00000000-0000-4000-9000-0000000000a1");

    private static GenerateAppointmentPacketArgs Args() => new()
    {
        AppointmentId = Guid.Parse("00000000-0000-4000-9000-0000000000b2"),
        TenantId = OfficeId,
    };

    private static ICurrentTenant Tenant()
    {
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns(OfficeId);
        tenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(Substitute.For<IDisposable>());
        return tenant;
    }

    [Fact]
    public async Task With_no_unit_of_work_the_job_is_enqueued_at_once()
    {
        var uowManager = Substitute.For<IUnitOfWorkManager>();
        uowManager.Current.Returns((IUnitOfWork?)null);
        var jobs = Substitute.For<IBackgroundJobManager>();
        var args = Args();

        await PacketJobOnCommit.EnqueueAsync(uowManager, jobs, Tenant(), NullLogger.Instance, args);

        await jobs.Received(1).EnqueueAsync(args, Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task Inside_a_unit_of_work_the_job_waits_for_completion_and_runs_in_the_office_scope()
    {
        Func<Task>? onCompleted = null;
        var uow = Substitute.For<IUnitOfWork>();
        uow.When(u => u.OnCompleted(Arg.Any<Func<Task>>())).Do(c => onCompleted = c.Arg<Func<Task>>());
        var uowManager = Substitute.For<IUnitOfWorkManager>();
        uowManager.Current.Returns(uow);
        var jobs = Substitute.For<IBackgroundJobManager>();
        var tenant = Tenant();
        var args = Args();

        await PacketJobOnCommit.EnqueueAsync(uowManager, jobs, tenant, NullLogger.Instance, args);

        await jobs.DidNotReceiveWithAnyArgs().EnqueueAsync(default(GenerateAppointmentPacketArgs)!, default, default);
        onCompleted.ShouldNotBeNull();

        await onCompleted!();

        await jobs.Received(1).EnqueueAsync(args, Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
        tenant.Received(1).Change(OfficeId, Arg.Any<string?>());
    }

    [Fact]
    public async Task A_disposed_job_store_at_completion_is_logged_not_thrown()
    {
        Func<Task>? onCompleted = null;
        var uow = Substitute.For<IUnitOfWork>();
        uow.When(u => u.OnCompleted(Arg.Any<Func<Task>>())).Do(c => onCompleted = c.Arg<Func<Task>>());
        var uowManager = Substitute.For<IUnitOfWorkManager>();
        uowManager.Current.Returns(uow);
        var jobs = Substitute.For<IBackgroundJobManager>();
        jobs.EnqueueAsync(Arg.Any<GenerateAppointmentPacketArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>())
            .Returns<Task<string>>(_ => throw new ObjectDisposedException("TEST-job-store"));

        await PacketJobOnCommit.EnqueueAsync(uowManager, jobs, Tenant(), NullLogger.Instance, Args());

        await Should.NotThrowAsync(() => onCompleted!());
        await jobs.Received(1).EnqueueAsync(Arg.Any<GenerateAppointmentPacketArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
    }
}
