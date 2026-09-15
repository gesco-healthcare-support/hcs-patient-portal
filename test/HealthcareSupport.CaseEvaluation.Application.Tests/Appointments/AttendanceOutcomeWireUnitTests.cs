using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 5 (2026-08-07) -- pure tests for the inbound outcome parser. This mapping is the only thing
/// stopping one endpoint from becoming a general-purpose status setter, so the rejections matter more
/// than the acceptances.
/// </summary>
public class AttendanceOutcomeWireUnitTests
{
    [Theory]
    [InlineData("NO_SHOW", AppointmentStatusType.NoShow)]
    [InlineData("NOT_SEEN", AppointmentStatusType.NotSeen)]
    public void TryParse_AcceptsTheTwoContractValues(string wire, AppointmentStatusType expected)
    {
        AttendanceOutcomeWire.TryParse(wire, out var outcome).ShouldBeTrue();
        outcome.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // Enum member names must NOT be accepted: parsing by name would change the accepted wire
    // format the moment someone renamed a member.
    [InlineData("NoShow")]
    [InlineData("NotSeen")]
    // Case and whitespace variants are rejected so the two systems cannot drift apart unnoticed.
    [InlineData("no_show")]
    [InlineData("No_Show")]
    [InlineData(" NO_SHOW")]
    [InlineData("NO_SHOW ")]
    // The rejections that matter most: any other status must not be reachable through this door.
    [InlineData("APPROVED")]
    [InlineData("Approved")]
    [InlineData("CANCELLED")]
    [InlineData("BILLED")]
    public void TryParse_RejectsEverythingElse(string? wire)
    {
        AttendanceOutcomeWire.TryParse(wire, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryParse_YieldsNoUsableOutcomeWhenItFails()
    {
        // The out value must not be quietly usable on the failure path -- default(enum) is 0, which
        // is not a real AppointmentStatusType (the enum starts at Pending = 1).
        AttendanceOutcomeWire.TryParse("APPROVED", out var outcome).ShouldBeFalse();
        ((int)outcome).ShouldBe(0);
    }
}
