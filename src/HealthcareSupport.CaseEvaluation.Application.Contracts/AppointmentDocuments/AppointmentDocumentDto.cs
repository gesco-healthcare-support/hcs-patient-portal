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
}
