using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenIddict.Server;
using Volo.Abp.Identity;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace HealthcareSupport.CaseEvaluation.OpenIddict;

/// <summary>
/// Production hardening 1.8b (2026-09-01). Revokes the ABP identity-session record on an
/// end-session request that carries no <c>id_token_hint</c>.
///
/// <para>ABP's own <c>OpenIddictRevokeIdentitySessionOnLogout</c> reads the session id from
/// <c>context.IdentityTokenHintPrincipal</c> and nothing else, and OpenIddict's
/// <c>AttachPrincipal</c> for end-session propagates only that hint -- it does not attach the
/// cookie-authenticated principal. So when the SPA signs out in the repaired no-token state it
/// has no id_token to send, the SSO cookie is still cleared (ABP's <c>/connect/endsession</c>
/// controller calls <c>SignInManager.SignOutAsync()</c> unconditionally), but the
/// <c>AbpSessions</c> row is left behind and the AuthServer logs "No SessionId was found in the
/// token during HandleLogoutRequestContext."</para>
///
/// <para>This handler is ADDITIVE ON PURPOSE. ABP's handler is neither removed nor derived from,
/// so the with-hint path is not "unchanged as far as we tested" -- it is literally the same code
/// still running, and this class cannot alter it. Removing ABP's handler and registering a
/// derived replacement was the alternative: it works today because that class is
/// <c>public virtual</c>, but it is copy-and-edit of a framework internal, and a change to the
/// base would drift silently through a version bump.</para>
///
/// <para>The session id comes from <c>HttpContext.User</c>, which the authentication middleware
/// populates at the start of the request. <c>SignInManager.SignOutAsync()</c> expires the cookie
/// in the RESPONSE and does not clear the principal already resolved for this request, so the
/// claim is still readable here.</para>
///
/// <para>Registered in <c>CaseEvaluationAuthServerModule.PreConfigureServices</c>, mirroring
/// <see cref="RevokePreviousSessionsHandler"/>.</para>
/// </summary>
public sealed class RevokeSessionWithoutTokenHintHandler
    : IOpenIddictServerHandler<HandleEndSessionRequestContext>
{
    private readonly IdentitySessionManager _sessionManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<RevokeSessionWithoutTokenHintHandler> _logger;

    public RevokeSessionWithoutTokenHintHandler(
        IdentitySessionManager sessionManager,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RevokeSessionWithoutTokenHintHandler> logger)
    {
        _sessionManager = sessionManager;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async ValueTask HandleAsync(HandleEndSessionRequestContext context)
    {
        // A hint was supplied, so ABP's handler has already revoked by session id. Doing anything
        // here would be a second revocation of the same record on every normal sign-out. This
        // early return is the entire regression guard and it is covered by its own test.
        if (context is null || context.IdentityTokenHintPrincipal is not null)
        {
            return;
        }

        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            // An unauthenticated end-session request has no session to revoke. Not an error:
            // a bookmarked sign-out URL hit while already signed out reaches here.
            return;
        }

        var sessionId = principal.FindSessionId();
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            _logger.LogWarning(
                "End-session without id_token_hint: the authenticated principal carries no "
                    + "session_id claim, so no session record could be revoked.");
            return;
        }

        await _sessionManager.RevokeAsync(sessionId);

        _logger.LogInformation(
            "End-session without id_token_hint: revoked session record {SessionId} from the "
                + "authenticated cookie principal.",
            sessionId);
    }
}
