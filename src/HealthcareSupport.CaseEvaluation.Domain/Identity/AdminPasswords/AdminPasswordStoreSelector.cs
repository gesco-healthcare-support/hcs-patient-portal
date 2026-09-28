using Microsoft.Extensions.Configuration;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;

/// <summary>Which admin-password store a process is configured to use.</summary>
public enum AdminPasswordStoreKind
{
    /// <summary>
    /// None configured. Reachable ONLY in Development, where seeding stays exactly as it was before
    /// B12 and the published default password is used on purpose.
    /// </summary>
    None,

    /// <summary>Azure Key Vault, selected by <c>AdminPasswords:VaultUri</c>.</summary>
    KeyVault,

    /// <summary>A folder on the box, selected by <c>AdminPasswords:Directory</c>.</summary>
    Directory,
}

/// <summary>
/// B12 -- the single place that decides which store is in use, and the startup gate that refuses an
/// ambiguous or absent configuration outside Development.
///
/// <para>Both the DbMigrator and the API call this at startup. Both, because the in-house server
/// runs both in Production and either may be the process that first creates a database: a rule
/// enforced in only one of them would let the other seed a published default.</para>
///
/// <para>Exactly one key must be set. BOTH set is refused rather than ranked, because a box with
/// both configured has two stores that disagree the moment either is written, and picking a winner
/// silently would decide which one the operator's recorded password is NOT in.</para>
/// </summary>
public static class AdminPasswordStoreSelector
{
    public const string VaultUriKey = "AdminPasswords:VaultUri";

    public const string DirectoryKey = "AdminPasswords:Directory";

    /// <summary>
    /// The configured store, or <see cref="AdminPasswordStoreKind.None"/> in Development with
    /// neither key set. Throws when the configuration is ambiguous or, outside Development, absent.
    /// </summary>
    public static AdminPasswordStoreKind Select(IConfiguration configuration, bool isDevelopment)
    {
        Check.NotNull(configuration, nameof(configuration));

        var hasVault = !string.IsNullOrWhiteSpace(configuration[VaultUriKey]);
        var hasDirectory = !string.IsNullOrWhiteSpace(configuration[DirectoryKey]);

        if (hasVault && hasDirectory)
        {
            throw new AbpException(
                "Both " + VaultUriKey + " and " + DirectoryKey + " are set. Exactly one admin-password " +
                "store must be configured: with two, an entry written to one is invisible to the " +
                "other, and the password an operator holds may not be the one the account has. Unset " +
                "whichever does not apply to this host. The values are not shown here on purpose.");
        }

        if (hasVault)
        {
            return AdminPasswordStoreKind.KeyVault;
        }

        if (hasDirectory)
        {
            return AdminPasswordStoreKind.Directory;
        }

        if (!isDevelopment)
        {
            throw new AbpException(
                "Neither " + VaultUriKey + " nor " + DirectoryKey + " is set. Outside Development " +
                "exactly one must be, so that each database's admin password is generated and stored " +
                "rather than left on a published default. Set " + VaultUriKey + " on Azure, or " +
                DirectoryKey + " on a self-hosted box.");
        }

        return AdminPasswordStoreKind.None;
    }
}
