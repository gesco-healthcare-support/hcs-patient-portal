using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;

/// <summary>
/// Renders a named packet template to a fillable PDF via the packet-renderer
/// (WeasyPrint) sidecar. This is the sole packet renderer.
///
/// <para>The HTML templates are NOT embedded in this app: the sidecar owns
/// them (its generators are the single source of truth). This client sends only the template
/// <em>name</em> plus the resolved <c>##Group.Field## -&gt; value</c> token map (built by
/// <see cref="Templates.PacketTokenMap"/>); the sidecar substitutes the tokens and renders.
/// Called from <see cref="Jobs.GenerateAppointmentPacketJob"/>; the returned PDF is persisted /
/// downloaded / emailed.</para>
/// </summary>
public interface IHtmlPacketRenderer
{
    /// <summary>
    /// POSTs <paramref name="templateName"/> (see <see cref="PacketTemplateNames"/>) + the
    /// <paramref name="tokens"/> map to the sidecar and returns the rendered fillable PDF bytes.
    /// Throws on transport, timeout, or remote rendering failure -- callers let the exception
    /// propagate so Hangfire's retry policy can re-attempt the whole job (the job-level per-kind
    /// catch is narrow: IO / InvalidOperation / Argument only, so transient HTTP failures DO retry).
    /// </summary>
    Task<byte[]> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string> tokens,
        CancellationToken cancellationToken = default);
}
