using System;
using System.IO;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Controllers.AppointmentDocuments;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Controllers.AppointmentDocuments;

/// <summary>
/// The document, packet and public-upload controllers' actions that do more than pass through:
/// the multipart uploads, the downloads, and the routes that carry an appointment id the service
/// does not take.
/// </summary>
/// <remarks>
/// The pass-through actions of these controllers are covered by
/// <see cref="ControllerDelegationTests"/>. What is pinned here: an upload with no file (or an
/// empty one) is refused BEFORE the service is called; an upload hands the service the file's own
/// name, type, length and stream; a download returns the service's stream as a file with its name
/// and type; and the document-scoped actions act on the document id, not the route's appointment id.
/// </remarks>
public class AppointmentDocumentControllersTests
{
    private static readonly Guid AppointmentId = new("5b1f0c1e-0000-4000-9000-000000000001");
    private static readonly Guid DocumentId = new("5b1f0c1e-0000-4000-9000-000000000002");
    private static readonly Guid TypeId = new("5b1f0c1e-0000-4000-9000-000000000003");

    private readonly IAppointmentDocumentsAppService _documents = Substitute.For<IAppointmentDocumentsAppService>();
    private readonly IAppointmentPacketsAppService _packets = Substitute.For<IAppointmentPacketsAppService>();

    private AppointmentDocumentController Documents() => new(_documents);

    private static DownloadResult Download() => new()
    {
        Content = new MemoryStream(new byte[] { 1, 2, 3 }),
        ContentType = "application/pdf",
        FileName = "synthetic-packet.pdf",
    };

    private static void ShouldBeTheDownload(IActionResult result, DownloadResult download)
    {
        var file = result.ShouldBeOfType<FileStreamResult>();
        file.FileStream.ShouldBeSameAs(download.Content);
        file.ContentType.ShouldBe(download.ContentType);
        file.FileDownloadName.ShouldBe(download.FileName);
    }

    // ------------------------------------------------------------------ AppointmentDocumentController

