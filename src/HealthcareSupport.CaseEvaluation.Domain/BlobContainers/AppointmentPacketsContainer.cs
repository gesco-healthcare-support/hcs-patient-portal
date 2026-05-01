using Volo.Abp.BlobStoring;

namespace HealthcareSupport.CaseEvaluation.BlobContainers;

/// <summary>
/// W2-11: ABP blob container marker for the per-appointment merged PDF
/// packets. Separate container from <see cref="AppointmentDocumentsContainer"/>
/// so a packet wipe does not collide with uploaded source documents.
/// </summary>
[BlobContainerName("appointment-packets")]
public class AppointmentPacketsContainer
{
}
