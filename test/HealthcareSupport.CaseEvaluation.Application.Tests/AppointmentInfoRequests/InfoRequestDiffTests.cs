using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Pins the Send Back per-round diff builder (Branch 2): change detection, registry
/// ordering, documents exclusion, and open-round handling. Pure (no DB). Values are
/// synthetic non-PHI sentinels.
/// </summary>
public class InfoRequestDiffTests
{
    private static Dictionary<string, string> Map(params (string Key, string Value)[] pairs)
    {
        var map = new Dictionary<string, string>();
        foreach (var (key, value) in pairs)
        {
            map[key] = value;
        }
        return map;
    }

    [Fact]
    public void Marks_a_field_changed_when_after_differs()
    {
        var diffs = InfoRequestSnapshot.BuildDiff(
            Map(("cellPhoneNumber", "(213) 555-0148")),
            Map(("cellPhoneNumber", "(310) 555-0199")),
            new HashSet<string> { "cellPhoneNumber" });

        diffs.Count.ShouldBe(1);
        diffs[0].Key.ShouldBe("cellPhoneNumber");
        diffs[0].OldValue.ShouldBe("(213) 555-0148");
        diffs[0].NewValue.ShouldBe("(310) 555-0199");
        diffs[0].Changed.ShouldBeTrue();
    }

    [Fact]
    public void Marks_unchanged_when_values_match()
    {
        var diffs = InfoRequestSnapshot.BuildDiff(
            Map(("address", "128 W 4th St")),
            Map(("address", "128 W 4th St")),
            new HashSet<string> { "address" });

        diffs[0].Changed.ShouldBeFalse();
    }

    [Fact]
    public void Open_round_with_no_after_reads_as_unchanged()
    {
        var diffs = InfoRequestSnapshot.BuildDiff(
            Map(("dateOfBirth", "old")),
            new Dictionary<string, string>(),
            new HashSet<string> { "dateOfBirth" });

        diffs.Count.ShouldBe(1);
        diffs[0].NewValue.ShouldBeNull();
        diffs[0].Changed.ShouldBeFalse();
    }

    [Fact]
    public void Excludes_the_documents_key()
    {
        var diffs = InfoRequestSnapshot.BuildDiff(
            Map(("address", "a")),
            Map(("address", "b")),
            new HashSet<string> { "documents", "address" });

        diffs.Count.ShouldBe(1);
        diffs[0].Key.ShouldBe("address");
    }

    [Fact]
    public void Orders_diffs_by_registry_not_by_input()
    {
        var diffs = InfoRequestSnapshot.BuildDiff(
            Map(("appointmentInsuranceName", "a"), ("dateOfBirth", "x")),
            Map(("appointmentInsuranceName", "b"), ("dateOfBirth", "y")),
            new HashSet<string> { "appointmentInsuranceName", "dateOfBirth" });

        diffs[0].Key.ShouldBe("dateOfBirth");
        diffs[1].Key.ShouldBe("appointmentInsuranceName");
    }

    [Fact]
    public void Empty_snapshots_yield_an_empty_diff()
    {
        var diffs = InfoRequestSnapshot.BuildDiff(
            new Dictionary<string, string>(),
            new Dictionary<string, string>(),
            new HashSet<string>());

        diffs.ShouldBeEmpty();
    }
}