    [Fact]
    public async Task Upload_refuses_a_missing_or_empty_file_before_reaching_the_service()
    {
        var controller = Documents();

        await Should.ThrowAsync<UserFriendlyException>(() => controller.UploadAsync(AppointmentId, null!));
        await Should.ThrowAsync<UserFriendlyException>(() =>
            controller.UploadAsync(AppointmentId, new UploadAppointmentDocumentForm { File = null! }));
        await Should.ThrowAsync<UserFriendlyException>(() =>
            controller.UploadAsync(AppointmentId, new UploadAppointmentDocumentForm { File = FormFiles.Empty() }));

        _documents.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Upload_hands_the_service_the_file_and_the_form_choices()
    {
        var expected = new AppointmentDocumentDto();
        var form = new UploadAppointmentDocumentForm
        {
            File = FormFiles.WithContent(out var stream),
            DocumentName = "Synthetic intake",
            AppointmentDocumentTypeId = TypeId,
            OtherDocumentTypeName = "Other label",
            IsPanelStrikeList = true,
        };
        _documents.UploadStreamAsync(default, default!, default!, default!, default, default!, default, default, default)
            .ReturnsForAnyArgs(expected);

        var result = await Documents().UploadAsync(AppointmentId, form);

        result.ShouldBeSameAs(expected);
        await _documents.Received(1).UploadStreamAsync(
            AppointmentId, "Synthetic intake", FormFiles.FileName, FormFiles.ContentType,
            form.File.Length, stream, TypeId, "Other label", true);
    }

    [Fact]
    public async Task Upload_sends_an_empty_name_when_the_form_has_none()
    {
        var form = new UploadAppointmentDocumentForm { File = FormFiles.WithContent(out _) };

        await Documents().UploadAsync(AppointmentId, form);

        await _documents.Received(1).UploadStreamAsync(
            AppointmentId, string.Empty, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<long>(),
            Arg.Any<Stream>(), null, null, false);
    }

    [Fact]
    public async Task List_reads_the_appointments_documents()
    {
        var rows = new System.Collections.Generic.List<AppointmentDocumentDto>();
        _documents.GetListByAppointmentAsync(AppointmentId).Returns(rows);

        (await Documents().GetListAsync(AppointmentId)).ShouldBeSameAs(rows);
    }

    [Fact]
    public async Task Download_returns_the_documents_stream_as_a_named_file()
    {
        var download = Download();
        _documents.DownloadAsync(DocumentId).Returns(download);

        var result = await Documents().DownloadAsync(AppointmentId, DocumentId);

        ShouldBeTheDownload(result, download);
    }

    [Fact]
    public async Task Document_actions_act_on_the_document_id_not_the_route_appointment_id()
    {
        var approved = new AppointmentDocumentDto();
        var rejected = new AppointmentDocumentDto();
        var input = new RejectDocumentInput();
        _documents.ApproveAsync(DocumentId).Returns(approved);
        _documents.RejectAsync(DocumentId, input).Returns(rejected);
        var controller = Documents();

        await controller.DeleteAsync(AppointmentId, DocumentId);
        (await controller.ApproveAsync(AppointmentId, DocumentId)).ShouldBeSameAs(approved);
        (await controller.RejectAsync(AppointmentId, DocumentId, input)).ShouldBeSameAs(rejected);

        await _documents.Received(1).DeleteAsync(DocumentId);
    }

    [Fact]
    public async Task Package_upload_refuses_an_empty_file_and_otherwise_updates_the_named_row()
    {
        var controller = Documents();
        await Should.ThrowAsync<UserFriendlyException>(() =>
            controller.UploadPackageAsync(AppointmentId, DocumentId, new UploadAppointmentDocumentForm { File = FormFiles.Empty() }));
        _documents.ReceivedCalls().ShouldBeEmpty();

        var expected = new AppointmentDocumentDto();
        var form = new UploadAppointmentDocumentForm { File = FormFiles.WithContent(out var stream) };
        _documents.UploadPackageDocumentAsync(DocumentId, FormFiles.FileName, FormFiles.ContentType, form.File.Length, stream)
            .Returns(expected);

        (await controller.UploadPackageAsync(AppointmentId, DocumentId, form)).ShouldBeSameAs(expected);
    }

    [Fact]
    public async Task Joint_declaration_upload_refuses_an_empty_file_and_otherwise_names_the_form()
    {
        var controller = Documents();
        await Should.ThrowAsync<UserFriendlyException>(() =>
            controller.UploadJointDeclarationAsync(AppointmentId, new UploadAppointmentDocumentForm { File = FormFiles.Empty() }));
        _documents.ReceivedCalls().ShouldBeEmpty();

        var form = new UploadAppointmentDocumentForm { File = FormFiles.WithContent(out var stream) };
        await controller.UploadJointDeclarationAsync(AppointmentId, form);

        await _documents.Received(1).UploadJointDeclarationAsync(
            AppointmentId, "Joint Declaration Form", FormFiles.FileName, FormFiles.ContentType, form.File.Length, stream);
    }

    // ------------------------------------------------------------------ AppointmentPacketController

    [Fact]
    public async Task Packet_downloads_return_the_packets_stream_as_a_named_file()
    {
        var latest = Download();
        var byKind = Download();
        _packets.DownloadAsync(AppointmentId).Returns(latest);
        _packets.DownloadByKindAsync(AppointmentId, PacketKind.Patient).Returns(byKind);
        var controller = new AppointmentPacketController(_packets);

        ShouldBeTheDownload(await controller.DownloadAsync(AppointmentId), latest);
        ShouldBeTheDownload(await controller.DownloadByKindAsync(AppointmentId, PacketKind.Patient), byKind);
    }

    // ------------------------------------------------------------------ PublicDocumentUploadController

    [Fact]
    public async Task Public_upload_refuses_an_empty_file_and_otherwise_passes_the_code_through()
    {
        var verificationCode = new Guid("5b1f0c1e-0000-4000-9000-000000000004");
        var controller = new PublicDocumentUploadController(_documents);
        await Should.ThrowAsync<UserFriendlyException>(() =>
            controller.UploadByVerificationCodeAsync(DocumentId, verificationCode, new UploadAppointmentDocumentForm { File = FormFiles.Empty() }));
        _documents.ReceivedCalls().ShouldBeEmpty();

        var expected = new AppointmentDocumentDto();
        var form = new UploadAppointmentDocumentForm { File = FormFiles.WithContent(out var stream) };
        _documents.UploadByVerificationCodeAsync(
                DocumentId, verificationCode, FormFiles.FileName, FormFiles.ContentType, form.File.Length, stream)
            .Returns(expected);

        (await controller.UploadByVerificationCodeAsync(DocumentId, verificationCode, form)).ShouldBeSameAs(expected);
    }
}
