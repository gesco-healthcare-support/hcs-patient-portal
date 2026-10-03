using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Uploads;

/// <summary>
/// Checks an uploaded file for malware before it is stored (B11).
/// </summary>
/// <remarks>
/// Fails CLOSED: an implementation that cannot reach a verdict -- scanner down, slow, or answering
/// anything it does not understand -- throws <see cref="UploadScanUnavailableException"/> rather than
/// returning <see cref="UploadScanResult.Clean"/>. A caller must never treat "no answer" as "clean".
/// </remarks>
public interface IUploadScanner
{
    /// <summary>
    /// Scans <paramref name="content"/> from its current position to the end. A seekable stream is
    /// returned at the position it was given in, so the caller can store the same bytes it scanned.
    /// </summary>
    Task<UploadScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default);
}
