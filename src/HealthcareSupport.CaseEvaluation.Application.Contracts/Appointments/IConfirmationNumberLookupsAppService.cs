using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// 2026-11-06 (R3) -- "I don't know the confirmation number". Like forgot-password: the answer is the
/// same whatever happened, and a match is emailed to the asking account's own verified login.
/// </summary>
public interface IConfirmationNumberLookupsAppService : IApplicationService
{
    /// <summary>
    /// Emails the confirmation number when the details match exactly one patient's case in this office.
    /// Returns nothing either way, so the answer never says whether anything matched.
    /// </summary>
    Task RequestAsync(ConfirmationNumberLookupDto input);
}
