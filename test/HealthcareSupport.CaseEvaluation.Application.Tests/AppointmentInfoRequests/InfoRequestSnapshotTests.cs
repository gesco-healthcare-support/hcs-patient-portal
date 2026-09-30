using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Pins the Send Back snapshot serialization round-trip (the app service builds the
/// value map via the field registry, then serializes it here). Pure (no DB). Values are
/// synthetic non-PHI sentinels.
/// </summary>
public class InfoRequestSnapshotTests
{
    [Fact]
    public void Serialize_then_deserialize_round_trips()
    {
        var map = new Dictionary<string, string> { ["cellPhoneNumber"] = "(213) 555-0148" };

        var back = InfoRequestSnapshot.Deserialize(InfoRequestSnapshot.Serialize(map));

        back["cellPhoneNumber"].ShouldBe("(213) 555-0148");
    }

    [Fact]
    public void Deserialize_handles_null_and_garbage()
    {
        InfoRequestSnapshot.Deserialize(null).ShouldBeEmpty();
        InfoRequestSnapshot.Deserialize("not json").ShouldBeEmpty();
    }
}
