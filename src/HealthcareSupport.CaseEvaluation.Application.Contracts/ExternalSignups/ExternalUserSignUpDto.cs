using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

public class ExternalUserSignUpDto
{
    [Required]
    public ExternalUserType UserType { get; set; }

    // Adrian (2026-04-30): names are NOT collected on the register page.
    // They are captured later on the booking form's patient/AA section, so
    // these are nullable here. The server stores them as-is on IdentityUser
    // (Name/Surname) when supplied; otherwise leaves them null.
    [StringLength(128)]
    public string? FirstName { get; set; }

    [StringLength(128)]
    public string? LastName { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string Password { get; set; } = null!;

    public Guid? TenantId { get; set; }
}
