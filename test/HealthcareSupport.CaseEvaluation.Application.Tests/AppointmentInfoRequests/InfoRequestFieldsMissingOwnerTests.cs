using System;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Appointments;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// The branches of <see cref="InfoRequestFields"/> that <c>InfoRequestFieldsTests</c> does not reach:
/// a correction for a section the appointment does not have yet (no patient, employer, insurance or
/// claim examiner record) reads as empty and writes nothing, and the SSN mask of a blank or short
/// value. Values are synthetic and deliberately not SSN-shaped.
/// </summary>
public class InfoRequestFieldsMissingOwnerTests
{
    private static T New<T>() => (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

    [Theory]
    [InlineData("Patient")]
    [InlineData("Employer")]
    [InlineData("Insurance")]
    [InlineData("ClaimExaminer")]
    public void A_field_whose_record_does_not_exist_reads_empty_and_writing_creates_nothing(string ownerName)
    {
        var owner = Enum.Parse<InfoRequestFieldOwner>(ownerName);
        var spec = InfoRequestFields.All.First(s => s.Owner == owner);
        var bundle = new CorrectionBundle { Appointment = New<Appointment>() };

        spec.Read(bundle).ShouldBeNull();
        spec.Write(bundle, "Synthetic value");

        spec.Read(bundle).ShouldBeNull();
        bundle.Patient.ShouldBeNull();
        bundle.Employer.ShouldBeNull();
        bundle.Insurance.ShouldBeNull();
        bundle.ClaimExaminer.ShouldBeNull();
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("SYN12", "***-**-12")]
    [InlineData("SYN-ABC-4321", "***-**-4321")]
    public void The_ssn_mask_keeps_at_most_the_last_four_digits(string? value, string masked)
    {
        InfoRequestFields.MaskSsn(value).ShouldBe(masked);
    }
}
