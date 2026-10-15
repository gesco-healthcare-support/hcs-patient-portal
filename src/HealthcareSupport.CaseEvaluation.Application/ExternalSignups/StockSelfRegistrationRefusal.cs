using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.DependencyInjection;
using Volo.Abp.DynamicProxy;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// Refuses ABP's stock self-registration. The portal registers people through its own route
/// (<see cref="ExternalSignupAppService.RegisterAsync"/>), which assigns the role the person
/// signed up as; ABP's stock <see cref="IAccountAppService.RegisterAsync"/> assigns only roles
/// flagged as default, and no role here is, so it creates an account holding NO role.
///
/// <para><b>WHY AN INTERCEPTOR AND NOT THE SETTING.</b> Two stock write paths reach that method:
/// <c>POST api/account/register</c>, which the Account module publishes on both the API host and
/// the AuthServer, and the POST handler of the stock <c>/Account/Register</c> page. The portal's
/// own sign-up UI IS that stock page -- <c>global-scripts.js</c> intercepts its submit and posts
/// to the portal route instead -- so the page's GET must keep working. ABP's open-source
/// <c>RegisterModel</c> refuses to render when <c>Abp.Account.IsSelfRegistrationEnabled</c> is
/// false; the Pro page is obfuscated and that behaviour was NOT verified for it, which is the
/// reason the setting is left alone. Refusing at the one method both write paths call closes
/// both and leaves the page, and the portal route, untouched.</para>
///
/// <para>The portal route does not call <see cref="IAccountAppService"/>; it creates the user
/// through <c>IdentityUserManager</c> directly, so this refusal cannot reach it.</para>
///
/// <para><b>ALSO REFUSED OVER HTTP: the stock anonymous account routes.</b> The same module
/// publishes <c>/api/account/send-password-reset-code</c>, <c>/reset-password</c>,
/// <c>/send-email-confirmation-token</c> and their siblings. They bypass the portal's own
/// throttled flows (<c>ExternalAccountAppService</c>: per-address limit and cooldown, per-IP
/// limiter on <c>/api/public/external-account</c>), so anyone could send mail to any address
/// without limit and probe which addresses hold accounts. Neither the SPA nor the AuthServer's
/// own pages use them: the Forgot, Reset, Resend and Email-confirmation pages are portal
/// pages that call <c>IExternalAccountAppService</c>. They are refused ONLY when the call
/// arrives on the <c>/api/account/</c> route, so any stock page that reaches the app service
/// in-process (for example the two-factor pages) keeps working.</para>
/// </summary>
public class StockSelfRegistrationRefusal : AbpInterceptor, ITransientDependency
{
    public const string RefusalMessage =
        "Self-registration is not available here. Please use the portal sign-up.";

    public const string RemoteRouteRefusalMessage =
        "This account route is not available here. Please use the portal sign-in pages.";

    public const string StockAccountRoutePrefix = "/api/account/";

    /// <summary>
    /// Stock <see cref="IAccountAppService"/> methods that are anonymous and send mail or reveal
    /// whether an account exists. Refused on the remote route only.
    /// </summary>
    public static readonly IReadOnlySet<string> RemoteRefusedMethods = new HashSet<string>(StringComparer.Ordinal)
    {
        nameof(IAccountAppService.SendPasswordResetCodeAsync),
        nameof(IAccountAppService.VerifyPasswordResetTokenAsync),
        nameof(IAccountAppService.ResetPasswordAsync),
        nameof(IAccountAppService.SendEmailConfirmationTokenAsync),
        nameof(IAccountAppService.VerifyEmailConfirmationTokenAsync),
        nameof(IAccountAppService.ConfirmEmailAsync),
        nameof(IAccountAppService.SendEmailConfirmationCodeAsync),
        nameof(IAccountAppService.GetEmailConfirmationCodeLimitAsync),
        nameof(IAccountAppService.GetConfirmationStateAsync),
        nameof(IAccountAppService.SendPhoneNumberConfirmationTokenAsync),
        nameof(IAccountAppService.ConfirmPhoneNumberAsync),
    };

    private readonly IHttpContextAccessor _httpContextAccessor;

    public StockSelfRegistrationRefusal(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public static bool IsRefusedOnRemoteRoute(string methodName, PathString requestPath) =>
        RemoteRefusedMethods.Contains(methodName)
        && requestPath.StartsWithSegments(StockAccountRoutePrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Attaches the refusal to every implementation of <see cref="IAccountAppService"/>. Must be
    /// called from <c>PreConfigureServices</c> so it is in place before the Account module
    /// registers its service.
    /// </summary>
    public static void RegisterIfAccountAppService(IOnServiceRegistredContext context)
    {
        if (AppliesTo(context.ImplementationType))
        {
            context.Interceptors.TryAdd<StockSelfRegistrationRefusal>();
        }
    }

    public static bool AppliesTo(Type implementationType) =>
        typeof(IAccountAppService).IsAssignableFrom(implementationType);

    public override async Task InterceptAsync(IAbpMethodInvocation invocation)
    {
        if (invocation.Method.Name == nameof(IAccountAppService.RegisterAsync))
        {
            throw new UserFriendlyException(RefusalMessage);
        }

        var path = _httpContextAccessor.HttpContext?.Request.Path ?? PathString.Empty;
        if (IsRefusedOnRemoteRoute(invocation.Method.Name, path))
        {
            throw new UserFriendlyException(RemoteRouteRefusalMessage);
        }

        await invocation.ProceedAsync();
    }
}
