using HealthcareSupport.CaseEvaluation.Enums;
using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Patients;

public class CreatePatientForAppointmentBookingInput
{
    [Required]
    [StringLength(PatientConsts.FirstNameMaxLength)]
    public string FirstName { get; set; } = null!;

    [Required]
    [StringLength(PatientConsts.LastNameMaxLength)]
    public string LastName { get; set; } = null!;

    [StringLength(PatientConsts.MiddleNameMaxLength)]
    public string? MiddleName { get; set; }

    // task_d5407b22 (2026-07-21): patient email is OPTIONAL (injured workers often lack one).
    // No [Required]; [EmailAddress] still validates the FORMAT of a provided value (and allows
    // null). The wizard sends null for a blank field; the service stores "".
    [EmailAddress]
    [StringLength(PatientConsts.EmailMaxLength)]
    public string? Email { get; set; }

    // G-06-08 (2026-06-01): default to the explicit "not provided" sentinel
    // rather than positional [0] (which silently meant Male before Unspecified
    // was added). The booking form requires a real gender; this governs omission.
    public Gender GenderId { get; set; } = Gender.Unspecified;

    public DateTime DateOfBirth { get; set; }

    [StringLength(PatientConsts.PhoneNumberMaxLength)]
    public string? PhoneNumber { get; set; }

    [StringLength(PatientConsts.SocialSecurityNumberMaxLength)]
    public string? SocialSecurityNumber { get; set; }

    [StringLength(PatientConsts.AddressMaxLength)]
    public string? Address { get; set; }

    [StringLength(PatientConsts.CityMaxLength)]
    public string? City { get; set; }

    [StringLength(PatientConsts.ZipCodeMaxLength)]
    public string? ZipCode { get; set; }

    [StringLength(PatientConsts.CellPhoneNumberMaxLength)]
    public string? CellPhoneNumber { get; set; }

    public PhoneNumberType PhoneNumberTypeId { get; set; } = Enum.GetValues<PhoneNumberType>()[0];

    [StringLength(PatientConsts.StreetMaxLength)]
    public string? Street { get; set; }

    [StringLength(PatientConsts.InterpreterVendorNameMaxLength)]
    public string? InterpreterVendorName { get; set; }

    [StringLength(PatientConsts.ApptNumberMaxLength)]
    public string? ApptNumber { get; set; }

    [StringLength(PatientConsts.OthersLanguageNameMaxLength)]
    public string? OthersLanguageName { get; set; }

    public Guid? StateId { get; set; }

    public Guid? AppointmentLanguageId { get; set; }
}
