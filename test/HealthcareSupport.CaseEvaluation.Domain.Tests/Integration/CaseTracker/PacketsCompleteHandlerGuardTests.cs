using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit coverage for the guards of <see cref="PacketsCompleteHandler"/> that
/// <c>PacketsCompleteHandlerTests</c> does not reach: a null event, an appointment that is gone, and a
/// publish that fails.
///
/// <para><b>WHAT IS PINNED.</b> Nothing is published without an appointment. A publish failure is
/// SWALLOWED: packet generation has already succeeded, so losing the push must not fail that job --
/// the reconciliation sweep re-drives it.</para>
///
/// <para><b>The results asserted are the publishes attempted, and that the handler returns
/// normally.</b> The publish service is substituted (its <c>PublishSettledPacketsAsync</c> is virtual),
/// so nothing is pushed. The first Fact is the positive control for the "publishes nothing" Facts.
/// Synthetic data only (HIPAA).</para>
/// </summary>
public class PacketsCompleteHandlerGuardTests
{
    private sealed class Rig
    {
        public Appointment? Appointment { get; set; } = new(
            id: Guid.NewGuid(),
            patientId: Guid.NewGuid(),
            identityUserId: null,
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: new DateTime(2026, 11, 7, 9, 0, 0),
            requestConfirmationNumber: "TEST-PC0001",
            appointmentStatus: AppointmentStatusType.Approved);

        /// <summary>Five nulls: the constructor only stores them and the publish method is virtual.</summary>
        public CaseTrackerPacketPublishService Publisher { get; } =
            Substitute.For<CaseTrackerPacketPublishService>(null, null, null, null, null);

        public PacketsCompleteHandler Build()
        {
            var appointments = Substitute.For<IRepository<Appointment, Guid>>();
            appointments.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(_ => Appointment);
            var packets = Substitute.For<IRepository<AppointmentPacket, Guid>>();
            packets.GetListAsync(Arg.Any<Expression<Func<AppointmentPacket, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(_ => PacketSetPolicy.AllKinds
                    .Select(kind => new AppointmentPacket(Guid.NewGuid(), null, Appointment?.Id ?? Guid.Empty, kind, "TEST-blob", PacketGenerationStatus.Generated))
                    .ToList());
            return new PacketsCompleteHandler(appointments, packets, Publisher, NullLogger<PacketsCompleteHandler>.Instance);
        }

        public int PublishCount() =>
            Publisher.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(CaseTrackerPacketPublishService.PublishSettledPacketsAsync));
    }

    private static PacketGeneratedEto Generated(Guid appointmentId) => new()
    {
        AppointmentId = appointmentId,
        TenantId = Guid.NewGuid(),
        PacketId = Guid.NewGuid(),
        Kind = PacketKind.Doctor,
        OccurredAt = new DateTime(2026, 9, 23, 20, 0, 0, DateTimeKind.Utc),
    };

    /// <summary><b>POSITIVE CONTROL.</b> A complete set for an approved appointment is published.</summary>
    [Fact]
    public async Task HandleEventAsync_ACompleteSet_IsPublished_PositiveControl()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(Generated(rig.Appointment!.Id));

        rig.PublishCount().ShouldBe(1);
    }

    [Fact]
    public async Task HandleEventAsync_NullEvent_PublishesNothing()
    {
        var rig = new Rig();

        await rig.Build().HandleEventAsync(null!);

        rig.PublishCount().ShouldBe(0);
    }

    [Fact]
    public async Task HandleEventAsync_AppointmentGone_PublishesNothing()
    {
        var rig = new Rig();
        var appointmentId = rig.Appointment!.Id;
        rig.Appointment = null;

        await rig.Build().HandleEventAsync(Generated(appointmentId));

        rig.PublishCount().ShouldBe(0);
    }

    /// <summary>
    /// A publish that throws is swallowed after the attempt: packet generation already succeeded, and
    /// the reconciliation sweep will re-drive the push.
    /// </summary>
    [Fact]
    public async Task HandleEventAsync_APublishThatThrows_IsSwallowedAfterTheAttempt()
    {
        var rig = new Rig();
        rig.Publisher.PublishSettledPacketsAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new InvalidOperationException("TEST-outbox write failed")));

        await Should.NotThrowAsync(() => rig.Build().HandleEventAsync(Generated(rig.Appointment!.Id)));

        rig.PublishCount().ShouldBe(1);
    }
}
