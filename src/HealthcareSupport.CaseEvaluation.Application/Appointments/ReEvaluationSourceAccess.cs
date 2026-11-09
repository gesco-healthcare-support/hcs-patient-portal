using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Patients;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// An earlier appointment a re-evaluation may follow from, and how the caller got to it.
/// <paramref name="Source"/> is the newest case in the chain the booker pointed at (2026-11-08, W2);
/// <paramref name="Requested"/> is the one whose number they typed.
/// </summary>
public sealed record ReEvaluationSourceMatch(Appointment Source, bool CallerIsParty, Appointment Requested)
{
    /// <summary>True when the form is filled from a later case than the one whose number was typed.</summary>
    public bool FilledFromNewer => Source.Id != Requested.Id;
}

/// <summary>
/// 2026-11-06 (R2c) -- who may start a re-evaluation from an earlier appointment. A party to it, as
/// before; and now also an external caller who is NOT a party (a new paralegal at the same firm, a
/// different claim examiner) and proves it with the confirmation number plus the patient's date of
/// birth. Used by the lookup the booking form calls and by the booking submit, which checks again.
///
/// <para><b>The newest case in the chain (2026-11-08, W2).</b> The answer is not the typed case but the
/// newest re-evaluable one reachable from it (<see cref="ReEvaluationChain"/>): typing the first exam's
/// number after a re-evaluation of it was approved fills the form from that re-evaluation. Being a party
/// is judged on THAT case. A caller who is a party to the typed case but not to the newest one (an
/// earlier attorney, say) needs the date of birth like anyone else, and then gets the newest case in
/// the non-party view: the office decided one rule, with no exception for a former party.</para>
///
/// <para><b>One answer for every refusal.</b> No such number, a wrong date of birth and an
/// appointment that cannot be re-evaluated all come back as null, so a caller cannot use this to
/// learn which numbers exist or what state a case is in.</para>
///
/// <para><b>Throttled</b> through <see cref="ReEvaluationLookupThrottle"/>, before the match, on the
/// date-of-birth path only. A party reading its own case is not a guess.</para>
/// </summary>
public class ReEvaluationSourceAccess : ITransientDependency
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly AppointmentReadAccessGuard _readAccessGuard;
    private readonly ReEvaluationLookupThrottle _throttle;
    private readonly ReEvaluationChain _chain;

    public ReEvaluationSourceAccess(
        IAppointmentRepository appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        AppointmentReadAccessGuard readAccessGuard,
        ReEvaluationLookupThrottle throttle,
        ReEvaluationChain chain)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _readAccessGuard = readAccessGuard;
        _throttle = throttle;
        _chain = chain;
    }

    /// <summary>
    /// The newest case in the chain behind <paramref name="confirmationNumber"/> when the caller may
    /// re-evaluate from it, or null.
    ///
    /// <para>A caller who can read the newest case gets it back whatever its status, and the date of
    /// birth is not consulted; the caller then applies the usual status gate, so a party keeps the
    /// specific "not yet approved" refusals it has always had. (The newest case is only ever not
    /// re-evaluable when nothing in the chain is, and then it is the typed case itself.) Anyone else
    /// needs a date of birth that matches, and a case that can be re-evaluated; both failures return
    /// the same null.</para>
    /// </summary>
    public virtual async Task<ReEvaluationSourceMatch?> FindAsync(
        string confirmationNumber,
        DateTime? patientDateOfBirth)
    {
        var requested = await _appointmentRepository.FindByConfirmationNumberAsync(confirmationNumber.Trim());
        if (requested != null && await _readAccessGuard.CanReadAsync(requested))
        {
            var newest = await _chain.FindNewestEligibleAsync(requested);
            if (newest.Id == requested.Id || await _readAccessGuard.CanReadAsync(newest))
            {
                return new ReEvaluationSourceMatch(newest, CallerIsParty: true, requested);
            }

            // A party to the typed case only: the same proof anyone else gives, for the newest case.
            return await ProveByDateOfBirthAsync(requested, newest, patientDateOfBirth);
        }

        if (patientDateOfBirth is not { } dateOfBirth)
        {
            return null;
        }

        // Counted whether the number exists or not, so the limit does not tell the two apart.
        await _throttle.EnsureAllowedAndStampAsync(LookupThrottleBudget.ReEvaluationSource);
        if (requested == null)
        {
            return null;
        }

        var newestCase = await _chain.FindNewestEligibleAsync(requested);
        if (!IsReEvaluable(newestCase) || !await DateOfBirthMatchesAsync(dateOfBirth, requested, newestCase))
        {
            return null;
        }

        // The date of birth opened it; a caller who is a party to the newest case is still one.
        return new ReEvaluationSourceMatch(
            newestCase,
            CallerIsParty: await _readAccessGuard.CanReadAsync(newestCase),
            requested);
    }

    /// <summary>
    /// The newest case for a party to the typed case who is not a party to the newest one: only with
    /// the patient's date of birth, throttled like any other guess, and in the non-party view.
    /// </summary>
    private async Task<ReEvaluationSourceMatch?> ProveByDateOfBirthAsync(
        Appointment requested,
        Appointment newest,
        DateTime? patientDateOfBirth)
    {
        if (patientDateOfBirth is not { } dateOfBirth)
        {
            return null;
        }

        await _throttle.EnsureAllowedAndStampAsync(LookupThrottleBudget.ReEvaluationSource);
        if (!IsReEvaluable(newest) || !await DateOfBirthMatchesAsync(dateOfBirth, requested, newest))
        {
            return null;
        }

        return new ReEvaluationSourceMatch(newest, CallerIsParty: false, requested);
    }

    private static bool IsReEvaluable(Appointment appointment) =>
        AppointmentLifecycleValidators.CanCreateReval(
            appointment.AppointmentStatus, appointment.EvaluationKind, callerIsItAdmin: false);

    /// <summary>
    /// True when <paramref name="dateOfBirth"/> is the patient's birth date, compared as calendar
    /// dates. The patient record or the copy taken onto either appointment counts: the chain keeps to
    /// one patient, so all of them are this patient's, and a copy is all there is if the record was
    /// corrected later.
    /// </summary>
    private async Task<bool> DateOfBirthMatchesAsync(DateTime dateOfBirth, Appointment requested, Appointment newest)
    {
        var date = dateOfBirth.Date;
        if (date == DateTime.MinValue.Date)
        {
            // An omitted date deserializes to 0001-01-01; it identifies nobody.
            return false;
        }

        if (requested.PatientDateOfBirth?.Date == date || newest.PatientDateOfBirth?.Date == date)
        {
            return true;
        }

        var patient = requested.PatientId == Guid.Empty
            ? null
            : await _patientRepository.FindAsync(requested.PatientId);
        return patient != null && patient.DateOfBirth.Date == date;
    }
}
