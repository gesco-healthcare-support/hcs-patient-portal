using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// 2026-05-21 (BUG-029 v3 fix) -- single authoritative source of
/// tenant-aware account URL composition. See
/// <see cref="IAccountUrlBuilder"/> for the contract.
/// </summary>
internal sealed class AccountUrlBuilder : IAccountUrlBuilder, ITransientDependency
{
    private readonly ISettingProvider _settingProvider;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<AccountUrlBuilder> _logger;

    public AccountUrlBuilder(
        ISettingProvider settingProvider,
        IRepository<Tenant, Guid> tenantRepository,
        ICurrentTenant currentTenant,
        ILogger<AccountUrlBuilder> logger)
    {
        _settingProvider = settingProvider;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    public async Task<string> BuildEmailConfirmationUrlAsync(Guid tenantId, Guid userId, string token)
    {
        var baseUrl = await ResolveAuthServerBaseUrlInternalAsync(tenantId);
        return AppendPath(baseUrl, "/Account/EmailConfirmation",
            ("userId", userId.ToString()),
            ("confirmationToken", WebUtility.UrlEncode(token)));
    }

    public async Task<string> BuildPasswordResetUrlAsync(Guid tenantId, Guid userId, string token)
    {
        var baseUrl = await ResolveAuthServerBaseUrlInternalAsync(tenantId);
        return ComposeResetUrl(baseUrl, userId, token);
    }

    public async Task<string> BuildHostPasswordResetUrlAsync(Guid userId, string token)
    {
        // Host-scoped reset (internal operators -- Phase D host logins): compose
        // against the host AuthServer root (null tenant -> bare-localhost, no
        // subdomain prefix). Same reset path as the tenant-scoped overload.
        var baseUrl = await BuildAuthServerRootUrlAsync(null);
        return ComposeResetUrl(baseUrl, userId, token);
    }

    private static string ComposeResetUrl(string baseUrl, Guid userId, string token) =>
        AppendPath(baseUrl, "/Account/ResetPassword",
            ("userId", userId.ToString()),
            ("resetToken", WebUtility.UrlEncode(token)));

    public async Task<string> BuildInviteUrlAsync(Guid tenantId, string rawToken)
    {
        var baseUrl = await ResolveAuthServerBaseUrlInternalAsync(tenantId);
        return AppendPath(baseUrl, "/Account/Register",
            ("inviteToken", WebUtility.UrlEncode(rawToken)));
    }

    public async Task<string> BuildPublicDocumentUploadUrlAsync(Guid tenantId, Guid documentId, Guid verificationCode)
    {
        // Explicit guard rejects default(Guid) with a clear message; without
        // it a Guid.Empty would flow into the tenant lookup as a generic
        // EntityNotFoundException. Mirrors the auth-URL methods.
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "tenantId must not be Guid.Empty; the public upload link is tenant-specific.",
                nameof(tenantId));
        }

