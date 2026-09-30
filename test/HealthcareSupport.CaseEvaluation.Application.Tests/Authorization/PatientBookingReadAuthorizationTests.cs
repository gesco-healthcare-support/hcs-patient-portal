using System.Linq;
using System.Reflection;
using HealthcareSupport.CaseEvaluation.Patients;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Authorization;

/// <summary>
/// The four "...ForAppointmentBooking" methods on PatientsAppService exist so the external booking
/// flow can touch patient data without holding CaseEvaluation.Patients. Three of them are reached
/// while serving a booker who does not hold it; one is reached by nothing at all. This class pins
/// that split in both directions.
///
/// <para><b>The split is decided by the CALLER SET, and the caller set is not what a search for the
/// generated proxy method names says it is.</b> Three of these routes are called by RAW URL through
/// RestService, so the proxy name appears nowhere and the route looks unused:</para>
///
/// <list type="bullet">
///   <item>GetPatientForAppointmentBookingAsync -- GET, appointment-add.component.ts:3002, from
///   onPatientSelected, behind a typeahead rendered only for isExternalUserNonPatient;</item>
///   <item>UpdatePatientForAppointmentBookingAsync -- PUT,
///   appointment-view.component.ts:1121;</item>
///   <item>GetOrCreatePatientForAppointmentBookingAsync -- server side,
///   AppointmentsAppService.cs:851.</item>
/// </list>
///
/// <para>Only GetPatientByEmailForAppointmentBookingAsync has no caller: its one UI call site is
/// loadPatientByEmail (appointment-add.component.ts:2892), and nothing invokes loadPatientByEmail.
/// Search by URL, not by proxy name:
/// <c>git grep -n "for-appointment-booking" -- angular/src/app ':!*.spec.ts'</c></para>
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
    /// The one with no caller is gated, so it cannot be read on a session alone. It returns the
    /// same patient record as GetListAsync, which has always required this permission.
    /// </summary>
    [Fact]
    public void TheBookingReadWithNoCaller_RequiresThePatientsPermission()
    {
        AuthorizationSurface.MethodAuthorization(Method("GetPatientByEmailForAppointmentBookingAsync"))
            .ShouldBe(PatientsPermission);
    }

    /// <summary>
    /// Asserted against the sibling rather than a literal: both return the same shape from the same
    /// repository, so if GetListAsync is ever re-gated to something stricter this fails until the
    /// by-email read follows.
    /// </summary>
    [Fact]
    public void TheGatedBookingRead_DemandsWhatTheEquivalentListDemands()
    {
        AuthorizationSurface.MethodAuthorization(Method("GetPatientByEmailForAppointmentBookingAsync"))
            .ShouldBe(AuthorizationSurface.MethodAuthorization(Method("GetListAsync")));
    }

    /// <summary>
    /// The three the booking flow actually calls stay reachable on a session alone.
    ///
    /// <para><b>This test failing is not a regression to revert on sight.</b> It means someone has
    /// added a permission to a method an external booker reaches, which returns 403 to attorneys
    /// and claim examiners mid-booking -- a break that no backend test would otherwise catch,
    /// because the calls are raw-URL and so invisible to a proxy-name search. If narrowing one of
    /// these is the intent, the question to answer first is what an external caller should receive
    /// instead, and the answer belongs with the change.</para>
    /// </summary>
    [Theory]
    [InlineData("GetPatientForAppointmentBookingAsync")]
    [InlineData("GetOrCreatePatientForAppointmentBookingAsync")]
    [InlineData("UpdatePatientForAppointmentBookingAsync")]
    public void AMethodTheBookingFlowCalls_StaysReachableOnASession(string name)
    {
        AuthorizationSurface.MethodAuthorization(Method(name))
            .ShouldBe(AuthorizationSurface.AuthenticatedOnly);
    }
}
