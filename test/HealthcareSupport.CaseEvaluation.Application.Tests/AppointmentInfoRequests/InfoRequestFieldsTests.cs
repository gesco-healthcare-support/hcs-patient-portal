using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Patients;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Pins the metadata-driven flaggable-field registry (QA item L): the 62 scalar fields,
/// their owners, and the read/write bindings the corrections + resubmit-gate paths use.
/// Entities are created uninitialized (bypassing ctors) and mutated directly. All values
/// are synthetic non-PHI sentinels.
/// </summary>
public class InfoRequestFieldsTests
{
    private static T New<T>() => (T)System.Activator.CreateInstance(typeof(T), nonPublic: true)!;

    private static CorrectionBundle FullBundle() => new()
    {
        Appointment = New<Appointment>(),
        Patient = New<Patient>(),
        Insurance = New<AppointmentPrimaryInsurance>(),
        ClaimExaminer = New<AppointmentClaimExaminer>(),
    };

    [Fact]
    public void Registry_has_62_unique_scalar_fields()
    {
        InfoRequestFields.All.Count.ShouldBe(62);
        System.Linq.Enumerable.Distinct(System.Linq.Enumerable.Select(InfoRequestFields.All, s => s.Key))
            .ShouldBe(System.Linq.Enumerable.Select(InfoRequestFields.All, s => s.Key), ignoreOrder: true);
    }

    [Fact]
    public void Dropped_and_non_scalar_keys_are_absent()
    {
        InfoRequestFields.ByKey.ShouldNotContainKey("needsInterpreter");
        InfoRequestFields.ByKey.ShouldNotContainKey("claimInformation");
        InfoRequestFields.ByKey.ShouldNotContainKey("documents");
    }

    [Theory]
    [InlineData("firstName", "Patient")]
    [InlineData("refferedBy", "Appointment")]
    [InlineData("applicantAttorneyFirmName", "Appointment")]
    [InlineData("defenseAttorneyZipCode", "Appointment")]
    [InlineData("employerName", "Employer")]
    [InlineData("appointmentInsuranceName", "Insurance")]
    [InlineData("appointmentClaimExaminerName", "ClaimExaminer")]
    public void Maps_keys_to_their_owning_entity(string key, string owner)
    {
        InfoRequestFields.ByKey[key].Owner.ToString().ShouldBe(owner);
    }

    [Fact]
    public void Read_is_null_when_unset_then_the_value_after_write()
    {
        var bundle = FullBundle();
        var spec = InfoRequestFields.ByKey["street"];

        spec.Read(bundle).ShouldBeNull();

        spec.Write(bundle, "128 W 4th St");

        spec.Read(bundle).ShouldBe("128 W 4th St");
        bundle.Patient!.Street.ShouldBe("128 W 4th St");
    }

    [Fact]
    public void Stores_ssn_raw_but_reads_it_masked()
    {
        var bundle = FullBundle();
        InfoRequestFields.ByKey["socialSecurityNumber"].Write(bundle, "xx-77-9012");

        bundle.Patient!.SocialSecurityNumber.ShouldBe("xx-77-9012");
        InfoRequestFields.ByKey["socialSecurityNumber"].Read(bundle).ShouldBe("***-**-9012");
    }

    [Fact]
    public void Parses_a_state_guid_on_write()
    {
        var bundle = FullBundle();
        var id = System.Guid.NewGuid();

        InfoRequestFields.ByKey["stateId"].Write(bundle, id.ToString());

        bundle.Patient!.StateId.ShouldBe(id);
    }

    [Fact]
    public void Writes_appointment_insurance_and_claim_examiner_fields()
    {
        var bundle = FullBundle();

        InfoRequestFields.ByKey["applicantAttorneyFirmName"].Write(bundle, "Acme LLP");
        InfoRequestFields.ByKey["appointmentInsuranceName"].Write(bundle, "Statewide Mutual");
        InfoRequestFields.ByKey["appointmentClaimExaminerName"].Write(bundle, "Jordan Vega");

        bundle.Appointment.ApplicantAttorneyFirmName.ShouldBe("Acme LLP");
        bundle.Insurance!.Name.ShouldBe("Statewide Mutual");
        bundle.ClaimExaminer!.Name.ShouldBe("Jordan Vega");
    }

    [Fact]
    public void Date_round_trips_in_us_format()
    {
        var bundle = FullBundle();

        InfoRequestFields.ByKey["dateOfBirth"].Write(bundle, "03/22/1985");

        InfoRequestFields.ByKey["dateOfBirth"].Read(bundle).ShouldBe("03/22/1985");
    }
}
