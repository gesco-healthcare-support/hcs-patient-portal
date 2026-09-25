using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.Controllers.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Controllers.Branding;
using HealthcareSupport.CaseEvaluation.Controllers.DocumentsControllers;
using HealthcareSupport.CaseEvaluation.Controllers.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Controllers.Integration;
using HealthcareSupport.CaseEvaluation.Controllers.PackageDetailsControllers;
using HealthcareSupport.CaseEvaluation.Controllers.Reports;
using HealthcareSupport.CaseEvaluation.Controllers.UserProfile;
using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using HealthcareSupport.CaseEvaluation.Reports;
using HealthcareSupport.CaseEvaluation.Shared;
using HealthcareSupport.CaseEvaluation.UserProfile;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Controllers;

/// <summary>
/// The controller actions that shape something on the way through: they unwrap a request body,
/// turn a download into a file response, map "not found" to a 404, refuse a missing upload, or
/// call a differently named service method.
/// </summary>
/// <remarks>
/// Each is named in <see cref="ControllerDelegationTests"/>'s hand-tested list, which is what
/// keeps this file and that theory from drifting apart. Every case asserts what reached the
/// service and what came back.
/// </remarks>
public class ShapingControllersTests
{
    private static readonly Guid Id = new("6c2e0d1f-0000-4000-9000-000000000001");
    private static readonly Guid OfficeId = new("6c2e0d1f-0000-4000-9000-000000000002");

    private static DownloadResult Download(string name = "synthetic.pdf") => new()
    {
        Content = new MemoryStream(new byte[] { 1, 2, 3 }),
        ContentType = "application/pdf",
        FileName = name,
    };

    private static void ShouldBeTheDownload(IActionResult result, DownloadResult download)
    {
        var file = result.ShouldBeOfType<FileStreamResult>();
        file.FileStream.ShouldBeSameAs(download.Content);
        file.ContentType.ShouldBe(download.ContentType);
        file.FileDownloadName.ShouldBe(download.FileName);
    }

    // ------------------------------------------------------------------ Branding

