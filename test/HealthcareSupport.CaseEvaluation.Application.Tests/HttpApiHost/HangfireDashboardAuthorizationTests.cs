using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Dashboard;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HttpApiHost.Tests;

/// <summary>
/// Who may open the background job dashboard. Outside Development it needs a signed-in HOST user
/// holding <see cref="CaseEvaluationPermissions.BackgroundJobsDashboard.Default"/>; in Development it
/// stays open for local work.
///
/// <para>Each fact evaluates the options the module builds the way Hangfire's dashboard middleware
/// does: every <c>Authorization</c> filter, then every <c>AsyncAuthorization</c> filter, refusing
/// on the first "no". The permission checker is a stub, so these facts prove the filter wiring, not
/// the permission store.</para>
/// </summary>
public class HangfireDashboardAuthorizationTests
{
    private static async Task<bool> IsAuthorizedAsync(DashboardOptions options, HttpContext httpContext)
    {
        var context = new AspNetCoreDashboardContext(Substitute.For<JobStorage>(), options, httpContext);
        foreach (var filter in options.Authorization)
        {
            if (!filter.Authorize(context))
            {
                return false;
            }
        }

        foreach (var filter in options.AsyncAuthorization)
        {
            if (!await filter.AuthorizeAsync(context))
            {
                return false;
            }
        }

        return true;
    }

    private static HttpContext Request(bool signedIn, bool officeRequest, bool holdsPermission)
    {
        var currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(officeRequest);

        var permissionChecker = Substitute.For<IPermissionChecker>();
        permissionChecker
            .IsGrantedAsync(Arg.Any<ClaimsPrincipal>(), CaseEvaluationPermissions.BackgroundJobsDashboard.Default)
            .Returns(holdsPermission);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddSingleton(currentTenant);
        services.AddSingleton<IAuthorizationHandler>(new PermissionRequirementHandler(permissionChecker));

        var identity = signedIn
            ? new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-operator")], "Bearer")
            : new ClaimsIdentity();

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            User = new ClaimsPrincipal(identity),
        };
    }

    [Fact]
    public async Task Outside_development_an_anonymous_request_is_refused()
    {
        var options = CaseEvaluationHttpApiHostModule.CreateHangfireDashboardOptions(isDevelopment: false);

        (await IsAuthorizedAsync(options, Request(signedIn: false, officeRequest: false, holdsPermission: false)))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Outside_development_an_office_request_is_refused_even_with_the_permission()
    {
        var options = CaseEvaluationHttpApiHostModule.CreateHangfireDashboardOptions(isDevelopment: false);

        (await IsAuthorizedAsync(options, Request(signedIn: true, officeRequest: true, holdsPermission: true)))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Outside_development_a_host_user_without_the_permission_is_refused()
    {
        var options = CaseEvaluationHttpApiHostModule.CreateHangfireDashboardOptions(isDevelopment: false);

        (await IsAuthorizedAsync(options, Request(signedIn: true, officeRequest: false, holdsPermission: false)))
            .ShouldBeFalse();
    }

    [Fact]
    public async Task Outside_development_a_host_user_with_the_permission_is_allowed()
    {
        var options = CaseEvaluationHttpApiHostModule.CreateHangfireDashboardOptions(isDevelopment: false);

        (await IsAuthorizedAsync(options, Request(signedIn: true, officeRequest: false, holdsPermission: true)))
            .ShouldBeTrue();
    }

    [Fact]
    public void Outside_development_antiforgery_is_enforced()
    {
        CaseEvaluationHttpApiHostModule.CreateHangfireDashboardOptions(isDevelopment: false)
            .IgnoreAntiforgeryToken.ShouldBeFalse();
    }

    [Fact]
    public async Task In_development_the_dashboard_stays_open()
    {
        var options = CaseEvaluationHttpApiHostModule.CreateHangfireDashboardOptions(isDevelopment: true);

        (await IsAuthorizedAsync(options, Request(signedIn: false, officeRequest: false, holdsPermission: false)))
            .ShouldBeTrue();
        options.Authorization.Single().ShouldBeOfType<BackgroundJobs.DevelopmentHangfireDashboardAuthorizationFilter>();
    }
}
