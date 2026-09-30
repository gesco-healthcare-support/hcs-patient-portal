using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.OpenIddict;
using Microsoft.Extensions.Configuration;
using OpenIddict.Abstractions;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.OpenIddict.Applications;
using Volo.Abp.OpenIddict.Scopes;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Seeding;

/// <summary>
/// <see cref="OpenIddictDataSeedContributor"/>'s client registrations: the Angular / console client
/// and the Swagger client are created only when their client ids are configured, with the
/// redirect URIs, grant types and client URI derived from the configured root URL. A second run
/// updates them in place rather than adding duplicates.
///
/// <para>The rig configures no clients, so the contributor is constructed here with an IN-MEMORY
/// configuration and the real repositories and managers. That keeps the configuration local to
/// this test; nothing process-wide changes. Client registrations are host-level rows, so there is
/// no office to decoy.</para>
/// </summary>
public class OpenIddictSeedTests : SeedContributorTestBase
{
    private const string AngularClientId = "TEST-angular-client";
    private const string SwaggerClientId = "TEST-swagger-client";

    private readonly IOpenIddictApplicationRepository _applications;

    public OpenIddictSeedTests()
    {
        _applications = GetRequiredService<IOpenIddictApplicationRepository>();
    }

    [Fact]
    public async Task ConfiguredClients_AreRegisteredFromTheirRootUrls_AndASecondRunUpdatesInPlace()
    {
        var seeder = SeederWith(new Dictionary<string, string?>
        {
            ["OpenIddict:Applications:CaseEvaluation_App:ClientId"] = AngularClientId,
            ["OpenIddict:Applications:CaseEvaluation_App:RootUrl"] = "https://portal.example.test/",
            ["OpenIddict:Applications:CaseEvaluation_Swagger:ClientId"] = SwaggerClientId,
            ["OpenIddict:Applications:CaseEvaluation_Swagger:RootUrl"] = "https://api.example.test",
        });

        await SeedAsync(seeder, new DataSeedContext());
        await SeedAsync(seeder, new DataSeedContext());

        var all = await InScopeAsync(null, () => _applications.GetListAsync());
        all.Count(a => a.ClientId == AngularClientId).ShouldBe(1);
        all.Count(a => a.ClientId == SwaggerClientId).ShouldBe(1);

        var angular = all.Single(a => a.ClientId == AngularClientId);
        angular.ClientType.ShouldBe(OpenIddictConstants.ClientTypes.Public);
        angular.ConsentType.ShouldBe(OpenIddictConstants.ConsentTypes.Implicit);
        angular.ClientUri.ShouldBe("https://portal.example.test"); // the trailing slash is trimmed
        angular.RedirectUris.ShouldNotBeNull().ShouldContain("\"https://portal.example.test\"");
        angular.PostLogoutRedirectUris.ShouldNotBeNull().ShouldContain("\"https://portal.example.test\"");
        angular.Permissions.ShouldNotBeNull().ShouldContain(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        angular.Permissions.ShouldContain(OpenIddictConstants.Permissions.Prefixes.GrantType + "Impersonation");

        var swagger = all.Single(a => a.ClientId == SwaggerClientId);
        swagger.ClientUri.ShouldBe("https://api.example.test/swagger");
        swagger.RedirectUris.ShouldNotBeNull().ShouldContain("\"https://api.example.test/swagger/oauth2-redirect.html\"");
        swagger.Permissions.ShouldNotBeNull().ShouldNotContain(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
    }

    [Fact]
    public async Task WithNoClientsConfigured_NoApplicationIsRegistered()
    {
        var before = await InScopeAsync(null, () => _applications.GetListAsync());

        await SeedAsync(SeederWith(new Dictionary<string, string?>()), new DataSeedContext());

        (await InScopeAsync(null, () => _applications.GetListAsync())).Count.ShouldBe(before.Count);
    }

    private OpenIddictDataSeedContributor SeederWith(Dictionary<string, string?> settings) =>
        new(
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            _applications,
            GetRequiredService<IAbpApplicationManager>(),
            GetRequiredService<IOpenIddictScopeRepository>(),
            GetRequiredService<IOpenIddictScopeManager>());
}
