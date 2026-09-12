using System;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// 2026-06-30 (QA item B) -- one internal-user row for the Staff table. Carries
/// the resolved primary internal role (precedence IT Admin &gt; Staff Supervisor
/// &gt; Intake Staff) and the composed full name so the client renders each row
/// without a second lookup. Row shape mirrors the former client-side
/// <c>InternalUserRow</c>.
/// </summary>
public class InternalUserListDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
