using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit coverage for <see cref="CaseTrackerReconciliationJob"/>, the 15-minute sweep that releases
/// packet sets stuck incomplete and kicks each office's Case Tracker drain.
///
/// <para><b>WHAT IS PINNED.</b> A set is released only when it is incomplete, has at least one
/// generated packet (releasing nothing would publish an empty set), and has not changed since the
/// settle cutoff -- and only for an appointment that still exists and is in a publishable status. At
/// most 50 sets are released per office per pass. Every office's drain is enqueued, and one office that
/// fails does not stop the others.</para>
///
/// <para><b>The results asserted are the publishes made and the drain jobs enqueued.</b> The publish
/// service is substituted (its <c>PublishSettledPacketsAsync</c> is virtual), so nothing is pushed.
/// The clock is fixed; in-memory packets carry the default (old) timestamps unless a Fact sets a recent
/// one, so every fixture sits far from the 30-minute settle cutoff. Synthetic data only (HIPAA).</para>
/// </summary>
public class CaseTrackerReconciliationJobTests
{
    private static readonly Guid OfficeA = new("f0000000-0000-0000-0000-00000000000a");
    private static readonly Guid OfficeB = new("f0000000-0000-0000-0000-00000000000b");
    private static readonly DateTime Now = new(2026, 9, 23, 20, 0, 0, DateTimeKind.Utc);

    private sealed class Rig
    {
        public List<AppointmentPacket> Packets { get; } = new();
        public List<Appointment> Appointments { get; } = new();
        public List<Guid> Offices { get; } = new() { OfficeA };
        public IRepository<AppointmentPacket, Guid> PacketRepository { get; } = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        public IBackgroundJobManager Jobs { get; } = Substitute.For<IBackgroundJobManager>();

        /// <summary>Five nulls: the constructor only stores them and the publish method is virtual.</summary>
        public CaseTrackerPacketPublishService Publisher { get; } =
            Substitute.For<CaseTrackerPacketPublishService>(null, null, null, null, null);

        public Rig()
        {
            PacketRepository.GetQueryableAsync().Returns(_ => Packets.AsQueryable());
            PacketRepository.GetListAsync(Arg.Any<Expression<Func<AppointmentPacket, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(ci => Packets.Where(ci.Arg<Expression<Func<AppointmentPacket, bool>>>().Compile()).ToList());
            Publisher.PublishSettledPacketsAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>()).Returns(true);
        }

        public CaseTrackerReconciliationJob Build()
        {
            var runner = Substitute.For<ITenantWorkRunner>();
            runner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>()).Returns(async ci =>
            {
                var work = ci.Arg<Func<Guid, Task>>();
                foreach (var office in Offices)
                {
                    await work(office);
                }
            });
            var appointments = Substitute.For<IRepository<Appointment, Guid>>();
            appointments.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(ci => Appointments.SingleOrDefault(a => a.Id == ci.Arg<Guid>()));
            var clock = Substitute.For<IClock>();
            clock.Now.Returns(Now);
            return new CaseTrackerReconciliationJob(
                runner, Jobs, appointments, PacketRepository, Publisher, clock, NullLogger<CaseTrackerReconciliationJob>.Instance);
        }

