using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// BUG-025 (2026-05-21) -- pure unit tests for
/// <see cref="AppointmentDocumentsAppService.EnsureFileSizeWithinLimit"/>.
/// The helper is shared by all four upload entry points
/// (UploadStreamAsync, UploadJointDeclarationAsync,
/// UploadPackageDocumentAsync, UploadByVerificationCodeAsync) via the
/// private OverwriteUploadedFileAsync. Testing the helper directly
/// gates the size rule without any DI / auth / seeding ceremony.
///
/// Wiring at each call site is exercised by the HTTP-level smoke
/// run documented in BUG-025-no-document-upload-size-limit.md.
/// </summary>
public class AppointmentDocumentSizeLimitTests
{
    private const long Max = AppointmentDocumentsAppService.MaxFileSizeBytes;

    [Fact]
    public void EnsureFileSizeWithinLimit_ExactlyAtLimit_DoesNotThrow()
    {
        Should.NotThrow(() =>
            AppointmentDocumentsAppService.EnsureFileSizeWithinLimit(Max));
    }

    [Fact]
    public void EnsureFileSizeWithinLimit_OneByteUnderLimit_DoesNotThrow()
    {
        Should.NotThrow(() =>
            AppointmentDocumentsAppService.EnsureFileSizeWithinLimit(Max - 1));
    }

    [Fact]
    public void EnsureFileSizeWithinLimit_OneByteOverLimit_Throws()
    {
        var ex = Should.Throw<BusinessException>(() =>
            AppointmentDocumentsAppService.EnsureFileSizeWithinLimit(Max + 1));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileTooLarge);
        ex.Data["MaxBytes"].ShouldBe(Max);
        ex.Data["ActualBytes"].ShouldBe(Max + 1);
    }

    [Fact]
    public void EnsureFileSizeWithinLimit_ElevenMegabytes_Throws()
    {
        const long elevenMb = 11L * 1024 * 1024;

        var ex = Should.Throw<BusinessException>(() =>
            AppointmentDocumentsAppService.EnsureFileSizeWithinLimit(elevenMb));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileTooLarge);
        ex.Data["MaxBytes"].ShouldBe(Max);
        ex.Data["ActualBytes"].ShouldBe(elevenMb);
    }

    [Fact]
    public void EnsureFileSizeWithinLimit_FiftyMegabytes_Throws()
    {
        const long fiftyMb = 50L * 1024 * 1024;

        var ex = Should.Throw<BusinessException>(() =>
            AppointmentDocumentsAppService.EnsureFileSizeWithinLimit(fiftyMb));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileTooLarge);
        ex.Data["ActualBytes"].ShouldBe(fiftyMb);
    }

    [Fact]
    public void EnsureFileSizeWithinLimit_ZeroBytes_DoesNotThrow()
    {
        // Zero / negative is handled earlier in each entry point via the
        // explicit empty-file check (BusinessException with code
        // AppointmentDocumentFileEmpty); the size helper only enforces
        // the upper bound. Asserting this guards against an accidental
        // change that would have the size helper also reject empties --
        // doing so would mask the localized "file is empty" message.
        Should.NotThrow(() =>
            AppointmentDocumentsAppService.EnsureFileSizeWithinLimit(0));
    }

    [Fact]
    public void MaxFileSizeBytes_IsExactlyTenMebibytes()
    {
        // Guards against an accidental change to the constant. If the
        // policy actually changes (e.g. to 5 MB or 20 MB), update this
        // test deliberately alongside the constant.
        AppointmentDocumentsAppService.MaxFileSizeBytes.ShouldBe(10L * 1024 * 1024);
        AppointmentDocumentsAppService.MaxFileSizeBytes.ShouldBe(10_485_760L);
    }
}
