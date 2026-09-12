using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace HealthcareSupport.CaseEvaluation.OpenIddict;

/// <summary>
/// Single-session enforcement. On a fresh interactive login (the
/// authorization_code token exchange) this revokes the user's previously
/// issued, still-valid refresh tokens, so a login on a second device ends the
/// first device's session. Reverses the 2026-05-01 "multi-session is
/// intentional" accepted-deviation.
///
/// <para>This is the "one durable session" variant: NEW validates
/// self-contained JWT access tokens locally at the API (no token-store lookup),
/// so revoking a token entry cannot kill a session instantly. Instead, the old
/// device keeps working only until its short-lived access token expires, then
/// its silent refresh fails because the refresh token was revoked here. Pairs
/// with the shortened access-token lifetime set in
/// <c>CaseEvaluationAuthServerModule</c>.</para>
///
/// <para>Only the authorization_code grant triggers revocation. Silent refreshes
/// (refresh_token grant) and ABP impersonation / client-credentials grants are
/// left untouched, so the SPA's background refresh and admin impersonation keep
/// working. The current login's own tokens are not yet persisted at this point,
/// so only prior sessions are affected.</para>
/// </summary>
public sealed class RevokePreviousSessionsHandler
    : IOpenIddictServerHandler<HandleTokenRequestContext>
{
    private readonly IOpenIddictTokenManager _tokenManager;
    private readonly ILogger<RevokePreviousSessionsHandler> _logger;

    public RevokePreviousSessionsHandler(
        IOpenIddictTokenManager tokenManager,
        ILogger<RevokePreviousSessionsHandler> logger)
    {
        _tokenManager = tokenManager;
        _logger = logger;
    }

    public async ValueTask HandleAsync(HandleTokenRequestContext context)
    {
        if (context is null || !context.Request.IsAuthorizationCodeGrantType())
        {
            return;
        }

        var subject = context.Principal?.GetClaim(Claims.Subject);
        if (string.IsNullOrWhiteSpace(subject))
        {
            _logger.LogWarning(
                "Single-session: authorization_code token request had no subject; skipping revocation.");
            return;
        }

        var revoked = 0;
        await foreach (var token in _tokenManager.FindBySubjectAsync(subject))
        {
            // Prior, still-valid REFRESH tokens only. The stored token Type is a
            // long URN (urn:ietf:params:oauth:token-type:refresh_token), NOT the
            // short "refresh_token" hint, so match on the suffix rather than the
            // TokenTypeHints constant (which caused this to silently no-op). The
            // new login's tokens are not persisted yet, and the redeemed auth code
            // is no longer Valid, so only prior sessions are affected. Access-token
            // entries are skipped: they are self-contained JWTs validated locally
            // at the API, so revoking the entry would have no effect anyway.
            var type = await _tokenManager.GetTypeAsync(token);
            if (type is null || !type.EndsWith("refresh_token", StringComparison.Ordinal))
            {
                continue;
            }

            if (!await _tokenManager.HasStatusAsync(token, Statuses.Valid))
            {
                continue;
            }

            if (await _tokenManager.TryRevokeAsync(token))
            {
                revoked++;
            }
        }

        _logger.LogInformation(
            "Single-session: revoked {Count} prior refresh token(s) for subject {Subject} on a new login.",
            revoked,
            subject);
    }
}
