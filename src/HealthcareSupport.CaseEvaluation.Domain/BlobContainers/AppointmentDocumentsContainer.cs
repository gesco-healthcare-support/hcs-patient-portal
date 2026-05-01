using Volo.Abp.BlobStoring;

namespace HealthcareSupport.CaseEvaluation.BlobContainers;

/// <summary>
/// Marker class for the appointment-documents blob container. Resolves to ABP's
/// <c>BlobStoringDatabaseModule</c> default provider (DB-BLOB at MVP); the consumer
/// AppService injects <c>IBlobContainer&lt;AppointmentDocumentsContainer&gt;</c> and is
/// unaware of the underlying provider. Post-MVP swap to S3 is config-only.
///
/// OLD reference: bucket name `patientpacket` from
/// `P:\PatientPortalOld\PatientAppointment.Infrastructure\Utilities\AmazonBlobStorage.cs:302-333`.
/// </summary>
[BlobContainerName("appointment-documents")]
public class AppointmentDocumentsContainer
{
}
