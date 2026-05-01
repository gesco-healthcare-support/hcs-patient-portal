using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

public interface IAppointmentDocumentsAppService
{
    Task<List<AppointmentDocumentDto>> GetListByAppointmentAsync(Guid appointmentId);

    /// <summary>
    /// Stream-based upload entry point. The controller layer accepts the
    /// IFormFile from a multipart request and forwards the stream + metadata
    /// here so the AppService stays free of ASP.NET Core dependencies.
    /// </summary>
    Task<AppointmentDocumentDto> UploadStreamAsync(
        Guid appointmentId,
        string documentName,
        string fileName,
        string? contentType,
        long fileSize,
        Stream content);

    Task<DownloadResult> DownloadAsync(Guid id);

    Task DeleteAsync(Guid id);

    /// <summary>W2-11: flip a document's status to Approved. Sets ResponsibleUserId.</summary>
    Task<AppointmentDocumentDto> ApproveAsync(Guid id);

    /// <summary>W2-11: flip a document's status to Rejected with a required reason. Sets RejectedByUserId.</summary>
    Task<AppointmentDocumentDto> RejectAsync(Guid id, RejectDocumentInput input);

    /// <summary>W2-11: re-trigger the packet generation job for an appointment.</summary>
    Task RegeneratePacketAsync(Guid appointmentId);
}

/// <summary>
/// Plain DTO carrying bytes + display metadata for a download response.
/// The manual controller wraps this in a <c>FileStreamResult</c>.
/// </summary>
public class DownloadResult
{
    public Stream Content { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
}
