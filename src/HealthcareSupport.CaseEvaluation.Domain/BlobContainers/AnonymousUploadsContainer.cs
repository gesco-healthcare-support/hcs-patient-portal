using Volo.Abp.BlobStoring;

namespace HealthcareSupport.CaseEvaluation.BlobContainers;

/// <summary>
/// Marker class for anonymous (magic-link) document uploads. Backed by ABP's DB-BLOB
/// provider at MVP. Consumer must set tenant context via
/// <c>using (_currentTenant.Change(tenantId))</c> before SaveAsync because the
/// uploader is not yet associated with a tenant via standard ABP resolution.
/// Post-MVP scope: anonymous-document-upload feature is deferred (per scope-lock 2026-04-24).
/// </summary>
[BlobContainerName("anonymous-uploads")]
public class AnonymousUploadsContainer
{
}
