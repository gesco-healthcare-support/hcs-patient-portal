using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Branding;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Pages.Shared.Components.BrandingHead;

/// <summary>
/// Phase E (2026-06-25): injects the current office's logo into the AuthServer auth
/// pages' &lt;head&gt; as a CSS-variable override. The E4 spike proved the LeptonX
/// login layout sources its logo from <c>--lpx-logo</c> (not from
/// <c>BrandingProvider.LogoUrl</c>), so this redefines <c>--lpx-logo</c> for the
/// subdomain-resolved office. Emitted as a self-contained data URI (read from the
/// host-side office-logos blob) so the login page needs no cross-origin API call.
/// No tenant / no logo -> renders nothing, leaving the static <c>global-styles.css</c>
/// default in place. Wired via <c>AbpLayoutHookOptions</c> (Head.Last) so it cascades
/// after that bundle.
/// </summary>
public class BrandingHeadViewComponent : ViewComponent
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<OfficeBranding, Guid> _brandingRepository;
    private readonly IBlobContainer<OfficeLogosContainer> _logoContainer;

    public BrandingHeadViewComponent(
        ICurrentTenant currentTenant,
        IRepository<OfficeBranding, Guid> brandingRepository,
        IBlobContainer<OfficeLogosContainer> logoContainer)
    {
        _currentTenant = currentTenant;
        _brandingRepository = brandingRepository;
        _logoContainer = logoContainer;
    }

    public virtual async Task<IViewComponentResult> InvokeAsync()
    {
        var model = new BrandingHeadViewModel();

        var officeId = _currentTenant.Id;
        if (officeId != null)
        {
            // The branding row + logo blob live host-side; read at host scope so an
            // impersonated office context does not route to the office DB.
            using (_currentTenant.Change(null))
            {
                var branding = await _brandingRepository.FirstOrDefaultAsync(x => x.OfficeId == officeId.Value);
                if (branding != null && !string.IsNullOrWhiteSpace(branding.LogoBlobName))
                {
                    var bytes = await _logoContainer.GetAllBytesOrNullAsync(branding.LogoBlobName);
                    if (bytes != null && bytes.Length > 0)
                    {
                        var contentType = string.IsNullOrWhiteSpace(branding.LogoContentType)
                            ? "image/png"
                            : branding.LogoContentType;
                        model.LogoDataUri = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
                    }
                }
            }
        }

        return View(model);
    }
}

/// <summary>View model for the auth-page branding head hook; LogoDataUri null -> render nothing.</summary>
public class BrandingHeadViewModel
{
    public string? LogoDataUri { get; set; }
}
