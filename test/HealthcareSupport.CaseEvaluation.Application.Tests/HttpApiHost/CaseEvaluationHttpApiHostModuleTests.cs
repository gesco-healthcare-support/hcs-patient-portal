using System.Net;
using System.Security.Claims;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HttpApiHost.Tests;

/// <summary>
/// BUG-025 (2026-05-21) -- unit tests for the two internal static
/// helpers extracted from <see cref="CaseEvaluationHttpApiHostModule"/>:
/// <see cref="CaseEvaluationHttpApiHostModule.MapAppointmentDocumentErrorCodes"/>
/// and <see cref="CaseEvaluationHttpApiHostModule.ConfigureUploadLimits"/>.
///
/// <para>The helpers are reached via <c>InternalsVisibleTo</c>
/// (declared in <c>src/.../HttpApi.Host/AssemblyInfo.cs</c>) so the
/// tests can drive them without booting the full ABP host. They
/// assert the configured-state of the options, not end-to-end HTTP
/// behaviour -- the integration-level verification lives in the
/// HTTP smoke tests recorded in BUG-025-no-document-upload-size-limit.md.</para>
/// </summary>
public class CaseEvaluationHttpApiHostModuleTests
{
    // ------------------------------------------------------------------
    // MapAppointmentDocumentErrorCodes
    // ------------------------------------------------------------------

    [Fact]
    public void MapAppointmentDocumentErrorCodes_MapsFileTooLargeTo413()
    {
        var options = new AbpExceptionHttpStatusCodeOptions();

        CaseEvaluationHttpApiHostModule.MapAppointmentDocumentErrorCodes(options);

        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.AppointmentDocumentFileTooLarge
        ].ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public void MapAppointmentDocumentErrorCodes_MapsFileEmptyTo400()
    {
        var options = new AbpExceptionHttpStatusCodeOptions();

        CaseEvaluationHttpApiHostModule.MapAppointmentDocumentErrorCodes(options);

        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty
        ].ShouldBe(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------
    // ConfigureUploadLimits -- defense-in-depth framework caps. The
    // AppService cap is 10 MB; the framework cap is 12 MB so the
    // friendly localized 413 from the AppService can fire before the
    // raw framework 413 (which has no localized message).
    // ------------------------------------------------------------------

    private const long ExpectedFrameworkCapBytes = 12L * 1024 * 1024;

    [Fact]
    public void ConfigureUploadLimits_SetsKestrelMaxRequestBodySizeTo12MB()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        var context = new ServiceConfigurationContext(services);

        CaseEvaluationHttpApiHostModule.ConfigureUploadLimits(context);

        var kestrel = services.BuildServiceProvider()
            .GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        kestrel.Limits.MaxRequestBodySize.ShouldBe(ExpectedFrameworkCapBytes);
    }

    [Fact]
    public void ConfigureUploadLimits_SetsFormOptionsMultipartBodyLengthLimitTo12MB()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        var context = new ServiceConfigurationContext(services);

        CaseEvaluationHttpApiHostModule.ConfigureUploadLimits(context);

