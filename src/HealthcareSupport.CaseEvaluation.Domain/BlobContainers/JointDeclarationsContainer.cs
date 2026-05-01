using Volo.Abp.BlobStoring;

namespace HealthcareSupport.CaseEvaluation.BlobContainers;

/// <summary>
/// Marker class for the joint-declarations blob container. Backed by ABP's
/// DB-BLOB provider at MVP.
///
/// OLD reference: bucket name `jointagreementletter` from
/// `P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\AmazonBlobStorage.cs:180`.
/// </summary>
[BlobContainerName("joint-declarations")]
public class JointDeclarationsContainer
{
}
