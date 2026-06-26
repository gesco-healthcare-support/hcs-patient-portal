using System;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Ui.Branding;

namespace HealthcareSupport.CaseEvaluation;

/// <summary>
/// Phase E (2026-06-25): per-office branding for the AuthServer (login + account
/// pages). Returns the current office's display name (<see cref="OfficeBranding"/>,
/// stored host-side, resolved by the subdomain tenant) so the login page brand text
/// + browser title reflect the office; falls back to the SaaS tenant name, then the
/// localized Gesco default at host scope.
///
/// <para>The office LOGO on the login page is injected separately as a CSS variable
/// (the LeptonX login layout sources the logo from <c>--lpx-logo</c>, NOT from
/// <c>BrandingProvider.LogoUrl</c> -- confirmed by the E4 spike), so this provider
/// intentionally does not override LogoUrl. The display-name read is memoized for the
/// provider instance to avoid repeated host-DB hits during a single page render; the
/// read runs at host scope because the branding row lives in the host DB.</para>
/// </summary>
[Dependency(ReplaceServices = true)]
public class CaseEvaluationBrandingProvider : DefaultBrandingProvider
{
    private readonly IStringLocalizer<CaseEvaluationResource> _localizer;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<OfficeBranding, Guid> _brandingRepository;

    private bool _resolved;
    private string? _officeDisplayName;

    public CaseEvaluationBrandingProvider(
        IStringLocalizer<CaseEvaluationResource> localizer,
        ICurrentTenant currentTenant,
        IRepository<OfficeBranding, Guid> brandingRepository)
    {
        _localizer = localizer;
        _currentTenant = currentTenant;
        _brandingRepository = brandingRepository;
    }

    public override string AppName
    {
        get
        {
            var officeName = ResolveOfficeDisplayName();
            if (!string.IsNullOrWhiteSpace(officeName))
            {
                return officeName!;
            }
            if (!string.IsNullOrWhiteSpace(_currentTenant.Name))
            {
                return _currentTenant.Name!;
            }
            return _localizer["AppName"];
        }
    }

    private string? ResolveOfficeDisplayName()
    {
        if (_resolved)
        {
            return _officeDisplayName;
        }
        _resolved = true;

        var officeId = _currentTenant.Id;
        if (officeId == null)
        {
            return _officeDisplayName = null;
        }

        // The branding row lives in the HOST DB; read it at host scope so an
        // impersonated office context does not route the query to the office DB.
        _officeDisplayName = AsyncHelper.RunSync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var branding = await _brandingRepository.FirstOrDefaultAsync(x => x.OfficeId == officeId.Value);
                return branding?.DisplayName;
            }
        });
        return _officeDisplayName;
    }
}
