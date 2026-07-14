using HealthcareSupport.CaseEvaluation.Notifications;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Task A (BUG-014, 2026-05-20) + T10/G3 (in-house hosting, 2026-07-09) -- pure unit tests
/// for <see cref="TenantUrlComposer"/>.
///
/// <para>T10 changes the composer from a bare-localhost-only swap to "prepend the office
/// slug as the leftmost host label of any hostname", matching the frontend
/// <c>angular/src/tenant-bootstrap.ts</c> prependSlug rule so email links work in production
/// (base URL <c>https://portal.example.com</c> -> <c>https://falkinstein.portal.example.com</c>)
/// as well as local dev (<c>http://localhost:4200</c> -> <c>http://falkinstein.localhost:4200</c>).
/// It still skips IP-address hosts (can't subdomain an IP) and is idempotent for URLs that
/// already carry the office subdomain.</para>
/// </summary>
public class TenantUrlComposerUnitTests
{
    [Fact]
    public void ComposeForTenant_BareLocalhostWithTenant_PrependsSubdomain()
    {
        TenantUrlComposer.ComposeForTenant("http://localhost:4200", "Falkinstein")
            .ShouldBe("http://falkinstein.localhost:4200");
    }

    [Fact]
    public void ComposeForTenant_ProductionBaseHost_PrependsOfficeSubdomain()
    {
        // T10: the prod PortalBaseUrl (App:AngularUrl default) is the office-less base host;
        // the composer prepends the office so email links land on the office SPA.
        TenantUrlComposer.ComposeForTenant("https://portal.example.com", "Falkinstein")
            .ShouldBe("https://falkinstein.portal.example.com");
    }

    [Fact]
    public void ComposeForTenant_RealDomainWithPort_PrependsOfficeSubdomain()
    {
        TenantUrlComposer.ComposeForTenant("http://example.com:4200", "Falkinstein")
            .ShouldBe("http://falkinstein.example.com:4200");
    }

    [Fact]
    public void ComposeForTenant_NullTenant_ReturnsUrlUnchanged()
    {
        TenantUrlComposer.ComposeForTenant("http://localhost:4200", null)
            .ShouldBe("http://localhost:4200");
    }

    [Fact]
    public void ComposeForTenant_EmptyTenant_ReturnsUrlUnchanged()
    {
        TenantUrlComposer.ComposeForTenant("http://localhost:4200", string.Empty)
            .ShouldBe("http://localhost:4200");
    }

    [Fact]
    public void ComposeForTenant_UrlAlreadyHasLocalhostSubdomain_IsIdempotent()
    {
        TenantUrlComposer.ComposeForTenant("http://falkinstein.localhost:4200", "Falkinstein")
            .ShouldBe("http://falkinstein.localhost:4200");
    }

    [Fact]
    public void ComposeForTenant_UrlAlreadyHasProdSubdomain_IsIdempotent()
    {
        TenantUrlComposer.ComposeForTenant("https://falkinstein.portal.example.com", "Falkinstein")
            .ShouldBe("https://falkinstein.portal.example.com");
    }

    [Fact]
    public void ComposeForTenant_IpAddressHost_ReturnsUrlUnchanged()
    {
        // Can't subdomain an IP address -- leave it alone.
        TenantUrlComposer.ComposeForTenant("http://127.0.0.1:4200", "Falkinstein")
            .ShouldBe("http://127.0.0.1:4200");
    }

    [Fact]
    public void ComposeForTenant_UrlWithPathAndQuery_PreservesPathAndQuery()
    {
        TenantUrlComposer.ComposeForTenant("https://portal.example.com/confirm?token=abc", "Falkinstein")
            .ShouldBe("https://falkinstein.portal.example.com/confirm?token=abc");
    }

    [Fact]
    public void ComposeForTenant_TenantNameIsLowercased()
    {
        TenantUrlComposer.ComposeForTenant("https://portal.example.com", "FALKINSTEIN")
            .ShouldBe("https://falkinstein.portal.example.com");
    }

    [Fact]
    public void ComposeForTenant_NullBaseUrl_ReturnsNull()
    {
        TenantUrlComposer.ComposeForTenant(null, "Falkinstein").ShouldBeNull();
    }
}