        public List<Appointment> Published() =>
            Publisher.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == nameof(CaseTrackerPacketPublishService.PublishSettledPacketsAsync))
                .Select(c => (Appointment)c.GetArguments()[0]!)
                .ToList();

        public List<Guid> DrainedOffices() =>
            Jobs.ReceivedCalls().SelectMany(c => c.GetArguments()).OfType<IntegrationOutboxDrainArgs>().Select(a => a.TenantId ?? Guid.Empty).ToList();

        /// <summary>An appointment whose packet set is stuck: one packet generated, one failed, both old.</summary>
        public Appointment AddStalledSet(AppointmentStatusType status = AppointmentStatusType.Approved)
        {
            var appointment = NewAppointment(status);
            Appointments.Add(appointment);
            Packets.Add(Packet(appointment.Id, PacketKind.Patient, PacketGenerationStatus.Generated));
            Packets.Add(Packet(appointment.Id, PacketKind.Doctor, PacketGenerationStatus.Failed));
            return appointment;
        }
    }

    private static Appointment NewAppointment(AppointmentStatusType status) => new(
        id: Guid.NewGuid(),
        patientId: Guid.NewGuid(),
        identityUserId: null,
        appointmentTypeId: Guid.NewGuid(),
        locationId: Guid.NewGuid(),
        doctorAvailabilityId: Guid.NewGuid(),
        appointmentDate: new DateTime(2026, 11, 6, 9, 0, 0),
        requestConfirmationNumber: "TEST-RC0001",
        appointmentStatus: status);

    private static AppointmentPacket Packet(Guid appointmentId, PacketKind kind, PacketGenerationStatus status) =>
        new(Guid.NewGuid(), OfficeA, appointmentId, kind, "TEST-blob", status);

    /// <summary>
    /// <b>POSITIVE CONTROL.</b> A stalled set for an approved appointment is released, and the office's
    /// drain is enqueued. Every "releases nothing" Fact depends on this fixture reaching the publisher.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_AStalledSet_IsReleasedAndTheOfficeDrained_PositiveControl()
    {
        var rig = new Rig();
        var appointment = rig.AddStalledSet();

        await rig.Build().ExecuteAsync();

        rig.Published().ShouldHaveSingleItem().Id.ShouldBe(appointment.Id);
        rig.DrainedOffices().ShouldBe(new[] { OfficeA });
    }

    /// <summary>A set with no generated packet at all is not released: that would publish nothing.</summary>
    [Fact]
    public async Task ExecuteAsync_ASetWithNothingGenerated_IsNotReleased()
    {
        var rig = new Rig();
        var appointment = NewAppointment(AppointmentStatusType.Approved);
        rig.Appointments.Add(appointment);
        rig.Packets.Add(Packet(appointment.Id, PacketKind.Patient, PacketGenerationStatus.Failed));

        await rig.Build().ExecuteAsync();

        rig.Published().ShouldBeEmpty();
        // The drain is kicked whether or not anything was released.
        rig.DrainedOffices().ShouldBe(new[] { OfficeA });
    }

    /// <summary>A set that changed since the cutoff is still settling, so it is left alone.</summary>
    [Fact]
    public async Task ExecuteAsync_ASetThatChangedRecently_IsNotReleased()
    {
        var rig = new Rig();
        rig.AddStalledSet();
        rig.Packets[0].LastModificationTime = Now.AddMinutes(-5);

        await rig.Build().ExecuteAsync();

        rig.Published().ShouldBeEmpty();
    }

    /// <summary>
    /// The appointment must still exist and be publishable: a request that was never approved has no
    /// case on the Case Tracker side, so its packets are not released.
    /// </summary>
    [Theory]
    [InlineData(false, AppointmentStatusType.Approved)]
    [InlineData(true, AppointmentStatusType.Pending)]
    [InlineData(true, AppointmentStatusType.Rejected)]
    public async Task ExecuteAsync_AGoneOrUnpublishableAppointment_IsNotReleased(bool appointmentExists, AppointmentStatusType status)
    {
        var rig = new Rig();
        var appointment = rig.AddStalledSet(status);
        if (!appointmentExists)
        {
            rig.Appointments.Remove(appointment);
        }

        await rig.Build().ExecuteAsync();

        rig.Published().ShouldBeEmpty();
    }

    /// <summary>At most 50 sets are released per office per pass; the rest wait for the next sweep.</summary>
    [Fact]
    public async Task ExecuteAsync_MoreThanFiftyStalledSets_ReleasesOnlyFifty()
    {
        var rig = new Rig();
        for (var i = 0; i < CaseTrackerReconciliationJob.PacketReleaseBatchSize + 3; i++)
        {
            rig.AddStalledSet();
        }

        await rig.Build().ExecuteAsync();

        rig.Published().Count.ShouldBe(CaseTrackerReconciliationJob.PacketReleaseBatchSize);
    }

    /// <summary>
    /// One office whose sweep throws must not stop the rest: the next office is still reconciled and
    /// drained.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_OneOfficeThatFails_DoesNotStopTheNext()
    {
        var rig = new Rig();
        rig.Offices.Add(OfficeB);
        rig.PacketRepository.GetQueryableAsync().Returns(
            _ => throw new InvalidOperationException("TEST-office A database unreachable"),
            _ => rig.Packets.AsQueryable());

        await Should.NotThrowAsync(() => rig.Build().ExecuteAsync());

        rig.DrainedOffices().ShouldBe(new[] { OfficeB });
    }
}
