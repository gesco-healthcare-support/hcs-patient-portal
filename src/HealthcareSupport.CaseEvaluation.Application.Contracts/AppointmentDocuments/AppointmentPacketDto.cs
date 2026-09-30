using System;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

public class AppointmentPacketDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public Guid AppointmentId { get; set; }
    public PacketKind Kind { get; set; }
    public string BlobName { get; set; } = string.Empty;
    public PacketGenerationStatus Status { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime? RegeneratedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
