using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation;

public static class CaseEvaluationConsts
{
    public const string DbTablePrefix = "App";
    public const string? DbSchema = null;
    public const string AdminEmailDefaultValue = IdentityDataSeedContributor.AdminEmailDefaultValue;
    public const string AdminPasswordDefaultValue = IdentityDataSeedContributor.AdminPasswordDefaultValue;

    /// <summary>
    /// The username ABP's IdentityDataSeeder gives the admin account it creates, in every database.
    /// The framework does not expose it as a constant -- it is a literal inside
    /// <c>IdentityDataSeeder.SeedAsync</c> -- so it is named here once rather than spelled inline
    /// wherever that account has to be found again.
    /// </summary>
    public const string AdminUserName = "admin";
}
