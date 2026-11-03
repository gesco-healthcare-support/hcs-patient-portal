using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using Microsoft.Extensions.Logging;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Reads an office's doctor signature image for the packet's QME Form 110 page and returns it
/// as a <c>data:image/png;base64,...</c> URI, or the empty string when the office has none (the
/// template then hides the image and prints the blank signature line, as before).
///
/// <para><b>Isolation.</b> The blob name is built here from the CURRENT office id
/// (<see cref="ICurrentTenant"/>, the office whose packet is being rendered); no caller supplies an
/// id and nothing a user typed reaches the key, so an office can only ever read its own file. The read runs at HOST scope under
/// <c>office-signatures/{officeId:N}.png</c>.</para>
///
/// <para><b>Never fails a packet.</b> A blob that is missing, is not a PNG, is over
/// <see cref="MaxBytes"/> or cannot be read (storage down) yields "" and, except for "missing",
/// a warning naming the office id only. The image bytes are never logged.</para>
/// </summary>
public class OfficeSignatureReader : ITransientDependency
{
    /// <summary>Largest stored signature, in bytes. The operator script enforces the same cap.</summary>
    public const int MaxBytes = 204800;

    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    private readonly IBlobContainer<OfficeSignaturesContainer> _container;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<OfficeSignatureReader> _logger;

    public OfficeSignatureReader(
        IBlobContainer<OfficeSignaturesContainer> container,
        ICurrentTenant currentTenant,
        ILogger<OfficeSignatureReader> logger)
    {
        _container = container;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    /// <summary>The host-scope blob name for an office: <c>office-signatures/{officeId:N}.png</c>.</summary>
    public static string BlobNameFor(Guid officeId) => $"office-signatures/{officeId:N}.png";

    /// <summary>The signature as a PNG data URI, or "" when absent or unusable.</summary>
    public virtual async Task<string> ReadDataUriAsync(CancellationToken cancellationToken = default)
    {
        // The office is the CURRENT one, read before the scope switch below; the caller supplies no id.
        if (_currentTenant.Id is not { } officeId)
        {
            return string.Empty;
        }

        try
        {
            byte[]? bytes;
            using (_currentTenant.Change(null))
            {
                bytes = await _container.GetAllBytesOrNullAsync(BlobNameFor(officeId), cancellationToken);
            }

            if (bytes == null)
            {
                return string.Empty;
            }

            if (bytes.Length == 0 || bytes.Length > MaxBytes || !StartsWithPngMagic(bytes))
            {
                _logger.LogWarning(
                    "Office signature for office {OfficeId} is not a PNG of at most {MaxBytes} bytes; printing the blank line.",
                    officeId, MaxBytes);
                return string.Empty;
            }

            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Could not read the office signature for office {OfficeId}; printing the blank line.", officeId);
            return string.Empty;
        }
    }

    private static bool StartsWithPngMagic(byte[] bytes)
    {
        return bytes.AsSpan(0, Math.Min(bytes.Length, PngMagic.Length)).SequenceEqual(PngMagic);
    }
}
