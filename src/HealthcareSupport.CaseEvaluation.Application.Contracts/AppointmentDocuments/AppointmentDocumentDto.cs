using System;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

public class AppointmentDocumentDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public Guid AppointmentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public Guid UploadedByUserId { get; set; }

    /// <summary>W2-11: review state.</summary>
    public DocumentStatus Status { get; set; }

    /// <summary>W2-11: free-text rejection reason (max 500 chars).</summary>
    public string? RejectionReason { get; set; }

    /// <summary>W2-11: user who approved or last actioned the document.</summary>
    public Guid? ResponsibleUserId { get; set; }

    /// <summary>W2-11: user who rejected the document.</summary>
    public Guid? RejectedByUserId { get; set; }
}
