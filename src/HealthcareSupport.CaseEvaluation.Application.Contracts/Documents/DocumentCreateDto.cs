using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Documents;

/// <summary>
/// Input for creating a master <c>Document</c> template. The blank-form
/// file itself is uploaded out-of-band via the controller using
/// IFormFile multipart -- this DTO carries only metadata. Mirrors OLD's
/// POST /api/Documents minus the local <c>DocumentFilePath</c> column
/// (NEW resolves that to an IBlobStorage blob name at AppService time).
/// </summary>
public class DocumentCreateDto
{
    [Required]
    [StringLength(DocumentConsts.NameMaxLength)]
    public string Name { get; set; } = null!;

    [StringLength(DocumentConsts.ContentTypeMaxLength)]
    public string? ContentType { get; set; }

    public bool IsActive { get; set; } = true;
}
