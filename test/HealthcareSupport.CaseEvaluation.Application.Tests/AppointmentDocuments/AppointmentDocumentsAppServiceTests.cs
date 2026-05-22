using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// BUG-025 (2026-05-21) -- DI-resolved integration tests for the two
/// public upload entry points where the size gate fires before any
/// DB lookup (UploadStreamAsync, UploadJointDeclarationAsync). These
/// complement the pure-helper tests in
/// <see cref="AppointmentDocumentSizeLimitTests"/> by exercising the
/// helper through real DI -- proves the helper IS wired at each call
/// site with the right argument source and that the localizer is
/// injected.
///
/// <para>The third helper call site lives in the private
/// <c>OverwriteUploadedFileAsync</c>, reached via
/// <c>UploadPackageDocumentAsync</c> and
/// <c>UploadByVerificationCodeAsync</c>. Both require a seeded
/// <c>AppointmentDocument</c> + <c>Appointment</c> before the size
/// gate is reached; not covered here. The helper itself is unit-tested
/// in the sibling file.</para>
/// </summary>
public abstract class AppointmentDocumentsAppServiceTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IAppointmentDocumentsAppService _appService;

    protected AppointmentDocumentsAppServiceTests()
    {
        _appService = GetRequiredService<IAppointmentDocumentsAppService>();
    }

    // ------------------------------------------------------------------
    // UploadStreamAsync -- size gate at line 171 fires BEFORE
    // _readAccessGuard.EnsureCanReadAsync, so no Appointment seeding
    // required.
    // ------------------------------------------------------------------

    [Fact]
    public async Task UploadStreamAsync_FileSizeOverLimit_ThrowsFileTooLarge()
    {
        const long elevenMb = 11L * 1024 * 1024;
        using var content = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF

        var ex = await Should.ThrowAsync<UserFriendlyException>(() =>
            _appService.UploadStreamAsync(
                appointmentId: Guid.NewGuid(),
                documentName: "BUG-025 over-limit",
                fileName: "over-limit.pdf",
                contentType: "application/pdf",
                fileSize: elevenMb,
                content: content));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileTooLarge);
        ex.Data["MaxBytes"].ShouldBe(AppointmentDocumentsAppService.MaxFileSizeBytes);
        ex.Data["ActualBytes"].ShouldBe(elevenMb);
    }

    [Fact]
    public async Task UploadStreamAsync_FileSizeZero_ThrowsFileEmpty()
    {
        using var content = new MemoryStream();

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.UploadStreamAsync(
                appointmentId: Guid.NewGuid(),
                documentName: "BUG-025 empty",
                fileName: "empty.pdf",
                contentType: "application/pdf",
                fileSize: 0,
                content: content));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty);
    }

    // ------------------------------------------------------------------
    // UploadJointDeclarationAsync -- size gate at line 309 fires
    // BEFORE the appointment repository lookup, so a fresh GUID for
    // appointmentId is fine.
    // ------------------------------------------------------------------

    [Fact]
    public async Task UploadJointDeclarationAsync_FileSizeOverLimit_ThrowsFileTooLarge()
    {
        const long elevenMb = 11L * 1024 * 1024;
        using var content = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        var ex = await Should.ThrowAsync<UserFriendlyException>(() =>
            _appService.UploadJointDeclarationAsync(
                appointmentId: Guid.NewGuid(),
                documentName: "BUG-025 JDF over-limit",
                fileName: "jdf-over.pdf",
                contentType: "application/pdf",
                fileSize: elevenMb,
                content: content));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileTooLarge);
        ex.Data["MaxBytes"].ShouldBe(AppointmentDocumentsAppService.MaxFileSizeBytes);
        ex.Data["ActualBytes"].ShouldBe(elevenMb);
    }

    [Fact]
    public async Task UploadJointDeclarationAsync_FileSizeZero_ThrowsFileEmpty()
    {
        using var content = new MemoryStream();

        var ex = await Should.ThrowAsync<BusinessException>(() =>
            _appService.UploadJointDeclarationAsync(
                appointmentId: Guid.NewGuid(),
                documentName: "BUG-025 JDF empty",
                fileName: "jdf-empty.pdf",
                contentType: "application/pdf",
                fileSize: 0,
                content: content));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty);
    }
}
