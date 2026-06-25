using System;
using System.Collections.Generic;
using System.Globalization;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Pins the Send Back per-round value snapshot (Branch 2): SSN masked at capture,
/// DOB formatted, only flagged scalar keys present, documents excluded. Pure (no DB),
/// reachable via the Application InternalsVisibleTo wiring. All inputs are synthetic
/// non-PHI sentinels.
/// </summary>
public class InfoRequestSnapshotTests
{
    [Fact]
    public void Captures_only_flagged_scalar_keys()
    {
        var values = new InfoRequestSnapshot.FieldValues
        {
            CellPhoneNumber = "(213) 555-0148",
            Street = "128 W 4th St",
            ApplicantAttorneyEmail = "aa@example.test",
        };

        var map = InfoRequestSnapshot.Capture(values, new HashSet<string> { "cellPhoneNumber" });

        map.Keys.ShouldBe(new[] { "cellPhoneNumber" });
        map["cellPhoneNumber"].ShouldBe("(213) 555-0148");
    }

    [Fact]
    public void Masks_ssn_to_last_four_at_capture()
    {
        // Synthetic, non-SSN-format input; the mask keeps only the trailing digits.
        var values = new InfoRequestSnapshot.FieldValues { SocialSecurityNumber = "xx-77-9012" };

        var map = InfoRequestSnapshot.Capture(values, new HashSet<string> { "socialSecurityNumber" });

        map["socialSecurityNumber"].ShouldBe("***-**-9012");
    }

    [Fact]
    public void Formats_date_of_birth_as_month_day_year()
    {
        var dob = new DateTime(1985, 3, 22);

        var map = InfoRequestSnapshot.Capture(
            new InfoRequestSnapshot.FieldValues { DateOfBirth = dob },
            new HashSet<string> { "dateOfBirth" });

        map["dateOfBirth"].ShouldBe(dob.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Excludes_the_documents_key()
    {
        var map = InfoRequestSnapshot.Capture(
            new InfoRequestSnapshot.FieldValues { Street = "128 W 4th St" },
            new HashSet<string> { "documents", "street" });

        map.ShouldContainKey("street");
        map.ShouldNotContainKey("documents");
    }

    [Fact]
    public void Flagged_but_empty_value_maps_to_empty_string()
    {
        var map = InfoRequestSnapshot.Capture(
            new InfoRequestSnapshot.FieldValues { Street = null },
            new HashSet<string> { "street" });

        map.ShouldContainKey("street");
        map["street"].ShouldBe(string.Empty);
    }

    [Fact]
    public void Insurance_name_is_the_value_for_the_shared_key()
    {
        var map = InfoRequestSnapshot.Capture(
            new InfoRequestSnapshot.FieldValues { InsuranceName = "Statewide Mutual" },
            new HashSet<string> { "appointmentInsuranceName" });

        map["appointmentInsuranceName"].ShouldBe("Statewide Mutual");
    }

    [Fact]
    public void State_name_is_the_value_for_the_state_key()
    {
        // StateId is a Guid; the snapshot stores the resolved display name, like language.
        var map = InfoRequestSnapshot.Capture(
            new InfoRequestSnapshot.FieldValues { StateName = "California" },
            new HashSet<string> { "stateId" });

        map["stateId"].ShouldBe("California");
    }

    [Fact]
    public void Serialize_then_deserialize_round_trips()
    {
        var map = InfoRequestSnapshot.Capture(
            new InfoRequestSnapshot.FieldValues { CellPhoneNumber = "(213) 555-0148" },
            new HashSet<string> { "cellPhoneNumber" });

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
