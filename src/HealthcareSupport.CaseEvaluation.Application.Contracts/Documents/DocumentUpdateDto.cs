using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Documents;

/// <summary>
/// Input for updating a master <c>Document</c>. The underlying blob can be
/// replaced via the multipart endpoint -- this DTO covers metadata-only
/// updates (rename, deactivate, change content-type). When the file is
/// replaced, the controller produces a new BlobName and the AppService
/// updates it as a side effect of the upload action, NOT through this DTO.
/// </summary>
public class DocumentUpdateDto
{
    [Required]
    [StringLength(DocumentConsts.NameMaxLength)]
    public string Name { get; set; } = null!;

    [StringLength(DocumentConsts.ContentTypeMaxLength)]
    public string? ContentType { get; set; }

    public bool IsActive { get; set; }
}
