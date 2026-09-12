using System.ComponentModel.DataAnnotations;
using HealthcareSupport.CaseEvaluation.Appointments;

namespace HealthcareSupport.CaseEvaluation.UserQueries;

/// <summary>
/// Submit payload for the "Help / Need Question?" (Contact-Us) modal.
/// <see cref="Message"/> is the required free-text question.
/// <see cref="RequestConfirmationNumber"/> is the optional appointment token
/// the user supplies when asking about a specific appointment -- it drives
/// email routing only and is NOT persisted (OLD kept it transient).
/// </summary>
public class UserQueryCreateDto
{
    [Required]
    [StringLength(UserQueryConsts.MessageMaxLength)]
    public string Message { get; set; } = string.Empty;

    [StringLength(AppointmentConsts.RequestConfirmationNumberMaxLength)]
    public string? RequestConfirmationNumber { get; set; }
}
