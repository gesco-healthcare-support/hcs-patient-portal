using System;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// DTO for Applicant Attorney details used in appointment add/view.
/// </summary>
public class ApplicantAttorneyDetailsDto
{
    public Guid? ApplicantAttorneyId { get; set; }

    public Guid IdentityUserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? FirmName { get; set; }

    public string? WebAddress { get; set; }

    public string? PhoneNumber { get; set; }

    public string? FaxNumber { get; set; }

    public string? Street { get; set; }

    public string? City { get; set; }

    public Guid? StateId { get; set; }

    public string? ZipCode { get; set; }

    public string? ConcurrencyStamp { get; set; }
}
