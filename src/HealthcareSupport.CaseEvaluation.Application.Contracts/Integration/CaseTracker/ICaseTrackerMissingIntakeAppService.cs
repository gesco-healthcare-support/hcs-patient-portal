using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Host-side, on-demand missing-intake report (#944). Report only: nothing here can enqueue, push or change an
/// outbox row. A person reads it and decides; the manual push inside the office is the way to act.
/// </summary>
public interface ICaseTrackerMissingIntakeAppService : IApplicationService
{
    /// <summary>Every office's appointments with no intake row, split by the office's first intake row.</summary>
    Task<CaseTrackerMissingIntakeReportDto> GetReportAsync();
}
