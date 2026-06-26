using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Phase E (2026-06-25) -- per-office branding app service. Covers the host-side
/// resolution + isolation (the security-relevant paths) and the upload validation.
/// The logo BLOB happy-path is not exercised here: the test harness binds the real
/// (MinIO) blob container, so a valid SaveAsync cannot run; the validation cases all
/// throw before any blob write, and the rendered logo is checked in the live gate.
/// Branding lives host-side (not IMultiTenant), so the shared-SQLite rig is fine.
/// </summary>
public abstract class BrandingAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IBrandingAppService _branding;
    private readonly ICurrentTenant _currentTenant;

    protected BrandingAppServiceTests()
    {
        _branding = GetRequiredService<IBrandingAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task GetBranding_HostScope_ReturnsDefaults()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var dto = await _branding.GetBrandingAsync();
                dto.DisplayName.ShouldBeNull();
                dto.HasLogo.ShouldBeFalse();
                dto.LogoUrl.ShouldBeNull();
            }
        });
    }

    [Fact]
    public async Task SetDisplayName_PersistsForTargetOffice_AndResolvesBySubdomain()
    {
        await _branding.SetDisplayNameAsync(TenantsTestData.TenantARef, "Office A Brand");

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var dto = await _branding.GetBrandingAsync();
                dto.DisplayName.ShouldBe("Office A Brand");
            }
        });
    }

    [Fact]
    public async Task Branding_IsIsolatedPerOffice()
    {
        await _branding.SetDisplayNameAsync(TenantsTestData.TenantARef, "Alpha Office");
        await _branding.SetDisplayNameAsync(TenantsTestData.TenantBRef, "Beta Office");

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                (await _branding.GetBrandingAsync()).DisplayName.ShouldBe("Alpha Office");
            }
            using (_currentTenant.Change(TenantsTestData.TenantBRef))
            {
                (await _branding.GetBrandingAsync()).DisplayName.ShouldBe("Beta Office");
            }
        });
    }

    [Fact]
    public async Task GetOfficeBrandings_ListsTargetOfficeWithName()
    {
        await _branding.SetDisplayNameAsync(TenantsTestData.TenantARef, "Listed A");

        var list = await _branding.GetOfficeBrandingsAsync();
        list.Items.ShouldContain(o =>
            o.OfficeId == TenantsTestData.TenantARef && o.DisplayName == "Listed A");
    }

    [Fact]
    public async Task UploadLogo_RejectsUnsupportedExtension()
    {
        using var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 });
        await Should.ThrowAsync<UserFriendlyException>(() =>
            _branding.UploadLogoAsync(TenantsTestData.TenantARef, "logo.txt", "text/plain", 4, stream));
    }

    [Fact]
    public async Task UploadLogo_RejectsOversizeFile()
    {
        using var stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        await Should.ThrowAsync<UserFriendlyException>(() =>
            _branding.UploadLogoAsync(
                TenantsTestData.TenantARef, "logo.png", "image/png", 2L * 1024 * 1024, stream));
    }

    [Fact]
    public async Task UploadLogo_RejectsContentThatIsNotAnImage()
    {
        var bytes = Encoding.ASCII.GetBytes("not-an-image-at-all");
        using var stream = new MemoryStream(bytes);
        await Should.ThrowAsync<UserFriendlyException>(() =>
            _branding.UploadLogoAsync(
                TenantsTestData.TenantARef, "logo.png", "image/png", bytes.Length, stream));
    }
}