        var form = services.BuildServiceProvider()
            .GetRequiredService<IOptions<FormOptions>>().Value;
        form.MultipartBodyLengthLimit.ShouldBe(ExpectedFrameworkCapBytes);
    }

    [Fact]
    public void ConfigureUploadLimits_FrameworkCapIsAtLeastTwoMegabytesAboveAppServiceCap()
    {
        // BUG-025 (2026-05-21) -- the 2 MB buffer between the
        // AppService cap (10 MB) and the framework cap is deliberate:
        // it lets multipart-boundary headers + small overhead pass
        // the framework's raw 413 so the AppService's localized
        // BusinessException fires first for files between 10 and 12 MB.
        // Guard against accidental tightening of either cap.
        var services = new ServiceCollection();
        services.AddOptions();
        var context = new ServiceConfigurationContext(services);

        CaseEvaluationHttpApiHostModule.ConfigureUploadLimits(context);

        var kestrel = services.BuildServiceProvider()
            .GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        var actualCap = kestrel.Limits.MaxRequestBodySize ?? 0;
        var appServiceCap = AppointmentDocumentsAppService.MaxFileSizeBytes;
        var buffer = actualCap - appServiceCap;
        buffer.ShouldBeGreaterThanOrEqualTo(2L * 1024 * 1024,
            "framework cap should be >= 2 MB above the AppService cap so the localized 413 wins");
    }

    // ------------------------------------------------------------------
    // BUG-035 fix -- partition-key resolvers for the password-reset
    // rate limiter. Per-account (email) is the OWASP-recommended
    // primary control; per-IP is the secondary cap.
    // ------------------------------------------------------------------

    [Fact]
    public void ResolvePasswordResetEmailPartitionKey_PreferStashedBodyEmailOverEverythingElse()
    {
        // Arrange -- stash an email like the body-peek middleware would,
        // and also set conflicting query/sub/IP signals to prove the
        // stash wins.
        var ctx = new DefaultHttpContext();
        ctx.Items[PasswordResetEmailPeekMiddleware.ContextItemKey] = "primary@example.test";
        ctx.Request.QueryString = new QueryString("?email=different-from-stash@example.test");
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "user-guid") }));
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");

        var key = CaseEvaluationHttpApiHostModule.ResolvePasswordResetEmailPartitionKey(ctx);

        key.ShouldBe("email:primary@example.test");
    }

    [Fact]
    public void ResolvePasswordResetEmailPartitionKey_FallBackToQueryWhenStashIsMissing()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.QueryString = new QueryString("?email=Query@Example.test");

        var key = CaseEvaluationHttpApiHostModule.ResolvePasswordResetEmailPartitionKey(ctx);

        // Email lowercased + trimmed at the query layer too, so different
        // casings end up in the same bucket.
        key.ShouldBe("email:query@example.test");
    }

    [Fact]
    public void ResolvePasswordResetEmailPartitionKey_FallBackToJwtSubWhenStashAndQueryAreMissing()
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "abc-123") }));
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.1");

        var key = CaseEvaluationHttpApiHostModule.ResolvePasswordResetEmailPartitionKey(ctx);

        key.ShouldBe("sub:abc-123");
    }

    [Fact]
    public void ResolvePasswordResetEmailPartitionKey_FallBackToIpWhenNothingElseResolves()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.42");

        var key = CaseEvaluationHttpApiHostModule.ResolvePasswordResetEmailPartitionKey(ctx);

        key.ShouldBe("ip:203.0.113.42");
    }

    [Fact]
    public void ResolvePasswordResetEmailPartitionKey_FallBackToGlobalWhenIpUnknown()
    {
        // Last-resort: no body, no query, no JWT, no IP. The limiter
        // still needs a deterministic key.
        var ctx = new DefaultHttpContext();

        var key = CaseEvaluationHttpApiHostModule.ResolvePasswordResetEmailPartitionKey(ctx);

        key.ShouldBe("global");
    }

    [Fact]
    public void ResolvePasswordResetEmailPartitionKey_EmptyStashedValueFallsThroughToNextSource()
    {
        // Edge case: the middleware stashed an empty string. Treat as
        // "no email available" and continue down the precedence chain.
        var ctx = new DefaultHttpContext();
        ctx.Items[PasswordResetEmailPeekMiddleware.ContextItemKey] = "   ";
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.2");

        var key = CaseEvaluationHttpApiHostModule.ResolvePasswordResetEmailPartitionKey(ctx);

        key.ShouldBe("ip:10.0.0.2");
    }

    [Fact]
    public void ResolvePasswordResetIpPartitionKey_PrefixesIpAddress()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.7");

        var key = CaseEvaluationHttpApiHostModule.ResolvePasswordResetIpPartitionKey(ctx);

        key.ShouldBe("ip:198.51.100.7");
    }

    [Fact]
    public void ResolvePasswordResetIpPartitionKey_FallBackToGlobalWhenIpMissing()
    {
        var ctx = new DefaultHttpContext();

        var key = CaseEvaluationHttpApiHostModule.ResolvePasswordResetIpPartitionKey(ctx);

        key.ShouldBe("global");
    }

    [Fact]
    public void ResolvePasswordResetIpPartitionKey_IgnoresBodyAndJwtSub()
    {
        // The IP secondary limiter must be purely IP-based -- otherwise
        // it would inherit the same shared-bucket gap the BUG-035 fix
        // is solving on the primary partition.
        var ctx = new DefaultHttpContext();
        ctx.Items[PasswordResetEmailPeekMiddleware.ContextItemKey] = "should-be-ignored@example.test";
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "should-be-ignored") }));
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("10.0.0.3");

        var key = CaseEvaluationHttpApiHostModule.ResolvePasswordResetIpPartitionKey(ctx);

        key.ShouldBe("ip:10.0.0.3");
    }

    // ------------------------------------------------------------------
    // ConfigureForwardedHeaders (G7, 2026-07-09) -- honor nginx's
    // X-Forwarded-Proto so the API sees https behind TLS termination.
    // Applied unconditionally (no dev-only gate) so production works.
    // 2026-07-29: X-Forwarded-For added so the per-IP rate-limit
    // partitions see the real client instead of the nginx container.
    // ------------------------------------------------------------------

    [Fact]
    public void ConfigureForwardedHeaders_ProcessesXForwardedProtoAndXForwardedFor()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        var context = new ServiceConfigurationContext(services);

        CaseEvaluationHttpApiHostModule.ConfigureForwardedHeaders(context);

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto).ShouldBeTrue();
        options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor).ShouldBeTrue();
    }

    [Fact]
    public void ConfigureForwardedHeaders_LeavesForwardLimitAtOne()
    {
        // The reason trusting X-Forwarded-For is safe here rests entirely on this value.
        // nginx's $proxy_add_x_forwarded_for APPENDS the real $remote_addr to whatever the
        // client sent, and the middleware reads right-to-left consuming ForwardLimit hops --
        // so at 1 a forged header ("1.2.3.4, <real>") still resolves to the real address.
        // Raising it would start honoring client-supplied hops and make the per-IP
        // partitions spoofable.
        var services = new ServiceCollection();
        services.AddOptions();
        var context = new ServiceConfigurationContext(services);

        CaseEvaluationHttpApiHostModule.ConfigureForwardedHeaders(context);

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        options.ForwardLimit.ShouldBe(1);
    }

    [Fact]
    public void ConfigureForwardedHeaders_TrustsTheInNetworkProxyByClearingAllowlists()
    {
        // .NET 8+ ignores X-Forwarded-* from proxies not in the allowlist; on the
        // single-ingress LAN box the only proxy is our own nginx, so both lists are
        // cleared to trust it. Guards against a future default that would silently
        // drop the forwarded scheme behind the proxy.
        var services = new ServiceCollection();
        services.AddOptions();
        var context = new ServiceConfigurationContext(services);

        CaseEvaluationHttpApiHostModule.ConfigureForwardedHeaders(context);

        var options = services.BuildServiceProvider()
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        options.KnownProxies.ShouldBeEmpty();
        options.KnownIPNetworks.ShouldBeEmpty();
    }

    // ------------------------------------------------------------------
    // Integration limiter (2026-07-29) -- the Case Tracker reconcile GET
    // is anonymous and token-gated, and returns the full claim/party
    // payload. Capped per source IP so a leaked token cannot be used to
    // enumerate at will.
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("/api/integration/offices/abc/appointments/def", true)]
    [InlineData("/api/integration", true)]
    [InlineData("/API/Integration/offices/abc/appointments/def", true)]
    [InlineData("/api/app/case-tracker/dead-letters", false)]
    [InlineData("/api/public/external-account/reset-password", false)]
    [InlineData("/api/integrationsomething", false)]
    public void IsIntegrationPath_MatchesOnlyTheIntegrationPrefix(string path, bool expected)
    {
        // "/api/integrationsomething" must NOT match: StartsWithSegments is
        // segment-aware, so a longer path that merely shares a prefix string
        // is a different route and gets no limit.
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;

        CaseEvaluationHttpApiHostModule.IsIntegrationPath(ctx).ShouldBe(expected);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public void IsIntegrationPath_IgnoresTheHttpMethod(string method)
    {
        // Deliberately verb-agnostic, unlike the other three matchers: the whole
        // prefix is anonymous, so a verb added later should already be throttled.
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/integration/offices/abc/appointments/def";
        ctx.Request.Method = method;

        CaseEvaluationHttpApiHostModule.IsIntegrationPath(ctx).ShouldBeTrue();
    }

    [Fact]
    public void ResolveIntegrationPartitionKey_PrefixesIpAddress()
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.35");

        CaseEvaluationHttpApiHostModule.ResolveIntegrationPartitionKey(ctx).ShouldBe("ip:192.0.2.35");
    }

    [Fact]
    public void ResolveIntegrationPartitionKey_FallBackToGlobalWhenIpUnknown()
    {
        // One shared bucket is the right failure mode for a machine caller: it still
        // caps total volume rather than handing out an unlimited partition.
        var ctx = new DefaultHttpContext();

        CaseEvaluationHttpApiHostModule.ResolveIntegrationPartitionKey(ctx).ShouldBe("global");
    }

    [Fact]
    public void IntegrationRequestsPerHour_LeavesRoomForARepairSweep()
    {
        // Guards the intent rather than the number: throttling the Case Tracker's
        // post-outage catch-up would be a worse failure than the enumeration this
        // limit prevents. If someone lowers this to a password-reset-sized value,
        // that trade has been forgotten.
        CaseEvaluationHttpApiHostModule.IntegrationRequestsPerHour.ShouldBeGreaterThanOrEqualTo(100);
    }
}
