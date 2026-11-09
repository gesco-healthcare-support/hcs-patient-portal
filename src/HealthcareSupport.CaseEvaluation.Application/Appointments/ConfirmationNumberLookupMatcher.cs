using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;
using HealthcareSupport.CaseEvaluation.Patients;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-06 (R3) -- finds the one earlier appointment a booker means when they do not know its
/// confirmation number. The office decided the match: the patient's full name, date of birth and a
/// claim number on that appointment.
///
/// <para><b>Matching rule.</b> Names compare trimmed, case-insensitive, with inner spaces collapsed
/// and a hyphen treated as a space ("Garcia-Lopez" = "garcia lopez"); the middle name is ignored. The
/// date of birth is the exact calendar day. The claim number is any injury row's claim number, ignoring
/// case, spaces and dashes. The office filter keeps it to the current office.</para>
///
/// <para><b>The newest case in the chain (2026-11-08, W2).</b> The claim may match ANY of the patient's
/// cases, whatever its status, so a claim number that changed in a later re-evaluation still finds the
/// chain. From every matching case the walk goes forward (<see cref="ReEvaluationChain"/>), and the case
/// sent is the newest one that can be re-evaluated (approved, or a re-evaluation that was a no-show /
/// not seen) among them all. A patient's separate chains under a different claim are not reached.</para>
///
/// <para><b>One answer.</b> Several cases of the SAME patient resolve to that one newest case. Cases of
/// more than one patient are ambiguous and return null, so nothing is sent; so does a chain with
/// nothing that can be re-evaluated.</para>
/// </summary>
public class ConfirmationNumberLookupMatcher : ITransientDependency
{
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<AppointmentInjuryDetail, Guid> _injuryRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public ConfirmationNumberLookupMatcher(
        IRepository<Patient, Guid> patientRepository,
        IAppointmentRepository appointmentRepository,
        IRepository<AppointmentInjuryDetail, Guid> injuryRepository,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _patientRepository = patientRepository;
        _appointmentRepository = appointmentRepository;
        _injuryRepository = injuryRepository;
        _asyncExecuter = asyncExecuter;
    }

    /// <summary>The matching appointment, or null for no match and for an ambiguous one.</summary>
    public virtual async Task<Appointment?> FindAsync(ConfirmationNumberLookupDto input)
    {
        var dateOfBirth = input.PatientDateOfBirth.Date;
        var firstName = NormalizeName(input.PatientFirstName);
        var lastName = NormalizeName(input.PatientLastName);
        var claimNumber = NormalizeClaimNumber(input.ClaimNumber);
        if (dateOfBirth == DateTime.MinValue.Date || firstName.Length == 0 || lastName.Length == 0
            || claimNumber.Length == 0)
        {
            return null;
        }

        var nextDay = dateOfBirth.AddDays(1);
        var bornThatDay = await _asyncExecuter.ToListAsync((await _patientRepository.GetQueryableAsync())
            .Where(p => p.DateOfBirth >= dateOfBirth && p.DateOfBirth < nextDay)
            .Select(p => new { p.Id, p.FirstName, p.LastName }));
        var patientIds = bornThatDay
            .Where(p => NormalizeName(p.FirstName) == firstName && NormalizeName(p.LastName) == lastName)
            .Select(p => p.Id)
            .ToList();
        if (patientIds.Count == 0)
        {
            return null;
        }

        // Every case of those patients, whatever its status: the claim may sit on an older one.
        var cases = await _asyncExecuter.ToListAsync((await _appointmentRepository.GetQueryableAsync())
            .Where(a => patientIds.Contains(a.PatientId)));
        if (cases.Count == 0)
        {
            return null;
        }

        var caseIds = cases.Select(a => a.Id).ToList();
        var claims = await _asyncExecuter.ToListAsync((await _injuryRepository.GetQueryableAsync())
            .Where(i => caseIds.Contains(i.AppointmentId))
            .Select(i => new { i.AppointmentId, i.ClaimNumber }));
        var withTheClaim = claims
            .Where(c => NormalizeClaimNumber(c.ClaimNumber) == claimNumber)
            .Select(c => c.AppointmentId)
            .ToHashSet();
        var matched = cases.Where(a => withTheClaim.Contains(a.Id)).ToList();

        if (matched.Select(a => a.PatientId).Distinct().Count() != 1)
        {
            return null;
        }

        var patientId = matched[0].PatientId;
        return ReEvaluationChain.NewestEligible(cases.Where(a => a.PatientId == patientId).ToList(), matched);
    }

    /// <summary>Trimmed, lower-case, a hyphen read as a space, runs of spaces collapsed to one.</summary>
    internal static string NormalizeName(string? name)
    {
        var words = (name ?? string.Empty)
            .Replace('-', ' ')
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words).ToLowerInvariant();
    }

    /// <summary>Upper-case with every space and dash removed.</summary>
    internal static string NormalizeClaimNumber(string? claimNumber)
    {
        return string.Concat((claimNumber ?? string.Empty)
            .Where(c => !char.IsWhiteSpace(c) && c != '-')
            .Select(char.ToUpperInvariant));
    }
}
