using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the rule that decides when an intake may be pushed automatically. All fixture data
/// is synthetic.
///
/// <para>The behaviour worth protecting is the NEGATIVE one: a mid-render set must NOT settle. That is
/// the whole point of the 2026-07-30 change -- a single approval was producing two intake pushes ten
/// seconds apart, the first packet-less and immediately superseded.</para>
///
/// <para>The positive cases matter almost as much, because each is a way an appointment could
/// otherwise be withheld from the Case Tracker forever: a failed template, a set that never got rows,
/// or a set stuck mid-render.</para>
/// </summary>
public class IntakeSettlePolicyTests
{
    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("df9c4239-56cd-82e6-6f58-3a22c6ad1093");
    private static readonly DateTime Now = new(2026, 7, 30, 20, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime Cutoff = PacketSetPolicy.Cutoff(Now);

    private static Appointment NewAppointment(DateTime? changedAt = null) =>
        new(
            AppointmentId,
            patientId: new Guid("e5f6a7b8-c9d0-4e1f-a2b3-c4d5e6f7a8bc"),
            identityUserId: null,
            appointmentTypeId: new Guid("a1c2e3f4-5566-4778-9900-aabbccddeeff"),
            locationId: new Guid("c0ffee0a-bcde-4f01-9abc-de0123456f7a"),
            doctorAvailabilityId: new Guid("d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5fa"),
            appointmentDate: new DateTime(2026, 8, 4, 10, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00004",
            appointmentStatus: AppointmentStatusType.Approved,
            panelNumber: null)
        {
            TenantId = TenantId,
            CreationTime = changedAt ?? Now,
        };

    private static AppointmentPacket NewPacket(
        PacketKind kind,
        PacketGenerationStatus status,
        DateTime lastChanged) =>
        new(
            Guid.NewGuid(),
            TenantId,
            AppointmentId,
            kind,
            blobName: "tenantseg/apptseg/packet/patient/228d6bed62e04be7b1146e58629bf901.pdf",
            status: status)
        {
            GeneratedAt = status == PacketGenerationStatus.Generated ? lastChanged : default,
            CreationTime = lastChanged,
        };

    private static List<AppointmentPacket> AllWith(PacketGenerationStatus status, DateTime lastChanged) =>
        PacketSetPolicy.AllKinds.Select(k => NewPacket(k, status, lastChanged)).ToList();

    [Fact]
    public void ACompleteSetIsSettled()
    {
        IntakeSettlePolicy
            .IsSettled(NewAppointment(), AllWith(PacketGenerationStatus.Generated, Now), Cutoff)
            .ShouldBeTrue();
    }

    [Fact]
    public void ASetStillRenderingIsNotSettled()
    {
        // The regression this policy exists for: pushing here is the packet-less first message.
        IntakeSettlePolicy
            .IsSettled(NewAppointment(), AllWith(PacketGenerationStatus.Generating, Now), Cutoff)
            .ShouldBeFalse();
    }

    [Fact]
    public void APartlyGeneratedSetStillMovingIsNotSettled()
    {
        var packets = new List<AppointmentPacket>
        {
            NewPacket(PacketKind.Patient, PacketGenerationStatus.Generated, Now),
            NewPacket(PacketKind.Doctor, PacketGenerationStatus.Generating, Now),
            NewPacket(PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Generating, Now),
        };

        IntakeSettlePolicy.IsSettled(NewAppointment(), packets, Cutoff).ShouldBeFalse();
    }

    [Fact]
    public void AStalledSetSettlesOnceTheCutoffPasses()
    {
        var stale = Now.AddHours(-2);
        var packets = new List<AppointmentPacket>
        {
            NewPacket(PacketKind.Patient, PacketGenerationStatus.Generated, stale),
            NewPacket(PacketKind.Doctor, PacketGenerationStatus.Failed, stale),
            NewPacket(PacketKind.AttorneyClaimExaminer, PacketGenerationStatus.Failed, stale),
        };

        IntakeSettlePolicy.IsSettled(NewAppointment(stale), packets, Cutoff).ShouldBeTrue();
    }

    [Fact]
    public void ASetWithNothingGeneratedStillSettles_UnlikeTheDocumentFeed()
    {
        // The deliberate divergence from PacketSetPolicy.ShouldRelease, which requires something
        // fetchable. An intake must eventually go even when every template failed: the appointment
        // itself is the news, and a withheld intake is a case their staff never see.
        var stale = Now.AddHours(-2);
        var allFailed = AllWith(PacketGenerationStatus.Failed, stale);

        PacketSetPolicy.ShouldRelease(allFailed, Cutoff).ShouldBeFalse();
        IntakeSettlePolicy.IsSettled(NewAppointment(stale), allFailed, Cutoff).ShouldBeTrue();
    }

    [Fact]
    public void WithNoPacketRows_TheAppointmentsOwnAgeDecides()
    {
        var empty = new List<AppointmentPacket>();

        // Freshly approved: generation may simply not have written its rows yet.
        IntakeSettlePolicy.IsSettled(NewAppointment(Now), empty, Cutoff).ShouldBeFalse();

        // Old enough that no rows means none are coming; withholding it forever would be worse.
        IntakeSettlePolicy.IsSettled(NewAppointment(Now.AddHours(-2)), empty, Cutoff).ShouldBeTrue();
    }

    [Fact]
    public void TheSettleCutoffIsOneSharedConstant()
    {
        // Two constants would let the intake and its packets disagree about when a set has given up.
        PacketSetPolicy.Cutoff(Now).ShouldBe(Now.AddMinutes(-PacketSetPolicy.SettleAfterMinutes));
    }

    [Fact]
    public void NullArgumentsThrowRatherThanQuietlySettling()
    {
        Should.Throw<ArgumentNullException>(() =>
            IntakeSettlePolicy.IsSettled(null!, new List<AppointmentPacket>(), Cutoff));
        Should.Throw<ArgumentNullException>(() =>
            IntakeSettlePolicy.IsSettled(NewAppointment(), null!, Cutoff));
    }
}
