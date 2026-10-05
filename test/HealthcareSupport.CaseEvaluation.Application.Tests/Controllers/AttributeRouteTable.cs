using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace HealthcareSupport.CaseEvaluation.Controllers;

/// <summary>
/// The hand-written controllers' attribute routes, read by reflection: verb, full path and the action
/// that answers it. Used by the route-surface tests, because no test host here serves HTTP and this is
/// the half that decides which action answers a request.
/// </summary>
internal static class AttributeRouteTable
{
    internal sealed record Entry(string Verb, string Path, MethodInfo Action);

    private static readonly Assembly HttpApiAssembly = typeof(CaseEvaluationController).Assembly;

    internal static List<Entry> Matching(string verb, string path) =>
        Read().Where(r => r.Verb == verb && r.Path == path).ToList();

    /// <summary>"Body", "Query" or "Unspecified", from the binding attribute on the parameter.</summary>
    internal static string BindingOf(ParameterInfo parameter)
    {
        if (parameter.GetCustomAttribute<FromBodyAttribute>() != null)
        {
            return "Body";
        }
        return parameter.GetCustomAttribute<FromQueryAttribute>() != null ? "Query" : "Unspecified";
    }

    private static IEnumerable<Entry> Read()
    {
        var controllers = HttpApiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);
        foreach (var controller in controllers)
        {
            var prefix = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;
            var actions = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(a => a.GetCustomAttribute<RouteAttribute>() != null
                    || a.GetCustomAttributes<HttpMethodAttribute>().Any());
            foreach (var action in actions)
            {
                // A route is its [Route] template, or else the template carried on the verb attribute, or else
                // the controller's own prefix (a bare [HttpGet] at the controller root).
                var template = action.GetCustomAttribute<RouteAttribute>()?.Template
                    ?? action.GetCustomAttributes<HttpMethodAttribute>().Select(m => m.Template).FirstOrDefault(t => t != null)
                    ?? string.Empty;
                var path = template.StartsWith("~/", StringComparison.Ordinal)
                    ? template[2..]
                    : $"{prefix.TrimEnd('/')}/{template}".TrimEnd('/');
                foreach (var verb in action.GetCustomAttributes<HttpMethodAttribute>().SelectMany(m => m.HttpMethods))
                {
                    yield return new Entry(verb, path, action);
                }
            }
        }
    }
}