        // PortalBaseUrl (the SPA), NOT the AuthServer: the upload page is an
        // anonymous SPA route. It authorizes by the verification code, so the
        // id + code are path segments (plain GUIDs -- nothing to URL-encode).
        var baseUrl = await ResolveAndComposeAsync(
            CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl,
            tenantId,
            allowNullTenant: false);
        return AppendPath(baseUrl, $"/public/document-upload/{documentId}/{verificationCode}");
    }

    public async Task<string> BuildChangeRequestConsentUrlAsync(Guid tenantId, string rawToken)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "tenantId must not be Guid.Empty; the consent link is tenant-specific.",
                nameof(tenantId));
        }

        // PortalBaseUrl (the SPA): the consent page is an anonymous SPA route that
        // authorizes by the single-use token (path segment; Base64Url is URL-safe).
        var baseUrl = await ResolveAndComposeAsync(
            CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl,
            tenantId,
            allowNullTenant: false);
        return AppendPath(baseUrl, $"/public/change-request-consent/{WebUtility.UrlEncode(rawToken)}");
    }

    public Task<string> BuildPortalRootUrlAsync(Guid? tenantId) =>
        ResolveAndComposeAsync(
            CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl,
            tenantId,
            allowNullTenant: true);

    public Task<string> BuildAuthServerRootUrlAsync(Guid? tenantId) =>
        ResolveAndComposeAsync(
            CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl,
            tenantId,
            allowNullTenant: true);

    private Task<string> ResolveAuthServerBaseUrlInternalAsync(Guid tenantId)
    {
        // The compiler enforces non-null; explicit guard also rejects
        // default(Guid). Without this, a default(Guid) silently flows
        // into the tenant lookup and triggers EntityNotFoundException
        // -- correct behavior but generic, harder to trace.
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "tenantId must not be Guid.Empty; auth-URL composition requires a real tenant.",
                nameof(tenantId));
        }
        return ResolveAndComposeAsync(
            CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl,
            tenantId,
            allowNullTenant: false);
    }

    private async Task<string> ResolveAndComposeAsync(
        string settingName,
        Guid? tenantId,
        bool allowNullTenant)
    {
        if (!allowNullTenant && !tenantId.HasValue)
        {
            // Defensive: the auth-URL methods take non-nullable Guid so
            // the compiler enforces this; if someone passes default(Guid)
            // we still surface it loudly rather than silently emitting
            // a host-scope URL.
            throw new ArgumentException(
                "tenantId is required for account URL composition; null/empty tenant is invalid for this URL kind.",
                nameof(tenantId));
        }

        var configured = await _settingProvider.GetOrNullAsync(settingName);
        if (string.IsNullOrWhiteSpace(configured))
        {
            // 3-step fallback chain landed here: per-tenant DB setting +
            // App__SelfUrl / App__AngularUrl env var (via setting default
            // sourced from IConfiguration in
            // CaseEvaluationSettingDefinitionProvider) were both empty.
            throw new InvalidOperationException(
                $"AccountUrlBuilder: setting '{settingName}' is not configured. " +
                "Set the corresponding App__SelfUrl / App__AngularUrl env var " +
                "(or appsettings.json App:* key) so this stack can compose " +
                "tenant-aware email URLs.");
        }

        var tenantName = tenantId.HasValue
            ? await ResolveTenantNameAsync(tenantId.Value)
            : null;

        var composed = TenantUrlComposer.ComposeForTenant(configured.TrimEnd('/'), tenantName)!;
        // Diagnostic for tenant-URL composition issues. Debug-level so
        // it's filtered out of prod logs by default; flip the namespace
        // to Debug in appsettings to surface it when investigating.
        // IsEnabled guard short-circuits MEL message-template binding when
        // Debug is off, even though all five args are cheap field reads.
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "AccountUrlBuilder: setting={Setting} configured={Configured} tenantId={TenantId} tenantName={TenantName} composed={Composed}",
                settingName, configured, tenantId, tenantName, composed);
        }
        return composed;
    }

    private async Task<string?> ResolveTenantNameAsync(Guid tenantId)
    {
        // Tenant rows live in the host scope; switch to host context so
        // the IMultiTenant filter does not exclude the row. Switch back
        // automatically on dispose.
        using (_currentTenant.Change(null))
        {
            var tenant = await _tenantRepository.FindAsync(tenantId);
            if (tenant == null)
            {
                throw new EntityNotFoundException(typeof(Tenant), tenantId);
            }
            return tenant.Name;
        }
    }

    private static string AppendPath(string baseUrl, string path, params (string Key, string Value)[] query)
    {
        var sb = new StringBuilder();
        sb.Append(baseUrl);
        sb.Append(path);
        if (query.Length > 0)
        {
            sb.Append('?');
            for (var i = 0; i < query.Length; i++)
            {
                if (i > 0) sb.Append('&');
                sb.Append(query[i].Key).Append('=').Append(query[i].Value);
            }
        }
        return sb.ToString();
    }
}
