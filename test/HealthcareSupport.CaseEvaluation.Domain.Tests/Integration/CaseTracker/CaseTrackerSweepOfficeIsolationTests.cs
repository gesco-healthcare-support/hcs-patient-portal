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
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The completeness sweep across TWO offices. The tenant runner aborts the whole run on a throw, so the
/// sweep catches per office: one office's failure must not cost the next office its recovery. Each
/// office owns one lost-intake appointment, and each must be recovered under its OWN office id, never
/// the other's. All data is synthetic.
/// </summary>
public class CaseTrackerSweepOfficeIsolationTests
{
    private static readonly Guid OfficeA = new("0a1b2c3d-4e5f-4061-8a7b-9c0d1e2f3a41");
    private static readonly Guid OfficeB = new("0b2c3d4e-5f60-4172-9b8c-0d1e2f3a4b52");
    private static readonly Guid AppointmentInA = new("1c3d4e5f-6071-4283-ac9d-1e2f3a4b5c63");
    private static readonly Guid AppointmentInB = new("2d4e5f60-7182-4394-bdae-2f3a4b5c6d74");
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    private static Appointment ApprovedIn(Guid officeId, Guid appointmentId) =>
        new(
            appointmentId,
            patientId: new Guid("3e5f6071-8293-44a5-8ebf-3a4b5c6d7e85"),
            identityUserId: null,
            appointmentTypeId: new Guid("4f607182-93a4-45b6-9fc0-4b5c6d7e8f96"),
            locationId: new Guid("50718293-a4b5-46c7-80d1-5c6d7e8f90a7"),
            doctorAvailabilityId: new Guid("618293a4-b5c6-47d8-91e2-6d7e8f90a1b8"),
            appointmentDate: new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A90081",
            appointmentStatus: AppointmentStatusType.Approved,
            panelNumber: "TEST-PANEL")
        {
            TenantId = officeId,
            // Inside the lookback window; an unsaved entity's CreationTime would otherwise be default.
            CreationTime = Now.AddHours(-1),
        };

    private static List<AppointmentPacket> SettledPackets() =>
        PacketSetPolicy.AllKinds
            .Select(k => new AppointmentPacket(
                Guid.NewGuid(),
                OfficeA,
                AppointmentInA,
                k,
                blobName: "tenantseg/apptseg/packet/sweep/1a2b3c4d5e6f708192a3b4c5d6e7f809.pdf",
                status: PacketGenerationStatus.Generated)
            {
                GeneratedAt = Now.AddHours(-1),
                CreationTime = Now.AddHours(-1),
            })
            .ToList();

    /// <summary>
    /// The runner visits office A, then office B. Each office's appointment query answers with that
    /// office's rows only (the tenant filter's job, done here by the stub), and office A's query can be
    /// made to throw.
    /// </summary>
    private static (CaseTrackerCompletenessSweepJob Job, ICaseTrackerIntakeQueue Queue) Build(bool officeAQueryThrows)
    {
        var current = Guid.Empty;

        var runner = Substitute.For<ITenantWorkRunner>();
        runner.ForEachOfficeAsync(Arg.Any<Func<Guid, Task>>())
            .Returns(async ci =>
            {
                var work = ci.Arg<Func<Guid, Task>>();
                current = OfficeA;
                await work(OfficeA);
                current = OfficeB;
                await work(OfficeB);
            });

        var appointments = Substitute.For<IRepository<Appointment, Guid>>();
        appointments.GetQueryableAsync().Returns(_ =>
        {
            if (current == OfficeA && officeAQueryThrows)
            {
                throw new InvalidOperationException("TEST-office-a-query-down");
            }

            var rows = current == OfficeA
                ? new List<Appointment> { ApprovedIn(OfficeA, AppointmentInA) }
                : new List<Appointment> { ApprovedIn(OfficeB, AppointmentInB) };
            return Task.FromResult(rows.AsQueryable());
        });

        var packets = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        packets.GetListAsync(
                Arg.Any<Expression<Func<AppointmentPacket, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(SettledPackets()));

        var outbox = Substitute.For<IIntegrationOutboxRepository>();
        outbox.GetQueryableAsync().Returns(_ => new List<IntegrationOutboxItem>().AsQueryable());

        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        var queue = Substitute.For<ICaseTrackerIntakeQueue>();

        return (
            new CaseTrackerCompletenessSweepJob(
                runner, appointments, packets, outbox, queue, clock,
                NullLogger<CaseTrackerCompletenessSweepJob>.Instance),
            queue);
    }

    [Fact]
    public async Task EachOfficesLostIntake_IsRecoveredUnderItsOwnOffice()
    {
        // Positive control for the Fact below: the same two offices, nothing failing.
        var (job, queue) = Build(officeAQueryThrows: false);

        await job.ExecuteAsync();

        await queue.Received(1).EnqueueIntakeAsync(AppointmentInA, OfficeA, Arg.Any<CancellationToken>());
        await queue.Received(1).EnqueueIntakeAsync(AppointmentInB, OfficeB, Arg.Any<CancellationToken>());
        await queue.DidNotReceive().EnqueueIntakeAsync(AppointmentInA, OfficeB, Arg.Any<CancellationToken>());
        await queue.DidNotReceive().EnqueueIntakeAsync(AppointmentInB, OfficeA, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OneOfficeFailing_DoesNotStopTheNextOfficesRecovery()
    {
        var (job, queue) = Build(officeAQueryThrows: true);

        await Should.NotThrowAsync(() => job.ExecuteAsync());

        await queue.Received(1).EnqueueIntakeAsync(AppointmentInB, OfficeB, Arg.Any<CancellationToken>());
        await queue.DidNotReceive().EnqueueIntakeAsync(AppointmentInA, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }
}
