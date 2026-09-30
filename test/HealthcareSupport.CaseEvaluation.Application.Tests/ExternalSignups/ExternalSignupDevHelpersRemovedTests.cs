using System;
using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.Controllers.ExternalSignups;
using Microsoft.AspNetCore.Mvc;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// The external-signup surface carries no development-only helpers, in any environment.
///
/// <para>WHAT IS PINNED. Routes for this area come from three types, so each is checked:</para>
/// <list type="bullet">
///   <item><description><see cref="IExternalSignupAppService"/> and <see cref="ExternalSignupAppService"/>:
///   ABP's conventional controllers expose the application service itself under
///   <c>api/app/external-signup</c>, and every public method of it becomes an action;</description></item>
///   <item><description><see cref="ExternalSignupController"/>: the hand-written controller under
///   <c>api/public/external-signup</c>.</description></item>
/// </list>
///
/// <para>WHAT IT DOES NOT PROVE. It is structural: the registered routes are derived from these types,
/// and this repository has no in-process host to probe the live route table. It shows nothing about
/// other controllers.</para>
/// </summary>
public class ExternalSignupDevHelpersRemovedTests
{
    private static readonly string[] RemovedHelpers = ["DeleteTestUsersAsync", "MarkEmailConfirmedAsync"];

    [Fact]
    public void The_application_service_contract_declares_no_development_only_helper()
    {
        typeof(IExternalSignupAppService).GetMethods()
            .Select(m => m.Name)
            .Where(RemovedHelpers.Contains)
            .ShouldBeEmpty();
    }

    [Fact]
    public void The_application_service_exposes_no_development_only_helper()
    {
        typeof(ExternalSignupAppService).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .Where(RemovedHelpers.Contains)
            .ShouldBeEmpty();
    }

    [Fact]
    public void The_signup_controller_has_no_development_only_action_or_route()
    {
        var actions = typeof(ExternalSignupController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        actions.Select(m => m.Name).Where(RemovedHelpers.Contains).ShouldBeEmpty();
        actions.SelectMany(m => m.GetCustomAttributes<RouteAttribute>())
            .Select(r => r.Template)
            .Where(t => t.StartsWith("dev/", StringComparison.OrdinalIgnoreCase))
            .ShouldBeEmpty();
    }
}
