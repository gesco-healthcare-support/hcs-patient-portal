using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 13b (2026-05-04) -- pure tests for
/// <see cref="ExternalUserDtoFilter"/>.
/// </summary>
public class ExternalUserDtoFilterUnitTests
{
    [Fact]
    public void MaskInternalFields_ExternalCaller_NullsInternalUserComments()
    {
        var dto = new AppointmentDto { InternalUserComments = "office-only note" };
        var result = ExternalUserDtoFilter.MaskInternalFields(dto, isExternalUser: true);
        result.InternalUserComments.ShouldBeNull();
    }

    [Fact]
    public void MaskInternalFields_InternalCaller_KeepsInternalUserComments()
    {
        var dto = new AppointmentDto { InternalUserComments = "office-only note" };
        var result = ExternalUserDtoFilter.MaskInternalFields(dto, isExternalUser: false);
        result.InternalUserComments.ShouldBe("office-only note");
    }

    [Fact]
    public void MaskInternalFields_ReturnsSameInstance()
    {
        var dto = new AppointmentDto { InternalUserComments = "x" };
        var result = ExternalUserDtoFilter.MaskInternalFields(dto, isExternalUser: true);
        ReferenceEquals(dto, result).ShouldBeTrue();
    }

    [Fact]
    public void MaskInternalFields_NullDto_ReturnsNullPassthrough()
    {
        // Defensive: the caller passes the DTO from ObjectMapper which
        // generally never returns null, but a null guard prevents NRE.
        var result = ExternalUserDtoFilter.MaskInternalFields((AppointmentDto)null!, isExternalUser: true);
        result.ShouldBeNull();
    }

    [Fact]
    public void MaskInternalFields_WithNavExternal_MasksWrappedAppointment()
    {
        var dto = new AppointmentWithNavigationPropertiesDto
        {
            Appointment = new AppointmentDto { InternalUserComments = "secret" },
        };
        var result = ExternalUserDtoFilter.MaskInternalFields(dto, isExternalUser: true);
        result.Appointment.InternalUserComments.ShouldBeNull();
    }

    [Fact]
    public void MaskInternalFields_WithNavInternal_KeepsWrappedAppointment()
    {
        var dto = new AppointmentWithNavigationPropertiesDto
        {
            Appointment = new AppointmentDto { InternalUserComments = "secret" },
        };
        var result = ExternalUserDtoFilter.MaskInternalFields(dto, isExternalUser: false);
        result.Appointment.InternalUserComments.ShouldBe("secret");
    }

    [Fact]
    public void MaskInternalFields_OtherFields_LeftUntouched()
    {
        var dto = new AppointmentDto
        {
            InternalUserComments = "secret",
            PatientEmail = "p@test.local",
            ApplicantAttorneyEmail = "aa@test.local",
            DefenseAttorneyEmail = "da@test.local",
            ClaimExaminerEmail = "ce@test.local",
            PanelNumber = "PNL-001",
        };
        ExternalUserDtoFilter.MaskInternalFields(dto, isExternalUser: true);
        dto.InternalUserComments.ShouldBeNull();
        dto.PatientEmail.ShouldBe("p@test.local");
        dto.ApplicantAttorneyEmail.ShouldBe("aa@test.local");
        dto.DefenseAttorneyEmail.ShouldBe("da@test.local");
        dto.ClaimExaminerEmail.ShouldBe("ce@test.local");
        dto.PanelNumber.ShouldBe("PNL-001");
    }
}
