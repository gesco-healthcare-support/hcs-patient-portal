using System.Reflection;
using System.Runtime.CompilerServices;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// BUG-029 v3 fix (2026-05-21) -- unit tests for the centralized
/// <see cref="AccountUrlBuilder"/>. Stubs <see cref="ISettingProvider"/>,
/// <see cref="IRepository{Tenant, Guid}"/> and <see cref="ICurrentTenant"/>
/// via NSubstitute. Covers: hard-fail paths (missing tenant, missing
/// setting, missing tenant row), happy path for all 5 URL methods, and
/// the token-URL-encoding contract.
///
/// <para>Tenant rows are materialized via
/// <see cref="RuntimeHelpers.GetUninitializedObject(System.Type)"/> because
/// <see cref="Tenant"/>'s only non-trivial constructor is
/// <c>protected internal</c> -- not callable from this assembly. Id and
/// Name (both with protected setters) are set via reflection.</para>
/// </summary>
public class AccountUrlBuilderTests
{
    private static readonly Guid FalkinsteinTenantId =
        new("961615d9-c8db-ec97-0334-3a215e75935f");
    private const string TenantName = "Falkinstein";
    private const string ConfiguredAuthServerUrl = "http://localhost:44398";
    private const string ConfiguredPortalUrl = "http://localhost:4200";

    // ------------------------------------------------------------------
    // Happy paths -- one per URL method
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuildEmailConfirmationUrlAsync_KnownTenant_PrependsSubdomainAndEncodesToken()
    {
        var sut = NewBuilder(authServerUrl: ConfiguredAuthServerUrl, tenantName: TenantName);
        var userId = new Guid("703850fc-ab36-6e2f-24cf-3a215e214e36");

        var url = await sut.BuildEmailConfirmationUrlAsync(
            FalkinsteinTenantId, userId, "raw token /+=");

        url.ShouldBe(
            $"http://falkinstein.localhost:44398/Account/EmailConfirmation" +
            $"?userId={userId}&confirmationToken=raw+token+%2F%2B%3D");
    }

    [Fact]
    public async Task BuildPasswordResetUrlAsync_KnownTenant_PrependsSubdomainAndEncodesToken()
    {
        var sut = NewBuilder(authServerUrl: ConfiguredAuthServerUrl, tenantName: TenantName);
        var userId = new Guid("703850fc-ab36-6e2f-24cf-3a215e214e36");

        var url = await sut.BuildPasswordResetUrlAsync(
            FalkinsteinTenantId, userId, "token with /+= chars");

        url.ShouldBe(
            $"http://falkinstein.localhost:44398/Account/ResetPassword" +
            $"?userId={userId}&resetToken=token+with+%2F%2B%3D+chars");
    }

    [Fact]
    public async Task BuildInviteUrlAsync_KnownTenant_PrependsSubdomainAndEncodesToken()
    {
        var sut = NewBuilder(authServerUrl: ConfiguredAuthServerUrl, tenantName: TenantName);

        var url = await sut.BuildInviteUrlAsync(
            FalkinsteinTenantId, "abc/def+ghi==");

        url.ShouldBe(
            "http://falkinstein.localhost:44398/Account/Register" +
            "?inviteToken=abc%2Fdef%2Bghi%3D%3D");
    }

    [Fact]
    public async Task BuildPortalRootUrlAsync_KnownTenant_ReturnsComposedRootOnly()
    {
        var sut = NewBuilder(portalUrl: ConfiguredPortalUrl, tenantName: TenantName);

        var url = await sut.BuildPortalRootUrlAsync(FalkinsteinTenantId);

        url.ShouldBe("http://falkinstein.localhost:4200");
    }

    [Fact]
    public async Task BuildAuthServerRootUrlAsync_KnownTenant_ReturnsComposedRootOnly()
    {
        var sut = NewBuilder(authServerUrl: ConfiguredAuthServerUrl, tenantName: TenantName);

        var url = await sut.BuildAuthServerRootUrlAsync(FalkinsteinTenantId);

        url.ShouldBe("http://falkinstein.localhost:44398");
    }

