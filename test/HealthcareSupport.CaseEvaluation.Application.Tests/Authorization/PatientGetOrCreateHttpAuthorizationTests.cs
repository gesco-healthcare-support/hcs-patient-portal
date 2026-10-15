using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.Controllers.Patients;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Authorization;

/// <summary>
/// <c>POST /api/app/patients/for-appointment-booking/get-or-create</c> resolves a patient from typed
/// details, email first, and returns the whole matched record. Left open, any signed-in caller could
/// turn a patient's email into their record. Its only browser caller was deleted when the wizard
/// stopped posting the patient separately (Item B PR2), so over HTTP it is now staff-only.
///
/// <para><b>Two halves, deliberately different.</b> The CONTROLLER action requires
/// <c>CaseEvaluation.Patients.Create</c>, which governs the HTTP route. The APP SERVICE method stays a
/// bare <c>[Authorize]</c>, because booking calls it in-process for external bookers too
/// (<c>AppointmentsAppService</c>, resolving the submit's patient), and that call never passes through
/// the controller. Gating the service would break external booking; leaving the controller open was the
/// hole.</para>
///
/// <para>Reflection, because the MVC pipeline cannot be booted here and the test module allows every
/// permission. The controller attribute is what MVC's authorization filter enforces for the route.</para>
/// </summary>
public class PatientGetOrCreateHttpAuthorizationTests
{
    private static MethodInfo ControllerAction() =>
        typeof(PatientController).GetMethod(nameof(PatientController.GetOrCreatePatientForAppointmentBookingAsync))!;

    private static MethodInfo ServiceMethod() =>
        typeof(PatientsAppService).GetMethod(nameof(PatientsAppService.GetOrCreatePatientForAppointmentBookingAsync))!;

    [Fact]
    public void The_http_route_requires_the_patient_create_permission()
    {
        ControllerAction().GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(a => a.Policy)
            .ShouldContain(CaseEvaluationPermissions.Patients.Create);
        ControllerAction().GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).ShouldBeEmpty();
    }

    [Fact]
    public void The_app_service_method_stays_callable_by_in_process_booking()
    {
        // A named permission here would refuse every external booker's submit.
        ServiceMethod().GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(a => a.Policy)
            .ShouldAllBe(policy => string.IsNullOrEmpty(policy));
    }
}
