using System;

namespace HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;

/// <summary>
/// B12 -- the one name scheme, used as secret names in the Key Vault store and as file names in the
/// folder store. Shared so the two stores cannot drift apart, and so a box migrated from one to the
/// other keeps its entries addressable.
/// </summary>
public static class AdminPasswordNames
{
    /// <summary>The host database's entry.</summary>
    public const string Host = "admin-password-host";

    private const string OfficePrefix = "admin-password-office-";

    /// <summary>
    /// The entry name for one database: <see cref="Host"/> when <paramref name="tenantId"/> is null,
    /// otherwise the office prefix plus the tenant id.
    ///
    /// <para>The TENANT ID, not the office name, so a rename does not orphan the entry -- the slug an
    /// office is reached by is its lowercased name, so names do change.</para>
    ///
    /// <para>Both stores accept the result: Key Vault allows 0-9, a-z, A-Z and hyphen up to 127
    /// characters, and the longest name here is 58. The same set is safe as a file name on Linux and
    /// on Windows, and contains no path separator or parent reference.</para>
    /// </summary>
    public static string For(Guid? tenantId)
    {
        if (tenantId is null)
        {
            return Host;
        }

        // "D" is the hyphenated 36-character form. .NET already emits it lowercase; ToLowerInvariant
        // is kept so the name does not depend on that staying true.
        return OfficePrefix + tenantId.Value.ToString("D").ToLowerInvariant();
    }
}
