namespace HealthcareSupport.CaseEvaluation.Saas;

/// <summary>
/// One office to seed: the SaaS tenant identity (Name + slug), the human-facing branding display
/// name, the office `admin` login email, and the single owner doctor (profile only -- doctors do
/// not log in). Logos are uploaded in-app per office.
/// </summary>
public sealed record OfficeSeedEntry(
    string Slug,
    string TenantName,
    string DisplayName,
    string AdminEmail,
    string DoctorFirstName,
    string DoctorLastName,
    string DoctorEmail);

/// <summary>
/// The ONE synthetic office every environment seeds, production included, so a fresh deployment
/// has an office to exercise end to end without touching a real practice's data. It is the single
/// source of truth for the tenant-registration seeder (tenant + connection string + branding), the
/// migrator loop (the office admin email), the location seeder (its one clinic) and the
/// doctor-profile seeder (its doctor). Tenant Name resolves the subdomain; Slug drives the database
/// name (CaseEvaluation_{slug}); DisplayName is the brand shown to users.
///
/// <para>Every value is synthetic (TEST- names, example.test addresses) because this repository is
/// public. Real practices are created through New Practice, never from this list.</para>
///
/// <para>Outside Development the TEST office gets no admin user (see
/// Identity/CaseEvaluationIdentityDataSeedContributor), so <see cref="OfficeSeedEntry.AdminEmail"/>
/// only matters on a local stack.</para>
/// </summary>
public static class OfficeSeedData
{
    public static readonly IReadOnlyList<OfficeSeedEntry> Offices = new[]
    {
        new OfficeSeedEntry("test-office", "TEST-Office", "TEST Office",
            "test.admin@example.test", "TEST-Doctor", "TEST-Office", "test.doctor@example.test"),
    };

    /// <summary>The synthetic office itself.</summary>
    public static OfficeSeedEntry TestOffice => Offices[0];

    public static OfficeSeedEntry? FindByTenantName(string? tenantName) =>
        tenantName == null
            ? null
            : Offices.FirstOrDefault(o =>
                string.Equals(o.TenantName, tenantName, StringComparison.OrdinalIgnoreCase));
}
