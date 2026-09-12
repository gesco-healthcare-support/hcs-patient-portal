using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Pure reflection guards that the appointment mutation endpoints carry the
/// correct ABP permission policy. The <c>[Authorize(policy)]</c> attribute is the
/// control ABP's authorization interceptor enforces at runtime, so asserting its
/// presence locks the control in place.
///
/// <para>Behavioral permission-denial tests are deliberately NOT used here: the
/// SQLite integration harness does not seed role-&gt;permission grants, so every
/// behavioral <c>AbpAuthorizationException</c> test in this suite is a
/// <c>[Fact(Skip)]</c> stub. A reflection guard is deterministic and
/// harness-independent.</para>
///
/// <para>Closes the deferred finding: <c>UpdateAsync</c> was bare
/// <c>[Authorize]</c> (any authenticated tenant user could edit any appointment).
/// OLD parity: appointment edit is internal-staff only, and the Edit permission is
/// granted only to internal roles, so <c>[Authorize(Appointments.Edit)]</c>
/// reproduces OLD's "internal users edit, external parties use change-requests"
/// model.</para>
/// </summary>
public class AppointmentsAppServiceAuthorizationTests
{
    private static bool RequiresPolicy(string methodName, string policy)
    {
        return typeof(AppointmentsAppService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == methodName)
            .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .Any(a => a.Policy == policy);
    }

    [Fact]
    public void UpdateAsync_requires_the_Edit_permission()
    {
        RequiresPolicy(nameof(AppointmentsAppService.UpdateAsync),
            CaseEvaluationPermissions.Appointments.Edit).ShouldBeTrue();
    }

    [Fact]
    public void CreateAsync_requires_the_Create_permission()
    {
        RequiresPolicy(nameof(AppointmentsAppService.CreateAsync),
            CaseEvaluationPermissions.Appointments.Create).ShouldBeTrue();
    }

    [Fact]
    public void ReSubmitAsync_requires_the_Create_permission()
    {
        RequiresPolicy(nameof(AppointmentsAppService.ReSubmitAsync),
            CaseEvaluationPermissions.Appointments.Create).ShouldBeTrue();
    }

    [Fact]
    public void CreateRevalAsync_requires_the_Create_permission()
    {
        RequiresPolicy(nameof(AppointmentsAppService.CreateRevalAsync),
            CaseEvaluationPermissions.Appointments.Create).ShouldBeTrue();
    }

    [Fact]
    public void DeleteAsync_requires_the_Delete_permission()
    {
        RequiresPolicy(nameof(AppointmentsAppService.DeleteAsync),
            CaseEvaluationPermissions.Appointments.Delete).ShouldBeTrue();
    }
}
