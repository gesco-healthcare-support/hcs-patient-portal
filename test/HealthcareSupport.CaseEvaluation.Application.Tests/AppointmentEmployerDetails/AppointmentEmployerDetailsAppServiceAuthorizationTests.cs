using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

/// <summary>
/// Reflection guards that the employer-detail mutation endpoints carry their
/// feature-specific ABP permission policy, at parity with the sibling child
/// services (Injury, PrimaryInsurance, ApplicantAttorney, ...). Before this fix
/// <c>CreateAsync</c>/<c>UpdateAsync</c> used bare <c>[Authorize]</c> (any
/// authenticated tenant user could write), while <c>Delete</c>/<c>Get</c>
/// already used the specific constants.
///
/// <para>Behavioral permission-denial tests are deliberately NOT used here: the
/// SQLite integration harness does not seed role-&gt;permission grants, so every
/// behavioral <c>AbpAuthorizationException</c> test in this suite is a
/// <c>[Fact(Skip)]</c> stub (see
/// <see cref="Appointments.AppointmentsAppServiceAuthorizationTests"/>). A
/// reflection guard is deterministic and harness-independent.</para>
/// </summary>
public class AppointmentEmployerDetailsAppServiceAuthorizationTests
{
    private static bool RequiresPolicy(string methodName, string policy)
    {
        return typeof(AppointmentEmployerDetailsAppService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == methodName)
            .SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>(inherit: true))
            .Any(a => a.Policy == policy);
    }

    [Fact]
    public void CreateAsync_requires_the_Create_permission()
    {
        RequiresPolicy(nameof(AppointmentEmployerDetailsAppService.CreateAsync),
            CaseEvaluationPermissions.AppointmentEmployerDetails.Create).ShouldBeTrue();
    }

    [Fact]
    public void UpdateAsync_requires_the_Edit_permission()
    {
        RequiresPolicy(nameof(AppointmentEmployerDetailsAppService.UpdateAsync),
            CaseEvaluationPermissions.AppointmentEmployerDetails.Edit).ShouldBeTrue();
    }
}
