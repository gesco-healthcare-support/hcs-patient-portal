using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Phase E (2026-06-25) -- per-office branding (display name + logo), stored in
/// the HOST/management database keyed by office (Volo SaaS tenant) id. Host-only
/// (mapped inside the IsHostDatabase block, never IMultiTenant) so the login page
/// and the host-side central manager can resolve an office's brand pre-auth and
/// without switching into the office database. One row per office (unique index
/// on <see cref="OfficeId"/>).
///
/// <para>The logo image bytes live in
/// <see cref="HealthcareSupport.CaseEvaluation.BlobContainers.OfficeLogosContainer"/>
/// (MinIO), accessed at host scope and keyed by the same office id; this row
/// stores only the blob reference + its content type.</para>
/// </summary>
public class OfficeBranding : FullAuditedAggregateRoot<Guid>
{
    public const int DisplayNameMaxLength = 128;
    public const int LogoBlobNameMaxLength = 256;
    public const int LogoContentTypeMaxLength = 100;

    /// <summary>The office (Volo SaaS tenant) id this branding belongs to.</summary>
    public Guid OfficeId { get; private set; }

    /// <summary>Office display name shown in the shell + browser title; null = fall back to the default.</summary>
    public string? DisplayName { get; private set; }

    /// <summary>Blob key of the uploaded logo in the office-logos container; null = no custom logo.</summary>
    public string? LogoBlobName { get; private set; }

    /// <summary>MIME type of the stored logo (image/png or image/jpeg); null when no logo.</summary>
    public string? LogoContentType { get; private set; }

    protected OfficeBranding()
    {
    }

    public OfficeBranding(Guid id, Guid officeId)
        : base(id)
    {
        OfficeId = officeId;
    }

    /// <summary>Sets (or clears, when null/blank) the office display name.</summary>
    public void SetDisplayName(string? displayName)
    {
        var trimmed = displayName?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            DisplayName = null;
            return;
        }

        Check.Length(trimmed, nameof(displayName), DisplayNameMaxLength);
        DisplayName = trimmed;
    }

    /// <summary>Records the uploaded logo's blob key + content type.</summary>
    public void SetLogo(string blobName, string contentType)
    {
        LogoBlobName = Check.NotNullOrWhiteSpace(blobName, nameof(blobName), LogoBlobNameMaxLength);
        LogoContentType = Check.NotNullOrWhiteSpace(contentType, nameof(contentType), LogoContentTypeMaxLength);
    }

    /// <summary>Removes the logo reference (the orphaned blob is a cleanup concern).</summary>
    public void ClearLogo()
    {
        LogoBlobName = null;
        LogoContentType = null;
    }
}
