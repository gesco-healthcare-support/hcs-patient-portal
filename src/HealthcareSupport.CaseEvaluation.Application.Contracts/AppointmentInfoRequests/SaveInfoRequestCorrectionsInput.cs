using System;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// The external user's fix-it corrections for an InfoRequested appointment. Every
/// property is optional -- a null (or, for value types, an absent) value means "no
/// change" and the existing value is preserved. Each PROVIDED value is locked
/// server-side to the open request's flagged-field set (see
/// <c>SaveCorrectionsAsync</c>) so the requester can only change what staff
/// flagged. The keys mirror the frontend send-back-fields registry.
/// </summary>
public class SaveInfoRequestCorrectionsInput
{
    public DateTime? DateOfBirth { get; set; }

    public string? SocialSecurityNumber { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    public Guid? StateId { get; set; }

    public string? ZipCode { get; set; }

    public string? CellPhoneNumber { get; set; }

    public Guid? AppointmentLanguageId { get; set; }

    public string? ApplicantAttorneyEmail { get; set; }

    public string? ClaimExaminerEmail { get; set; }

    public string? InsuranceName { get; set; }

    public string? InsurancePhoneNumber { get; set; }

    public string? DefenseAttorneyFirmName { get; set; }
}