    [Fact]
    public async Task BuildPortalRootUrlAsync_NullTenant_ReturnsHostScopeUrlUnchanged()
    {
        var sut = NewBuilder(portalUrl: ConfiguredPortalUrl, tenantName: TenantName);

        var url = await sut.BuildPortalRootUrlAsync(tenantId: null);

        url.ShouldBe("http://localhost:4200");
    }

    [Fact]
    public async Task BuildPublicDocumentUploadUrlAsync_KnownTenant_ComposesPortalUrlWithPathParams()
    {
        var sut = NewBuilder(portalUrl: ConfiguredPortalUrl, tenantName: TenantName);
        var documentId = new Guid("11111111-1111-4111-8111-111111111111");
        var code = new Guid("22222222-2222-4222-8222-222222222222");

        var url = await sut.BuildPublicDocumentUploadUrlAsync(FalkinsteinTenantId, documentId, code);

        url.ShouldBe(
            $"http://falkinstein.localhost:4200/public/document-upload/{documentId}/{code}");
    }

    [Fact]
    public async Task BuildPublicDocumentUploadUrlAsync_EmptyTenantId_ThrowsArgumentException()
    {
        var sut = NewBuilder(portalUrl: ConfiguredPortalUrl, tenantName: TenantName);

        await Should.ThrowAsync<ArgumentException>(() =>
            sut.BuildPublicDocumentUploadUrlAsync(Guid.Empty, Guid.NewGuid(), Guid.NewGuid()));
    }

    // ------------------------------------------------------------------
    // Hard-fail paths
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuildEmailConfirmationUrlAsync_EmptyTenantId_ThrowsArgumentException()
    {
        var sut = NewBuilder(authServerUrl: ConfiguredAuthServerUrl, tenantName: TenantName);

        await Should.ThrowAsync<ArgumentException>(() =>
            sut.BuildEmailConfirmationUrlAsync(Guid.Empty, Guid.NewGuid(), "tok"));
    }

    [Fact]
    public async Task BuildPasswordResetUrlAsync_EmptyTenantId_ThrowsArgumentException()
    {
        var sut = NewBuilder(authServerUrl: ConfiguredAuthServerUrl, tenantName: TenantName);

        await Should.ThrowAsync<ArgumentException>(() =>
            sut.BuildPasswordResetUrlAsync(Guid.Empty, Guid.NewGuid(), "tok"));
    }

    [Fact]
    public async Task BuildInviteUrlAsync_EmptyTenantId_ThrowsArgumentException()
    {
        var sut = NewBuilder(authServerUrl: ConfiguredAuthServerUrl, tenantName: TenantName);

        await Should.ThrowAsync<ArgumentException>(() =>
            sut.BuildInviteUrlAsync(Guid.Empty, "raw"));
    }

    [Fact]
    public async Task BuildAuthServerRootUrlAsync_SettingUnset_ThrowsInvalidOperationException()
    {
        var sut = NewBuilder(authServerUrl: null, tenantName: TenantName);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.BuildAuthServerRootUrlAsync(FalkinsteinTenantId));

