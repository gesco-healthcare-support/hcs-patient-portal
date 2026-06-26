namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Public per-office branding for the CURRENT office (resolved by subdomain).
/// Returned by the AllowAnonymous GetBranding endpoint for the login page + SPA
/// boot. <see cref="LogoUrl"/> is a relative serve path (+ cache-buster); the SPA
/// resolves it against its API root.
/// </summary>
public class BrandingDto
{
    /// <summary>Office display name; null = caller should fall back to the default brand.</summary>
    public string? DisplayName { get; set; }

    /// <summary>True when the office has a custom logo on file.</summary>
    public bool HasLogo { get; set; }

    /// <summary>Relative serve path for the office logo (+ cache-buster); null when none.</summary>
    public string? LogoUrl { get; set; }
}
