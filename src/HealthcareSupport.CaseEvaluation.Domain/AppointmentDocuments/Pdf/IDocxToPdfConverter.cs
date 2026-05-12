using System.Threading;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;

/// <summary>
/// Phase 2 (2026-05-11) -- DOCX to PDF conversion for the appointment
/// packet pipeline. Called from
/// <see cref="Jobs.GenerateAppointmentPacketJob"/> after
/// <see cref="Templates.IDocxTemplateRenderer"/> fills the embedded
/// template; the converted PDF is what gets persisted to the
/// <c>AppointmentPacketsContainer</c> blob store and downloaded /
/// emailed.
///
/// <para>The interface lives in Domain alongside the renderer and token
/// resolver so the job can call it without crossing layers. Concrete
/// implementations are infrastructure adapters (current:
/// <see cref="GotenbergDocxToPdfConverter"/>, a sidecar HTTP client).
/// If a second external HTTP integration ever lands in Domain, extract
/// these adapters into a dedicated Infrastructure.Http project at that
/// point.</para>
/// </summary>
public interface IDocxToPdfConverter
{
    /// <summary>
    /// Converts in-memory DOCX bytes to PDF bytes. Throws on transport,
    /// timeout, or remote rendering failure -- callers should let the
    /// exception propagate so Hangfire's retry policy can re-attempt
    /// the whole job. The job-level per-kind catch is intentionally
    /// narrow (IO/InvalidOperation/Argument only) so transient HTTP
    /// failures DO trigger retry; permanent rendering failures from
    /// the OpenXml renderer do NOT.
    /// </summary>
    Task<byte[]> ConvertAsync(byte[] docxBytes, CancellationToken cancellationToken = default);
}
