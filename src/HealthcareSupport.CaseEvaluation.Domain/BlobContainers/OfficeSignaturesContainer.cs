using Volo.Abp.BlobStoring;

namespace HealthcareSupport.CaseEvaluation.BlobContainers;

/// <summary>
/// ABP blob container for each office's doctor signature image, printed on the QME Form 110
/// page of the PQME packet. Read at HOST scope with the blob name
/// <c>office-signatures/{officeId:N}.png</c> (see
/// <see cref="HealthcareSupport.CaseEvaluation.Branding.OfficeSignatureReader"/>), so the
/// object key is <c>host/office-signatures/{officeId:N}.png</c>: outside the
/// <c>tenants/{officeId}/*</c> prefix the Case Tracker's read-only key covers, and distinct
/// from the logo's <c>host/{officeId:N}.png</c>.
///
/// <para>There is no upload screen. An operator stores the file with
/// <c>scripts/hosting/office-signature.sh</c>; the application only reads it.</para>
/// </summary>
[BlobContainerName("office-signatures")]
public class OfficeSignaturesContainer
{
}
