using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Phase E (2026-06-25) -- per-office branding (display name + logo). The branding
/// row (<see cref="OfficeBranding"/>) and the logo blob both live HOST-side keyed by
/// office id, so the AllowAnonymous GetBranding + logo-serve resolve an office's
/// brand by subdomain pre-auth WITHOUT an office-DB hop, and the host-side central
/// manager edits any office by id without impersonating it. Every repository + blob
/// access runs inside <c>CurrentTenant.Change(null)</c> so the host DB / host blob
/// namespace is used even when a host operator is impersonating an office (otherwise
/// the request would route to the office DB, which has no branding table). Image
/// validation mirrors <c>UserSignatureAppService</c> (1 MB, PNG/JPG/JPEG, magic bytes).
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize]
public class BrandingAppService : CaseEvaluationAppService, IBrandingAppService
{
    /// <summary>1 MB cap (parity with the signature upload).</summary>
    public const long MaxFileSizeBytes = 1024 * 1024;

    private static readonly string[] AllowedExtensions = { ".png", ".jpg", ".jpeg" };

    private readonly IRepository<OfficeBranding, Guid> _brandingRepository;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly IBlobContainer<OfficeLogosContainer> _logoContainer;

    public BrandingAppService(
        IRepository<OfficeBranding, Guid> brandingRepository,
        IRepository<Tenant, Guid> tenantRepository,
        IBlobContainer<OfficeLogosContainer> logoContainer)
    {
        _brandingRepository = brandingRepository;
        _tenantRepository = tenantRepository;
        _logoContainer = logoContainer;
    }

    [AllowAnonymous]
    public virtual async Task<BrandingDto> GetBrandingAsync()
    {
        var officeId = CurrentTenant.Id;
        if (officeId == null)
        {
            // Host surface (admin.localhost) or an unresolved subdomain -> default brand.
            return new BrandingDto();
        }

        using (CurrentTenant.Change(null))
        {
            var branding = await _brandingRepository.FirstOrDefaultAsync(x => x.OfficeId == officeId.Value);
            return ToDto(branding, officeId.Value, officeQualifiedUrl: false);
        }
    }

    [AllowAnonymous]
    public virtual async Task<DownloadResult> DownloadLogoAsync()
    {
        var officeId = CurrentTenant.Id;
        if (officeId == null)
        {
            throw new UserFriendlyException(L["Branding:LogoNotFound"]);
        }

        return await ReadLogoAsync(officeId.Value);
    }

    [Authorize(CaseEvaluationPermissions.Branding.Default)]
    public virtual async Task<ListResultDto<OfficeBrandingDto>> GetOfficeBrandingsAsync()
    {
        using (CurrentTenant.Change(null))
        {
            var offices = await _tenantRepository.GetListAsync();
            var brandings = await _brandingRepository.GetListAsync();
            var byOffice = brandings
                .GroupBy(b => b.OfficeId)
                .ToDictionary(g => g.Key, g => g.First());

            var dtos = offices
                .Select(office =>
                {
                    byOffice.TryGetValue(office.Id, out var branding);
                    var hasLogo = branding != null && !string.IsNullOrWhiteSpace(branding.LogoBlobName);
                    return new OfficeBrandingDto
                    {
                        OfficeId = office.Id,
                        OfficeName = office.Name ?? string.Empty,
                        DisplayName = branding?.DisplayName,
                        HasLogo = hasLogo,
                        LogoUrl = hasLogo ? BuildOfficeLogoUrl(office.Id, branding!) : null,
                    };
                })
                .OrderBy(d => d.OfficeName)
                .ToList();

            return new ListResultDto<OfficeBrandingDto>(dtos);
        }
    }

    [Authorize(CaseEvaluationPermissions.Branding.Default)]
    public virtual async Task<DownloadResult> DownloadLogoForOfficeAsync(Guid officeId)
    {
        return await ReadLogoAsync(officeId);
    }

    [Authorize(CaseEvaluationPermissions.Branding.Edit)]
    public virtual async Task SetDisplayNameAsync(Guid? officeId, string? displayName)
    {
        var target = ResolveTargetOffice(officeId);
        using (CurrentTenant.Change(null))
        {
            var branding = await GetOrCreateAsync(target);
            branding.SetDisplayName(displayName);
            await _brandingRepository.UpdateAsync(branding, autoSave: true);
        }
    }

    [Authorize(CaseEvaluationPermissions.Branding.Edit)]
    public virtual async Task<BrandingDto> UploadLogoAsync(
        Guid? officeId,
        string fileName,
        string? contentType,
        long fileSize,
        Stream content)
    {
        EnsureValidUpload(fileName, fileSize, content);
        var target = ResolveTargetOffice(officeId);

        var extension = NormalizeExtension(Path.GetExtension(fileName).ToLowerInvariant());
        var blobName = $"{target:N}{extension}";
        var resolvedContentType = string.IsNullOrWhiteSpace(contentType)
            ? GuessContentType(extension)
            : contentType;

        using (CurrentTenant.Change(null))
        {
            if (content.CanSeek)
            {
                content.Seek(0, SeekOrigin.Begin);
            }
            await _logoContainer.SaveAsync(blobName, content, overrideExisting: true);

            var branding = await GetOrCreateAsync(target);
            branding.SetLogo(blobName, resolvedContentType);
            await _brandingRepository.UpdateAsync(branding, autoSave: true);
            return ToDto(branding, target, officeQualifiedUrl: false);
        }
    }

