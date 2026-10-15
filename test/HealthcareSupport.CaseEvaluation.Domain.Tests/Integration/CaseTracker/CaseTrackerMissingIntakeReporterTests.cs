using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the missing-intake report (#944): published appointments with NO intake outbox row, with no
/// date floor, split by the office's first intake row. Report only: the reporter has no dependency that can
/// enqueue, and the EF test proves it writes nothing.
///
/// <para>All fixture data is synthetic.</para>
/// </summary>
public class CaseTrackerMissingIntakeReporterTests
{
    private static readonly Guid OfficeId = new("5d1f0a7e-2b3c-4d5e-8f90-a1b2c3d4e5f6");
    private static readonly Guid SecondOfficeId = new("6e2a1b8f-3c4d-4e5f-9a01-b2c3d4e5f6a7");

    private static readonly DateTime Now = new(2026, 9, 28, 15, 0, 0, DateTimeKind.Utc);

    /// <summary>When the office wrote its first intake row. Everything is measured against this.</summary>
    private static readonly DateTime FirstRowAt = new(2026, 7, 30, 18, 0, 0, DateTimeKind.Utc);

    private static Appointment NewAppointment(
        AppointmentStatusType status,
        DateTime? approvedAt,
        DateTime? createdAt = null,
        string confirmation = "A00101") =>
        new(
            Guid.NewGuid(),
            patientId: new Guid("e5f6a7b8-c9d0-4e1f-a2b3-c4d5e6f7a8bc"),
            identityUserId: null,
            appointmentTypeId: new Guid("a1c2e3f4-5566-4778-9900-aabbccddeeff"),
            locationId: new Guid("c0ffee0a-bcde-4f01-9abc-de0123456f7a"),
            doctorAvailabilityId: new Guid("d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5fa"),
            appointmentDate: new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: confirmation,
            appointmentStatus: status,
            panelNumber: "PN-SAMPLE")
        {
            TenantId = OfficeId,
            AppointmentApproveDate = approvedAt,
            CreationTime = createdAt ?? (approvedAt ?? Now).AddDays(-2),
        };

    private static IntegrationOutboxItem IntakeRow(Guid appointmentId, DateTime createdAt)
    {
        return new IntegrationOutboxItem(
            Guid.NewGuid(), OfficeId, IntegrationMessageType.Intake,
            CaseTrackerEndpoints.Intake, appointmentId, "{\"data\":{}}",
            "key-" + appointmentId.ToString("N"))
        {
            CreationTime = createdAt,
        };
    }

    /// <summary>
    /// The office's first intake row, for an appointment that is not under test. LOAD-BEARING: without it the
    /// office has no intake row at all and every hit reads as before-integration.
    /// </summary>
    private static IntegrationOutboxItem FirstRow() => IntakeRow(Guid.NewGuid(), FirstRowAt);

    private static List<AppointmentPacket> SettledPackets(Guid appointmentId) =>
        PacketSetPolicy.AllKinds
            .Select(k => new AppointmentPacket(
                Guid.NewGuid(), OfficeId, appointmentId, k,
                blobName: "tenantseg/apptseg/packet/patient/228d6bed62e04be7b1146e58629bf901.pdf",
                status: PacketGenerationStatus.Generated)
            {
                GeneratedAt = FirstRowAt,
                CreationTime = FirstRowAt,
            })
            .ToList();

    private static CaseTrackerMissingIntakeReporter Build(
        List<Appointment> appointments,
        List<IntegrationOutboxItem> outboxRows,
        Func<List<AppointmentPacket>>? packets = null,
        IRepository<Appointment, Guid>? appointmentRepoOverride = null,
        params Guid[] offices)
    {
        var officeIds = offices.Length == 0 ? new[] { OfficeId } : offices;

        var runner = Substitute.For<ITenantWorkRunner>();
        runner.AggregateAcrossOfficesAsync(Arg.Any<Func<Guid, Task<CaseTrackerMissingIntakeOffice>>>())
            .Returns(async ci =>
            {
                var selector = ci.Arg<Func<Guid, Task<CaseTrackerMissingIntakeOffice>>>();
                var results = new List<CaseTrackerMissingIntakeOffice>();
                foreach (var id in officeIds)
                {
                    results.Add(await selector(id));
                }
                return results;
            });

        var tenantStore = Substitute.For<ITenantStore>();
        tenantStore.FindAsync(Arg.Any<Guid>())
            .Returns(ci => new TenantConfiguration(ci.Arg<Guid>(), "Synthetic Office"));

        var appointmentRepo = appointmentRepoOverride ?? Substitute.For<IRepository<Appointment, Guid>>();
        if (appointmentRepoOverride == null)
        {
            appointmentRepo.GetQueryableAsync().Returns(_ => appointments.AsQueryable());
        }

        var packetRepo = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        packetRepo.GetListAsync(
                Arg.Any<Expression<Func<AppointmentPacket, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult((packets ?? (() => SettledPackets(Guid.NewGuid())))()));

        var outboxRepo = Substitute.For<IIntegrationOutboxRepository>();
        outboxRepo.GetQueryableAsync().Returns(_ => outboxRows.AsQueryable());

        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        return new CaseTrackerMissingIntakeReporter(
            runner, tenantStore, appointmentRepo, packetRepo, outboxRepo, clock,
            NullLogger<CaseTrackerMissingIntakeReporter>.Instance);
    }

    // ---- Classify: the rule, without a database ----

    [Fact]
    public void Classify_ApprovedBeforeTheFirstIntakeRow_IsBeforeIntegration()
    {
        CaseTrackerMissingIntakeReporter.Classify(FirstRowAt.AddSeconds(-1), FirstRowAt, settled: true)
            .ShouldBe(CaseTrackerMissingIntakeKind.BeforeIntegration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Classify_ApprovedAtOrAfterTheFirstIntakeRow_AndSettled_IsLikelyLost(int secondsAfter)
    {
        CaseTrackerMissingIntakeReporter.Classify(FirstRowAt.AddSeconds(secondsAfter), FirstRowAt, settled: true)
            .ShouldBe(CaseTrackerMissingIntakeKind.LikelyLost);
    }

    [Fact]
    public void Classify_ApprovedAfterTheFirstIntakeRow_AndStillSettling_IsSettling()
    {
        CaseTrackerMissingIntakeReporter.Classify(FirstRowAt.AddDays(1), FirstRowAt, settled: false)
            .ShouldBe(CaseTrackerMissingIntakeKind.Settling);
    }

    [Fact]
    public void Classify_InAnOfficeThatNeverWroteAnIntakeRow_IsBeforeIntegration()
    {
        CaseTrackerMissingIntakeReporter.Classify(Now, firstIntakeRowAt: null, settled: true)
            .ShouldBe(CaseTrackerMissingIntakeKind.BeforeIntegration);
    }

    // ---- BuildAsync ----

    [Fact]
    public async Task AnAppointmentApprovedLongAgo_AfterTheFirstIntakeRow_IsLikelyLost_BecauseThereIsNoDateFloor()
    {
        // Approved 55 days before "now" -- far outside the hourly sweep's 7-day window, which is the gap.
        var lost = NewAppointment(AppointmentStatusType.Approved, approvedAt: FirstRowAt.AddDays(5), confirmation: "A00104");
        var reporter = Build([lost], [FirstRow()]);

        var office = (await reporter.BuildAsync()).ShouldHaveSingleItem();

        office.FirstIntakeRowAt.ShouldBe(FirstRowAt);
        var item = office.LikelyLost.ShouldHaveSingleItem();
        item.AppointmentId.ShouldBe(lost.Id);
        item.ConfirmationNumber.ShouldBe("A00104");
        item.Status.ShouldBe(AppointmentStatusType.Approved);
        item.ApprovedAt.ShouldBe(FirstRowAt.AddDays(5));
        office.Settling.ShouldBeEmpty();
        office.BeforeIntegrationCount.ShouldBe(0);
    }

    [Fact]
    public async Task AnAppointmentApprovedBeforeTheFirstIntakeRow_IsCountedAsBeforeIntegration_NotLost()
    {
        var old = NewAppointment(AppointmentStatusType.Approved, approvedAt: FirstRowAt.AddDays(-30), confirmation: "A00001");
        var reporter = Build([old], [FirstRow()]);

        var office = (await reporter.BuildAsync()).ShouldHaveSingleItem();

        office.LikelyLost.ShouldBeEmpty();
        office.BeforeIntegrationCount.ShouldBe(1);
        office.BeforeIntegration.ShouldHaveSingleItem().AppointmentId.ShouldBe(old.Id);
    }

    [Fact]
    public async Task WithNoApprovalDate_TheCreationTimeDecidesWhichSide()
    {
        var created = NewAppointment(AppointmentStatusType.Approved, approvedAt: null, createdAt: FirstRowAt.AddDays(3));
        var reporter = Build([created], [FirstRow()]);

        var office = (await reporter.BuildAsync()).ShouldHaveSingleItem();

        office.LikelyLost.ShouldHaveSingleItem().ApprovedAt.ShouldBe(FirstRowAt.AddDays(3));
    }

    [Fact]
    public async Task AnOfficeThatNeverWroteAnIntakeRow_SaysSo_AndListsEverythingAsBeforeIntegration()
    {
        var recent = NewAppointment(AppointmentStatusType.Approved, approvedAt: Now.AddDays(-1));
        var reporter = Build([recent], []);

        var office = (await reporter.BuildAsync()).ShouldHaveSingleItem();

        office.FirstIntakeRowAt.ShouldBeNull();
        office.LikelyLost.ShouldBeEmpty();
        office.BeforeIntegrationCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("sent")]
    [InlineData("failed")]
    [InlineData("resolved")]
    public async Task AnIntakeRowInAnyState_MeansTheAppointmentIsNotReported(string state)
    {
        var appointment = NewAppointment(AppointmentStatusType.Approved, approvedAt: FirstRowAt.AddDays(5));
        var row = IntakeRow(appointment.Id, FirstRowAt.AddDays(5));
        switch (state)
        {
            case "sent":
                row.MarkSent(Now);
                break;
            case "failed":
                row.MarkFatal(Now, "Case Tracker responded 401.");
                break;
            case "resolved":
                row.MarkFatal(Now, "Case Tracker responded 401.");
                row.MarkResolved(Now);
                break;
        }
        var reporter = Build([appointment], [FirstRow(), row]);

        var office = (await reporter.BuildAsync()).ShouldHaveSingleItem();

        office.LikelyLost.ShouldBeEmpty();
        office.Settling.ShouldBeEmpty();
        office.BeforeIntegrationCount.ShouldBe(0);
    }

    [Fact]
    public async Task ADocumentUpdateRow_DoesNotCountAsAnIntakeRow()
    {
        var appointment = NewAppointment(AppointmentStatusType.Approved, approvedAt: FirstRowAt.AddDays(5));
        var documentRow = new IntegrationOutboxItem(
            Guid.NewGuid(), OfficeId, IntegrationMessageType.DocumentUpdate,
            CaseTrackerEndpoints.DocumentUpdate(appointment.Id), appointment.Id, "[]", "doc-key");
        var reporter = Build([appointment], [FirstRow(), documentRow]);

        (await reporter.BuildAsync()).ShouldHaveSingleItem().LikelyLost.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(AppointmentStatusType.Pending)]
    [InlineData(AppointmentStatusType.Rejected)]
    public async Task AnUnpublishedAppointment_IsNeverReported(AppointmentStatusType status)
    {
        var unpublished = NewAppointment(status, approvedAt: null, createdAt: FirstRowAt.AddDays(5));
        var reporter = Build([unpublished], [FirstRow()]);

        var office = (await reporter.BuildAsync()).ShouldHaveSingleItem();

        office.LikelyLost.ShouldBeEmpty();
        office.BeforeIntegrationCount.ShouldBe(0);
    }

    [Fact]
    public async Task AnAppointmentWhosePacketsAreStillRendering_IsSettling_NotLost()
    {
        var fresh = NewAppointment(AppointmentStatusType.Approved, approvedAt: Now.AddMinutes(-5));
        fresh.LastModificationTime = Now.AddMinutes(-5);
        var reporter = Build([fresh], [FirstRow()], packets: () => []);

        var office = (await reporter.BuildAsync()).ShouldHaveSingleItem();

        office.LikelyLost.ShouldBeEmpty();
        office.Settling.ShouldHaveSingleItem().AppointmentId.ShouldBe(fresh.Id);
    }

    [Fact]
    public async Task TheBeforeIntegrationList_IsCapped_NewestFirst_AndTheCountStaysTrue()
    {
        var old = Enumerable.Range(1, CaseTrackerMissingIntakeReporter.BeforeIntegrationListLimit + 5)
            .Select(i => NewAppointment(AppointmentStatusType.Approved, approvedAt: FirstRowAt.AddDays(-400).AddHours(i)))
            .ToList();
        var reporter = Build(old, [FirstRow()]);

        var office = (await reporter.BuildAsync()).ShouldHaveSingleItem();

        office.BeforeIntegrationCount.ShouldBe(CaseTrackerMissingIntakeReporter.BeforeIntegrationListLimit + 5);
        office.BeforeIntegration.Count.ShouldBe(CaseTrackerMissingIntakeReporter.BeforeIntegrationListLimit);
        office.BeforeIntegration[0].ApprovedAt.ShouldBe(old.Max(a => a.AppointmentApproveDate!.Value));
    }

    [Fact]
    public async Task LikelyLost_IsNeverCapped_AndIsNewestFirst()
    {
        var lost = Enumerable.Range(1, CaseTrackerMissingIntakeReporter.BeforeIntegrationListLimit + 5)
            .Select(i => NewAppointment(AppointmentStatusType.Approved, approvedAt: FirstRowAt.AddHours(i)))
            .ToList();
        var reporter = Build(lost, [FirstRow()]);

        var office = (await reporter.BuildAsync()).ShouldHaveSingleItem();

        office.LikelyLost.Count.ShouldBe(lost.Count);
        office.LikelyLost[0].ApprovedAt.ShouldBe(lost.Max(a => a.AppointmentApproveDate!.Value));
    }

    [Fact]
    public async Task AnOfficeThatCannotBeRead_IsReportedAsFailed_AndTheNextOfficeIsStillReported()
    {
        var calls = 0;
        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.GetQueryableAsync().Returns(_ =>
        {
            calls++;
            return calls == 1
                ? throw new InvalidOperationException("synthetic database failure")
                : Task.FromResult(new List<Appointment>().AsQueryable());
        });
        var reporter = Build([], [FirstRow()], appointmentRepoOverride: appointmentRepo, offices: [OfficeId, SecondOfficeId]);

        var offices = await reporter.BuildAsync();

        offices.Count.ShouldBe(2);
        offices[0].Failed.ShouldBeTrue();
        offices[0].OfficeName.ShouldBe("Synthetic Office");
        offices[1].Failed.ShouldBeFalse();
        offices[1].OfficeId.ShouldBe(SecondOfficeId);
    }
}
