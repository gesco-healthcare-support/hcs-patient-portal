using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.Patients;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Authorization;

/// <summary>
/// GHSA-99wf-55x9-f8ww -- the patient-read half.
///
/// <para>PatientsAppService carries four "...ForAppointmentBooking" methods that exist so the
/// external booking flow can touch patient data without holding CaseEvaluation.Patients. Two of
/// them are byte-for-byte clones of a sibling that IS gated -- GetPatientForAppointmentBookingAsync
/// of GetWithNavigationPropertiesAsync, and the by-email one of a filtered GetList -- so the pair
/// was a permission bypass of its own twin, reachable by any authenticated caller including an
/// external booker or a patient reading somebody else's record.</para>
///
/// <para>They were gated. The advisory's stated reason for leaving them open -- that gating would
/// break booking -- was measured and does not hold for these two: neither has a caller anywhere,
/// in src/ or in angular/src/app outside the generated proxy. The two that ARE load-bearing keep
/// the bare [Authorize], and this class pins that split so neither half moves by accident.</para>
/// </summary>
public sealed class PatientBookingReadAuthorizationTests
{
    private const string PatientsPermission = "CaseEvaluation.Patients";

    private static MethodInfo Method(string name)
    {
        return typeof(PatientsAppService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Single(m => m.Name == name);
    }

    /// <summary>
    /// The clone must demand exactly what its twin demands. Asserted against the twin rather than
    /// against a literal, because the point is not "this string appears" -- it is that two methods
    /// returning the same DTO from the same repository call cannot diverge. If the twin is ever
    /// re-gated to something stricter, this fails until the clone follows.
    /// </summary>
    [Theory]
    [InlineData("GetPatientForAppointmentBookingAsync", "GetWithNavigationPropertiesAsync")]
    public void ABookingRead_DemandsWhatItsGatedTwinDemands(string clone, string twin)
    {
        AuthorizationSurface.MethodAuthorization(Method(clone))
            .ShouldBe(AuthorizationSurface.MethodAuthorization(Method(twin)));
    }

    [Theory]
    [InlineData("GetPatientForAppointmentBookingAsync")]
    [InlineData("GetPatientByEmailForAppointmentBookingAsync")]
    public void ABookingRead_IsNotReachableOnASessionAlone(string name)
    {
        AuthorizationSurface.MethodAuthorization(Method(name)).ShouldBe(PatientsPermission);
    }

    /// <summary>
    /// The deliberate remainder, pinned so that gating it is a decision rather than a tidy-up.
    ///
    /// <para>Both have a real caller inside the booking flow -- AppointmentsAppService reaches
    /// GetOrCreate... and UpdatePatient... while serving an external booker who does not hold the
    /// permission. A named permission here would be demanded of that booker and would break
    /// booking, which is what the advisory describes and what does NOT apply to the two above.</para>
    ///
    /// <para>So this test failing is not a regression to revert: it means someone gated one of
    /// them, and the question to answer first is what the external caller is then supposed to
    /// receive. That question is still open on the advisory.</para>
    /// </summary>
    [Theory]
    [InlineData("GetOrCreatePatientForAppointmentBookingAsync")]
    [InlineData("UpdatePatientForAppointmentBookingAsync")]
    public void AWriteTheBookingFlowActuallyCalls_IsStillAuthenticatedOnly(string name)
    {
        AuthorizationSurface.MethodAuthorization(Method(name))
            .ShouldBe(AuthorizationSurface.AuthenticatedOnly);
    }
}
