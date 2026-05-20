using HealthcareSupport.CaseEvaluation.Notifications;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Task A (BUG-014 fix, 2026-05-20) -- pure unit tests for
/// <see cref="TenantUrlComposer"/>. Verifies the bare-localhost
/// host-token regex behavior against the 8 cases enumerated in
/// <c>docs/superpowers/specs/2026-05-20-task-a-config-driven-email-urls.md</c>
/// section 6.
///
/// <para>The regex pattern <c>(^|//)localhost(?=([:/]|$))</c> is lifted
/// byte-for-byte from <c>angular/src/tenant-bootstrap.ts:99</c> so the
/// frontend (subdomain bootstrap) and backend (email URL rendering)
/// share one substitution rule. These tests pin that behavior on the
/// backend side.</para>
/// </summary>
public class TenantUrlComposerUnitTests
{
    [Fact]
    public void ComposeForTenant_BareLocalhostWithTenant_PrependsSubdomain()
    {
        var result = TenantUrlComposer.ComposeForTenant(
            baseUrl: "http://localhost:4200",
            tenantName: "Falkinstein");

        result.ShouldBe("http://falkinstein.localhost:4200");
    }

    [Fact]
    public void ComposeForTenant_NullTenant_ReturnsUrlUnchanged()
    {
        var result = TenantUrlComposer.ComposeForTenant(
            baseUrl: "http://localhost:4200",
            tenantName: null);

        result.ShouldBe("http://localhost:4200");
    }

    [Fact]
    public void ComposeForTenant_EmptyTenant_ReturnsUrlUnchanged()
    {
        var result = TenantUrlComposer.ComposeForTenant(
            baseUrl: "http://localhost:4200",
            tenantName: string.Empty);

        result.ShouldBe("http://localhost:4200");
    }

    [Fact]
    public void ComposeForTenant_UrlAlreadyHasSubdomain_IsIdempotent()
    {
        var result = TenantUrlComposer.ComposeForTenant(
            baseUrl: "http://falkinstein.localhost:4200",
            tenantName: "Falkinstein");

        result.ShouldBe("http://falkinstein.localhost:4200");
    }

    [Fact]
    public void ComposeForTenant_IpAddressHost_ReturnsUrlUnchanged()
    {
        var result = TenantUrlComposer.ComposeForTenant(
            baseUrl: "http://127.0.0.1:4200",
            tenantName: "Falkinstein");

        result.ShouldBe("http://127.0.0.1:4200");
    }

    [Fact]
    public void ComposeForTenant_RealDomainHost_ReturnsUrlUnchanged()
    {
        var result = TenantUrlComposer.ComposeForTenant(
            baseUrl: "http://example.com:4200",
            tenantName: "Falkinstein");

        result.ShouldBe("http://example.com:4200");
    }

    [Fact]
    public void ComposeForTenant_UrlWithPathAndQuery_PreservesPathAndQuery()
    {
        var result = TenantUrlComposer.ComposeForTenant(
            baseUrl: "http://localhost:4200/some/path?q=1",
            tenantName: "Falkinstein");

        result.ShouldBe("http://falkinstein.localhost:4200/some/path?q=1");
    }

    [Fact]
    public void ComposeForTenant_NullBaseUrl_ReturnsNull()
    {
        var result = TenantUrlComposer.ComposeForTenant(
            baseUrl: null,
            tenantName: "Falkinstein");

        result.ShouldBeNull();
    }
}
