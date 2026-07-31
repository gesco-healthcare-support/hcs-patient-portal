using System;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Branding;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Pages.Shared.Components.BrandingHead;

/// <summary>
/// Injects the auth pages' logo (the LeptonX login sources it from the
/// <c>--lpx-logo</c> CSS variable -- E4 spike) as a Head.Last override. Resolution:
/// <list type="bullet">
///   <item>Host (no tenant, admin.localhost): the "Evaluators" parent crest (F3).</item>
///   <item>Office WITH an uploaded logo: that logo, inlined as a data URI.</item>
///   <item>Office WITHOUT a logo: an artistic SVG wordmark of the office display
///         name (F1) -- so the login shows the office identity instead of the
///         generic default mark.</item>
/// </list>
/// The branding row + logo blob live host-side, so the office read runs at host
/// scope (an impersonated office context would otherwise route to the office DB).
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
        if (officeId == null)
        {
            // Host scope: the Evaluators parent crest (static AuthServer asset).
            model.LogoCss = "/images/brand/evaluators-logo.png";
            return View(model);
        }

        var officeName = _currentTenant.Name;
        using (_currentTenant.Change(null))
        {
            var branding = await _brandingRepository.FirstOrDefaultAsync(x => x.OfficeId == officeId.Value);
            if (branding != null)
            {
                if (!string.IsNullOrWhiteSpace(branding.DisplayName))
                {
                    officeName = branding.DisplayName;
                }
                if (!string.IsNullOrWhiteSpace(branding.LogoBlobName))
                {
                    var bytes = await _logoContainer.GetAllBytesOrNullAsync(branding.LogoBlobName);
                    if (bytes != null && bytes.Length > 0)
                    {
                        var contentType = string.IsNullOrWhiteSpace(branding.LogoContentType)
                            ? "image/png"
                            : branding.LogoContentType;
                        model.LogoCss = $"data:{contentType};base64,{Convert.ToBase64String(bytes)}";
                        return View(model);
                    }
                }
            }
        }

        // Office without an uploaded logo: an artistic wordmark of its name.
        model.LogoCss = BuildWordmarkDataUri(
            string.IsNullOrWhiteSpace(officeName) ? "Appointment Portal" : officeName!);
        return View(model);
    }

    /// <summary>
    /// A self-contained SVG wordmark (navy serif, centered) of the office name,
    /// base64-encoded as a data URI so it drops straight into the <c>--lpx-logo</c>
    /// CSS variable with no escaping hazards.
    /// </summary>
    private static string BuildWordmarkDataUri(string name)
    {
        var safe = SecurityElement.Escape(name) ?? name;
        var svg =
            "<svg xmlns='http://www.w3.org/2000/svg' width='360' height='60' viewBox='0 0 360 60'>" +
            "<text x='180' y='40' text-anchor='middle' font-family='Georgia, \"Times New Roman\", serif' " +
            $"font-size='28' font-weight='600' fill='#1f3a5f'>{safe}</text></svg>";
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        return $"data:image/svg+xml;base64,{b64}";
    }
}

/// <summary>View model for the auth-page branding head hook; LogoCss null -> render nothing.</summary>
public class BrandingHeadViewModel
{
    /// <summary>The value placed inside <c>--lpx-logo: url(...)</c> -- a static path, a
    /// logo data URI, or an SVG-wordmark data URI. Null leaves the global default.</summary>
    public string? LogoCss { get; set; }
}
