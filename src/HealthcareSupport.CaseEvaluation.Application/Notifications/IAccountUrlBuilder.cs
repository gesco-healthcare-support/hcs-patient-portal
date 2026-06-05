using System;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// 2026-05-21 (BUG-029) -- centralized tenant-aware URL composition
/// for every email body that links into the AuthServer Razor pages or
/// the SPA.
///
/// <para>
/// Replaces the prior pattern (16 sites each reading
/// <see cref="CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl"/>
/// or <see cref="CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl"/>
/// directly, then either forgetting to call
/// <see cref="TenantUrlComposer"/> or calling it against an empty
/// <c>ICurrentTenant.Name</c>). Every account URL now routes through
/// this service, which:
/// </para>
///
/// <list type="bullet">
///   <item>Reads the configured base URL via the 3-step fallback
///         chain: per-tenant DB setting (most specific) -&gt;
///         <c>App__SelfUrl</c> / <c>App__AngularUrl</c> env var -&gt;
///         <see cref="InvalidOperationException"/> with a "set the
///         env var" message.</item>
///   <item>Looks up the tenant <see cref="Volo.SaaS.Tenants.Tenant.Name"/>
///         from <see cref="Volo.Abp.MultiTenancy.ITenantStore"/> using
///         the explicit <c>tenantId</c> argument.</item>
///   <item>Composes the final URL via
///         <see cref="TenantUrlComposer.ComposeForTenant"/> with the
///         tenant name prepended to the bare-localhost host token.</item>
/// </list>
///
/// <para>
/// The auth-URL methods (verify, reset, invite) take a non-nullable
/// <c>Guid tenantId</c> -- external users are always tenant-scoped,
/// so a null tenant is a compile error. The two helper root-URL
/// methods take <c>Guid?</c> to support genuine host-scope use cases
/// (host admin emails about tenant provisioning, future host UI).
/// </para>
/// </summary>
public interface IAccountUrlBuilder
{
    /// <summary>
    /// Builds the AuthServer-hosted email-confirmation URL the
    /// post-register / resend-verification flows email to the user.
    /// Format:
    /// <c>{tenantPrefix}{authServerBaseUrl}/Account/EmailConfirmation?userId={guid}&amp;confirmationToken={url-encoded}</c>.
    /// </summary>
    Task<string> BuildEmailConfirmationUrlAsync(Guid tenantId, Guid userId, string token);

    /// <summary>
    /// Builds the AuthServer-hosted password-reset URL the
    /// forgot-password flow emails to the user. Format:
    /// <c>{tenantPrefix}{authServerBaseUrl}/Account/ResetPassword?userId={guid}&amp;resetToken={url-encoded}</c>.
    /// </summary>
    Task<string> BuildPasswordResetUrlAsync(Guid tenantId, Guid userId, string token);

    /// <summary>
    /// Builds the AuthServer-hosted invite-acceptance URL the
    /// IT-Admin invite flow emails to the prospective external user.
    /// Format:
    /// <c>{tenantPrefix}{authServerBaseUrl}/Account/Register?inviteToken={url-encoded}</c>.
    /// </summary>
    Task<string> BuildInviteUrlAsync(Guid tenantId, string rawToken);

    /// <summary>
    /// Builds the SPA-hosted public document-upload URL the future
    /// document-request email will send to the patient. The page is
    /// anonymous and authorizes by the per-document verification code,
    /// so the document id + code travel as path segments (no token to
    /// URL-encode). Format:
    /// <c>{tenantPrefix}{portalBaseUrl}/public/document-upload/{documentId}/{verificationCode}</c>.
    /// </summary>
    Task<string> BuildPublicDocumentUploadUrlAsync(Guid tenantId, Guid documentId, Guid verificationCode);

    /// <summary>
    /// Returns the SPA root URL for the given tenant (e.g.
    /// <c>http://falkinstein.localhost:4200</c>). Used by callers
    /// that build SPA deep-links (appointment view, dashboard).
    /// Pass <c>null</c> for host-scoped contexts (returns the
    /// bare-localhost form).
    /// </summary>
    Task<string> BuildPortalRootUrlAsync(Guid? tenantId);

    /// <summary>
    /// Returns the AuthServer root URL for the given tenant (e.g.
    /// <c>http://falkinstein.localhost:44368</c>). Used by callers
    /// that build account-area paths beyond the three named verbs.
    /// Pass <c>null</c> for host-scoped contexts.
    /// </summary>
    Task<string> BuildAuthServerRootUrlAsync(Guid? tenantId);
}
