using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// 2026-10-26 -- the role a staff member is switched to by Staff Management's "Change role": Intake Staff
/// or Staff Supervisor. Anything else is refused by the service.
/// </summary>
public class ChangeInternalUserRoleDto
{
    [Required]
    public string RoleName { get; set; } = string.Empty;
}