    [Authorize(CaseEvaluationPermissions.Branding.Edit)]
    public virtual async Task RemoveLogoAsync(Guid? officeId)
    {
        var target = ResolveTargetOffice(officeId);
        using (CurrentTenant.Change(null))
        {
            var branding = await _brandingRepository.FirstOrDefaultAsync(x => x.OfficeId == target);
            if (branding == null || string.IsNullOrWhiteSpace(branding.LogoBlobName))
            {
                return;
            }

            var blobName = branding.LogoBlobName;
            branding.ClearLogo();
            await _brandingRepository.UpdateAsync(branding, autoSave: true);

            try
            {
                await _logoContainer.DeleteAsync(blobName);
            }
            catch
            {
                // The branding row is the source of truth; an orphaned blob is a cleanup-job concern.
            }
        }
    }

    private async Task<DownloadResult> ReadLogoAsync(Guid officeId)
    {
        using (CurrentTenant.Change(null))
        {
            var branding = await _brandingRepository.FirstOrDefaultAsync(x => x.OfficeId == officeId);
            if (branding == null || string.IsNullOrWhiteSpace(branding.LogoBlobName))
            {
                throw new UserFriendlyException(L["Branding:LogoNotFound"]);
            }

            var stream = await _logoContainer.GetAsync(branding.LogoBlobName);
            return new DownloadResult
            {
                Content = stream,
                FileName = $"office-logo{Path.GetExtension(branding.LogoBlobName)}",
                ContentType = branding.LogoContentType ?? "image/png",
            };
        }
    }

    /// <summary>Finds the office's branding row or creates+inserts a blank one (host scope).</summary>
    private async Task<OfficeBranding> GetOrCreateAsync(Guid officeId)
    {
        var existing = await _brandingRepository.FirstOrDefaultAsync(x => x.OfficeId == officeId);
        if (existing != null)
        {
            return existing;
        }
        return await _brandingRepository.InsertAsync(new OfficeBranding(GuidGenerator.Create(), officeId), autoSave: true);
    }

    /// <summary>Host-central edits target an explicit office id; in-office edits target the current office.</summary>
    private Guid ResolveTargetOffice(Guid? officeId)
    {
        if (officeId.HasValue && officeId.Value != Guid.Empty)
        {
            return officeId.Value;
        }
        var current = CurrentTenant.Id;
        if (current == null)
        {
            throw new UserFriendlyException(L["Branding:NoTargetOffice"]);
        }
        return current.Value;
    }

    private static BrandingDto ToDto(OfficeBranding? branding, Guid officeId, bool officeQualifiedUrl)
    {
        var hasLogo = branding != null && !string.IsNullOrWhiteSpace(branding.LogoBlobName);
        return new BrandingDto
        {
            DisplayName = branding?.DisplayName,
            HasLogo = hasLogo,
            LogoUrl = hasLogo
                ? (officeQualifiedUrl ? BuildOfficeLogoUrl(officeId, branding!) : BuildLogoUrl(branding!))
                : null,
        };
    }

    // The SPA / login resolve the office by subdomain, so the public serve path needs
    // no office id; the cache-buster forces a refresh when the logo changes.
    private static string BuildLogoUrl(OfficeBranding branding) =>
        $"api/app/branding/logo?v={CacheBuster(branding)}";

    // The host-central manager runs at admin.localhost (no subdomain office), so its
    // preview qualifies the office id explicitly.
    private static string BuildOfficeLogoUrl(Guid officeId, OfficeBranding branding) =>
        $"api/app/branding/offices/{officeId:D}/logo?v={CacheBuster(branding)}";

    private static long CacheBuster(OfficeBranding branding) =>
        (branding.LastModificationTime ?? branding.CreationTime).Ticks;

    private void EnsureValidUpload(string fileName, long fileSize, Stream content)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new UserFriendlyException(L["Branding:FileNameRequired"]);
        }
        if (content == null || fileSize <= 0)
        {
            throw new UserFriendlyException(L["Branding:FileEmpty"]);
        }
        if (fileSize > MaxFileSizeBytes)
        {
            throw new UserFriendlyException(L["Branding:LogoTooLarge"]);
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            throw new UserFriendlyException(L["Branding:UnsupportedFormat"]);
        }

        EnsureValidImageMagicBytes(content);
    }

    // Mirrors UserSignatureAppService.EnsureValidImageMagicBytes: the browser-supplied
    // filename + content type are spoofable, so confirm a PNG/JPEG header in the bytes.
    private void EnsureValidImageMagicBytes(Stream stream)
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
            throw new UserFriendlyException(L["Branding:FileEmpty"]);
        }

        var isPng = magic[0] == 0x89 && magic[1] == 0x50 && magic[2] == 0x4E && magic[3] == 0x47;
        var isJpeg = magic[0] == 0xFF && magic[1] == 0xD8 && magic[2] == 0xFF;

        if (!isPng && !isJpeg)
        {
            throw new UserFriendlyException(L["Branding:UnsupportedFormat"]);
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
}
