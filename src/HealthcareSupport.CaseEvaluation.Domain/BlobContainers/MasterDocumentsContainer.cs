using Volo.Abp.BlobStoring;

namespace HealthcareSupport.CaseEvaluation.BlobContainers;

/// <summary>
/// Marker for the master Document template catalog -- blank PDF / DOCX
/// forms uploaded by IT Admin and later linked to one or more
/// <c>PackageDetail</c>s. Distinct from <c>DocumentPackagesContainer</c>
/// (per-appointment packet bundles) and <c>AppointmentDocumentsContainer</c>
/// (per-appointment patient uploads). Mirrors OLD's
/// <c>spm.Documents.DocumentFilePath</c> file-system path with an ABP blob
/// reference (Phase 5, 2026-05-03).
/// </summary>
[BlobContainerName("master-documents")]
public class MasterDocumentsContainer
{
}
