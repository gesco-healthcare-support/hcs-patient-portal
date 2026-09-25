using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.UserProfile;

/// <summary>
/// The storage half of <see cref="UserSignatureAppService"/>: upload, download and the
/// bytes-by-user read that the packet generator uses.
/// </summary>
/// <remarks>
/// <para>
/// <c>EfCoreUserSignatureAppServiceTests</c> records that these paths were out of reach, because
/// the rig binds the real MinIO provider against a host that does not resolve. This class replaces
/// the signature blob container for THIS class only, in <c>AfterAddApplication</c>. Each storage
/// call can then be asserted, or made to return a known stream.
/// </para>
/// <para>
/// Every fixture user is host-scoped and created per test, for the reason the existing class
/// gives: the rig's ambient tenant is the host, so a tenant user would be invisible to the
/// user manager. All names and emails are synthetic.
/// </para>
/// </remarks>
public abstract class UserSignatureStorageTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const string FileNameProperty = "UserSignatureFileName";
    private const string ContentTypeProperty = "UserSignatureContentType";

    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01 };
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };

    private IBlobContainer<UserSignaturesContainer> _blobs = null!;

    private readonly IUserSignatureAppService _signatures;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _principal;

    protected UserSignatureStorageTests()
    {
        _signatures = GetRequiredService<IUserSignatureAppService>();
        _userManager = GetRequiredService<IdentityUserManager>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _blobs = Substitute.For<IBlobContainer<UserSignaturesContainer>>();
        services.Replace(ServiceDescriptor.Singleton(typeof(IBlobContainer<UserSignaturesContainer>), _blobs));
    }

    // ------------------------------------------------------------------ harness

    private async Task<Guid> CreateUserAsync(Action<IdentityUser>? shape = null)
    {
        var token = Guid.NewGuid().ToString("N")[..8];
        var user = new IdentityUser(Guid.NewGuid(), $"sig-{token}", $"sig-{token}@example.test", tenantId: null);
        shape?.Invoke(user);
        await WithUnitOfWorkAsync(async () => (await _userManager.CreateAsync(user)).Succeeded.ShouldBeTrue());
        return user.Id;
    }

    private Task<IdentityUser> ReloadAsync(Guid userId) =>
        WithUnitOfWorkAsync(() => _userManager.GetByIdAsync(userId));

    private async Task<T> AsUser<T>(Guid userId, Func<Task<T>> call)
    {
        using (WithCurrentUser.Run(_principal, userId))
        {
            return await WithUnitOfWorkAsync(call);
        }
    }

    private Task<UserSignatureInfoDto> UploadAsync(Guid userId, string fileName, byte[] bytes, string? contentType = "image/png", long? size = null) =>
        AsUser(userId, () => _signatures.UploadAsync(fileName, contentType, size ?? bytes.Length, new MemoryStream(bytes)));

    private string[] SavedBlobNames() =>
        _blobs.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IBlobContainer.SaveAsync))
            .Select(c => (string)c.GetArguments()[0]!)
            .ToArray();

    // ------------------------------------------------------------------ upload

    [Fact]
    public async Task An_upload_is_refused_before_anything_is_stored()
    {
        var userId = await CreateUserAsync();

        await Should.ThrowAsync<UserFriendlyException>(() => UploadAsync(userId, "   ", PngBytes));
        await Should.ThrowAsync<UserFriendlyException>(() => UploadAsync(userId, "sig.png", PngBytes, size: 0));
        await Should.ThrowAsync<UserFriendlyException>(() =>
            UploadAsync(userId, "sig.png", PngBytes, size: UserSignatureAppService.MaxFileSizeBytes + 1));
        await Should.ThrowAsync<UserFriendlyException>(() => UploadAsync(userId, "sig.gif", PngBytes));
        (await Should.ThrowAsync<UserFriendlyException>(() => UploadAsync(userId, "sig.png", "GIF89a-x"u8.ToArray())))
            .Message.ShouldBe("UserSignature:UnsupportedFormat");
        (await Should.ThrowAsync<UserFriendlyException>(() => UploadAsync(userId, "sig.png", new byte[] { 0x89, 0x50, 0x4E })))
            .Message.ShouldBe("UserSignature:FileEmpty");

        SavedBlobNames().ShouldBeEmpty();
        (await ReloadAsync(userId)).GetProperty<string>(CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName)
            .ShouldBeNull();
    }

    [Fact]
    public async Task A_png_upload_is_stored_under_the_users_id_and_recorded_on_the_user()
    {
        var userId = await CreateUserAsync();

        var info = await UploadAsync(userId, "my-signature.png", PngBytes, contentType: "image/png");

        info.HasSignature.ShouldBeTrue();
        info.FileName.ShouldBe("my-signature.png");
        info.ContentType.ShouldBe("image/png");
        SavedBlobNames().ShouldBe(new[] { $"{userId:N}.png" });

        var stored = await ReloadAsync(userId);
        stored.GetProperty<string>(CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName)
            .ShouldBe($"{userId:N}.png");
        stored.GetProperty<string>(FileNameProperty).ShouldBe("my-signature.png");
    }

    [Fact]
    public async Task A_jpeg_upload_with_no_content_type_is_named_jpg_and_typed_as_jpeg()
    {
        var userId = await CreateUserAsync();

        var info = await UploadAsync(userId, "scan.JPEG", JpegBytes, contentType: null);

        info.ContentType.ShouldBe("image/jpeg");
        SavedBlobNames().ShouldBe(new[] { $"{userId:N}.jpg" });
    }

    [Fact]
    public async Task Uploading_again_replaces_the_stored_signature()
    {
        var userId = await CreateUserAsync();
        await UploadAsync(userId, "first.png", PngBytes);

        var info = await UploadAsync(userId, "second.jpg", JpegBytes, contentType: "image/jpeg");

        info.FileName.ShouldBe("second.jpg");
        SavedBlobNames().ShouldBe(new[] { $"{userId:N}.png", $"{userId:N}.jpg" });
        await _blobs.Received(2).SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), true, Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------ download and delete

    [Fact]
    public async Task A_download_returns_the_stored_stream_with_its_name_and_type()
    {
        var userId = await CreateUserAsync();
        await UploadAsync(userId, "my-signature.png", PngBytes, contentType: "image/png");
        var stream = new MemoryStream(PngBytes);
        _blobs.GetAsync($"{userId:N}.png", Arg.Any<CancellationToken>()).Returns(stream);

        var result = await AsUser(userId, () => _signatures.DownloadAsync());

        result.Content.ShouldBeSameAs(stream);
        result.FileName.ShouldBe("my-signature.png");
        result.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task A_download_with_no_recorded_name_or_type_falls_back_to_a_png()
    {
        var userId = await CreateUserAsync(u =>
            u.SetProperty(CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName, "legacy-blob.png"));
        _blobs.GetAsync("legacy-blob.png", Arg.Any<CancellationToken>()).Returns(new MemoryStream(PngBytes));

        var result = await AsUser(userId, () => _signatures.DownloadAsync());

        result.FileName.ShouldBe("signature.png");
        result.ContentType.ShouldBe("image/png");
    }

    [Fact]
    public async Task Deleting_removes_the_stored_file_and_clears_the_record()
    {
        var userId = await CreateUserAsync();
        await UploadAsync(userId, "my-signature.png", PngBytes);

        await AsUser(userId, async () =>
        {
            await _signatures.DeleteAsync();
            return true;
        });

        await _blobs.Received(1).DeleteAsync($"{userId:N}.png", Arg.Any<CancellationToken>());
        var stored = await ReloadAsync(userId);
        stored.GetProperty<string>(CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName).ShouldBeNull();
        stored.GetProperty<string>(FileNameProperty).ShouldBeNull();
        stored.GetProperty<string>(ContentTypeProperty).ShouldBeNull();
    }

    // ------------------------------------------------------------------ bytes for the packet generator

    [Fact]
    public async Task The_packet_generator_reads_the_stored_bytes_by_user_id()
    {
        var userId = await CreateUserAsync(u =>
            u.SetProperty(CaseEvaluationModuleExtensionConfigurator.UserSignatureBlobNamePropertyName, "stored.png"));
        _blobs.GetOrNullAsync("stored.png", Arg.Any<CancellationToken>()).Returns(new MemoryStream(PngBytes));

        var bytes = await WithUnitOfWorkAsync(() => _signatures.GetBytesByUserIdAsync(userId));

        bytes.ShouldBe(PngBytes);
    }
}