    [Fact]
    public async Task Branding_logos_are_files_and_a_missing_logo_is_a_404()
    {
        var service = Substitute.For<IBrandingAppService>();
        var controller = new BrandingController(service);
        var mine = Download("logo.png");
        var office = Download("office-logo.png");
        service.DownloadLogoAsync().Returns(mine);
        service.DownloadLogoForOfficeAsync(OfficeId).Returns(office);

        ShouldBeTheDownload(await controller.GetLogoAsync(), mine);
        ShouldBeTheDownload(await controller.GetOfficeLogoAsync(OfficeId), office);

        service.DownloadLogoAsync().Returns((DownloadResult?)null);
        service.DownloadLogoForOfficeAsync(OfficeId).Returns((DownloadResult?)null);
        (await controller.GetLogoAsync()).ShouldBeOfType<NotFoundResult>();
        (await controller.GetOfficeLogoAsync(OfficeId)).ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Branding_logo_upload_refuses_an_empty_file_and_otherwise_targets_the_office()
    {
        var service = Substitute.For<IBrandingAppService>();
        var controller = new BrandingController(service);
        await Should.ThrowAsync<UserFriendlyException>(() =>
            controller.UploadLogoAsync(new UploadBrandingLogoForm { File = FormFiles.Empty() }, OfficeId));
        service.ReceivedCalls().ShouldBeEmpty();

        var expected = new BrandingDto();
        var form = new UploadBrandingLogoForm { File = FormFiles.WithContent(out var stream) };
        service.UploadLogoAsync(OfficeId, FormFiles.FileName, FormFiles.ContentType, form.File.Length, stream)
            .Returns(expected);

        (await controller.UploadLogoAsync(form, OfficeId)).ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task Branding_display_name_is_unwrapped_from_the_body_and_a_missing_body_clears_it()
    {
        var service = Substitute.For<IBrandingAppService>();
        var controller = new BrandingController(service);

        await controller.SetDisplayNameAsync(OfficeId, new SetBrandingDisplayNameInput { DisplayName = "Example Practice" });
        await controller.SetDisplayNameAsync(null, null!);

        await service.Received(1).SetDisplayNameAsync(OfficeId, "Example Practice");
        await service.Received(1).SetDisplayNameAsync(null, null);
    }

    // ------------------------------------------------------------------ User signature

    [Fact]
    public async Task Signature_upload_refuses_an_empty_file_and_download_is_a_file()
    {
        var service = Substitute.For<IUserSignatureAppService>();
        var controller = new UserSignatureController(service);
        await Should.ThrowAsync<UserFriendlyException>(() =>
            controller.UploadAsync(new UploadUserSignatureForm { File = FormFiles.Empty() }));
        service.ReceivedCalls().ShouldBeEmpty();

        var expected = new UserSignatureInfoDto();
        var form = new UploadUserSignatureForm { File = FormFiles.WithContent(out var stream) };
        service.UploadAsync(FormFiles.FileName, FormFiles.ContentType, form.File.Length, stream).Returns(expected);
        (await controller.UploadAsync(form)).ShouldBeSameAs(expected);

        var download = Download("signature.png");
        service.DownloadAsync().Returns(download);
        ShouldBeTheDownload(await controller.DownloadAsync(), download);
    }

    // ------------------------------------------------------------------ Documents library

    [Fact]
    public async Task Document_create_and_replace_require_a_file_and_pass_its_stream_and_name()
    {
        var service = Substitute.For<IDocumentsAppService>();
        var controller = new DocumentsController(service);
        var input = new DocumentCreateDto();
        await Should.ThrowAsync<ArgumentNullException>(() => controller.CreateAsync(input, null!));
        await Should.ThrowAsync<ArgumentNullException>(() => controller.ReplaceFileAsync(Id, null!));
        service.ReceivedCalls().ShouldBeEmpty();

        var created = new DocumentDto();
        var replaced = new DocumentDto();
        var createFile = FormFiles.WithContent(out var createStream);
        var replaceFile = FormFiles.WithContent(out var replaceStream);
        service.CreateAsync(input, createStream, FormFiles.FileName).Returns(created);
        service.ReplaceFileAsync(Id, replaceStream, FormFiles.FileName, FormFiles.ContentType).Returns(replaced);

        (await controller.CreateAsync(input, createFile)).ShouldBeSameAs(created);
        (await controller.ReplaceFileAsync(Id, replaceFile)).ShouldBeSameAs(replaced);
    }

    // ------------------------------------------------------------------ Reports

    [Fact]
    public async Task Report_exports_and_the_demographics_pdf_are_files()
    {
        var reports = Substitute.For<IReportsAppService>();
        var demographics = Substitute.For<IAppointmentDemographicsAppService>();
        var input = new GetAppointmentReportInput();
        var csv = Download("report.csv");
        var pdf = Download("report.pdf");
        var sheet = Download("demographics.pdf");
        reports.GetReportCsvAsync(input).Returns(csv);
        reports.GetReportPdfAsync(input).Returns(pdf);
        demographics.GetPdfAsync(Id).Returns(sheet);

        ShouldBeTheDownload(await new ReportController(reports).ExportCsvAsync(input), csv);
        ShouldBeTheDownload(await new ReportController(reports).ExportPdfAsync(input), pdf);
        ShouldBeTheDownload(await new AppointmentDemographicsController(demographics).GetPdfAsync(Id), sheet);
    }

    // ------------------------------------------------------------------ External signups

    [Fact]
    public async Task Signup_bodies_are_unwrapped_before_reaching_the_service()
    {
        var service = Substitute.For<IExternalSignupAppService>();
        var controller = new ExternalSignupController(service);
        var emails = new List<string> { "one@example.test", "two@example.test" };
        var deleted = new DeleteTestUsersResultDto();
        service.DeleteTestUsersAsync(emails).Returns(deleted);

        (await controller.DeleteTestUsersAsync(new DeleteTestUsersDto { Emails = emails })).ShouldBeSameAs(deleted);
        await controller.MarkEmailConfirmedAsync(new MarkEmailConfirmedDto { Email = "one@example.test" });

        await service.Received(1).MarkEmailConfirmedAsync("one@example.test");
    }

    [Fact]
    public async Task Tenant_resolution_is_a_404_when_no_practice_matches()
    {
        var service = Substitute.For<IExternalSignupAppService>();
        var controller = new ExternalSignupController(service);
        var practice = new LookupDto<Guid> { Id = OfficeId, DisplayName = "Example Practice" };
        service.ResolveTenantByNameAsync("example").Returns(practice);

        var found = (await controller.ResolveTenantByNameAsync("example")).ShouldBeOfType<OkObjectResult>();
        found.Value.ShouldBeSameAs(practice);

        (await controller.ResolveTenantByNameAsync("nobody")).ShouldBeOfType<NotFoundResult>();
    }

    // ------------------------------------------------------------------ Package details

    [Fact]
    public async Task Package_document_links_are_unwrapped_from_the_request()
    {
        var service = Substitute.For<IPackageDetailsAppService>();
        var request = new LinkDocumentsRequest { DocumentIds = new List<Guid> { Id } };
        var expected = new PackageDetailWithDocumentsDto();
        service.LinkDocumentsAsync(OfficeId, request.DocumentIds).Returns(expected);

        (await new PackageDetailsController(service).LinkDocumentsAsync(OfficeId, request)).ShouldBeSameAs(expected);
    }

    // ------------------------------------------------------------------ Case Tracker administration

    [Fact]
    public async Task Case_tracker_dead_letters_and_the_push_toggle_reach_their_services()
    {
        var deadLetters = Substitute.For<ICaseTrackerDeadLetterAppService>();
        var pushSettings = Substitute.For<ICaseTrackerPushSettingsAppService>();
        var list = new List<CaseTrackerDeadLetterDto>();
        var retried = new CaseTrackerDeadLetterRetryResultDto();
        var retriedAll = new CaseTrackerDeadLetterRetryAllResultDto();
        var state = new CaseTrackerOfficePushStateDto();
        deadLetters.GetListAsync().Returns(list);
        deadLetters.RetryAsync(OfficeId, Id).Returns(retried);
        deadLetters.RetryAllAsync(OfficeId).Returns(retriedAll);
        pushSettings.SetPushEnabledAsync(OfficeId, true).Returns(state);
        var letters = new CaseTrackerDeadLetterController(deadLetters);

        (await letters.GetDeadLettersAsync()).ShouldBeSameAs(list);
        (await letters.RetryDeadLetterAsync(OfficeId, Id)).ShouldBeSameAs(retried);
        (await letters.RetryAllDeadLettersAsync(OfficeId)).ShouldBeSameAs(retriedAll);
        (await new CaseTrackerOfficesController(pushSettings)
            .SetPushEnabledAsync(OfficeId, new CaseTrackerPushToggleInput { Enabled = true })).ShouldBeSameAs(state);
    }

    // ------------------------------------------------------------------ Change requests

    [Fact]
    public async Task Pending_change_requests_and_the_public_consent_routes_reach_their_services()
    {
        var approvals = Substitute.For<IAppointmentChangeRequestsApprovalAppService>();
        var consent = Substitute.For<IPublicChangeRequestConsentAppService>();
        var input = new GetChangeRequestsInput();
        var pending = new PagedResultDto<AppointmentChangeRequestDto>();
        var info = new ChangeRequestConsentInfoDto();
        var decided = new ChangeRequestConsentInfoDto();
        var decision = new SubmitChangeRequestConsentDto();
        approvals.GetPendingChangeRequestsAsync(input).Returns(pending);
        consent.GetConsentInfoAsync("synthetic-token").Returns(info);
        consent.SubmitDecisionAsync("synthetic-token", decision).Returns(decided);
        var consentController = new PublicChangeRequestConsentController(consent);

        (await new AppointmentChangeRequestApprovalController(approvals).GetPendingAsync(input)).ShouldBeSameAs(pending);
        (await consentController.GetAsync("synthetic-token")).ShouldBeSameAs(info);
        (await consentController.SubmitAsync("synthetic-token", decision)).ShouldBeSameAs(decided);
    }
}
