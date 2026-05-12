using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

public interface IAppointmentDocumentsAppService
{
    Task<List<AppointmentDocumentDto>> GetListByAppointmentAsync(Guid appointmentId);

    /// <summary>
    /// Stream-based ad-hoc upload entry point. Marks the new row
    /// <c>IsAdHoc = true</c>; ad-hoc uploads have no status / due-date
    /// gate (mirrors OLD <c>AppointmentNewDocumentDomain</c>). The
    /// controller layer accepts the IFormFile and forwards the stream +
    /// metadata so the AppService stays free of ASP.NET Core
    /// dependencies.
    /// </summary>
    Task<AppointmentDocumentDto> UploadStreamAsync(
        Guid appointmentId,
        string documentName,
        string fileName,
        string? contentType,
        long fileSize,
        Stream content);

    /// <summary>
    /// Phase 14: package-document upload. Updates an existing
    /// <c>Pending</c> row created by the package-doc auto-queue
    /// handler. Gates: appointment Approved/RescheduleRequested +
    /// not past DueDate; document not currently Accepted (external
    /// users only -- internal users bypass).
    /// </summary>
    Task<AppointmentDocumentDto> UploadPackageDocumentAsync(
        Guid documentId,
        string fileName,
        string? contentType,
        long fileSize,
        Stream content);

    /// <summary>
    /// Phase 14: AME Joint Declaration Form upload. Creates a NEW row
    /// with <c>IsJointDeclaration = true</c>. Gates: appointment
    /// Approved + not past DueDate; AppointmentType is AME; caller is
    /// the booking attorney (Applicant or Defense Attorney role +
    /// creator match).
    /// </summary>
    Task<AppointmentDocumentDto> UploadJointDeclarationAsync(
        Guid appointmentId,
        string documentName,
        string fileName,
        string? contentType,
        long fileSize,
        Stream content);

    /// <summary>
    /// Phase 14: anonymous package-doc upload via per-document
    /// verification code. Used by the patient via the email link
    /// without an authenticated session. Rate-limited at the HTTP
    /// layer.
    /// </summary>
    Task<AppointmentDocumentDto> UploadByVerificationCodeAsync(
        Guid documentId,
        Guid verificationCode,
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

    /// <summary>
    /// Phase 1D.10: combined patient-portal documents view. Returns the
    /// patient-uploaded documents UNIONed with the generated Patient
    /// Packet (Status=Generated only) for an appointment, sorted by
    /// CreatedAt desc. Mirrors OLD's UX where the Patient Packet appeared
    /// in the patient's documents list alongside their uploads.
    /// </summary>
    Task<List<PatientPortalDocumentDto>> GetCombinedForAppointmentAsync(Guid appointmentId);
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
