using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.DynamicProxy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Pins the stock account routes the portal does not use: the anonymous mail and reset methods of
/// ABP's <see cref="IAccountAppService"/> are refused on the <c>/api/account/</c> HTTP route, and
/// still run when a page calls them in-process (no request on that route). Registration is refused
/// everywhere, as before.
/// </summary>
public class StockAccountRouteRefusalTests
{
    private static StockSelfRegistrationRefusal Interceptor(string? path)
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        if (path != null)
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            accessor.HttpContext.Returns(context);
        }

        return new StockSelfRegistrationRefusal(accessor);
    }

    private static IAbpMethodInvocation Invocation(string methodName)
    {
        var invocation = Substitute.For<IAbpMethodInvocation>();
        invocation.Method.Returns(typeof(IAccountAppService).GetMethod(methodName)!);
        invocation.ProceedAsync().Returns(Task.CompletedTask);
        return invocation;
    }

    public static TheoryData<string> RefusedMethods() =>
        new(StockSelfRegistrationRefusal.RemoteRefusedMethods.OrderBy(x => x));

    [Fact]
    public void The_refused_set_names_real_account_methods()
    {
        StockSelfRegistrationRefusal.RemoteRefusedMethods.Count.ShouldBe(11);
        foreach (var name in StockSelfRegistrationRefusal.RemoteRefusedMethods)
        {
            typeof(IAccountAppService).GetMethod(name).ShouldNotBeNull(name);
        }
    }

    [Theory]
    [MemberData(nameof(RefusedMethods))]
    public async Task A_refused_method_on_the_stock_http_route_is_refused_and_never_runs(string method)
    {
        var invocation = Invocation(method);

        var ex = await Should.ThrowAsync<UserFriendlyException>(
            () => Interceptor("/api/account/" + method).InterceptAsync(invocation));

        ex.Message.ShouldBe(StockSelfRegistrationRefusal.RemoteRouteRefusalMessage);
        await invocation.DidNotReceive().ProceedAsync();
    }

    [Theory]
    [MemberData(nameof(RefusedMethods))]
    public async Task A_refused_method_called_in_process_by_a_page_still_runs(string method)
    {
        var invocation = Invocation(method);

        await Interceptor("/Account/ForgotPassword").InterceptAsync(invocation);
        await Interceptor(null).InterceptAsync(invocation);

        await invocation.Received(2).ProceedAsync();
    }

    [Theory]
    [InlineData(nameof(IAccountAppService.GetTwoFactorProvidersAsync))]
    [InlineData(nameof(IAccountAppService.SendTwoFactorCodeAsync))]
    [InlineData(nameof(IAccountAppService.GetSecurityLogListAsync))]
    [InlineData(nameof(IAccountAppService.GetProfilePictureAsync))]
    public async Task Other_stock_methods_are_left_alone_on_the_http_route(string method)
    {
        var invocation = Invocation(method);

        await Interceptor("/api/account/anything").InterceptAsync(invocation);

        await invocation.Received(1).ProceedAsync();
    }

    [Theory]
    [InlineData("/api/account/")]
    [InlineData("/API/ACCOUNT/send-password-reset-code")]
    public void The_route_match_is_case_insensitive_and_segment_based(string path)
    {
        StockSelfRegistrationRefusal.IsRefusedOnRemoteRoute(
            nameof(IAccountAppService.SendPasswordResetCodeAsync), path).ShouldBeTrue();
        StockSelfRegistrationRefusal.IsRefusedOnRemoteRoute(
            nameof(IAccountAppService.SendPasswordResetCodeAsync), "/api/accountant/x").ShouldBeFalse();
        StockSelfRegistrationRefusal.IsRefusedOnRemoteRoute(
            nameof(IAccountAppService.SendPasswordResetCodeAsync), "/api/public/external-account/x").ShouldBeFalse();
    }

    [Fact]
    public async Task Registration_stays_refused_with_or_without_a_request()
    {
        foreach (var path in new string?[] { null, "/Account/Register", "/api/account/register" })
        {
            var invocation = Invocation(nameof(IAccountAppService.RegisterAsync));

            var ex = await Should.ThrowAsync<UserFriendlyException>(() => Interceptor(path).InterceptAsync(invocation));

            ex.Message.ShouldBe(StockSelfRegistrationRefusal.RefusalMessage);
        }
    }
}
