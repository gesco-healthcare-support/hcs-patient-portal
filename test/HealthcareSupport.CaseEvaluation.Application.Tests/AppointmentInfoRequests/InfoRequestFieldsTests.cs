using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Patients;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Pins the metadata-driven flaggable-field registry (QA item L): the 63 scalar fields,
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
    public void Registry_has_63_unique_scalar_fields()
    {
        InfoRequestFields.All.Count.ShouldBe(63);
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
    public void Writes_the_patient_unit_number_address_line2()
    {
        // 2026-07-10 QA (item 4a): the Patient "Unit #" must be requestable + correctable via the
        // send-back / info-request flow.
        //
        // 2026-08-13: this used to assert Patient.Address. The unit was split across two columns --
        // staff screens wrote ApptNumber, booking and send-back wrote Address -- and the Case
        // Tracker read only Address, so a correction made here never reached them. Both writers now
        // target ApptNumber. The KEY stays "address" because historic info-request rows persist it.
        var bundle = FullBundle();
        var spec = InfoRequestFields.ByKey["address"];

        spec.Read(bundle).ShouldBeNull();

        spec.Write(bundle, "Suite 210");

        spec.Read(bundle).ShouldBe("Suite 210");
        bundle.Patient!.ApptNumber.ShouldBe("Suite 210");
        bundle.Patient!.Address.ShouldBeNull("the legacy column must not be written any more");
    }

    [Fact]
    public void Reads_a_legacy_unit_from_the_old_column()
    {
        // Rows booked before 2026-08-13 keep their unit in Address and were deliberately not
        // backfilled, so the send-back screen must still SHOW it or staff would think it was blank
        // and retype it.
        var bundle = FullBundle();
        bundle.Patient!.Address = "4B";

        InfoRequestFields.ByKey["address"].Read(bundle).ShouldBe("4B");
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
