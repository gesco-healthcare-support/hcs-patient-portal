using System;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// One office's branding row for the host-side central manager grid.
/// <see cref="OfficeName"/> is the Volo SaaS tenant name; the other fields reflect
/// the per-office branding row (null/false when the office has none yet).
/// <see cref="LogoUrl"/> is office-qualified because the host surface
/// (admin.localhost) has no subdomain office to resolve.
/// </summary>
public class OfficeBrandingDto
{
    public Guid OfficeId { get; set; }

    public string OfficeName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public bool HasLogo { get; set; }

    public string? LogoUrl { get; set; }
}
