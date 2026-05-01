using Volo.Abp.BlobStoring;

namespace HealthcareSupport.CaseEvaluation.BlobContainers;

/// <summary>
/// Marker class for document-packages (admin-CRUD-managed packet bundles). Backed by
/// ABP's DB-BLOB provider at MVP. Consolidates OLD's per-role packet buckets
/// (doctorpacket / attornypacket* / claimexaminerpacket*) into one logical container --
/// the package metadata identifies which role-flavor a given blob serves.
/// </summary>
[BlobContainerName("document-packages")]
public class DocumentPackagesContainer
{
}
