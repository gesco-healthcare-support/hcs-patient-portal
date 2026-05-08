using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.UserProfile;

/// <summary>
/// Per-user signature image management. Replicates OLD's
/// <c>User.SignatureAWSFilePath</c> profile feature: internal staff
/// (Clinic Staff / Staff Supervisor / IT Admin) upload a PNG/JPG/JPEG
/// (max 1 MB) signature image that the packet-generation flow stamps
/// onto the Patient Packet at <c>##Appointments.Signature##</c>.
///
/// <para>OLD code reference:
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\UserModule\UserDomain.cs</c>
/// lines 191-231 (upload), and
/// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs</c>
/// lines 476-481 (download + stamp).</para>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class UserSignatureAppService : CaseEvaluationAppService, IUserSignatureAppService
{
    /// <summary>OLD parity: 1 MB cap (UserDomain.cs:205 enforces 1000 KB at the frontend).</summary>
    public const long MaxFileSizeBytes = 1024 * 1024;

    private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg" };

    private readonly IBlobContainer<UserSignaturesContainer> _blobContainer;
    private readonly IdentityUserManager _userManager;

    public UserSignatureAppService(
        IBlobContainer<UserSignaturesContainer> blobContainer,
        IdentityUserManager userManager)
    {
        _blobContainer = blobContainer;
        _userManager = userManager;
    }

    [Authorize(CaseEvaluationPermissions.UserSignatures.ManageOwn)]
    public virtual async Task<UserSignatureInfoDto> GetInfoAsync()
    {
        var user = await GetCurrentUserOrFailAsync();
        return BuildInfo(user);
    }

    [Authorize(CaseEvaluationPermissions.UserSignatures.ManageOwn)]
    public virtual async Task<UserSignatureInfoDto> UploadAsync(
        string fileName,
        string? contentType,
        long fileSize,
        Stream content)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new UserFriendlyException(L["UserSignature:FileNameRequired"]);
        }
        if (content == null || fileSize <= 0)
        {
            throw new UserFriendlyException(L["UserSignature:FileEmpty"]);
        }
        if (fileSize > MaxFileSizeBytes)
        {
            throw new UserFriendlyException(L["UserSignature:FileTooLarge"]);
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new UserFriendlyException(L["UserSignature:UnsupportedFormat"]);
        }

        EnsureValidImageMagicBytes(content);

        var user = await GetCurrentUserOrFailAsync();

        // OLD parity: blob key is keyed on user id; replacing the signature
        // overwrites the same blob. The .png extension in the key is for
        // operator readability only -- ContentType is authoritative.
        var blobName = $"{user.Id:N}{NormalizeExtension(extension)}";

        // Reset stream position so SaveAsync starts from the actual file
        // content (EnsureValidImageMagicBytes already rewound, but the
        // explicit reset keeps the contract clear).
        if (content.CanSeek)
        {
            content.Seek(0, SeekOrigin.Begin);
        }
        await _blobContainer.SaveAsync(blobName, content, overrideExisting: true);

        var resolvedContentType = string.IsNullOrWhiteSpace(contentType)
            ? GuessContentType(extension)
            : contentType;

        user.SetProperty(
            CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName,
            blobName);
        user.SetProperty(UserSignatureFileNamePropertyName, fileName.Trim());
        user.SetProperty(UserSignatureContentTypePropertyName, resolvedContentType);

        await _userManager.UpdateAsync(user);

        return BuildInfo(user);
    }

    [Authorize(CaseEvaluationPermissions.UserSignatures.ManageOwn)]
    public virtual async Task<DownloadResult> DownloadAsync()
    {
        var user = await GetCurrentUserOrFailAsync();
        var blobName = user.GetProperty<string>(
            CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName);
        if (string.IsNullOrWhiteSpace(blobName))
        {
            throw new UserFriendlyException(L["UserSignature:NotFound"]);
        }

        var stream = await _blobContainer.GetAsync(blobName);
        return new DownloadResult
        {
            Content = stream,
            FileName = user.GetProperty<string>(UserSignatureFileNamePropertyName)
                       ?? "signature.png",
            ContentType = user.GetProperty<string>(UserSignatureContentTypePropertyName)
                          ?? "image/png",
        };
    }

    [Authorize(CaseEvaluationPermissions.UserSignatures.ManageOwn)]
    public virtual async Task DeleteAsync()
    {
        var user = await GetCurrentUserOrFailAsync();
        var blobName = user.GetProperty<string>(
            CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName);
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return;
        }

        try
        {
            await _blobContainer.DeleteAsync(blobName);
        }
        catch
        {
            // Entity is the source of truth; orphan blob is a cleanup-job concern.
        }

        user.SetProperty(
            CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName,
            null);
        user.SetProperty(UserSignatureFileNamePropertyName, null);
        user.SetProperty(UserSignatureContentTypePropertyName, null);

        await _userManager.UpdateAsync(user);
    }

    [RemoteService(IsEnabled = false)]
    public virtual async Task<byte[]?> GetBytesByUserIdAsync(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            return null;
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return null;
        }

        var blobName = user.GetProperty<string>(
            CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName);
        if (string.IsNullOrWhiteSpace(blobName))
        {
            // OLD silent-skip: AppointmentDocumentDomain.cs:657, :704, :755, :816
            // checks `if (fileName != null)` and skips InsertAPicture when null.
            return null;
        }

        return await _blobContainer.GetAllBytesOrNullAsync(blobName);
    }

    private async Task<IdentityUser> GetCurrentUserOrFailAsync()
    {
        var userId = CurrentUser.Id;
        if (userId == null)
        {
            throw new AbpAuthorizationException();
        }
        var user = await _userManager.GetByIdAsync(userId.Value);
        return user;
    }

    private static UserSignatureInfoDto BuildInfo(IdentityUser user)
    {
        var blobName = user.GetProperty<string>(
            CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName);
        var hasSignature = !string.IsNullOrWhiteSpace(blobName);
        return new UserSignatureInfoDto
        {
            HasSignature = hasSignature,
            FileName = hasSignature ? user.GetProperty<string>(UserSignatureFileNamePropertyName) : null,
            ContentType = hasSignature ? user.GetProperty<string>(UserSignatureContentTypePropertyName) : null,
        };
    }

    /// <summary>
    /// Sniffs the first bytes of the stream to confirm a PNG or JPEG header.
    /// Browser-supplied filename + ContentType are spoofable; the file
    /// header is part of the file itself. Mirrors the
    /// <c>EnsureValidFileFormat</c> approach in
    /// <see cref="AppointmentDocuments.AppointmentDocumentsAppService"/>.
    /// </summary>
    private static void EnsureValidImageMagicBytes(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return;
        }

        var magic = new byte[8];
        stream.Seek(0, SeekOrigin.Begin);
        var read = stream.Read(magic, 0, magic.Length);
        stream.Seek(0, SeekOrigin.Begin);

        if (read < 4)
        {
            throw new UserFriendlyException("UserSignature:FileEmpty");
        }

        var isPng = magic[0] == 0x89 && magic[1] == 0x50 && magic[2] == 0x4E && magic[3] == 0x47;
        var isJpeg = magic[0] == 0xFF && magic[1] == 0xD8 && magic[2] == 0xFF;

        if (!isPng && !isJpeg)
        {
            throw new UserFriendlyException("UserSignature:UnsupportedFormat");
        }
    }

    private static string NormalizeExtension(string extension) =>
        extension == ".jpeg" ? ".jpg" : extension;

    private static string GuessContentType(string extension) =>
        extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream",
        };

    // Filename + ContentType are stored alongside the blob key so the
    // download endpoint can return the original metadata and the profile
    // page can render an accurate thumbnail. ABP extra-properties are
    // typed as object; reading via GetProperty<string>(...) returns
    // null when unset.
    private const string UserSignatureFileNamePropertyName = "UserSignatureFileName";
    private const string UserSignatureContentTypePropertyName = "UserSignatureContentType";
}
