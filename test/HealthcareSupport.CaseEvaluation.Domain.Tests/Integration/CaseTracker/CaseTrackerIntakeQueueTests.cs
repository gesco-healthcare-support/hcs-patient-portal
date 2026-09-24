using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Guids;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for <see cref="CaseTrackerIntakeQueue"/>'s half of the #931 ordering guarantee.
///
/// <para>The intake builds its payload from the current document list and only then writes its row.
/// A document accepted in that gap would be suppressed by the document gate (no intake row yet) and
/// be missing from the payload. The per-appointment lock closes the gap only if the intake takes it
/// BEFORE it reads; <c>CaseTrackerDocumentQueueTests</c> pins the other half.</para>
///
/// <para>The lock itself is a SQL Server application lock, a no-op on the SQLite test database, so
/// these tests prove ORDER, not blocking. All fixture data is synthetic.</para>
/// </summary>
public class CaseTrackerIntakeQueueTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("3c9e1f47-8a2d-4b6e-9f05-7d1a2c4e6b83");

    private sealed class Harness
    {
        public CaseTrackerIntakeQueue Queue { get; init; } = null!;
        public IIntegrationOutboxRepository Repository { get; init; } = null!;
        public IIntakePayloadBuilder PayloadBuilder { get; init; } = null!;
        public List<IntegrationOutboxItem> Rows { get; init; } = null!;
    }

    private static IntakeEnvelope Envelope() => new()
    {
        Data = new IntakePayload
        {
            AppointmentId = AppointmentId,
            ConfirmationNumber = "A00123",
            UpdatedAt = "2026-09-01T00:00:00Z",
            Patient = new IntakePatientSection
            {
                FirstName = "Testadora",
                LastName = "Synthetica",
                Street = "1200 Sample Street",
                City = "Sample City",
                ZipCode = "90210",
            },
        },
        Meta = new IntakeMeta
        {
            RequestId = new Guid("7e2b4c9a-1d3f-4a58-b6c0-2e9f8a7d5c41"),
            Timestamp = "2026-09-01T00:00:00.0000000Z",
        },
    };

    private static Harness Build()
    {
        var rows = new List<IntegrationOutboxItem>();
        var repo = Substitute.For<IIntegrationOutboxRepository>();
        repo.GetQueryableAsync().Returns(_ => rows.AsQueryable());
        repo.InsertAsync(Arg.Any<IntegrationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var item = ci.Arg<IntegrationOutboxItem>();
                rows.Add(item);
                return Task.FromResult(item);
            });

        var payloadBuilder = Substitute.For<IIntakePayloadBuilder>();
        payloadBuilder.BuildAsync(AppointmentId, Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Envelope()));

        // MUST be explicit: NSubstitute would otherwise hand back a stub IUnitOfWork for Current.
        var uowManager = Substitute.For<IUnitOfWorkManager>();
        uowManager.Current.Returns((IUnitOfWork?)null);

        return new Harness
        {
            Queue = new CaseTrackerIntakeQueue(
                payloadBuilder,
                new IntegrationOutboxManager(repo, SimpleGuidGenerator.Instance),
                Substitute.For<IBackgroundJobManager>(),
                uowManager,
                NullLogger<CaseTrackerIntakeQueue>.Instance),
            Repository = repo,
            PayloadBuilder = payloadBuilder,
            Rows = rows,
        };
    }

    [Fact]
    public async Task EnqueueIntakeAsync_TakesTheAppointmentLockBeforeBuildingThePayload()
    {
        var h = Build();
        using var cts = new CancellationTokenSource();

        await h.Queue.EnqueueIntakeAsync(AppointmentId, TenantId, cts.Token);

        // Lock, THEN read the document list (inside the build), THEN write the row. Any other order
        // leaves a gap in which an accepted document is both suppressed and missing from the intake.
        Received.InOrder(() =>
        {
            h.Repository.AcquireAppointmentLockAsync(AppointmentId, cts.Token);
            h.PayloadBuilder.BuildAsync(AppointmentId, cts.Token);
            h.Repository.InsertAsync(Arg.Any<IntegrationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task EnqueueIntakeAsync_StillWritesTheIntakeRow()
    {
        // The lock must not change what is written: one Intake row targeted at the intake endpoint.
        var h = Build();

        var row = await h.Queue.EnqueueIntakeAsync(AppointmentId, TenantId);

        h.Rows.Count.ShouldBe(1);
        row.MessageType.ShouldBe(IntegrationMessageType.Intake);
        row.TargetPath.ShouldBe(CaseTrackerEndpoints.Intake);
        row.AppointmentId.ShouldBe(AppointmentId);
    }

    [Fact]
    public async Task EnqueueIntakeAsync_WhenTheLockIsNotGranted_WritesNothing()
    {
        // Fail loudly, never carry on unlocked: an unlocked build silently reopens the race.
        var h = Build();
        h.Repository.AcquireAppointmentLockAsync(AppointmentId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("lock not granted")));

        await Should.ThrowAsync<InvalidOperationException>(() => h.Queue.EnqueueIntakeAsync(AppointmentId, TenantId));

        h.Rows.ShouldBeEmpty();
        await h.PayloadBuilder.DidNotReceiveWithAnyArgs().BuildAsync(default, default);
    }
}
