using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.OpenIddict;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using Shouldly;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace HealthcareSupport.CaseEvaluation.AuthServer.Tests;

/// <summary>
/// Single-session enforcement: a fresh interactive login revokes the user's prior, still-valid
/// REFRESH tokens, and nothing else.
/// </summary>
/// <remarks>
/// The token store is seeded with DECOYS that must survive a login: an access-token entry, a refresh
/// token that is no longer valid, and a token of an unknown type. A handler that revoked everything
/// for the subject would pass a test that only checked the one real target.
/// </remarks>
public class RevokePreviousSessionsHandlerTests
{
    private const string Subject = "TEST-user-1";
    private const string RefreshType = "urn:ietf:params:oauth:token-type:refresh_token";
    private const string AccessType = "urn:ietf:params:oauth:token-type:access_token";

    private sealed record StoredToken(string Name, string? Type, bool Valid, bool RevokeSucceeds = true);

    private readonly List<string> _revoked = new();

    private IOpenIddictTokenManager TokenStore(params StoredToken[] tokens)
    {
        var manager = Substitute.For<IOpenIddictTokenManager>();
        manager.FindBySubjectAsync(Subject, Arg.Any<CancellationToken>()).Returns(ToAsync(tokens));
        foreach (var token in tokens)
        {
            manager.GetTypeAsync(token, Arg.Any<CancellationToken>()).Returns(new ValueTask<string?>(token.Type));
            manager.HasStatusAsync(token, Statuses.Valid, Arg.Any<CancellationToken>()).Returns(new ValueTask<bool>(token.Valid));
            manager.TryRevokeAsync(token, Arg.Any<CancellationToken>()).Returns(_ =>
            {
                if (token.RevokeSucceeds)
                {
                    _revoked.Add(token.Name);
                }
                return new ValueTask<bool>(token.RevokeSucceeds);
            });
        }
        return manager;
    }

    private static async IAsyncEnumerable<object> ToAsync(IEnumerable<StoredToken> tokens)
    {
        foreach (var token in tokens)
        {
            await Task.Yield();
            yield return token;
        }
    }

    private static HandleTokenRequestContext TokenRequest(string grantType, string? subject = Subject)
    {
        var transaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest { GrantType = grantType },
        };
        var context = new HandleTokenRequestContext(transaction);
        if (subject != null)
        {
            context.Principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(Claims.Subject, subject) }, "Test"));
        }
        else
        {
            context.Principal = new ClaimsPrincipal(new ClaimsIdentity());
        }
        return context;
    }

    private static readonly StoredToken[] SeededStore =
    {
        new("prior-refresh", RefreshType, Valid: true),
        // LOAD-BEARING DECOYS: each must survive the login.
        new("access-entry", AccessType, Valid: true),
        new("already-revoked-refresh", RefreshType, Valid: false),
        new("untyped-entry", null, Valid: true),
    };

    [Fact]
    public async Task A_fresh_login_revokes_only_the_prior_valid_refresh_tokens()
    {
        var handler = new RevokePreviousSessionsHandler(TokenStore(SeededStore), NullLogger<RevokePreviousSessionsHandler>.Instance);

        await handler.HandleAsync(TokenRequest(GrantTypes.AuthorizationCode));

        _revoked.ShouldBe(new[] { "prior-refresh" });
    }

    [Fact]
    public async Task Every_prior_valid_refresh_token_is_revoked_and_a_failed_revoke_does_not_stop_the_rest()
    {
        var handler = new RevokePreviousSessionsHandler(
            TokenStore(
                new StoredToken("refresh-a", RefreshType, Valid: true, RevokeSucceeds: false),
                new StoredToken("refresh-b", RefreshType, Valid: true)),
            NullLogger<RevokePreviousSessionsHandler>.Instance);

        await handler.HandleAsync(TokenRequest(GrantTypes.AuthorizationCode));

        _revoked.ShouldBe(new[] { "refresh-b" });
    }

    [Theory]
    [InlineData(GrantTypes.RefreshToken)]
    [InlineData(GrantTypes.ClientCredentials)]
    [InlineData("Impersonation")]
    public async Task A_silent_refresh_or_another_grant_revokes_nothing(string grantType)
    {
        var store = TokenStore(SeededStore);
        var handler = new RevokePreviousSessionsHandler(store, NullLogger<RevokePreviousSessionsHandler>.Instance);

        await handler.HandleAsync(TokenRequest(grantType));

        _revoked.ShouldBeEmpty();
        store.DidNotReceiveWithAnyArgs().FindBySubjectAsync(default!, default);
    }

    [Fact]
    public async Task A_login_with_no_subject_revokes_nothing()
    {
        var store = TokenStore(SeededStore);
        var handler = new RevokePreviousSessionsHandler(store, NullLogger<RevokePreviousSessionsHandler>.Instance);

        await handler.HandleAsync(TokenRequest(GrantTypes.AuthorizationCode, subject: null));

        _revoked.ShouldBeEmpty();
        store.DidNotReceiveWithAnyArgs().FindBySubjectAsync(default!, default);
    }
}
