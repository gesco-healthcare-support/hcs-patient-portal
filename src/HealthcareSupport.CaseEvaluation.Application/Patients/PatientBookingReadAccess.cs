using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.Patients;

/// <summary>
/// Who may READ a patient record through the booking flow's by-id route
/// (<c>PatientsAppService.GetPatientForAppointmentBookingAsync</c>, reached by raw URL from the
/// booking wizard's "existing patient" typeahead). Holding a session is not enough: the route used
/// to admit any signed-in caller, which let any external user read every patient in their office.
///
/// <para><b>Admitted:</b></para>
/// <list type="bullet">
///   <item>internal staff (the app service additionally holds them to <c>CaseEvaluation.Patients</c>);</item>
///   <item>the patient's own login;</item>
///   <item>an Applicant Attorney, Defense Attorney or Claim Examiner who is a party to one of the
///   patient's appointments, by the SAME rule the wizard's patient typeahead uses
///   (<see cref="BookingPartyAppointments"/>, which <c>AppointmentsAppService.GetPatientLookupAsync</c> also uses): booker or linked attorney for the two
///   attorney roles, the named claim-examiner email for a Claim Examiner. So an id opens exactly the
///   records the typeahead could have offered, and "book for an existing patient" keeps working.</item>
/// </list>
///
/// <para><b>Unlike <see cref="PatientBookingEditAccess"/>, parties ARE admitted, and that is a
/// deliberate difference.</b> An attorney who booked for a patient has to see that patient to book
/// again, and the appointment they booked already shows them the same details. The edit rule refuses
/// parties because a party relationship can be self-granted by booking; for a read that grant needs the
/// patient's id or email first, which is the booking path's own question and is recorded where that
/// path lives, not here.</para>
///
/// <para>Everyone else is refused, including a Patient-role caller asking for someone else's record and
/// any external role this rule does not name.</para>
/// </summary>
public class PatientBookingReadAccess : ITransientDependency
{
    // Role names as seeded by ExternalUserRoleDataSeedContributor, matching the typeahead's checks.
    internal const string ApplicantAttorneyRole = "Applicant Attorney";
    internal const string DefenseAttorneyRole = "Defense Attorney";
    internal const string ClaimExaminerRole = "Claim Examiner";

    private readonly ICurrentUser _currentUser;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentApplicantAttorney, Guid> _applicantAttorneyLinks;
    private readonly IRepository<AppointmentDefenseAttorney, Guid> _defenseAttorneyLinks;
    private readonly IRepository<Patient, Guid> _patientRepository;

    public PatientBookingReadAccess(
        ICurrentUser currentUser,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentApplicantAttorney, Guid> applicantAttorneyLinks,
        IRepository<AppointmentDefenseAttorney, Guid> defenseAttorneyLinks,
        IRepository<Patient, Guid> patientRepository)
    {
        _patientRepository = patientRepository;
        _currentUser = currentUser;
        _appointmentRepository = appointmentRepository;
        _applicantAttorneyLinks = applicantAttorneyLinks;
        _defenseAttorneyLinks = defenseAttorneyLinks;
    }

    /// <summary>
    /// Booking against a SUPPLIED patient id (<c>AppointmentsAppService.SubmitAsync</c> with
    /// <c>PatientId</c>) is held to the same rule as reading that patient: an id alone is not
    /// entitlement. Booking makes the caller a party to the record, so accepting any id would let a
    /// caller who knows or guesses one attach themselves to someone else's patient.
    /// An EXTERNAL caller gets the same refusal for a missing id as for someone else's record, so the
    /// refusal does not tell them which ids are real; staff keep a not-found, they may see every
    /// patient in the office anyway.
    /// </summary>
    public virtual async Task EnsureCanBookForAsync(Guid patientId)
    {
        var patient = await _patientRepository.FindAsync(patientId);
        if (patient == null && BookingFlowRoles.IsInternalUserCaller(_currentUser.Roles))
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Patient), patientId);
        }

        if (patient == null || !await CanReadAsync(patient))
        {
            throw new Volo.Abp.Authorization.AbpAuthorizationException("Not authorized to book for this patient.");
        }
    }

    public virtual async Task<bool> CanReadAsync(Patient patient)
    {
        if (BookingFlowRoles.IsInternalUserCaller(_currentUser.Roles))
        {
            return true;
        }

        if (_currentUser.Id.HasValue
            && patient.IdentityUserId.HasValue
            && _currentUser.Id.Value == patient.IdentityUserId.Value)
        {
            return true;
        }

        return await IsPartyByTheTypeaheadRuleAsync(patient.Id);
    }

    private async Task<bool> IsPartyByTheTypeaheadRuleAsync(Guid patientId)
    {
        var appointments = (await _appointmentRepository.GetQueryableAsync())
            .Where(a => a.PatientId == patientId);

        if (_currentUser.Id.HasValue && _currentUser.IsInRole(ApplicantAttorneyRole)
            && BookingPartyAppointments.ForApplicantAttorney(
                appointments, await _applicantAttorneyLinks.GetQueryableAsync(), _currentUser.Id.Value).Any())
        {
            return true;
        }

        if (_currentUser.Id.HasValue && _currentUser.IsInRole(DefenseAttorneyRole)
            && BookingPartyAppointments.ForDefenseAttorney(
                appointments, await _defenseAttorneyLinks.GetQueryableAsync(), _currentUser.Id.Value).Any())
        {
            return true;
        }

        return _currentUser.IsInRole(ClaimExaminerRole)
            && BookingPartyAppointments.ForClaimExaminer(appointments, _currentUser.GetVerifiedEmail()).Any();
    }
}
