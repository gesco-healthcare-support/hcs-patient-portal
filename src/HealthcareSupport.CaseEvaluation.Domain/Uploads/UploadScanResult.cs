namespace HealthcareSupport.CaseEvaluation.Uploads;

/// <summary>
/// The scanner's verdict on one file: clean, or a named signature found.
/// </summary>
/// <remarks>
/// There is deliberately no "unknown" value. A scan that cannot reach a verdict throws
/// <see cref="UploadScanUnavailableException"/> instead, so no caller can mistake it for clean.
/// </remarks>
public sealed class UploadScanResult
{
    public static readonly UploadScanResult Clean = new(null);

    private UploadScanResult(string? signature)
    {
        Signature = signature;
    }

    /// <summary>The detected signature name, or null when the file is clean.</summary>
    public string? Signature { get; }

    public bool IsClean => Signature is null;

    public static UploadScanResult Found(string signature) => new(signature);
}
