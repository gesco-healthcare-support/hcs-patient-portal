using Volo.Abp.BlobStoring;

namespace HealthcareSupport.CaseEvaluation.BlobContainers;

/// <summary>
/// Phase E (2026-06-25): ABP blob container for per-office logo images. Accessed
/// at HOST scope (<c>CurrentTenant.Change(null)</c>) with the office id as the
/// blob name, so the AllowAnonymous login / SPA branding serve endpoint and the
/// host-side central manager read/write any office's logo without an office-DB
/// hop. Companion to <see cref="HealthcareSupport.CaseEvaluation.Branding.OfficeBranding"/>,
/// which stores the blob reference + content type.
/// </summary>
[BlobContainerName("office-logos")]
public class OfficeLogosContainer
{
}
