using System;
using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.AppointmentDrafts;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using HealthcareSupport.CaseEvaluation.HostOperators;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.MyAttorneyProfiles;
using HealthcareSupport.CaseEvaluation.MyClaimExaminerProfiles;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Doctors;
using HealthcareSupport.CaseEvaluation.Users;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Controllers;

/// <summary>
/// Six more app services have exactly one HTTP surface: their hand-written controllers.
/// </summary>
/// <remarks>
/// <para>
/// ABP's <c>ConventionalControllers.Create</c> publishes every public method of an app service a second time
/// under <c>/api/app/&lt;service&gt;/*</c> unless the class carries <c>[RemoteService(IsEnabled = false)]</c>.
/// Authorization attributes apply identically to both routes, so an <c>[Authorize]</c> audit finds nothing
/// wrong; what differs is every control keyed on an exact PATH (a rate limiter, a proxy rule, a WAF match),
/// which the auto-generated twin matches none of.
/// </para>
/// <para>
/// The generated Angular client was produced WHILE these services were auto-exposed, so it calls the auto
/// routes. Switching the auto routes off is therefore only safe where a hand-written action answers each
/// route the client calls, with the same verb and parameter binding. A near miss compiles and breaks only
/// at runtime, which is why each route is pinned here against the client's own values.
/// </para>
/// <para>
/// NOT covered here, deliberately: <see cref="DoctorTenantAppService"/>, <see cref="CaseEvaluationProfileAppService"/>
/// and <see cref="UserExtendedAppService"/> each extend a Volo service and are still auto-exposed. See
/// <see cref="Volo_derived_services_are_never_anonymous_on_any_exposed_method"/>.
/// </para>
/// </remarks>
public class AutoExposedAppServiceSurfaceTests
{
    public static TheoryData<Type> DisabledServices() => new()
    {
        typeof(AppointmentDraftAppService),
        typeof(IntakeAssignmentsAppService),
        typeof(CaseTrackerMissingIntakeAppService),
        typeof(MyAttorneyProfileAppService),
        typeof(MyClaimExaminerProfileAppService),
        typeof(AppNotificationAppService),
    };

    [Theory]
    [MemberData(nameof(DisabledServices))]
    public void The_service_is_not_auto_exposed(Type service)
    {
        var attribute = service.GetCustomAttribute<RemoteServiceAttribute>(inherit: false);

        attribute.ShouldNotBeNull("without it ABP publishes a second, auto-generated route for every public method");
        attribute!.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Every route the generated client (or a literal-string caller) uses: verb, path, parameter, binding.</summary>
    public static TheoryData<string, string, string?, string?> ClientCalls() => new()
    {
        { "GET", "api/app/appointment-draft/mine", null, null },
        { "POST", "api/app/appointment-draft/upsert", "input", "Body" },
        { "POST", "api/app/appointment-draft/discard-mine", null, null },

        { "GET", "api/app/my-attorney-profile", null, null },
        { "PUT", "api/app/my-attorney-profile", "input", "Body" },
        { "GET", "api/app/my-claim-examiner-profile", null, null },
        { "PUT", "api/app/my-claim-examiner-profile", "input", "Body" },

        { "GET", "api/app/app-notification/my-notifications", "input", "Query" },
        { "GET", "api/app/app-notification/my-unread-count", null, null },
        { "POST", "api/app/app-notification/mark-all-read", null, null },
        { "POST", "api/app/app-notification/{id}/mark-read", "id", "Unspecified" },

        { "GET", "api/app/intake-assignments", null, null },
        { "GET", "api/app/intake-assignments/paged-list", "input", "Query" },
        { "POST", "api/app/intake-assignments/assign", "input", "Body" },
        { "GET", "api/app/intake-assignments/assignable-operators", null, null },
        { "GET", "api/app/intake-assignments/office-options", null, null },
        { "GET", "api/app/intake-assignments/my-offices", null, null },
        { "GET", "api/app/intake-assignments/my-office-metrics", null, null },
        { "GET", "api/app/intake-assignments/switchable-offices", null, null },
        { "GET", "api/app/intake-assignments/impersonator-info", null, null },

        // Literal-string caller (admin/case-tracker-missing-intakes.component.ts); the controller already existed.
        { "GET", "api/app/case-tracker/missing-intakes", null, null },
    };

    [Theory]
    [MemberData(nameof(ClientCalls))]
    public void Each_route_the_client_calls_is_answered_by_exactly_one_action(
        string verb, string path, string? parameter, string? binding)
    {
        var route = AttributeRouteTable.Matching(verb, path)
            .ShouldHaveSingleItem($"{verb} {path} must be answered by exactly one hand-written action");

        var parameters = route.Action.GetParameters();
        if (parameter == null)
        {
            parameters.ShouldBeEmpty();
            return;
        }
        var bound = parameters.ShouldHaveSingleItem();
        bound.Name.ShouldBe(parameter);
        AttributeRouteTable.BindingOf(bound).ShouldBe(binding);
    }

    /// <summary>
    /// The one two-parameter route is checked apart: both values travel in the query string, as the client sends them.
    /// </summary>
    [Fact]
    public void Unassign_reads_both_ids_from_the_query_string()
    {
        var route = AttributeRouteTable.Matching("POST", "api/app/intake-assignments/unassign")
            .ShouldHaveSingleItem();

        var parameters = route.Action.GetParameters();
        parameters.Select(p => p.Name).ShouldBe(new[] { "operatorUserId", "officeId" });
        parameters.ShouldAllBe(p => AttributeRouteTable.BindingOf(p) == "Query");
    }

    /// <summary>
    /// The three services that extend a Volo service are still auto-exposed, because the generated client calls
    /// roughly sixty of their routes and re-declaring a Volo contract by hand is a much larger change. What can be
    /// held is the property that matters most while they stay exposed: no method on any of them is reachable
    /// anonymously. A class with no authorization attribute at all would be exactly that.
    /// </summary>
    [Theory]
    [InlineData(typeof(DoctorTenantAppService))]
    [InlineData(typeof(CaseEvaluationProfileAppService))]
    [InlineData(typeof(UserExtendedAppService))]
    public void Volo_derived_services_are_never_anonymous_on_any_exposed_method(Type service)
    {
        var unprotected = service
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.DeclaringType != typeof(object) && !m.IsSpecialName)
            .Where(m => !IsAuthorized(service, m))
            .Where(m => !TokenGatedByVolo.Contains(m.Name))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        unprotected.ShouldBeEmpty(
            "every public method must carry [Authorize], on the method or inherited from the class, and none may be [AllowAnonymous]");
    }

    /// <summary>
    /// The four Volo identity file downloads carry no [Authorize] by design: the caller first obtains a
    /// single-use download token from an authorized call (<c>GetDownloadTokenAsync</c>) and presents it here.
    /// They are inherited unchanged and equally reachable on Volo's own <c>/api/identity/users</c> routes, so
    /// they are not a new exposure. Named, not wildcarded, so any OTHER unprotected method still fails.
    /// </summary>
    private static readonly string[] TokenGatedByVolo =
    {
        "GetListAsExcelFileAsync",
        "GetListAsCsvFileAsync",
        "GetImportUsersSampleFileAsync",
        "GetImportInvalidUsersFileAsync",
    };

    private static bool IsAuthorized(Type service, MethodInfo method)
    {
        if (method.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any()
            || service.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
        {
            return false;
        }
        return method.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any()
            || service.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any();
    }
}
