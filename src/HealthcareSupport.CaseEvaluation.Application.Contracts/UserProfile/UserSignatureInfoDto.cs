namespace HealthcareSupport.CaseEvaluation.UserProfile;

/// <summary>
/// Metadata about a user's signature. Returned by
/// <see cref="IUserSignatureAppService.GetInfoAsync"/> and
/// <see cref="IUserSignatureAppService.UploadAsync"/>. The frontend uses
/// <c>HasSignature</c> to decide whether to render the thumbnail; when
/// true, it issues a separate request to the download endpoint.
/// </summary>
public class UserSignatureInfoDto
{
    /// <summary>True when the user has a signature on file.</summary>
    public bool HasSignature { get; set; }

    /// <summary>Original uploaded filename (e.g. "my-sig.png"). Null when <see cref="HasSignature"/> is false.</summary>
    public string? FileName { get; set; }

    /// <summary>MIME type of the uploaded image (e.g. "image/png"). Null when <see cref="HasSignature"/> is false.</summary>
    public string? ContentType { get; set; }
}
