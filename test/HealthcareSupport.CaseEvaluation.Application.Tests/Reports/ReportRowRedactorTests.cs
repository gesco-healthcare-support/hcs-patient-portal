using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-01: every report row is redacted at the Application boundary before it
/// reaches the grid or the PDF. SSN shows the last 4 only; DOB shows the birth
/// year only; name / email / phone pass through in full (internal worklist).
/// Synthetic data only (.claude/rules/test-data.md).
/// </summary>
public class ReportRowRedactorTests
{
    private static readonly Guid SampleId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ReportRowSource SampleSource() => new()
    {
        AppointmentId = SampleId,
        RequestConfirmationNumber = "CONF-1001",
        AppointmentTypeName = "PQME",
        LocationName = "Downtown Clinic",
        AppointmentDate = new DateTime(2026, 6, 10, 9, 30, 0),
        AppointmentStatus = AppointmentStatusType.Pending,
        PatientName = "DOE, JANE",
        DateOfBirth = new DateTime(1985, 3, 12),
        Email = "jane.doe@example.test",
        PhoneNumber = "555-0101",
        SocialSecurityNumber = "SYNTHSSN9012",
    };

    [Fact]
    public void Masks_ssn_to_last_four()
    {
        ReportRowRedactor.ToMaskedDto(SampleSource()).SocialSecurityNumber.ShouldBe("***-**-9012");
    }

    [Fact]
    public void Masks_dob_to_birth_year()
    {
        ReportRowRedactor.ToMaskedDto(SampleSource()).DateOfBirth.ShouldBe("1985");
    }

    [Fact]
    public void Shows_name_email_phone_in_full()
    {
        var dto = ReportRowRedactor.ToMaskedDto(SampleSource());
        dto.PatientName.ShouldBe("DOE, JANE");
        dto.Email.ShouldBe("jane.doe@example.test");
        dto.PhoneNumber.ShouldBe("555-0101");
    }

    [Fact]
    public void Preserves_non_phi_columns()
    {
        var dto = ReportRowRedactor.ToMaskedDto(SampleSource());
        dto.AppointmentId.ShouldBe(SampleId);
        dto.RequestConfirmationNumber.ShouldBe("CONF-1001");
        dto.AppointmentTypeName.ShouldBe("PQME");
        dto.LocationName.ShouldBe("Downtown Clinic");
        dto.AppointmentDate.ShouldBe(new DateTime(2026, 6, 10, 9, 30, 0));
        dto.AppointmentStatus.ShouldBe(AppointmentStatusType.Pending);
    }

    [Fact]
    public void Null_ssn_and_dob_do_not_throw()
    {
        var dto = ReportRowRedactor.ToMaskedDto(new ReportRowSource
        {
            RequestConfirmationNumber = "CONF-2002",
            DateOfBirth = null,
            SocialSecurityNumber = null,
        });

        dto.SocialSecurityNumber.ShouldBeNull();
        dto.DateOfBirth.ShouldBeNull();
    }
}
