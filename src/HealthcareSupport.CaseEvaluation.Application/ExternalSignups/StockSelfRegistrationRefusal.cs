using System;
using System.Threading.Tasks;
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
/// </summary>
public class StockSelfRegistrationRefusal : AbpInterceptor, ITransientDependency
{
    public const string RefusalMessage =
        "Self-registration is not available here. Please use the portal sign-up.";

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

        await invocation.ProceedAsync();
    }
}
