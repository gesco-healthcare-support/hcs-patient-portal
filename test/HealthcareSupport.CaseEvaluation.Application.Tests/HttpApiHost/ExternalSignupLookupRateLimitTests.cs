using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HttpApiHost.Tests;

/// <summary>
/// The anonymous external-signup lookups are rate limited, in a bucket of their own.
///
/// <para>Before this, the only limiter under <c>/api/public/external-signup</c> matched the register
/// POST and nothing else, so <c>tenant-options</c>, <c>resolve-tenant</c> and <c>validate-invite</c>
/// fell through to the unlimited partition. The predicate facts below are necessary and not
/// sufficient: deleting the partitioner branch leaves them green. The facts that drive the real
/// configured limiter are the ones that fail when the wiring goes.</para>
/// </summary>
public class ExternalSignupLookupRateLimitTests
{
    private const string Prefix = "/api/public/external-signup";

    private static PartitionedRateLimiter<HttpContext> BuildConfiguredGlobalLimiter()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        CaseEvaluationHttpApiHostModule.ConfigurePasswordResetRateLimiter(new ServiceConfigurationContext(services));
        var options = services.BuildServiceProvider().GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        options.GlobalLimiter.ShouldNotBeNull();
        return options.GlobalLimiter!;
    }

    private static DefaultHttpContext Request(string method, string path, string ip = "203.0.113.9", bool signedIn = false)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(ip);
        if (signedIn)
        {
            ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "TEST-staff") }, "Bearer"));
        }
        return ctx;
    }

    private static RateLimitLease? Exhaust(PartitionedRateLimiter<HttpContext> limiter, System.Func<HttpContext> next, int attempts)
    {
        RateLimitLease? last = null;
        for (var i = 0; i < attempts; i++)
        {
            last = limiter.AttemptAcquire(next());
        }
        return last;
    }

    [Theory]
    [InlineData("GET", Prefix + "/tenant-options")]
    [InlineData("GET", Prefix + "/resolve-tenant")]
    [InlineData("GET", Prefix + "/validate-invite")]
    [InlineData("GET", "/API/PUBLIC/EXTERNAL-SIGNUP/TENANT-OPTIONS")]
    [InlineData("GET", Prefix + "/a-route-added-later")]
    public void Anonymous_lookups_under_the_prefix_are_matched(string method, string path)
    {
        CaseEvaluationHttpApiHostModule.IsExternalSignupAnonymousLookupPath(Request(method, path)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("POST", Prefix + "/register")]
    [InlineData("POST", Prefix + "/dev/delete-test-users")]
    [InlineData("POST", Prefix + "/dev/mark-email-confirmed")]
    [InlineData("GET", "/api/public/external-signup-something-else")]
    [InlineData("GET", "/api/public/external-account/reset-password")]
    public void The_register_post_dev_helpers_and_neighbouring_paths_are_not(string method, string path)
    {
        CaseEvaluationHttpApiHostModule.IsExternalSignupAnonymousLookupPath(Request(method, path)).ShouldBeFalse();
    }

    [Fact]
    public void Signed_in_callers_are_not_matched()
    {
        CaseEvaluationHttpApiHostModule.IsExternalSignupAnonymousLookupPath(
            Request("GET", Prefix + "/external-user-lookup", signedIn: true)).ShouldBeFalse();
    }

    [Fact]
    public void Tenant_options_is_throttled_once_the_limit_is_exceeded_from_one_address()
    {
        var limiter = BuildConfiguredGlobalLimiter();

        var last = Exhaust(limiter, () => Request("GET", Prefix + "/tenant-options"),
            CaseEvaluationHttpApiHostModule.ExternalSignupLookupRequestsPerHour + 1);

        last.ShouldNotBeNull();
        last!.IsAcquired.ShouldBeFalse();
    }

    [Fact]
    public void The_limit_is_per_address_not_global()
    {
        var limiter = BuildConfiguredGlobalLimiter();
        Exhaust(limiter, () => Request("GET", Prefix + "/resolve-tenant", "198.51.100.20"),
            CaseEvaluationHttpApiHostModule.ExternalSignupLookupRequestsPerHour + 1);

        limiter.AttemptAcquire(Request("GET", Prefix + "/resolve-tenant", "192.0.2.21")).IsAcquired.ShouldBeTrue();
    }

    [Fact]
    public void Lookups_do_not_spend_the_register_budget()
    {
        var limiter = BuildConfiguredGlobalLimiter();
        Exhaust(limiter, () => Request("GET", Prefix + "/validate-invite", "198.51.100.30"),
            CaseEvaluationHttpApiHostModule.ExternalSignupLookupRequestsPerHour + 1);

        limiter.AttemptAcquire(Request("POST", Prefix + "/register", "198.51.100.30")).IsAcquired.ShouldBeTrue();
    }

    [Fact]
    public void Signed_in_lookups_are_never_refused_by_this_limit()
    {
        var limiter = BuildConfiguredGlobalLimiter();

        var last = Exhaust(limiter, () => Request("GET", Prefix + "/external-user-lookup", "198.51.100.40", signedIn: true),
            CaseEvaluationHttpApiHostModule.ExternalSignupLookupRequestsPerHour * 3);

        last!.IsAcquired.ShouldBeTrue();
    }

    [Fact]
    public void The_lookup_budget_covers_a_clinic_registering_its_full_hourly_allowance()
    {
        // The register cap is 15/hour per address and the sign-up page makes one or two lookups per
        // load. Below twice the register cap, a busy clinic would be refused before it could register.
        CaseEvaluationHttpApiHostModule.ExternalSignupLookupRequestsPerHour.ShouldBeGreaterThanOrEqualTo(30);
    }
}
