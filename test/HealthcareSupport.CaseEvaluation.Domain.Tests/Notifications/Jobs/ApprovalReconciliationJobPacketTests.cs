using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Unit coverage for the PACKET half of <see cref="ApprovalReconciliationJob"/>: the 15-minute
/// backstop that re-enqueues packet generation for approved appointments whose packets never
/// finished. <c>ApprovalReconciliationJobTests</c> already pins per-office isolation; these pin
/// WHICH packets get re-driven.
///
/// <para><b>WHAT IS PINNED.</b> A missing, failed, or stale "generating" packet is re-enqueued, one
/// job per incomplete kind; a generated packet, or one still legitimately generating, is not; and
/// the outbox drain is kicked for the office every time, packets or not.</para>
///
/// <para><b>The result asserted is the set of jobs ENQUEUED</b> on a substituted
/// <see cref="IBackgroundJobManager"/> -- nothing runs, renders or sends. The stale threshold is 30
/// minutes; the fixtures sit at 1 minute and 2 hours, far from the edge, so the real clock the job
/// reads cannot flip them.</para>
///
/// <para>Synthetic data only (HIPAA).</para>
/// </summary>
public class ApprovalReconciliationJobPacketTests
{
    private static readonly Guid OfficeId = new("55555555-5555-5555-5555-555555555555");

    private sealed class Harness
    {
        public List<Appointment> Appointments { get; } = new();
        public List<AppointmentPacket> Packets { get; } = new();
        public IRepository<AppointmentPacket, Guid> PacketRepository { get; } = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        public IBackgroundJobManager Jobs { get; } = Substitute.For<IBackgroundJobManager>();
        public ApprovalReconciliationJob Job { get; }

        public Harness()
        {
            var appointmentRepository = Substitute.For<IRepository<Appointment, Guid>>();
            appointmentRepository.GetQueryableAsync().Returns(_ => Appointments.AsQueryable());
            PacketRepository.GetQueryableAsync().Returns(_ => Packets.AsQueryable());
            var tenantRunner = Substitute.For<ITenantWorkRunner>();
            tenantRunner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>())
                .Returns(ci => ci.Arg<Func<Guid, Task>>()(OfficeId));

            Job = new ApprovalReconciliationJob(
                tenantRunner,
                appointmentRepository,
                PacketRepository,
                Jobs,
                NullLogger<ApprovalReconciliationJob>.Instance);
        }

        public List<GenerateAppointmentPacketArgs> PacketJobs() =>
            Jobs.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<GenerateAppointmentPacketArgs>().ToList();

        public List<OutboxDrainArgs> DrainJobs() =>
            Jobs.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<OutboxDrainArgs>().ToList();
    }

    private static Appointment Approved() => new(
        id: Guid.NewGuid(),
        patientId: Guid.NewGuid(),
        identityUserId: null,
        appointmentTypeId: Guid.NewGuid(),
        locationId: Guid.NewGuid(),
        doctorAvailabilityId: Guid.NewGuid(),
        appointmentDate: new DateTime(2026, 11, 3, 9, 0, 0),
        requestConfirmationNumber: "TEST-AR0001",
        appointmentStatus: AppointmentStatusType.Approved);

    private static AppointmentPacket Packet(Guid appointmentId, PacketKind kind, PacketGenerationStatus status, DateTime? lastAttemptAt = null) =>
        new(Guid.NewGuid(), OfficeId, appointmentId, kind, "TEST-blob", status)
        {
            GeneratedAt = lastAttemptAt ?? DateTime.UtcNow,
            LastAttemptAt = lastAttemptAt,
        };

    /// <summary>
    /// <b>POSITIVE CONTROL.</b> An approved appointment with no packets at all gets one generation job
    /// per expected kind, for this office. The "re-enqueues nothing" Facts depend on this one.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ApprovedAppointmentWithNoPackets_EnqueuesEveryKind_PositiveControl()
    {
        var h = new Harness();
        var appointment = Approved();
        h.Appointments.Add(appointment);

        await h.Job.ExecuteAsync();

        var jobs = h.PacketJobs();
        jobs.Select(j => j.Kind).ShouldBe(
            new PacketKind?[] { PacketKind.Patient, PacketKind.Doctor, PacketKind.AttorneyClaimExaminer },
            ignoreOrder: true);
        jobs.ShouldAllBe(j => j.AppointmentId == appointment.Id && j.TenantId == OfficeId);
    }

    [Fact]
    public async Task ExecuteAsync_EveryPacketGenerated_EnqueuesNoPacketJobButStillDrainsTheOutbox()
    {
        var h = new Harness();
        var appointment = Approved();
        h.Appointments.Add(appointment);
        h.Packets.Add(Packet(appointment.Id, PacketKind.Patient, PacketGenerationStatus.Generated));
        h.Packets.Add(Packet(appointment.Id, PacketKind.Doctor, PacketGenerationStatus.Generated));
        h.Packets.Add(Packet(appointment.Id, PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Generated));

        await h.Job.ExecuteAsync();

        h.PacketJobs().ShouldBeEmpty("a fully generated appointment needs no re-drive");
        h.DrainJobs().ShouldHaveSingleItem().TenantId.ShouldBe(OfficeId);
    }

    /// <summary>
    /// A FAILED packet and a "generating" one whose last attempt is long past the 30-minute threshold
    /// are re-driven; one that started a minute ago is left alone, since its worker may still be running.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_ReEnqueuesFailedAndStaleGeneratingPacketsButNotFreshOnes()
    {
        var h = new Harness();
        var appointment = Approved();
        h.Appointments.Add(appointment);
        h.Packets.Add(Packet(appointment.Id, PacketKind.Patient, PacketGenerationStatus.Failed));
        h.Packets.Add(Packet(appointment.Id, PacketKind.Doctor, PacketGenerationStatus.Generating, DateTime.UtcNow.AddHours(-2)));
        h.Packets.Add(Packet(appointment.Id, PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Generating, DateTime.UtcNow.AddMinutes(-1)));

        await h.Job.ExecuteAsync();

        h.PacketJobs().Select(j => j.Kind).ShouldBe(
            new PacketKind?[] { PacketKind.Patient, PacketKind.Doctor },
            ignoreOrder: true);
    }

    /// <summary>
    /// With no approved appointment the packet table is never read -- and the outbox drain is still
    /// kicked, because pending emails do not depend on packets.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_NoApprovedAppointments_ReadsNoPacketsAndStillDrainsTheOutbox()
    {
        var h = new Harness();

        await h.Job.ExecuteAsync();

        await h.PacketRepository.DidNotReceive().GetQueryableAsync();
        h.PacketJobs().ShouldBeEmpty();
        h.DrainJobs().ShouldHaveSingleItem().TenantId.ShouldBe(OfficeId);
    }
}
