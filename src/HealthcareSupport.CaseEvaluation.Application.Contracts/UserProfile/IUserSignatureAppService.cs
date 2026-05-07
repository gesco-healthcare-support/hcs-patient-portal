using System;
using System.IO;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;

namespace HealthcareSupport.CaseEvaluation.UserProfile;

/// <summary>
/// Per-user signature image management. Replicates OLD's
/// <c>User.SignatureAWSFilePath</c> profile feature where internal staff
/// (Clinic Staff / Staff Supervisor / IT Admin) upload a signature image
/// that the packet-generation flow stamps onto the Patient Packet at
/// <c>##Appointments.Signature##</c>. External roles (Patient / Claim
/// Examiner / Applicant Attorney / Defense Attorney) do not have
/// signatures (OLD parity).
///
/// <para>All caller-facing methods scope to <c>CurrentUser.Id</c>; there
/// is no per-user-target API on the public surface. The internal
/// <see cref="GetBytesByUserIdAsync"/> is consumed in-process by the
/// packet resolver and is hidden via
/// <c>[RemoteService(IsEnabled = false)]</c> on the implementation.</para>
/// </summary>
public interface IUserSignatureAppService
{
    /// <summary>
    /// Returns whether the current user has a signature on file plus
    /// display metadata. Returns a non-null DTO with
    /// <c>HasSignature = false</c> when none is uploaded.
    /// </summary>
    Task<UserSignatureInfoDto> GetInfoAsync();

    /// <summary>
    /// Uploads or replaces the current user's signature. Validates the
    /// file extension (PNG / JPG / JPEG) and size (<= 1 MB) per OLD
    /// parity. Persists to the user-signatures blob container and writes
    /// the blob key to the user's
    /// <c>UserSignatureBlobName</c> extra property.
    /// </summary>
    Task<UserSignatureInfoDto> UploadAsync(
        string fileName,
        string? contentType,
        long fileSize,
        Stream content);

    /// <summary>
    /// Streams the current user's signature image so the profile page
    /// can render the thumbnail. Throws when no signature is on file.
    /// </summary>
    Task<DownloadResult> DownloadAsync();

    /// <summary>
    /// Deletes the current user's signature blob and clears the
    /// <c>UserSignatureBlobName</c> extra property.
    /// </summary>
    Task DeleteAsync();

    /// <summary>
    /// In-process accessor used by the packet resolver to fetch a
    /// responsible user's signature bytes. Hidden from the HTTP API via
    /// <c>[RemoteService(IsEnabled = false)]</c>. Returns <c>null</c>
    /// when the user has no signature on file -- callers must handle
    /// the null case (matches OLD silent-skip behavior at
    /// <c>AppointmentDocumentDomain.cs:657, :704, :755, :816</c>).
    /// </summary>
    Task<byte[]?> GetBytesByUserIdAsync(Guid userId);
}
