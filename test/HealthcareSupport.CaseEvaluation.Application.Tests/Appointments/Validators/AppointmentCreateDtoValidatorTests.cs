using FluentValidation.TestHelper;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Validators;
using System;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments.Validators;

/// <summary>
/// Issue 2.8 (2026-05-12) -- unit tests for
/// <see cref="AppointmentCreateDtoValidator"/>. Uses
/// FluentValidation.TestHelper's <c>TestValidate()</c> sugar for
/// per-property error assertions.
/// </summary>
public class AppointmentCreateDtoValidatorTests
{
    private readonly AppointmentCreateDtoValidator _validator = new();

    private static AppointmentCreateDto ValidDto() => new()
    {
        PatientId = Guid.NewGuid(),
        IdentityUserId = Guid.NewGuid(),
        AppointmentTypeId = Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        DoctorAvailabilityId = Guid.NewGuid(),
        AppointmentDate = DateTime.UtcNow.AddDays(7),
        RequestConfirmationNumber = string.Empty,
    };

    [Fact]
    public void HappyPath_NoErrors()
    {
        var result = _validator.TestValidate(ValidDto());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void EmptyPatientId_FailsValidation()
    {
        var dto = ValidDto();
        dto.PatientId = Guid.Empty;
        var result = _validator.TestValidate(dto);
        result.ShouldHaveValidationErrorFor(x => x.PatientId);
    }

    [Fact]
    public void EmptyAppointmentTypeId_FailsValidation()
    {
        var dto = ValidDto();
        dto.AppointmentTypeId = Guid.Empty;
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.AppointmentTypeId);
    }

    [Fact]
    public void EmptyLocationId_FailsValidation()
    {
        var dto = ValidDto();
        dto.LocationId = Guid.Empty;
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.LocationId);
    }

    [Fact]
    public void EmptyDoctorAvailabilityId_FailsValidation()
    {
        var dto = ValidDto();
        dto.DoctorAvailabilityId = Guid.Empty;
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.DoctorAvailabilityId);
    }

    [Fact]
    public void DefaultAppointmentDate_FailsValidation()
    {
        var dto = ValidDto();
        dto.AppointmentDate = default;
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.AppointmentDate);
    }

    [Fact]
    public void NullPatientEmail_PassesValidation()
    {
        var dto = ValidDto();
        dto.PatientEmail = null;
        _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.PatientEmail);
    }

    [Fact]
    public void InvalidPatientEmail_FailsValidation()
    {
        var dto = ValidDto();
        dto.PatientEmail = "not-an-email";
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.PatientEmail);
    }

    [Fact]
    public void InvalidApplicantAttorneyEmail_FailsValidation()
    {
        var dto = ValidDto();
        dto.ApplicantAttorneyEmail = "garbage";
        _validator.TestValidate(dto).ShouldHaveValidationErrorFor(x => x.ApplicantAttorneyEmail);
    }

    [Fact]
    public void ValidApplicantAttorneyEmail_PassesValidation()
    {
        var dto = ValidDto();
        dto.ApplicantAttorneyEmail = "attorney@example.com";
        _validator.TestValidate(dto).ShouldNotHaveValidationErrorFor(x => x.ApplicantAttorneyEmail);
    }
}
