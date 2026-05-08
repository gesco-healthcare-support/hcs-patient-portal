using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.SystemParameters;

/// <summary>
/// Per-tenant singleton <c>SystemParameter</c> read + update surface. Mirrors
/// OLD's <c>SystemParametersController</c> minus its dead-code endpoints
/// (POST / PATCH / DELETE / GET-list) -- OLD's Angular UI only ever calls
/// GET-by-id and PUT, and the singleton-per-tenant invariant is enforced by
/// the data seed contributor in Phase 1.
///
/// Authorization (Phase 2.5):
///   - <c>GetAsync</c>: any role with <c>CaseEvaluation.SystemParameters</c>
///     (Default). All three internal roles plus IT Admin hold this.
///   - <c>UpdateAsync</c>: requires <c>CaseEvaluation.SystemParameters.Edit</c>.
///     IT Admin + Staff Supervisor hold this; Clinic Staff is read-only.
/// </summary>
public interface ISystemParametersAppService : IApplicationService
{
    /// <summary>
    /// Returns the per-tenant singleton row for the calling tenant scope.
    /// Throws <c>BusinessException(CaseEvaluation:SystemParameter.NotSeeded)</c>
    /// if the row is missing -- callers are expected to escalate to the
    /// SaaS host to re-run the data seeder rather than auto-recover.
    /// </summary>
    Task<SystemParameterDto> GetAsync();

    /// <summary>
    /// Updates the per-tenant singleton. Validates positive-integer ranges
    /// on every int field (mirroring OLD's `[Range(1, int.MaxValue)]`)
    /// before writing. Optimistic concurrency via <c>ConcurrencyStamp</c>.
    /// </summary>
    Task<SystemParameterDto> UpdateAsync(SystemParameterUpdateDto input);
}
