using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the same-person grouping key. The receiver's ONLY use is equality -- "do these two
/// appointments belong to one human?" -- so the value is a hash rather than our raw
/// <c>Patient.Id</c>. Two reasons, both load-bearing:
///
/// <list type="bullet">
/// <item><description>Our patient row key means nothing in CalMed's world, where patient identity is
/// actually minted. Publishing it raw invites someone downstream to store it as a patient
/// identifier.</description></item>
/// <item><description>Salting with the office makes a cross-office false match impossible BY
/// CONSTRUCTION, instead of depending on the receiver honouring an office-scoping rule.</description></item>
/// </list>
/// </summary>
public class SamePersonGroupKeyTests
{
    private static readonly Guid OfficeA = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid OfficeB = new("c3d4e5f6-a7b8-49ca-8bdc-ed2143658709");
    private static readonly Guid PatientId = new("e5f6a7b8-c9d0-4e1f-a2b3-c4d5e6f7a8bc");
    private static readonly Guid OtherPatientId = new("f97796c9-365b-4ad3-a164-08f72981cae3");

    [Fact]
    public void TheSameOfficeAndPatient_AlwaysProduceTheSameKey()
    {
        // Equality must hold across pushes and across restarts, or the receiver's grouping breaks
        // silently over time.
        SamePersonGroupKey.Compute(OfficeA, PatientId)
            .ShouldBe(SamePersonGroupKey.Compute(OfficeA, PatientId));
    }

    [Fact]
    public void TheSamePatientInADifferentOffice_ProducesADifferentKey()
    {
        // Patient deduplication is per office, so the same human at two offices is two unrelated rows.
        // Different keys make an accidental cross-office match impossible rather than merely discouraged.
        SamePersonGroupKey.Compute(OfficeA, PatientId)
            .ShouldNotBe(SamePersonGroupKey.Compute(OfficeB, PatientId));
    }

    [Fact]
    public void DifferentPatientsInOneOffice_ProduceDifferentKeys()
    {
        SamePersonGroupKey.Compute(OfficeA, PatientId)
            .ShouldNotBe(SamePersonGroupKey.Compute(OfficeA, OtherPatientId));
    }

    [Fact]
    public void TheHostScopeIsDistinctFromAnyOffice()
    {
        SamePersonGroupKey.Compute(null, PatientId)
            .ShouldNotBe(SamePersonGroupKey.Compute(OfficeA, PatientId));
    }

    [Fact]
    public void TheKeyNeverContainsTheRawPatientId()
    {
        // The whole point of hashing: nothing downstream can recover or accidentally display our row key.
        var key = SamePersonGroupKey.Compute(OfficeA, PatientId);

        key.ShouldNotContain(PatientId.ToString("D"), Case.Insensitive);
        key.ShouldNotContain(PatientId.ToString("N"), Case.Insensitive);
    }

    [Fact]
    public void TheKeyIsLowercaseHexOfFixedLength()
    {
        var key = SamePersonGroupKey.Compute(OfficeA, PatientId);

        key.Length.ShouldBe(64); // SHA-256 as hex
        key.ShouldBe(key.ToLowerInvariant());
        key.ShouldAllBe(c => Uri.IsHexDigit(c));
    }
}
