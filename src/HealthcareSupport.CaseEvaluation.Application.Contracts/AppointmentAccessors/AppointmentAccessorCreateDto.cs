using HealthcareSupport.CaseEvaluation.Enums;
using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

/// <summary>
/// G-10-08 (Group J): accessors are added by free-typed name + email + role.
/// The server resolves the email to an existing user or auto-provisions + invites
/// one (see <c>AppointmentAccessorManager.CreateOrLinkAsync</c>) -- no pre-existing
/// user id is required, restoring the OLD email-based create-or-link flow.
/// </summary>
public class AppointmentAccessorCreateDto
{
    public Guid AppointmentId { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(64)]
    public string? FirstName { get; set; }

    [StringLength(64)]
    public string? LastName { get; set; }

    /// <summary>External role name granted to the accessor (e.g. "Applicant Attorney").</summary>
    [Required]
    [StringLength(256)]
    public string Role { get; set; } = string.Empty;

    public AccessType AccessTypeId { get; set; } = Enum.GetValues<AccessType>()[0];
}
