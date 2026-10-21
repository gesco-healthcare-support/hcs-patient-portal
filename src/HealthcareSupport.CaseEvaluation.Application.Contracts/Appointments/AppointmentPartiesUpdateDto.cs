using System;
using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Validation;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Staff "Edit details" change to the parties on an appointment, at any status (2026-10-21).
///
/// <para>Each party is optional: <c>null</c> leaves that party exactly as it is. A party with
/// <c>Remove = true</c> is taken off the appointment. Otherwise the party is added, or its details
/// changed, and its email becomes the appointment's party-email column for that role.</para>
///
/// <para><b>Staff only.</b> Those columns are what <c>AppointmentAccessRules</c> reads to let a
/// party open the case, so whoever writes them decides who can see it. The service refuses every
/// caller that is not internal staff; external parties keep the read-only screen and the Send Back
/// fix-it page.</para>
/// </summary>
public class AppointmentPartiesUpdateDto
{
    /// <summary>The patient's email on this appointment. <c>null</c> = unchanged.</summary>
    [EmailAddress]
    [StringLength(AppointmentConsts.PartyEmailMaxLength)]
    public string? PatientEmail { get; set; }

    public StaffAttorneyChangeDto? ApplicantAttorney { get; set; }

    public StaffAttorneyChangeDto? DefenseAttorney { get; set; }

    public StaffClaimExaminerChangeDto? ClaimExaminer { get; set; }
}

/// <summary>An applicant or defense attorney as staff typed it, or a removal.</summary>
public class StaffAttorneyChangeDto
{
    public bool Remove { get; set; }

    /// <summary>
    /// The attorney record loaded with the page, when the email is unchanged. Ignored when it
    /// names a different email, so replacing one attorney with another never rewrites the first
    /// one's record.
    /// </summary>
    public Guid? AttorneyId { get; set; }

    [StringLength(DefenseAttorneyConsts.FirstNameMaxLength)]
    public string? FirstName { get; set; }

    [StringLength(DefenseAttorneyConsts.LastNameMaxLength)]
    public string? LastName { get; set; }

    [EmailAddress]
    [StringLength(DefenseAttorneyConsts.EmailMaxLength)]
    public string? Email { get; set; }

    [StringLength(DefenseAttorneyConsts.FirmNameMaxLength)]
    public string? FirmName { get; set; }

    [StringLength(DefenseAttorneyConsts.WebAddressMaxLength)]
    public string? WebAddress { get; set; }

    [PhoneNumber]
    public string? PhoneNumber { get; set; }

    [PhoneNumber]
    public string? FaxNumber { get; set; }

    [StringLength(DefenseAttorneyConsts.StreetMaxLength)]
    public string? Street { get; set; }

    [StringLength(DefenseAttorneyConsts.CityMaxLength)]
    public string? City { get; set; }

    public Guid? StateId { get; set; }

    [StringLength(DefenseAttorneyConsts.ZipCodeMaxLength)]
    public string? ZipCode { get; set; }
}

/// <summary>The claim examiner as staff typed it, or a removal.</summary>
public class StaffClaimExaminerChangeDto
{
    public bool Remove { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.NameMaxLength)]
    public string? Name { get; set; }

    [EmailAddress]
    [StringLength(AppointmentClaimExaminerConsts.EmailMaxLength)]
    public string? Email { get; set; }

    [PhoneNumber]
    public string? PhoneNumber { get; set; }

    [PhoneNumber]
    public string? Fax { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.SuiteMaxLength)]
    public string? Suite { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.StreetMaxLength)]
    public string? Street { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.CityMaxLength)]
    public string? City { get; set; }

    public Guid? StateId { get; set; }

    [StringLength(AppointmentClaimExaminerConsts.ZipMaxLength)]
    public string? Zip { get; set; }
}
