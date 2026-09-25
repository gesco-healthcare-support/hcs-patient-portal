using System;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Phase 6 T5 (2026-08-08) -- the requesting side on the wire.
///
/// <para>Outbound, so an unmappable value THROWS rather than degrading: it would be a programming
/// error, and a silent default would tell Case Tracker the wrong party asked for the change.</para>
/// </summary>
public class ChangeRequestSideWireTests
{
    [Theory]
    [InlineData(ChangeRequestSide.SideA, ChangeRequestSideWire.SideA)]
    [InlineData(ChangeRequestSide.SideB, ChangeRequestSideWire.SideB)]
    public void ToWire_MapsEachSideToItsContractValue(ChangeRequestSide side, string expected)
    {
        ChangeRequestSideWire.ToWire(side).ShouldBe(expected);
    }

    [Fact]
    public void ToWire_DoesNotSerializeTheEnumName()
    {
        // Renaming the C# member must not change the wire format.
        ChangeRequestSideWire.ToWire(ChangeRequestSide.SideA)
            .ShouldNotBe(nameof(ChangeRequestSide.SideA));
    }

    [Fact]
    public void ToWire_ThrowsForAnUnknownSide()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ChangeRequestSideWire.ToWire((ChangeRequestSide)99));
    }

    [Fact]
    public void ToWireOrNull_SendsNullWhenNoSideWasRecorded()
    {
        // A staff-initiated request has no requesting party side. Null is the honest answer;
        // defaulting to SideA would attribute the request to the patient.
        ChangeRequestSideWire.ToWireOrNull(null).ShouldBeNull();
    }

    [Fact]
    public void ToWireOrNull_MapsAPresentSide()
    {
        ChangeRequestSideWire.ToWireOrNull(ChangeRequestSide.SideB)
            .ShouldBe(ChangeRequestSideWire.SideB);
    }
}
