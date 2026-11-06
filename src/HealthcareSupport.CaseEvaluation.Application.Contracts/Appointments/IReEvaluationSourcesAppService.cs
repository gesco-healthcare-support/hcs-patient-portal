using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-06 -- the earlier appointment a re-evaluation follows from, for any external party (a
/// patient, an attorney, a paralegal or a claim examiner), including one who is not a party to it.
/// </summary>
public interface IReEvaluationSourcesAppService : IApplicationService
{
    /// <summary>
    /// The earlier appointment, or null. Null means the same thing for a number nobody issued, a wrong
    /// date of birth and an appointment that cannot be re-evaluated, so the answer never confirms
    /// that a guessed number exists.
    /// </summary>
    Task<ReEvaluationSourceDto?> LookupAsync(ReEvaluationSourceLookupDto input);
}
