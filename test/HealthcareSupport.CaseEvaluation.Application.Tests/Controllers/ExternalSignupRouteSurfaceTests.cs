using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Controllers;

/// <summary>
/// The external-signup service has exactly one HTTP surface: its hand-written controllers.
/// </summary>
/// <remarks>
/// <para>
/// The generated Angular client (<c>angular/src/app/proxy/external-signups/external-signup.service.ts</c>)
/// was produced while the service was still auto-exposed, so it calls routes under
/// <c>api/app/external-signup</c>. Three of those calls are live in the UI. With the auto-exposure
/// switched off, each of the three must be answered by a hand-written action with the SAME route,
/// verb and parameter binding -- a near miss compiles and breaks only at runtime, which is why it is
/// pinned here against the client's own values rather than left to review.
/// </para>
/// <para>
/// Read from attribute routing by reflection, because no test host here serves HTTP. That is the
/// half that decides which action answers a request; the pass-through half (arguments reach the
/// service unchanged) is covered for every action by <see cref="ControllerDelegationTests"/>.
/// </para>
/// </remarks>
public class ExternalSignupRouteSurfaceTests
{
    private static readonly Assembly HttpApiAssembly = typeof(CaseEvaluationController).Assembly;

    [Fact]
    public void The_service_is_not_auto_exposed()
    {
        var attribute = typeof(ExternalSignupAppService).GetCustomAttribute<RemoteServiceAttribute>(inherit: false);

        attribute.ShouldNotBeNull("without it ABP publishes a second, auto-generated route for every public method");
        attribute!.IsEnabled.ShouldBeFalse();
    }

    /// <summary>The three calls the UI makes through the generated client: verb, path, parameter, binding.</summary>
    public static IEnumerable<object[]> ProxyCalls() => new[]
    {
        new object[] { "GET", "api/app/external-signup/active-invited-emails", "emails", "Query" },
        new object[] { "GET", "api/app/external-signup/tenant-options", "filter", "Query" },
        new object[] { "POST", "api/app/external-signup/send-portal-link", "input", "Body" },
    };

    [Theory]
    [MemberData(nameof(ProxyCalls))]
    public void Each_route_the_client_calls_is_answered_by_exactly_one_action(
        string verb, string path, string parameter, string binding)
    {
        var matches = Routes().Where(r => r.Verb == verb && r.Path == path).ToList();

        var route = matches.ShouldHaveSingleItem($"{verb} {path} must be answered by exactly one hand-written action");
        var bound = route.Action.GetParameters().ShouldHaveSingleItem();
        bound.Name.ShouldBe(parameter);
        BindingOf(bound).ShouldBe(binding);
    }

    private sealed record RouteEntry(string Verb, string Path, MethodInfo Action);

    private static IEnumerable<RouteEntry> Routes()
    {
        foreach (var controller in HttpApiAssembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
            foreach (var action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var template = action.GetCustomAttribute<RouteAttribute>()?.Template;
                if (template == null)
                {
                    continue;
                }
                var path = template.StartsWith("~/", StringComparison.Ordinal)
                    ? template[2..]
                    : $"{prefix.TrimEnd('/')}/{template}";
                foreach (var verb in action.GetCustomAttributes<HttpMethodAttribute>().SelectMany(m => m.HttpMethods))
                {
                    yield return new RouteEntry(verb, path, action);
                }
            }
        }
    }

    private static string BindingOf(ParameterInfo parameter)
    {
        if (parameter.GetCustomAttribute<FromBodyAttribute>() != null)
        {
            return "Body";
        }
        return parameter.GetCustomAttribute<FromQueryAttribute>() != null ? "Query" : "Unspecified";
    }
}
