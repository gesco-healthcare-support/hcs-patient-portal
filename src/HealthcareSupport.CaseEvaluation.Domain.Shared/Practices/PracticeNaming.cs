using System.Linq;

namespace HealthcareSupport.CaseEvaluation.Practices;

/// <summary>
/// Naming conventions for a practice (Volo SaaS tenant === one doctor's office).
///
/// The practice slug (subdomain + database) is validated by
/// <see cref="MultiTenancy.TenantNaming"/>. This helper owns the human-facing
/// branding display name: when the creator leaves it blank, it defaults to
/// "Dr. {FirstName} {LastName}" -- "Dr." fixed, the names taken from the doctor
/// entered on the New Practice form. Kept pure (no DI) so it unit-tests directly
/// and both the create flow and its tests share one source of truth.
/// </summary>
public static class PracticeNaming
{
    /// <summary>The fixed honorific prefixing a derived practice display name.</summary>
    public const string DoctorTitlePrefix = "Dr.";

    /// <summary>
    /// Builds the default practice display name "Dr. {FirstName} {LastName}" from the
    /// doctor's names. Each name is trimmed; blank names are dropped (so a missing
    /// last name yields "Dr. {FirstName}"). When both are blank, returns just the
    /// "Dr." prefix.
    /// </summary>
    public static string DefaultDisplayName(string? firstName, string? lastName)
    {
        var name = string.Join(
            " ",
            new[] { firstName, lastName }
                .Select(part => part?.Trim())
                .Where(part => !string.IsNullOrEmpty(part)));

        return string.IsNullOrEmpty(name) ? DoctorTitlePrefix : $"{DoctorTitlePrefix} {name}";
    }
}
