using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for <see cref="CaseTrackerDrainKickJob"/> (#917): every office gets a drain enqueued, and
/// one office that cannot be enqueued does not stop the rest. All fixture data is synthetic.
/// </summary>
public class CaseTrackerDrainKickJobTests
{
    private static readonly Guid OfficeA = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid OfficeB = new("5d1f0c3e-7a2b-4c6d-9e8f-0a1b2c3d4e5f");

    private static (CaseTrackerDrainKickJob Job, IBackgroundJobManager Jobs) Build()
    {
        var runner = Substitute.For<ITenantWorkRunner>();
        runner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>())
            .Returns(async ci =>
            {
                var work = ci.Arg<Func<Guid, Task>>();
                await work(OfficeA);
                await work(OfficeB);
            });

        var jobs = Substitute.For<IBackgroundJobManager>();
        return (new CaseTrackerDrainKickJob(runner, jobs, NullLogger<CaseTrackerDrainKickJob>.Instance), jobs);
    }

    [Fact]
    public async Task EveryOfficeGetsADrainEnqueued()
    {
        var (job, jobs) = Build();

        await job.ExecuteAsync();

        await jobs.Received(1).EnqueueAsync(
            Arg.Is<IntegrationOutboxDrainArgs>(a => a.TenantId == OfficeA),
            Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
        await jobs.Received(1).EnqueueAsync(
            Arg.Is<IntegrationOutboxDrainArgs>(a => a.TenantId == OfficeB),
            Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task AnOfficeThatCannotBeEnqueued_DoesNotStopTheRest()
    {
        // ForEachOfficeAsync abandons the whole run if a delegate throws.
        var (job, jobs) = Build();
        jobs.EnqueueAsync(
                Arg.Is<IntegrationOutboxDrainArgs>(a => a.TenantId == OfficeA),
                Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>())
            .ThrowsAsync(new InvalidOperationException("queue unavailable"));

        await Should.NotThrowAsync(job.ExecuteAsync());

        await jobs.Received(1).EnqueueAsync(
            Arg.Is<IntegrationOutboxDrainArgs>(a => a.TenantId == OfficeB),
            Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public void RunsEveryFiveMinutes()
    {
        // The shortest retry wait is 5 minutes; a slower kick would stretch every wait to the kick.
        CaseTrackerDrainKickJob.CronExpression.ShouldBe("*/5 * * * *");
    }
}
