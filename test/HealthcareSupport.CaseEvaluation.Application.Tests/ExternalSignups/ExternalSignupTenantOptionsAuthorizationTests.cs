using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.Controllers.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// The office roster behind <c>GET /api/public/external-signup/tenant-options</c> is for signed-in
/// staff who may invite, not for anonymous callers. It used to be anonymous, so an ordinary browser
/// request to the reserved admin host returned every office name and id.
///
/// <para>Reflection guards, as in the sibling <c>*AuthorizationTests</c>: the test module calls
/// <c>AddAlwaysAllowAuthorization()</c>, so a behavioural test through the app service would pass
/// whether the gate existed or not. The service attribute is the one ABP's interceptor enforces on
/// every call; the controller attribute keeps MVC from treating the route as anonymous.</para>
/// </summary>
public class ExternalSignupTenantOptionsAuthorizationTests
{
    private static MethodInfo ServiceMethod() =>
        typeof(ExternalSignupAppService).GetMethod(nameof(ExternalSignupAppService.GetTenantOptionsAsync))!;

    private static MethodInfo ControllerAction() =>
        typeof(ExternalSignupController).GetMethod(nameof(ExternalSignupController.GetTenantOptionsAsync))!;

    [Fact]
    public void The_app_service_method_requires_the_invite_permission()
    {
        ServiceMethod().GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(a => a.Policy)
            .ShouldContain(CaseEvaluationPermissions.UserManagement.InviteExternalUser);
    }

    [Fact]
    public void Neither_the_service_method_nor_the_controller_action_is_anonymous()
    {
        ServiceMethod().GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).ShouldBeEmpty();
        ControllerAction().GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).ShouldBeEmpty();
    }

    [Fact]
    public void The_controller_action_requires_a_signed_in_caller()
    {
        ControllerAction().GetCustomAttributes<AuthorizeAttribute>(inherit: true).ShouldNotBeEmpty();
    }
}
