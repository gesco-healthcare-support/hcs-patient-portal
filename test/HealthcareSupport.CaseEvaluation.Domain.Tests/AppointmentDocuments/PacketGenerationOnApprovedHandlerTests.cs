using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Handlers;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// BUG-036 sub-bug 2: <see cref="PacketGenerationOnApprovedHandler"/>
/// must defer <see cref="IBackgroundJobManager.EnqueueAsync"/> to the
/// current unit of work's OnCompleted hook. ABP's Hangfire-backed
/// background-job manager enqueues immediately (not transactional),
/// so calling EnqueueAsync inside [UnitOfWork] without OnCompleted lets
/// the worker dequeue and run before the approve UoW commits -- the
/// worker then queries an appointment row that does not yet exist /
/// is not yet in the approved state. Mirrors the same OnCompleted
/// pattern already used in <c>GenerateAppointmentPacketJob</c> at
/// line 202-210 for <c>PacketGeneratedEto</c>.
/// </summary>
public class PacketGenerationOnApprovedHandlerTests
{
    private static readonly Guid AppointmentId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task HandleEventAsync_InsideUnitOfWork_DefersEnqueueToOnCompleted()
    {
        var backgroundJobManager = Substitute.For<IBackgroundJobManager>();
        var unitOfWorkManager = Substitute.For<IUnitOfWorkManager>();
        var currentUow = Substitute.For<IUnitOfWork>();
        unitOfWorkManager.Current.Returns(currentUow);

        Func<Task>? capturedCallback = null;
        currentUow.OnCompleted(Arg.Do<Func<Task>>(cb => capturedCallback = cb));

        var handler = new PacketGenerationOnApprovedHandler(
            backgroundJobManager,
            unitOfWorkManager,
            NullLogger<PacketGenerationOnApprovedHandler>.Instance);

        var eventData = NewApprovedEto();

        await handler.HandleEventAsync(eventData);

        await backgroundJobManager.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateAppointmentPacketArgs>(),
            Arg.Any<BackgroundJobPriority>(),
            Arg.Any<TimeSpan?>());
        capturedCallback.ShouldNotBeNull(
            "BUG-036: handler must register the enqueue via CurrentUnitOfWork.OnCompleted so Hangfire dispatches only after the approve UoW commits.");

        await capturedCallback!();

        await backgroundJobManager.Received(1).EnqueueAsync(
            Arg.Is<GenerateAppointmentPacketArgs>(a =>
                a.AppointmentId == AppointmentId && a.TenantId == TenantId),
            Arg.Any<BackgroundJobPriority>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task HandleEventAsync_OutsideUnitOfWork_EnqueuesImmediately()
    {
        var backgroundJobManager = Substitute.For<IBackgroundJobManager>();
        var unitOfWorkManager = Substitute.For<IUnitOfWorkManager>();
        unitOfWorkManager.Current.Returns((IUnitOfWork?)null);

        var handler = new PacketGenerationOnApprovedHandler(
            backgroundJobManager,
            unitOfWorkManager,
            NullLogger<PacketGenerationOnApprovedHandler>.Instance);

        await handler.HandleEventAsync(NewApprovedEto());

        await backgroundJobManager.Received(1).EnqueueAsync(
            Arg.Is<GenerateAppointmentPacketArgs>(a =>
                a.AppointmentId == AppointmentId && a.TenantId == TenantId),
            Arg.Any<BackgroundJobPriority>(),
            Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task HandleEventAsync_WhenToStatusNotApproved_DoesNotEnqueue()
    {
        var backgroundJobManager = Substitute.For<IBackgroundJobManager>();
        var unitOfWorkManager = Substitute.For<IUnitOfWorkManager>();
        var currentUow = Substitute.For<IUnitOfWork>();
        unitOfWorkManager.Current.Returns(currentUow);

        var handler = new PacketGenerationOnApprovedHandler(
            backgroundJobManager,
            unitOfWorkManager,
            NullLogger<PacketGenerationOnApprovedHandler>.Instance);

        var eventData = NewApprovedEto();
        eventData.ToStatus = AppointmentStatusType.Rejected;

        await handler.HandleEventAsync(eventData);

        await backgroundJobManager.DidNotReceive().EnqueueAsync(
            Arg.Any<GenerateAppointmentPacketArgs>(),
            Arg.Any<BackgroundJobPriority>(),
            Arg.Any<TimeSpan?>());
        currentUow.DidNotReceive().OnCompleted(Arg.Any<Func<Task>>());
    }

    private static AppointmentStatusChangedEto NewApprovedEto() =>
        new()
        {
            AppointmentId = AppointmentId,
            TenantId = TenantId,
            FromStatus = AppointmentStatusType.Pending,
            ToStatus = AppointmentStatusType.Approved,
            OccurredAt = new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc),
        };
}
