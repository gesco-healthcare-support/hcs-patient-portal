namespace HealthcareSupport.CaseEvaluation.Saas;

/// <summary>
/// One office to seed in Development: the SaaS tenant identity (Name + slug), the
/// human-facing branding display name, the office `admin` login email, and the single
/// owner doctor (profile only -- doctors do not log in). The seed password is the shared
/// dev default with force-reset-on-first-login (see InternalUsersDataSeedContributor);
/// logos are uploaded in-app per office. Dev-gated and disposable (feature-branch DB).
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
/// The offices seeded for multi-office testing (Falkinstein + 3 more), single source of
/// truth shared by the tenant-registration seeder (creates tenant + connection string +
/// branding), the migrator loop (per-office admin email), and the doctor-profile seeder
/// (per-office doctor). Tenant Name resolves the subdomain; Slug drives the database name
/// (CaseEvaluation_{slug}); DisplayName is the brand shown to users.
/// </summary>
public static class OfficeSeedData
{
    public static readonly IReadOnlyList<OfficeSeedEntry> Offices = new[]
    {
        new OfficeSeedEntry("falkinstein", "Falkinstein", "Dr. Yuri Falkinstein",
            "adriang@evaluators.com", "Yuri", "Falkinstein", "yf@socalpm.com"),
        new OfficeSeedEntry("hekmat", "Hekmat", "Dr. Hekmat",
            "adriang@evaluators.com", "Farshid", "Hekmat", "fh@socalpm.com"),
        new OfficeSeedEntry("longacre", "Longacre", "Dr. Longacre",
            "adriang@evaluators.com", "Matthew", "Longacre", "mlc@socalpm.com"),
        new OfficeSeedEntry("pelton", "Pelton", "Dr. Pelton",
            "adriang@evaluators.com", "Kevin", "Pelton", "kp@socalpm.com"),
    };

    public static OfficeSeedEntry? FindByTenantName(string? tenantName) =>
        tenantName == null
            ? null
            : Offices.FirstOrDefault(o =>
                string.Equals(o.TenantName, tenantName, StringComparison.OrdinalIgnoreCase));
}
