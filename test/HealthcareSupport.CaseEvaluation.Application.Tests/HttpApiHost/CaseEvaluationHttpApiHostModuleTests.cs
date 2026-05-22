using System.Net;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.Modularity;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.HttpApiHost.Tests;

/// <summary>
/// BUG-025 (2026-05-21) -- unit tests for the two internal static
/// helpers extracted from <see cref="CaseEvaluationHttpApiHostModule"/>:
/// <see cref="CaseEvaluationHttpApiHostModule.MapAppointmentDocumentErrorCodes"/>
/// and <see cref="CaseEvaluationHttpApiHostModule.ConfigureUploadLimits"/>.
///
/// <para>The helpers are reached via <c>InternalsVisibleTo</c>
/// (declared in <c>src/.../HttpApi.Host/AssemblyInfo.cs</c>) so the
/// tests can drive them without booting the full ABP host. They
/// assert the configured-state of the options, not end-to-end HTTP
/// behaviour -- the integration-level verification lives in the
/// HTTP smoke tests recorded in BUG-025-no-document-upload-size-limit.md.</para>
/// </summary>
public class CaseEvaluationHttpApiHostModuleTests
{
    // ------------------------------------------------------------------
    // MapAppointmentDocumentErrorCodes
    // ------------------------------------------------------------------

    [Fact]
    public void MapAppointmentDocumentErrorCodes_MapsFileTooLargeTo413()
    {
        var options = new AbpExceptionHttpStatusCodeOptions();

        CaseEvaluationHttpApiHostModule.MapAppointmentDocumentErrorCodes(options);

        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.AppointmentDocumentFileTooLarge
        ].ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public void MapAppointmentDocumentErrorCodes_MapsFileEmptyTo400()
    {
        var options = new AbpExceptionHttpStatusCodeOptions();

        CaseEvaluationHttpApiHostModule.MapAppointmentDocumentErrorCodes(options);

        options.ErrorCodeToHttpStatusCodeMappings[
            CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty
        ].ShouldBe(HttpStatusCode.BadRequest);
    }

    // ------------------------------------------------------------------
    // ConfigureUploadLimits -- defense-in-depth framework caps. The
    // AppService cap is 10 MB; the framework cap is 12 MB so the
    // friendly localized 413 from the AppService can fire before the
    // raw framework 413 (which has no localized message).
    // ------------------------------------------------------------------

    private const long ExpectedFrameworkCapBytes = 12L * 1024 * 1024;

    [Fact]
    public void ConfigureUploadLimits_SetsKestrelMaxRequestBodySizeTo12MB()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        var context = new ServiceConfigurationContext(services);

        CaseEvaluationHttpApiHostModule.ConfigureUploadLimits(context);

        var kestrel = services.BuildServiceProvider()
            .GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        kestrel.Limits.MaxRequestBodySize.ShouldBe(ExpectedFrameworkCapBytes);
    }

    [Fact]
    public void ConfigureUploadLimits_SetsFormOptionsMultipartBodyLengthLimitTo12MB()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        var context = new ServiceConfigurationContext(services);

        CaseEvaluationHttpApiHostModule.ConfigureUploadLimits(context);

        var form = services.BuildServiceProvider()
            .GetRequiredService<IOptions<FormOptions>>().Value;
        form.MultipartBodyLengthLimit.ShouldBe(ExpectedFrameworkCapBytes);
    }

    [Fact]
    public void ConfigureUploadLimits_FrameworkCapIsAtLeastTwoMegabytesAboveAppServiceCap()
    {
        // BUG-025 (2026-05-21) -- the 2 MB buffer between the
        // AppService cap (10 MB) and the framework cap is deliberate:
        // it lets multipart-boundary headers + small overhead pass
        // the framework's raw 413 so the AppService's localized
        // BusinessException fires first for files between 10 and 12 MB.
        // Guard against accidental tightening of either cap.
        var services = new ServiceCollection();
        services.AddOptions();
        var context = new ServiceConfigurationContext(services);

        CaseEvaluationHttpApiHostModule.ConfigureUploadLimits(context);

        var kestrel = services.BuildServiceProvider()
            .GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        var actualCap = kestrel.Limits.MaxRequestBodySize ?? 0;
        var appServiceCap = AppointmentDocumentsAppService.MaxFileSizeBytes;
        var buffer = actualCap - appServiceCap;
        buffer.ShouldBeGreaterThanOrEqualTo(2L * 1024 * 1024,
            "framework cap should be >= 2 MB above the AppService cap so the localized 413 wins");
    }
}
