using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.Documents;

/// <summary>
/// Read model for a master Document template (a blank PDF / DOCX form
/// uploaded by IT Admin and later linked to one or more PackageDetails).
/// Mirrors OLD's <c>spm.Documents</c> row with NEW columns -- BlobName
/// replaces OLD's <c>DocumentFilePath</c> (file system path -> ABP
/// IBlobStorage reference).
/// </summary>
public class DocumentDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string BlobName { get; set; } = null!;
    public string? ContentType { get; set; }
    public bool IsActive { get; set; }
}
