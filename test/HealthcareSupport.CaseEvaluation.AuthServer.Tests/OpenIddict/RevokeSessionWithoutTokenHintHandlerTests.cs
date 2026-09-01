using System.Security.Claims;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.OpenIddict;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenIddict.Server;
using Volo.Abp.Caching;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.SettingManagement;
using Volo.Abp.Settings;
using Volo.Abp.Uow;
using Volo.Abp.Users;
using Xunit;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace HealthcareSupport.CaseEvaluation.AuthServer.Tests.OpenIddict;

/// <summary>
/// Production hardening 1.8b (2026-09-01).
///
/// <para>The FIRST of these two tests is the one that matters most, and it is not the defect.
/// The defect only shows up on the no-hint path; the risk this change carries is to the WITH-hint
/// path, which every normal sign-out takes. A handler that fired regardless of the hint would
/// double-revoke on every sign-out, and no broken-path test would notice.</para>
///
/// <para>These are plain unit tests, not an ABP integration host: the handler is a small class
/// with three constructor dependencies, and OpenIddict's
/// <see cref="OpenIddictServerTransaction"/> and <see cref="HandleEndSessionRequestContext"/> are
/// both directly constructible, so no module bootstrap or database is needed.</para>
/// </summary>
public class RevokeSessionWithoutTokenHintHandlerTests
{
    private const string SessionId = "11111111-1111-1111-1111-111111111111";

    private readonly IdentitySessionManager _sessionManager = Substitute.For<IdentitySessionManager>(
        Substitute.For<IIdentitySessionRepository>(),
        Substitute.For<ICurrentUser>(),
        Substitute.For<IDistributedCache<IdentitySessionCacheItem>>(),
        Substitute.For<IUnitOfWorkManager>(),
        Substitute.For<ISettingProvider>(),
        null!
    );

    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();

    private RevokeSessionWithoutTokenHintHandler CreateHandler()
    {
        return new RevokeSessionWithoutTokenHintHandler(
            _sessionManager,
            _httpContextAccessor,
            NullLogger<RevokeSessionWithoutTokenHintHandler>.Instance
        );
    }

    private static HandleEndSessionRequestContext ContextWith(ClaimsPrincipal? identityTokenHint)
    {
        return new HandleEndSessionRequestContext(new OpenIddictServerTransaction())
        {
            IdentityTokenHintPrincipal = identityTokenHint,
        };
    }

    private void SignedInAs(string? sessionId)
    {
        var claims = sessionId is null
            ? new Claim[] { new(AbpClaimTypes.UserName, "synthetic.user") }
            : new Claim[]
            {
                new(AbpClaimTypes.UserName, "synthetic.user"),
                new(AbpClaimTypes.SessionId, sessionId),
            };

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "TestCookie")),
        };

        _httpContextAccessor.HttpContext.Returns(httpContext);
    }

    /// <summary>
    /// THE REGRESSION GUARD. A normal sign-out carries an id_token_hint, ABP's own handler revokes
    /// by that hint, and this handler must stay out of the way entirely.
    /// </summary>
    [Fact]
    public async Task Does_Nothing_When_An_IdTokenHint_Is_Present()
    {
        SignedInAs(SessionId);
        var context = ContextWith(
            new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(AbpClaimTypes.SessionId, SessionId) }))
        );

        await CreateHandler().HandleAsync(context);

        await _sessionManager.DidNotReceiveWithAnyArgs().RevokeAsync(default(string)!);
    }

    /// <summary>
    /// The repaired path: no id_token_hint because the SPA had no tokens left to build one from,
    /// but the request still arrives on an authenticated cookie carrying the session id.
    /// </summary>
    [Fact]
    public async Task Revokes_The_Cookie_Principals_Session_When_No_IdTokenHint_Is_Present()
    {
        SignedInAs(SessionId);

        await CreateHandler().HandleAsync(ContextWith(null));

        await _sessionManager.Received(1).RevokeAsync(SessionId);
    }

    [Fact]
    public async Task Does_Nothing_When_The_Request_Is_Not_Authenticated()
    {
        _httpContextAccessor.HttpContext.Returns(new DefaultHttpContext());

        await CreateHandler().HandleAsync(ContextWith(null));

        await _sessionManager.DidNotReceiveWithAnyArgs().RevokeAsync(default(string)!);
    }

    /// <summary>
    /// Dynamic claims are what put `session_id` on the principal. If that ever stops happening the
    /// handler must degrade to doing nothing rather than throwing on an auth path.
    /// </summary>
    [Fact]
    public async Task Does_Nothing_When_The_Principal_Carries_No_Session_Claim()
    {
        SignedInAs(null);

        await CreateHandler().HandleAsync(ContextWith(null));

        await _sessionManager.DidNotReceiveWithAnyArgs().RevokeAsync(default(string)!);
    }
}
