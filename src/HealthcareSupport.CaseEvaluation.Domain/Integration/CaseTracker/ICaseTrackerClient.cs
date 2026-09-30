using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Transport seam for the Case Tracker. An interface so the drain can be unit-tested without a
/// live endpoint -- which matters here because the endpoint does not exist yet on their side.
/// </summary>
public interface ICaseTrackerClient
{
    /// <summary>
    /// POSTs an already-rendered JSON body to <paramref name="targetPath"/> (relative to the
    /// configured base URL) and classifies the outcome. Never throws for a transport failure --
    /// the failure is returned as a retryable result so the caller's ledger stays authoritative.
    /// </summary>
    Task<CaseTrackerPushResult> PostAsync(
        string targetPath,
        string payloadJson,
        CancellationToken cancellationToken = default);
}