        ex.Message.ShouldContain("App__SelfUrl");
    }

    [Fact]
    public async Task BuildPortalRootUrlAsync_SettingUnsetAndTenantNull_ThrowsInvalidOperationException()
    {
        var sut = NewBuilder(portalUrl: null, tenantName: TenantName);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.BuildPortalRootUrlAsync(tenantId: null));

        ex.Message.ShouldContain("App__AngularUrl");
    }

    [Fact]
    public async Task BuildEmailConfirmationUrlAsync_SettingBlank_ThrowsInvalidOperationException()
    {
        var sut = NewBuilder(authServerUrl: "   ", tenantName: TenantName);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.BuildEmailConfirmationUrlAsync(FalkinsteinTenantId, Guid.NewGuid(), "tok"));
    }

    [Fact]
    public async Task BuildEmailConfirmationUrlAsync_TenantNotFound_ThrowsEntityNotFoundException()
    {
        var sut = NewBuilder(
            authServerUrl: ConfiguredAuthServerUrl,
            tenantName: TenantName,
            tenantLookupReturnsNull: true);

        await Should.ThrowAsync<EntityNotFoundException>(() =>
            sut.BuildEmailConfirmationUrlAsync(FalkinsteinTenantId, Guid.NewGuid(), "tok"));
    }

    // ------------------------------------------------------------------
    // Idempotency / non-localhost host
    // ------------------------------------------------------------------

    [Fact]
    public async Task BuildAuthServerRootUrlAsync_AlreadyPrefixedSetting_IsIdempotent()
    {
        var sut = NewBuilder(
            authServerUrl: "http://falkinstein.localhost:44398",
            tenantName: TenantName);

        var url = await sut.BuildAuthServerRootUrlAsync(FalkinsteinTenantId);

        url.ShouldBe("http://falkinstein.localhost:44398");
    }

    [Fact]
    public async Task BuildAuthServerRootUrlAsync_NonLocalhostHost_PassesThrough()
    {
        var sut = NewBuilder(
            authServerUrl: "https://auth.staging.example.com",
            tenantName: TenantName);

        var url = await sut.BuildAuthServerRootUrlAsync(FalkinsteinTenantId);

        url.ShouldBe("https://auth.staging.example.com");
    }

    [Fact]
    public async Task BuildAuthServerRootUrlAsync_TrailingSlash_IsTrimmed()
    {
        var sut = NewBuilder(
            authServerUrl: "http://localhost:44398/",
            tenantName: TenantName);

        var url = await sut.BuildAuthServerRootUrlAsync(FalkinsteinTenantId);

        url.ShouldBe("http://falkinstein.localhost:44398");
    }

    // ------------------------------------------------------------------
    // Test fixture wiring
    // ------------------------------------------------------------------

    private static AccountUrlBuilder NewBuilder(
        string? authServerUrl = null,
        string? portalUrl = null,
        string tenantName = TenantName,
        bool tenantLookupReturnsNull = false)
    {
        var settingProvider = Substitute.For<ISettingProvider>();
        settingProvider
            .GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl)
            .Returns(Task.FromResult<string?>(authServerUrl));
        settingProvider
            .GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl)
            .Returns(Task.FromResult<string?>(portalUrl));

        var tenantRepository = Substitute.For<IRepository<Tenant, Guid>>();
        var tenant = tenantLookupReturnsNull
            ? null
            : CreateTenantWithoutCtor(FalkinsteinTenantId, tenantName);
        tenantRepository
            .FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(tenant));

        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(Substitute.For<IDisposable>());

        return new AccountUrlBuilder(
            settingProvider,
            tenantRepository,
            currentTenant,
            NullLogger<AccountUrlBuilder>.Instance);
    }

    private static Tenant CreateTenantWithoutCtor(Guid id, string name)
    {
        var tenant = (Tenant)RuntimeHelpers.GetUninitializedObject(typeof(Tenant));
        // Both setters are protected -- reflect to set.
        SetNonPublicProperty(tenant, "Id", id);
        SetNonPublicProperty(tenant, "Name", name);
        return tenant;
    }

    private static void SetNonPublicProperty(object target, string name, object value)
    {
        var prop = target.GetType().GetProperty(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var setter = prop?.GetSetMethod(nonPublic: true);
        if (setter == null)
        {
            throw new InvalidOperationException(
                $"Cannot reflect setter for {target.GetType().FullName}.{name}.");
        }
        setter.Invoke(target, new[] { value });
    }
}
