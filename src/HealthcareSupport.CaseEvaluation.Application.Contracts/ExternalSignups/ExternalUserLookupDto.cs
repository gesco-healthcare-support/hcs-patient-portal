using System;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

public class ExternalUserLookupDto
{
    public Guid IdentityUserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string UserRole { get; set; } = string.Empty;
}
