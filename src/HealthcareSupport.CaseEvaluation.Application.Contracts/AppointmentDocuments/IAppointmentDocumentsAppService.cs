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
