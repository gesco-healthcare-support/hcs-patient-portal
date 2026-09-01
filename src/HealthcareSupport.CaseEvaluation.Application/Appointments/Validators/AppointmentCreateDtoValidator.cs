using FluentValidation;
using HealthcareSupport.CaseEvaluation.Appointments;
using System;

namespace HealthcareSupport.CaseEvaluation.Appointments.Validators;

/// <summary>
/// Issue 2.8 (2026-05-12) -- FluentValidation rules for
/// <see cref="AppointmentCreateDto"/>. Volo.Abp.FluentValidation
/// auto-discovers <c>AbstractValidator&lt;T&gt;</c> instances and runs
/// them at the AppService boundary; failures raise
/// <c>AbpValidationException</c> which the API surfaces as HTTP 400
/// with the standard <c>validationErrors[]</c> payload.
///
/// Rules:
///   - PatientId / AppointmentTypeId / LocationId / DoctorAvailabilityId
///     must be a non-empty Guid (the AppService also rejects
///     <c>Guid.Empty</c> with a UserFriendlyException; FluentValidation
///     fires first and produces the more uniform 400-shaped error).
///   - AppointmentDate must be a real value (default(DateTime) is the
///     1/1/0001 sentinel and is treated as "not provided").
///   - Party-email fields, when supplied, must look like an email.
///
/// Conditional rules for the AA / DA / CE / Insurance toggle sections
/// live on the nested DTOs (AppointmentInjuryDetailCreateDtoValidator
/// etc.) so this top-level validator stays focused on the appointment
/// shell. Adding more rules here is fine; keep them additive.
/// </summary>
public class AppointmentCreateDtoValidator : AbstractValidator<AppointmentCreateDto>
{
    public AppointmentCreateDtoValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEqual(Guid.Empty).WithMessage("Patient is required.");

        RuleFor(x => x.AppointmentTypeId)
            .NotEqual(Guid.Empty).WithMessage("Appointment type is required.");

        RuleFor(x => x.LocationId)
            .NotEqual(Guid.Empty).WithMessage("Location is required.");

        RuleFor(x => x.DoctorAvailabilityId)
            .NotEqual(Guid.Empty).WithMessage("Time slot is required.");

        RuleFor(x => x.IdentityUserId)
            .NotEqual(Guid.Empty).WithMessage("Booker (IdentityUser) is required.");

        RuleFor(x => x.AppointmentDate)
            .NotEqual(default(DateTime)).WithMessage("Appointment date is required.");

        // Patient email is collected but optional on the form; if a value
        // is present it must be a valid address.
        RuleFor(x => x.PatientEmail)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.PatientEmail))
            .WithMessage("Patient email must be a valid email address.");

        RuleFor(x => x.ApplicantAttorneyEmail)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ApplicantAttorneyEmail))
            .WithMessage("Applicant attorney email must be a valid email address.");

        RuleFor(x => x.DefenseAttorneyEmail)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.DefenseAttorneyEmail))
            .WithMessage("Defense attorney email must be a valid email address.");

        RuleFor(x => x.ClaimExaminerEmail)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ClaimExaminerEmail))
            .WithMessage("Claim examiner email must be a valid email address.");
    }
}
