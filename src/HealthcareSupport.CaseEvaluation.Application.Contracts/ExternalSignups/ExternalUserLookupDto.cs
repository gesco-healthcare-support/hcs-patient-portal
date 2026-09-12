using System;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

public class ExternalUserLookupDto
{
    public Guid IdentityUserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string UserRole { get; set; } = string.Empty;

    /// <summary>
    /// Phase 1 / C2 / D4 (firm-based AA/DA registration, 2026-06-11) -- the
    /// external user's law-firm name (FirmName IdentityUser extension property),
    /// empty when not set. The picker renders the display label via
    /// <c>resolveExternalUserDisplayName</c>, falling back to this when
    /// First/Last are blank (the firm-account shape).
    /// </summary>
    public string FirmName { get; set; } = string.Empty;
}
