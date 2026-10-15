using System.Collections.Generic;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// B12 task 5 -- the startup gate. Every case here is a configuration a real deployment can be in,
/// and the two that throw are the two that would otherwise leave an account on a published password
/// without anything saying so.
/// </summary>
public sealed class AdminPasswordStoreSelectorTests
{
    private static IConfiguration Config(string? vaultUri = null, string? directory = null)
    {
        var values = new Dictionary<string, string?>
        {
            [AdminPasswordStoreSelector.VaultUriKey] = vaultUri,
            [AdminPasswordStoreSelector.DirectoryKey] = directory,
        };

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void AVaultUriAlone_SelectsTheKeyVaultStore()
    {
        AdminPasswordStoreSelector.Select(Config(vaultUri: "https://example.vault.azure.net/"), isDevelopment: false)
            .ShouldBe(AdminPasswordStoreKind.KeyVault);
    }

    [Fact]
    public void ADirectoryAlone_SelectsTheFolderStore()
    {
        AdminPasswordStoreSelector.Select(Config(directory: "/run/admin-passwords"), isDevelopment: false)
            .ShouldBe(AdminPasswordStoreKind.Directory);
    }

    /// <summary>
    /// Refused rather than ranked. A box with both configured has two stores that disagree the
    /// moment either is written, and silently preferring one decides which store the password the
    /// operator wrote down is NOT in.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothSet_ThrowsNamingBothKeys(bool isDevelopment)
    {
        var thrown = Should.Throw<AbpException>(() => AdminPasswordStoreSelector.Select(
            Config(vaultUri: "https://example.vault.azure.net/", directory: "/run/admin-passwords"),
            isDevelopment));

        thrown.Message.ShouldContain(AdminPasswordStoreSelector.VaultUriKey);
        thrown.Message.ShouldContain(AdminPasswordStoreSelector.DirectoryKey);
    }

    /// <summary>
    /// The case that matters most: a production box where nobody set either key must REFUSE TO
    /// START. Starting would seed a published default and look like a successful deploy.
    /// </summary>
    [Fact]
    public void NeitherSetOutsideDevelopment_ThrowsNamingBothKeys()
    {
        var thrown = Should.Throw<AbpException>(
            () => AdminPasswordStoreSelector.Select(Config(), isDevelopment: false));

        thrown.Message.ShouldContain(AdminPasswordStoreSelector.VaultUriKey);
        thrown.Message.ShouldContain(AdminPasswordStoreSelector.DirectoryKey);
    }

    /// <summary>
    /// The control. Development with neither key set keeps today's seeding exactly, so a developer
    /// clone still comes up with the documented credentials and nothing about local work changes.
    /// </summary>
    [Fact]
    public void NeitherSetInDevelopment_SelectsNothingAndDoesNotThrow()
    {
        AdminPasswordStoreSelector.Select(Config(), isDevelopment: true)
            .ShouldBe(AdminPasswordStoreKind.None);
    }

    /// <summary>
    /// Whitespace is not configuration. An env var set to the empty string -- which is exactly what
    /// a compose file produces from an unset variable, as "${ADMIN_PASSWORD_VAULT_URI:-}" -- must
    /// count as absent, or the gate passes on a box that has no store at all.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankValue_CountsAsUnset(string blank)
    {
        Should.Throw<AbpException>(
            () => AdminPasswordStoreSelector.Select(Config(vaultUri: blank, directory: blank), isDevelopment: false));

        AdminPasswordStoreSelector.Select(Config(vaultUri: blank, directory: "/run/admin-passwords"), isDevelopment: false)
            .ShouldBe(AdminPasswordStoreKind.Directory);
    }
}
