using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Result of <see cref="IExternalSignupAppService.DeleteTestUsersAsync"/>.
/// Lists which emails were found+deleted vs. which were not present.
/// </summary>
public class DeleteTestUsersResultDto
{
    public IList<string> Deleted { get; set; } = new List<string>();
    public IList<string> NotFound { get; set; } = new List<string>();
}
