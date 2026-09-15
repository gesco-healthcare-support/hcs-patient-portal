using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Authorization;

/// <summary>
/// Reflects the declared authorization surface of every application service into a
/// deterministic, sorted table.
///
/// WHY THIS EXISTS (#707). Both test harnesses call <c>AddAlwaysAllowAuthorization()</c>
/// (<c>CaseEvaluationTestBaseModule.cs:27</c> and
/// <c>CaseEvaluationMultiOfficeTestModule.cs:103</c>), so ABP's authorization interceptor
/// always succeeds and every <c>[Authorize]</c> attribute is a no-op under test. That is
/// ABP's own template default and is not a vulnerability -- the checks do run in
/// production -- but it means deleting any one of the permission-bearing attributes
/// leaves the whole suite green. Nothing in this repository could notice.
///
/// This type does not prove a permission check WORKS. It proves the DECLARATION has not
/// changed without someone saying so, which is the requirement #707 actually states:
/// "any change is always intended".
///
/// WHY CLASS AND METHOD ARE RECORDED SEPARATELY, AND NOT COMBINED. ASP.NET Core and ABP
/// both AND class-level and method-level [Authorize] together; a method-level attribute
/// does not replace the class-level one. An earlier draft of this file modelled it the
/// other way round, and the cost was concrete: for a service carrying
/// <c>[Authorize(X.Default)]</c> on the class and <c>[Authorize(X.Create)]</c> on
/// CreateAsync, deleting the CLASS attribute changed nothing in the rendered line, so the
/// gate would have sat silent through exactly the edit it exists to catch.
///
/// Rather than re-model the combination rule and depend on being right about ABP
/// internals across upgrades, this records both levels verbatim. Any change to either
/// appears in the diff, whatever the framework does with them at runtime.
/// </summary>
public static class AuthorizationSurface
{
    /// <summary>Rendered for a level carrying no authorization attribute.</summary>
    public const string NoAuthorization = "-";

    /// <summary>Rendered where [AllowAnonymous] appears.</summary>
    public const string Anonymous = "(anonymous)";

    /// <summary>
    /// Rendered for a bare [Authorize] carrying no policy. It demands a signed-in caller
    /// but no specific permission, which is a materially weaker guarantee than a named
    /// permission and must not be allowed to read like one in the snapshot.
    /// </summary>
    public const string AuthenticatedOnly = "(authenticated)";

    /// <summary>
    /// The arity marker .NET appends to a generic type's name, written as an escape
    /// rather than a literal so this file survives shell heredocs and grep intact.
    /// </summary>
    private const char GenericArityMarker = '`';

    /// <summary>
    /// Every concrete application service in <paramref name="assembly"/>, sorted ordinal.
    ///
    /// Ordinal rather than culture-aware so the rendered file is byte-identical on every
    /// machine. A culture-sensitive sort is exactly how a golden file starts differing
    /// between a developer's box and CI for a reason nobody can see in the diff.
    /// </summary>
    public static IReadOnlyList<Type> Services(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
            .Where(t => typeof(IApplicationService).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Public instance methods DECLARED ON the service itself.
    ///
    /// Declared-only on purpose: members inherited from an ABP base class are that base
    /// class's surface, not this service's. Including them would churn the snapshot on
    /// every ABP upgrade while saying nothing about this repository's own code.
    /// </summary>
    public static IReadOnlyList<MethodInfo> Methods(Type service)
    {
        ArgumentNullException.ThrowIfNull(service);

        return service
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)              // drop property get_/set_ accessors
            .Where(m => m.DeclaringType != typeof(object))
            .OrderBy(Signature, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// What the SERVICE TYPE declares, walking the hierarchy so an [Authorize] carried on
    /// a base class is not missed.
    /// </summary>
    public static string ClassAuthorization(Type service)
    {
        ArgumentNullException.ThrowIfNull(service);

        if (service.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
        {
            return Anonymous;
        }

        return RenderPolicies(service.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }

    /// <summary>What the METHOD itself declares, independent of its declaring type.</summary>
    public static string MethodAuthorization(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        if (method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
        {
            return Anonymous;
        }

        return RenderPolicies(method.GetCustomAttributes<AuthorizeAttribute>(inherit: true));
    }

    /// <summary>
    /// True when neither level declares anything at all -- neither a permission, nor a
    /// bare [Authorize], nor an explicit [AllowAnonymous]. That is the shape layer 2
    /// refuses: a method nobody decided about.
    /// </summary>
    public static bool HasNoDeclaredAuthorization(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        return MethodAuthorization(method) == NoAuthorization
            && ClassAuthorization(method.DeclaringType!) == NoAuthorization;
    }

    /// <summary>
    /// The whole surface as one deterministic block of text, LF-terminated.
    ///
    /// LF is fixed rather than Environment.NewLine so the committed file is the same
    /// bytes whether it was generated on Windows or in the Linux CI container. The packet
    /// templates learned this the expensive way: a bare text-mode write emitted CRLF on
    /// Windows and LF on Linux, so three "byte-exact" checks were Windows-local and had
    /// never once been compared against what CI produces.
    /// </summary>
    public static string Render(Assembly assembly)
    {
        var builder = new StringBuilder();

        foreach (var service in Services(assembly))
        {
            var classLevel = ClassAuthorization(service);

            foreach (var method in Methods(service))
            {
                builder.Append(service.FullName)
                       .Append('.')
                       .Append(Signature(method))
                       .Append(" -> class=")
                       .Append(classLevel)
                       .Append(" method=")
                       .Append(MethodAuthorization(method))
                       .Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Method name plus parameter type names. Parameters are included so two overloads
    /// cannot collapse onto one line and hide a difference between them.
    /// </summary>
    public static string Signature(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);

        var parameters = string.Join(
            ", ",
            method.GetParameters().Select(p => TypeName(p.ParameterType)));

        return method.Name + "(" + parameters + ")";
    }

    private static string RenderPolicies(IEnumerable<AuthorizeAttribute> attributes)
    {
        var policies = attributes
            .Select(a => string.IsNullOrWhiteSpace(a.Policy) ? AuthenticatedOnly : a.Policy)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        // " + " because multiple [Authorize] attributes are AND-ed: the caller must
        // satisfy every one. A comma would read like a choice.
        return policies.Count == 0 ? NoAuthorization : string.Join(" + ", policies);
    }

    /// <summary>
    /// Short, stable rendering of a parameter type. Generic arguments are spelled out so
    /// PagedResultDto&lt;AppointmentDto&gt; and PagedResultDto&lt;PatientDto&gt; cannot
    /// render identically.
    /// </summary>
    private static string TypeName(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name;
        var marker = name.IndexOf(GenericArityMarker);
        if (marker >= 0)
        {
            name = name[..marker];
        }

        var args = string.Join(", ", type.GetGenericArguments().Select(TypeName));
        return name + "<" + args + ">";
    }
}
