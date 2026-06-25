using System;
using System.IO;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Per-office branding (display name + logo). The branding row + logo blob live
/// HOST-side keyed by office id, so the AllowAnonymous reads resolve an office's
/// brand by subdomain pre-auth, and the host-side central manager edits any office
/// by id without impersonating it. Writes are gated by
/// <c>CaseEvaluation.Branding.Edit</c>; the host-central list/preview by
/// <c>CaseEvaluation.Branding</c>.
/// </summary>
public interface IBrandingAppService
{
    /// <summary>AllowAnonymous: current office's branding (resolved by subdomain). Host scope -> defaults.</summary>
    Task<BrandingDto> GetBrandingAsync();

    /// <summary>AllowAnonymous: streams the current office's logo (resolved by subdomain).</summary>
    Task<DownloadResult> DownloadLogoAsync();

    /// <summary>Host-central: every office + its branding, for the central manager grid.</summary>
    Task<ListResultDto<OfficeBrandingDto>> GetOfficeBrandingsAsync();

    /// <summary>Host-central: streams a specific office's logo (the host surface has no subdomain office).</summary>
    Task<DownloadResult> DownloadLogoForOfficeAsync(Guid officeId);

    /// <summary>Sets (or clears, when null/blank) the display name for the target office (explicit id) or the current office.</summary>
    Task SetDisplayNameAsync(Guid? officeId, string? displayName);

    /// <summary>Uploads/replaces the logo for the target office (explicit id) or the current office.</summary>
    Task<BrandingDto> UploadLogoAsync(Guid? officeId, string fileName, string? contentType, long fileSize, Stream content);

    /// <summary>Removes the logo for the target office (explicit id) or the current office.</summary>
    Task RemoveLogoAsync(Guid? officeId);
}
