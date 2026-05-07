using Volo.Abp.BlobStoring;

namespace HealthcareSupport.CaseEvaluation.BlobContainers;

/// <summary>
/// ABP blob container marker for per-user signature images uploaded by
/// internal staff (Clinic Staff / Staff Supervisor / IT Admin).
///
/// Replicates OLD's per-user signature feature where the responsible user's
/// signature image is stamped onto generated packet PDFs at the
/// <c>##Appointments.Signature##</c> placeholder. OLD stored a path string in
/// <c>User.SignatureAWSFilePath</c> referencing a file at
/// <c>wwwroot/Documents/userSignature/{userId}</c>; NEW stores a blob key in
/// the <see cref="CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName"/>
/// extra property on <c>IdentityUser</c> referencing a row in this container.
/// </summary>
[BlobContainerName("user-signatures")]
public class UserSignaturesContainer
{
}
