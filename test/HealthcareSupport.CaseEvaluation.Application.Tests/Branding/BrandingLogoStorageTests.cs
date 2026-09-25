using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// The storage half of <see cref="BrandingAppService"/>: uploading, reading and removing an
/// office logo, the display name's first write, and the per-office reads.
/// </summary>
/// <remarks>
/// <para>
/// <c>BrandingAppServiceTests</c> records that the logo store was out of reach, because the rig
/// binds the real MinIO provider against a host that does not resolve. This class replaces the
/// office-logo blob container for THIS class only, in <c>AfterAddApplication</c>, so each storage
/// call is asserted or can be made to fail.
/// </para>
/// <para>Offices A and B are the seeded tenants. All file names and labels are synthetic.</para>
/// </remarks>
public abstract class BrandingLogoStorageTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01 };
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
    // Properties, NOT static readonly fields: TenantARef/TenantBRef are populated when the rig
    // seeds, after this type's static initialiser would already have captured Guid.Empty.
    private static Guid OfficeA => TenantsTestData.TenantARef;
    private static Guid OfficeB => TenantsTestData.TenantBRef;

    private IBlobContainer<OfficeLogosContainer> _logos = null!;

    private readonly IBrandingAppService _branding;
    private readonly ICurrentTenant _currentTenant;

    protected BrandingLogoStorageTests()
    {
        _branding = GetRequiredService<IBrandingAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _logos = Substitute.For<IBlobContainer<OfficeLogosContainer>>();
        services.Replace(ServiceDescriptor.Singleton(typeof(IBlobContainer<OfficeLogosContainer>), _logos));
    }

    // ------------------------------------------------------------------ harness

    private async Task<T> InOffice<T>(Guid? officeId, Func<Task<T>> call)
    {
        using (_currentTenant.Change(officeId))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private Task InOffice(Guid? officeId, Func<Task> call) =>
        InOffice(officeId, async () =>
        {
            await call();
            return true;
        });

    private Task<BrandingDto> UploadAsync(Guid? asOffice, Guid? forOffice, string fileName, byte[] bytes,
        string? contentType = "image/png", long? size = null) =>
        InOffice(asOffice, () => _branding.UploadLogoAsync(forOffice, fileName, contentType, size ?? bytes.Length, new MemoryStream(bytes)));

    private string[] SavedBlobNames() =>
        _logos.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IBlobContainer.SaveAsync))
            .Select(c => (string)c.GetArguments()[0]!)
            .ToArray();

    // ------------------------------------------------------------------ upload

    [Fact]
    public async Task A_logo_upload_is_refused_before_anything_is_stored()
    {
        await Should.ThrowAsync<UserFriendlyException>(() => UploadAsync(OfficeA, null, "   ", PngBytes));
        await Should.ThrowAsync<UserFriendlyException>(() => UploadAsync(OfficeA, null, "logo.png", PngBytes, size: 0));
        await Should.ThrowAsync<UserFriendlyException>(() =>
            UploadAsync(OfficeA, null, "logo.png", PngBytes, size: BrandingAppService.MaxFileSizeBytes + 1));
        await Should.ThrowAsync<UserFriendlyException>(() => UploadAsync(OfficeA, null, "logo.gif", PngBytes));
        await Should.ThrowAsync<UserFriendlyException>(() => UploadAsync(OfficeA, null, "logo.png", "GIF89a-x"u8.ToArray()));
        await Should.ThrowAsync<UserFriendlyException>(() => UploadAsync(OfficeA, null, "logo.png", new byte[] { 0x89, 0x50, 0x4E }));

        SavedBlobNames().ShouldBeEmpty();
        (await InOffice(OfficeA, () => _branding.GetBrandingAsync())).HasLogo.ShouldBeFalse();
    }

    [Fact]
    public async Task A_host_upload_that_names_no_office_is_refused()
    {
        await Should.ThrowAsync<UserFriendlyException>(() => UploadAsync(null, null, "logo.png", PngBytes));

        SavedBlobNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task An_office_upload_stores_the_logo_under_the_office_and_shows_it_on_its_branding()
    {
        var result = await UploadAsync(OfficeA, null, "logo.png", PngBytes);

        result.HasLogo.ShouldBeTrue();
        result.LogoUrl.ShouldStartWith("api/app/branding/logo?v=");
        SavedBlobNames().ShouldBe(new[] { $"{OfficeA:N}.png" });

        var shown = await InOffice(OfficeA, () => _branding.GetBrandingAsync());
        shown.HasLogo.ShouldBeTrue();
        (await InOffice(OfficeB, () => _branding.GetBrandingAsync())).HasLogo.ShouldBeFalse();
    }

    [Fact]
    public async Task A_host_upload_for_a_named_office_with_no_content_type_is_named_jpg_and_typed_jpeg()
    {
        await UploadAsync(null, OfficeB, "logo.JPEG", JpegBytes, contentType: null);
        _logos.GetAsync($"{OfficeB:N}.jpg", Arg.Any<CancellationToken>()).Returns(new MemoryStream(JpegBytes));

        var download = await InOffice(null, () => _branding.DownloadLogoForOfficeAsync(OfficeB));

        SavedBlobNames().ShouldBe(new[] { $"{OfficeB:N}.jpg" });
        download.ShouldNotBeNull();
        download.ContentType.ShouldBe("image/jpeg");
        download.FileName.ShouldBe("office-logo.jpg");
    }

    // ------------------------------------------------------------------ reads

    [Fact]
    public async Task An_office_downloads_its_own_logo_and_the_host_has_none()
    {
        await UploadAsync(OfficeA, null, "logo.png", PngBytes, contentType: "image/png");
        var stream = new MemoryStream(PngBytes);
        _logos.GetAsync($"{OfficeA:N}.png", Arg.Any<CancellationToken>()).Returns(stream);

        var own = await InOffice(OfficeA, () => _branding.DownloadLogoAsync());
        var host = await InOffice(null, () => _branding.DownloadLogoAsync());
        var other = await InOffice(null, () => _branding.DownloadLogoForOfficeAsync(OfficeB));

        own.ShouldNotBeNull();
        own.Content.ShouldBeSameAs(stream);
        own.FileName.ShouldBe("office-logo.png");
        own.ContentType.ShouldBe("image/png");
        host.ShouldBeNull();
        other.ShouldBeNull();
    }

    [Fact]
    public async Task The_office_list_carries_each_offices_logo_link_and_display_name()
    {
        await UploadAsync(OfficeA, null, "logo.png", PngBytes);
        await InOffice(null, () => _branding.SetDisplayNameAsync(OfficeB, "Synthetic Office B"));

        var list = await InOffice(null, () => _branding.GetOfficeBrandingsAsync());

        var a = list.Items.Single(o => o.OfficeId == OfficeA);
        a.HasLogo.ShouldBeTrue();
        a.LogoUrl.ShouldStartWith($"api/app/branding/offices/{OfficeA:D}/logo?v=");
        var b = list.Items.Single(o => o.OfficeId == OfficeB);
        b.HasLogo.ShouldBeFalse();
        b.DisplayName.ShouldBe("Synthetic Office B");
    }

    [Fact]
    public async Task The_paged_office_list_sorts_by_display_name_in_either_direction()
    {
        await InOffice(null, () => _branding.SetDisplayNameAsync(OfficeA, "Synthetic Beta"));
        await InOffice(null, () => _branding.SetDisplayNameAsync(OfficeB, "Synthetic Alpha"));

        var ascending = await InOffice(null, () => _branding.GetPagedOfficeBrandingsAsync(
            new GetOfficeBrandingInput { Filter = "Synthetic", Sorting = "displayname asc", MaxResultCount = 10 }));
        var descending = await InOffice(null, () => _branding.GetPagedOfficeBrandingsAsync(
            new GetOfficeBrandingInput { Filter = "Synthetic", Sorting = "displayname desc", MaxResultCount = 10 }));

        ascending.Items.Select(o => o.DisplayName).ShouldBe(new[] { "Synthetic Alpha", "Synthetic Beta" });
        descending.Items.Select(o => o.DisplayName).ShouldBe(new[] { "Synthetic Beta", "Synthetic Alpha" });
    }

    // ------------------------------------------------------------------ display name and removal

    [Fact]
    public async Task Setting_a_display_name_creates_the_branding_record_on_first_use()
    {
        await InOffice(OfficeA, () => _branding.SetDisplayNameAsync(null, "Synthetic Office A"));

        var shown = await InOffice(OfficeA, () => _branding.GetBrandingAsync());
        shown.DisplayName.ShouldBe("Synthetic Office A");
        shown.HasLogo.ShouldBeFalse();
    }

    [Fact]
    public async Task Removing_a_logo_clears_it_and_deletes_the_file_even_when_the_delete_fails()
    {
        await UploadAsync(OfficeA, null, "logo.png", PngBytes);
        _logos.DeleteAsync($"{OfficeA:N}.png", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Synthetic storage outage"));

        await InOffice(OfficeA, () => _branding.RemoveLogoAsync(null));

        await _logos.Received(1).DeleteAsync($"{OfficeA:N}.png", Arg.Any<CancellationToken>());
        (await InOffice(OfficeA, () => _branding.GetBrandingAsync())).HasLogo.ShouldBeFalse();
    }

    [Fact]
    public async Task Removing_a_logo_that_was_never_uploaded_touches_nothing_not_even_another_offices()
    {
        // LOAD-BEARING DECOY: office A HAS a logo. A removal that ignored the office would take it.
        await UploadAsync(OfficeA, null, "logo.png", PngBytes);
        _logos.ClearReceivedCalls();

        await InOffice(OfficeB, () => _branding.RemoveLogoAsync(null));

        _logos.ReceivedCalls().ShouldBeEmpty();
        (await InOffice(OfficeB, () => _branding.GetBrandingAsync())).HasLogo.ShouldBeFalse();
        (await InOffice(OfficeA, () => _branding.GetBrandingAsync())).HasLogo.ShouldBeTrue();
    }
}
