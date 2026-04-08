using System;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

public class ExternalUserSignUpDto
{
    [Required]
    public ExternalUserType UserType { get; set; }

    [Required]
    [StringLength(128)]
    public string FirstName { get; set; } = null!;

    [Required]
    [StringLength(128)]
    public string LastName { get; set; } = null!;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string Password { get; set; } = null!;

    public Guid? TenantId { get; set; }
}
