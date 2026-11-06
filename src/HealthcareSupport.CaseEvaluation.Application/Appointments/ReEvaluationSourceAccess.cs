using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Patients;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>An earlier appointment a re-evaluation may follow from, and how the caller got to it.</summary>
public sealed record ReEvaluationSourceMatch(Appointment Source, bool CallerIsParty);

/// <summary>
/// 2026-11-06 (R2c) -- who may start a re-evaluation from an earlier appointment. A party to it, as
/// before; and now also an external caller who is NOT a party (a new paralegal at the same firm, a
/// different claim examiner) and proves it with the confirmation number plus the patient's date of
/// birth. Used by the lookup the booking form calls and by the booking submit, which checks again.
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

    public ReEvaluationSourceAccess(
        IAppointmentRepository appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        AppointmentReadAccessGuard readAccessGuard,
        ReEvaluationLookupThrottle throttle)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _readAccessGuard = readAccessGuard;
        _throttle = throttle;
    }

    /// <summary>
    /// The appointment behind <paramref name="confirmationNumber"/> when the caller may re-evaluate
    /// from it, or null.
    ///
    /// <para>A caller who can already read it gets it back whatever its status, and the date of birth
    /// is not consulted; the caller then applies the usual status gate, so a party keeps the specific
    /// "not yet approved" refusals it has always had. Anyone else needs a date of birth that matches,
    /// and an appointment that can be re-evaluated; both failures return the same null.</para>
    /// </summary>
    public virtual async Task<ReEvaluationSourceMatch?> FindAsync(
        string confirmationNumber,
        DateTime? patientDateOfBirth)
    {
        var source = await _appointmentRepository.FindByConfirmationNumberAsync(confirmationNumber.Trim());
        if (source != null && await _readAccessGuard.CanReadAsync(source))
        {
            return new ReEvaluationSourceMatch(source, CallerIsParty: true);
        }

        if (patientDateOfBirth is not { } dateOfBirth)
        {
            return null;
        }

        // Counted whether the number exists or not, so the limit does not tell the two apart.
        await _throttle.EnsureAllowedAndStampAsync(LookupThrottleBudget.ReEvaluationSource);

        if (source == null
            || !AppointmentLifecycleValidators.CanCreateReval(
                source.AppointmentStatus, source.EvaluationKind, callerIsItAdmin: false)
            || !await DateOfBirthMatchesAsync(source, dateOfBirth))
        {
            return null;
        }

        return new ReEvaluationSourceMatch(source, CallerIsParty: false);
    }

    /// <summary>
    /// True when <paramref name="dateOfBirth"/> is the patient's birth date, compared as calendar
    /// dates. Either the patient record or the copy taken onto the appointment counts: both are this
    /// patient's, and the copy is all there is if the record was corrected later.
    /// </summary>
    private async Task<bool> DateOfBirthMatchesAsync(Appointment source, DateTime dateOfBirth)
    {
        var date = dateOfBirth.Date;
        if (date == DateTime.MinValue.Date)
        {
            // An omitted date deserializes to 0001-01-01; it identifies nobody.
            return false;
        }

        if (source.PatientDateOfBirth?.Date == date)
        {
            return true;
        }

        var patient = source.PatientId == Guid.Empty
            ? null
            : await _patientRepository.FindAsync(source.PatientId);
        return patient != null && patient.DateOfBirth.Date == date;
    }
}
