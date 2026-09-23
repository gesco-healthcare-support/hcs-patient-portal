using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Documents;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.PackageDetails;
using HealthcareSupport.CaseEvaluation.Security;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// The appointment-document and packet services end to end: listing, the upload paths and every
/// refusal in front of them, download, approve / reject / delete, the combined patient view,
/// packet regeneration, and the required-documents indicator.
/// </summary>
/// <remarks>
/// <para>
/// The services sat at 434 of 486 and 49 of 83 lines uncovered. The existing
/// <c>AppointmentDocumentsAppServiceTests</c> pins only the size gate, which fires before
/// anything is stored.
/// </para>
/// <para>
/// Runs on the standard integration base (real SQLite, seeded office A and appointment 1). Four
/// collaborators are replaced for THIS class only, in <see cref="AfterAddApplication"/>:
/// <list type="bullet">
///   <item>the two blob containers, so storage calls can be asserted and made to fail;</item>
///   <item>the background job manager, so an enqueue is asserted rather than run;</item>
///   <item>the local event bus, so each published event is asserted, and so the notification
///   handlers (Session A's area) do not run under these tests.</item>
/// </list>
/// Every call is made as a Staff Supervisor in office A, which the read-access guard admits.
/// </para>
/// <para>All names, file bytes and identifiers below are synthetic.</para>
/// </remarks>
public abstract class AppointmentDocumentsServiceFlowTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static readonly Guid StaffUserId = new("7d3f1e2a-0000-4000-9000-000000000001");
    private static readonly Guid AppointmentId = AppointmentsTestData.Appointment1Id;
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7 synthetic");

    private IBlobContainer<AppointmentDocumentsContainer> _blobs = null!;
    private IBlobContainer<AppointmentPacketsContainer> _packetBlobs = null!;
    private IBackgroundJobManager _jobs = null!;
    private ILocalEventBus _events = null!;

    private readonly IAppointmentDocumentsAppService _documents;
    private readonly IAppointmentPacketsAppService _packets;
    private readonly MissingRequiredDocumentsResolver _resolver;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentDocumentType, Guid> _typeRepository;
    private readonly IRepository<Document, Guid> _masterRepository;
    private readonly IPackageDetailRepository _packageRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _principal;

    protected AppointmentDocumentsServiceFlowTests()
    {
        _documents = GetRequiredService<IAppointmentDocumentsAppService>();
        _packets = GetRequiredService<IAppointmentPacketsAppService>();
        _resolver = GetRequiredService<MissingRequiredDocumentsResolver>();
        _documentRepository = GetRequiredService<IRepository<AppointmentDocument, Guid>>();
        _packetRepository = GetRequiredService<IRepository<AppointmentPacket, Guid>>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _typeRepository = GetRequiredService<IRepository<AppointmentDocumentType, Guid>>();
        _masterRepository = GetRequiredService<IRepository<Document, Guid>>();
        _packageRepository = GetRequiredService<IPackageDetailRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _principal = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    protected override void AfterAddApplication(IServiceCollection services)
    {
        _blobs = Substitute.For<IBlobContainer<AppointmentDocumentsContainer>>();
        _packetBlobs = Substitute.For<IBlobContainer<AppointmentPacketsContainer>>();
        _jobs = Substitute.For<IBackgroundJobManager>();
        _events = Substitute.For<ILocalEventBus>();
        services.Replace(ServiceDescriptor.Singleton(typeof(IBlobContainer<AppointmentDocumentsContainer>), _blobs));
        services.Replace(ServiceDescriptor.Singleton(typeof(IBlobContainer<AppointmentPacketsContainer>), _packetBlobs));
        services.Replace(ServiceDescriptor.Singleton(typeof(IBackgroundJobManager), _jobs));
        services.Replace(ServiceDescriptor.Singleton(typeof(ILocalEventBus), _events));
    }

    // ------------------------------------------------------------------ harness

    /// <summary>Runs the body as a Staff Supervisor inside office A.</summary>
    private async Task AsStaff(Func<Task> body, params string[] roles)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(_principal, StaffUserId, roles.Length > 0 ? roles : new[] { "Staff Supervisor" }))
        {
            await body();
        }
    }

    private static MemoryStream Pdf() => new(PdfBytes);

    private List<T> Published<T>() =>
        _events.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ILocalEventBus.PublishAsync))
            .Select(c => c.GetArguments()[0])
            .OfType<T>()
            .ToList();

    private List<string> SavedBlobNames() =>
        _blobs.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IBlobContainer.SaveAsync))
            .Select(c => (string)c.GetArguments()[0]!)
            .ToList();

    private Task<AppointmentDocument> InsertDocumentAsync(Action<AppointmentDocument>? shape = null) =>
        WithUnitOfWorkAsync(async () =>
        {
            var document = new AppointmentDocument(
                Guid.NewGuid(), TenantsTestData.TenantARef, AppointmentId,
                "Synthetic intake", "synthetic-intake.pdf", "office-a/old-blob", "application/pdf", 10, StaffUserId);
            shape?.Invoke(document);
            return await _documentRepository.InsertAsync(document, autoSave: true);
        });

    /// <summary>Reads a document back inside office A, where the tenant filter can see it.</summary>
    private async Task<AppointmentDocument> ReloadAsync(Guid id)
    {
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            return await WithUnitOfWorkAsync(() => _documentRepository.GetAsync(id));
        }
    }

    private Task<AppointmentDocumentType> InsertTypeAsync(
        string name, bool appliesToAll = false, bool isActive = true, bool isSystem = false, Guid? forType = null) =>
        WithUnitOfWorkAsync(async () =>
        {
            var type = new AppointmentDocumentType(
                Guid.NewGuid(), name, appliesToAll, isActive, isSystem, TenantsTestData.TenantARef);
            if (forType.HasValue)
            {
                type.AddAppointmentType(forType.Value);
            }

            return await _typeRepository.InsertAsync(type, autoSave: true);
        });

    private static MemoryStream Docx(params string[] entries)
    {
        var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var name in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                writer.Write("<synthetic/>");
            }
        }

        buffer.Position = 0;
        return buffer;
    }

    private Task<AppointmentDocumentDto> Upload(
        Stream content,
        string fileName = "synthetic-intake.pdf",
        string documentName = "Synthetic intake",
        long? size = null,
        Guid? typeId = null,
        string? other = null,
        bool strikeList = false) =>
        _documents.UploadStreamAsync(
            AppointmentId, documentName, fileName, "application/pdf", size ?? content.Length, content,
            typeId, other, strikeList);

    // ------------------------------------------------------------------ required ids

    [Fact]
    public async Task Every_appointment_scoped_call_refuses_an_empty_appointment_id()
    {
        await AsStaff(async () =>
        {
            var calls = new Func<Task>[]
            {
                () => _documents.GetListByAppointmentAsync(Guid.Empty),
                () => _documents.GetDocumentTypeOptionsAsync(Guid.Empty),
                () => _documents.GetMissingRequiredDocumentsAsync(Guid.Empty),
                () => _documents.UploadStreamAsync(Guid.Empty, "n", "f.pdf", null, 1, Pdf()),
                () => _documents.UploadJointDeclarationAsync(Guid.Empty, "n", "f.pdf", null, 1, Pdf()),
                () => _documents.GetCombinedForAppointmentAsync(Guid.Empty),
                () => _documents.RegeneratePacketAsync(Guid.Empty),
                () => _packets.GetByAppointmentAsync(Guid.Empty),
                () => _packets.DownloadAsync(Guid.Empty),
                () => _packets.GetListByAppointmentAsync(Guid.Empty),
                () => _packets.DownloadByKindAsync(Guid.Empty, PacketKind.Doctor),
            };

            foreach (var call in calls)
            {
                await Should.ThrowAsync<UserFriendlyException>(call);
            }
        });

        _blobs.ReceivedCalls().ShouldBeEmpty();
        _jobs.ReceivedCalls().ShouldBeEmpty();
    }

    // ------------------------------------------------------------------ upload refusals

    [Fact]
    public async Task Upload_refuses_a_missing_file_name_or_an_empty_file_before_storing_anything()
    {
        await AsStaff(async () =>
        {
            await Should.ThrowAsync<UserFriendlyException>(() => Upload(Pdf(), fileName: " "));
            (await Should.ThrowAsync<BusinessException>(() => Upload(Pdf(), size: 0)))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty);
            (await Should.ThrowAsync<BusinessException>(() =>
                    _documents.UploadStreamAsync(AppointmentId, "n", "f.pdf", null, 5, null!)))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty);
        });

        SavedBlobNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task Upload_refuses_a_document_type_selection_it_cannot_honour()
    {
        await AsStaff(async () =>
        {
            var active = await InsertTypeAsync("Example radiology", appliesToAll: true);
            var inactive = await InsertTypeAsync("Example retired", appliesToAll: true, isActive: false);
            var system = await InsertTypeAsync("Example system", appliesToAll: true, isSystem: true);

            await Should.ThrowAsync<UserFriendlyException>(() => Upload(Pdf(), typeId: active.Id, other: "Also other"));
            await Should.ThrowAsync<UserFriendlyException>(() =>
                Upload(Pdf(), other: new string('x', AppointmentDocumentConsts.OtherDocumentTypeNameMaxLength + 1)));
            await Should.ThrowAsync<UserFriendlyException>(() => Upload(Pdf(), typeId: Guid.NewGuid()));
            await Should.ThrowAsync<UserFriendlyException>(() => Upload(Pdf(), typeId: inactive.Id));
            await Should.ThrowAsync<UserFriendlyException>(() => Upload(Pdf(), typeId: system.Id));
        });

        SavedBlobNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task Upload_refuses_a_file_whose_bytes_are_not_an_accepted_format()
    {
        await AsStaff(async () =>
        {
            async Task RefusedAs(string code, Stream content, string fileName)
            {
                var ex = await Should.ThrowAsync<UserFriendlyException>(() => Upload(content, fileName: fileName));
                ex.Code.ShouldBe(code);
            }

            await RefusedAs(CaseEvaluationDomainErrorCodes.AppointmentDocumentInvalidFileFormat, Pdf(), "script.exe");
            await RefusedAs(
                CaseEvaluationDomainErrorCodes.AppointmentDocumentInvalidFileFormat,
                new MemoryStream(Encoding.ASCII.GetBytes("plain text, not a pdf")),
                "renamed.pdf");
            await RefusedAs(
                CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty,
                new MemoryStream(new byte[] { 0x25, 0x50 }),
                "short.pdf");
            await RefusedAs(CaseEvaluationDomainErrorCodes.AppointmentDocumentInvalidFileFormat, Pdf(), "renamed.docx");
            await RefusedAs(
                CaseEvaluationDomainErrorCodes.AppointmentDocumentInvalidFileFormat,
                Docx("[Content_Types].xml", "word/document.xml", "word/vbaProject.bin"),
                "macros.docx");
            await RefusedAs(
                CaseEvaluationDomainErrorCodes.AppointmentDocumentInvalidFileFormat,
                Docx("[Content_Types].xml"),
                "no-body.docx");
        });

        SavedBlobNames().ShouldBeEmpty();
    }

    // ------------------------------------------------------------------ upload

    [Fact]
    public async Task A_staff_upload_is_stored_under_the_office_and_appointment_and_lands_approved()
    {
        AppointmentDocumentDto result = null!;
        await AsStaff(async () => result = await Upload(Pdf(), documentName: "  Synthetic intake  "));

        result.Status.ShouldBe(DocumentStatus.Accepted);
        result.DocumentName.ShouldBe("Synthetic intake");
        var stored = await ReloadAsync(result.Id);
        stored.IsAdHoc.ShouldBeTrue();
        stored.ResponsibleUserId.ShouldBe(StaffUserId);
        stored.BlobName.ShouldStartWith($"{TenantsTestData.TenantARef:N}/{AppointmentId:N}/");
        SavedBlobNames().ShouldBe(new[] { stored.BlobName });

        var uploaded = Published<AppointmentDocumentUploadedEto>().ShouldHaveSingleItem();
        uploaded.AppointmentDocumentId.ShouldBe(result.Id);
        uploaded.IsAdHoc.ShouldBeTrue();
        uploaded.IsJointDeclaration.ShouldBeFalse();
    }

    [Fact]
    public async Task An_upload_with_no_document_name_is_named_after_its_file()
    {
        AppointmentDocumentDto result = null!;
        await AsStaff(async () => result = await Upload(Pdf(), documentName: " ", fileName: "scan-0001.pdf"));
        result.DocumentName.ShouldBe("scan-0001.pdf");
    }

    [Fact]
    public async Task A_valid_word_document_is_accepted()
    {
        AppointmentDocumentDto result = null!;
        await AsStaff(async () =>
            result = await Upload(Docx("[Content_Types].xml", "word/document.xml"), fileName: "letter.docx"));
        result.FileName.ShouldBe("letter.docx");
    }

    [Fact]
    public async Task An_upload_is_a_strike_list_when_flagged_or_when_filed_under_the_strike_list_label()
    {
        AppointmentDocumentDto flagged = null!, labelled = null!, plain = null!;
        await AsStaff(async () =>
        {
            var label = await WithUnitOfWorkAsync(() =>
                _typeRepository.FindAsync(t => !t.IsSystem && t.Name == AppointmentDocumentTypeConsts.PanelStrikeListName))
                ?? await InsertTypeAsync(AppointmentDocumentTypeConsts.PanelStrikeListName, appliesToAll: true);
            var other = await InsertTypeAsync("Example medical records", appliesToAll: true);

            flagged = await Upload(Pdf(), strikeList: true);
            labelled = await Upload(Pdf(), typeId: label.Id);
            plain = await Upload(Pdf(), typeId: other.Id, other: null);
        });

        flagged.IsPanelStrikeList.ShouldBeTrue();
        labelled.IsPanelStrikeList.ShouldBeTrue();
        plain.IsPanelStrikeList.ShouldBeFalse();
    }

    [Fact]
    public async Task A_failed_upload_deletes_the_file_it_already_stored()
    {
        // The event publish runs AFTER the blob save. Making it throw fails the unit of work, and
        // the compensating delete must then remove the orphaned file -- even though the delete
        // itself also fails, which must not mask the original error.
        _events.PublishAsync(Arg.Any<AppointmentDocumentUploadedEto>(), Arg.Any<bool>())
            .ThrowsAsync(new InvalidOperationException("synthetic publish failure"));
        _blobs.DeleteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("synthetic delete failure"));

        await AsStaff(async () =>
            (await Should.ThrowAsync<InvalidOperationException>(() => Upload(Pdf())))
                .Message.ShouldBe("synthetic publish failure"));

        var saved = SavedBlobNames().ShouldHaveSingleItem();
        await _blobs.Received(1).DeleteAsync(saved, Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------ reads

    [Fact]
    public async Task The_list_holds_the_appointments_documents_newest_first()
    {
        List<AppointmentDocumentDto> rows = null!;
        await AsStaff(async () =>
        {
            await InsertDocumentAsync();
            await InsertDocumentAsync();
            rows = await _documents.GetListByAppointmentAsync(AppointmentId);
        });

        rows.Count.ShouldBe(2);
        rows.Select(r => r.CreationTime).ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task Type_options_are_the_active_non_system_labels_that_apply_to_the_appointment_type()
    {
        List<Shared.LookupDto<Guid>> forAppointment = null!, forType = null!;
        await AsStaff(async () =>
        {
            await InsertTypeAsync("Example A all types", appliesToAll: true);
            await InsertTypeAsync("Example B this type", forType: LocationsTestData.AppointmentType1Id);
            await InsertTypeAsync("Example C other type", forType: CaseEvaluationSeedIds.AppointmentTypes.Ame);
            await InsertTypeAsync("Example D retired", appliesToAll: true, isActive: false);
            await InsertTypeAsync("Example E system", appliesToAll: true, isSystem: true);

            forAppointment = await _documents.GetDocumentTypeOptionsAsync(AppointmentId);
            forType = await _documents.GetDocumentTypeOptionsByAppointmentTypeAsync(LocationsTestData.AppointmentType1Id);
        });

        foreach (var options in new[] { forAppointment, forType })
        {
            var examples = options.Select(o => o.DisplayName).Where(n => n.StartsWith("Example ")).ToList();
            examples.ShouldBe(new[] { "Example A all types", "Example B this type" });
            options.Select(o => o.DisplayName).ShouldBeInOrder(SortDirection.Ascending);
        }
    }

    [Fact]
    public async Task A_missing_document_or_a_missing_file_is_refused_on_download()
    {
        await AsStaff(async () =>
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _documents.DownloadAsync(Guid.NewGuid()));

            var document = await InsertDocumentAsync();
            _blobs.GetAsync(document.BlobName, Arg.Any<CancellationToken>()).Returns((Stream)null!);
            await Should.ThrowAsync<UserFriendlyException>(() => _documents.DownloadAsync(document.Id));
        });
    }

    [Fact]
    public async Task A_download_returns_the_stored_file_and_defaults_an_unknown_type()
    {
        DownloadResult result = null!;
        var stream = Pdf();
        await AsStaff(async () =>
        {
            var document = await InsertDocumentAsync(d => d.ContentType = null);
            _blobs.GetAsync(document.BlobName, Arg.Any<CancellationToken>()).Returns(stream);
            result = await _documents.DownloadAsync(document.Id);
        });

        result.Content.ShouldBeSameAs(stream);
        result.FileName.ShouldBe("synthetic-intake.pdf");
        result.ContentType.ShouldBe("application/octet-stream");
    }

    [Fact]
    public async Task The_patient_view_combines_uploads_with_generated_patient_packets_only()
    {
        List<PatientPortalDocumentDto> rows = null!;
        AppointmentDocument upload = null!;
        await AsStaff(async () =>
        {
            upload = await InsertDocumentAsync();
            await WithUnitOfWorkAsync(async () =>
            {
                var ready = new AppointmentPacket(Guid.NewGuid(), TenantsTestData.TenantARef, AppointmentId,
                    PacketKind.Patient, "office-a/packets/patient-packet.docx", PacketGenerationStatus.Generated)
                {
                    GeneratedAt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                };
                await _packetRepository.InsertAsync(ready, autoSave: true);
                // One packet per kind per appointment (a unique index), so the two excluded rows
                // differ in kind: a generated doctor packet, and a still-generating attorney one.
                await _packetRepository.InsertAsync(new AppointmentPacket(Guid.NewGuid(), TenantsTestData.TenantARef,
                    AppointmentId, PacketKind.AttorneyClaimExaminer, "office-a/packets/pending.pdf"), autoSave: true);
                await _packetRepository.InsertAsync(new AppointmentPacket(Guid.NewGuid(), TenantsTestData.TenantARef,
                    AppointmentId, PacketKind.Doctor, "office-a/packets/doctor.pdf", PacketGenerationStatus.Generated),
                    autoSave: true);
            });
            rows = await _documents.GetCombinedForAppointmentAsync(AppointmentId);
        });

        rows.Count.ShouldBe(2);
        rows[0].Source.ShouldBe(PatientPortalDocumentSource.GeneratedPacket);
        rows[0].FileName.ShouldBe("patient-packet.docx");
        rows[0].ContentType.ShouldBe("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        rows[0].PacketKind.ShouldBe(PacketKind.Patient);
        rows[1].Id.ShouldBe(upload.Id);
        rows[1].Source.ShouldBe(PatientPortalDocumentSource.Uploaded);
        rows[1].UploadStatus.ShouldBe(upload.Status);
    }

    // ------------------------------------------------------------------ approve / reject / delete

    [Fact]
    public async Task Approving_accepts_the_document_clears_any_rejection_and_announces_it()
    {
        AppointmentDocumentDto result = null!;
        AppointmentDocument document = null!;
        await AsStaff(async () =>
        {
            document = await InsertDocumentAsync(d =>
            {
                d.Status = DocumentStatus.Rejected;
                d.RejectionReason = "Illegible";
                d.RejectedByUserId = Guid.NewGuid();
            });
            result = await _documents.ApproveAsync(document.Id);
        });

        result.Status.ShouldBe(DocumentStatus.Accepted);
        var stored = await ReloadAsync(document.Id);
        stored.RejectionReason.ShouldBeNull();
        stored.RejectedByUserId.ShouldBeNull();
        stored.ResponsibleUserId.ShouldBe(StaffUserId);
        Published<AppointmentDocumentAcceptedEto>().ShouldHaveSingleItem().AcceptedByUserId.ShouldBe(StaffUserId);
    }

    [Fact]
    public async Task Rejecting_needs_a_reason_and_records_it_trimmed()
    {
        // A blank reason is refused by the input DTO's own validation before the method body
        // runs, so the in-method guard is a backstop this test cannot reach through the service.
        AppointmentDocumentDto result = null!;
        await AsStaff(async () =>
        {
            var document = await InsertDocumentAsync();
            await Should.ThrowAsync<Volo.Abp.Validation.AbpValidationException>(() =>
                _documents.RejectAsync(document.Id, new RejectDocumentInput { Reason = "  " }));
            result = await _documents.RejectAsync(document.Id, new RejectDocumentInput { Reason = "  Illegible scan  " });
        });

        result.Status.ShouldBe(DocumentStatus.Rejected);
        result.RejectionReason.ShouldBe("Illegible scan");
        Published<AppointmentDocumentRejectedEto>().ShouldHaveSingleItem().RejectionNotes.ShouldBe("Illegible scan");
    }

    [Fact]
    public async Task Deleting_removes_the_document_and_announces_it_and_an_unknown_id_is_a_no_op()
    {
        AppointmentDocument document = null!;
        await AsStaff(async () =>
        {
            await _documents.DeleteAsync(Guid.NewGuid());
            Published<AppointmentDocumentDeletedEto>().ShouldBeEmpty();

            document = await InsertDocumentAsync();
            await _documents.DeleteAsync(document.Id);
        });

        (await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await _documentRepository.FindAsync(document.Id);
            }
        })).ShouldBeNull();
        Published<AppointmentDocumentDeletedEto>().ShouldHaveSingleItem().AppointmentDocumentId.ShouldBe(document.Id);
    }

    [Fact]
    public async Task Regenerating_the_packet_queues_the_job_for_the_appointment_and_office()
    {
        await AsStaff(() => _documents.RegeneratePacketAsync(AppointmentId));

        var args = _jobs.ReceivedCalls().ShouldHaveSingleItem().GetArguments()[0].ShouldBeOfType<GenerateAppointmentPacketArgs>();
        args.AppointmentId.ShouldBe(AppointmentId);
        args.TenantId.ShouldBe(TenantsTestData.TenantARef);
    }

    // ------------------------------------------------------------------ package and code uploads

    [Fact]
    public async Task A_package_upload_replaces_the_file_and_removes_the_old_one()
    {
        AppointmentDocumentDto result = null!;
        await AsStaff(async () =>
        {
            var document = await InsertDocumentAsync(d =>
            {
                d.Status = DocumentStatus.Rejected;
                d.RejectionReason = "Wrong form";
            });
            result = await _documents.UploadPackageDocumentAsync(document.Id, "signed-form.pdf", "application/pdf", 20, Pdf());
        });

        result.FileName.ShouldBe("signed-form.pdf");
        result.Status.ShouldBe(DocumentStatus.Accepted);
        result.RejectionReason.ShouldBeNull();
        await _blobs.Received(1).DeleteAsync("office-a/old-blob", Arg.Any<CancellationToken>());
        Published<AppointmentDocumentUploadedEto>().ShouldHaveSingleItem().IsAdHoc.ShouldBeFalse();
    }

    [Fact]
    public async Task A_package_upload_still_succeeds_when_the_old_file_cannot_be_removed()
    {
        _blobs.DeleteAsync("office-a/old-blob", Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException("synthetic delete failure"));
        AppointmentDocumentDto result = null!;
        await AsStaff(async () =>
        {
            var document = await InsertDocumentAsync();
            result = await _documents.UploadPackageDocumentAsync(document.Id, "signed-form.pdf", "application/pdf", 20, Pdf());
        });

        result.FileName.ShouldBe("signed-form.pdf");
    }

    [Fact]
    public async Task A_package_upload_refuses_a_missing_file_name_or_an_empty_file()
    {
        await AsStaff(async () =>
        {
            var document = await InsertDocumentAsync();
            await Should.ThrowAsync<UserFriendlyException>(() =>
                _documents.UploadPackageDocumentAsync(document.Id, " ", "application/pdf", 20, Pdf()));
            (await Should.ThrowAsync<BusinessException>(() =>
                    _documents.UploadPackageDocumentAsync(document.Id, "form.pdf", "application/pdf", 0, Pdf())))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty);
        });

        SavedBlobNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_code_upload_needs_the_documents_own_code_and_lands_for_review()
    {
        var code = new Guid("7d3f1e2a-0000-4000-9000-000000000002");
        AppointmentDocumentDto result = null!;
        await AsStaff(async () =>
        {
            var document = await InsertDocumentAsync(d =>
            {
                d.VerificationCode = code;
                d.BlobName = "(pending-upload)";
            });

            (await Should.ThrowAsync<BusinessException>(() =>
                    _documents.UploadByVerificationCodeAsync(document.Id, Guid.NewGuid(), "form.pdf", "application/pdf", 20, Pdf())))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.DocumentUnauthorizedVerificationCode);

            result = await _documents.UploadByVerificationCodeAsync(document.Id, code, "form.pdf", "application/pdf", 20, Pdf());
        });

        result.Status.ShouldBe(DocumentStatus.Uploaded);
        await _blobs.DidNotReceive().DeleteAsync("(pending-upload)", Arg.Any<CancellationToken>());
        Published<AppointmentDocumentUploadedEto>().ShouldHaveSingleItem().UploadedByUserId.ShouldBeNull();
    }

    // ------------------------------------------------------------------ joint declaration

    [Fact]
    public async Task A_joint_declaration_refuses_a_bad_file_and_a_non_ame_appointment()
    {
        await AsStaff(async () =>
        {
            await Should.ThrowAsync<UserFriendlyException>(() =>
                _documents.UploadJointDeclarationAsync(AppointmentId, "JDF", " ", "application/pdf", 20, Pdf()));
            (await Should.ThrowAsync<BusinessException>(() =>
                    _documents.UploadJointDeclarationAsync(AppointmentId, "JDF", "jdf.pdf", "application/pdf", 0, Pdf())))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentDocumentFileEmpty);
            (await Should.ThrowAsync<BusinessException>(() =>
                    _documents.UploadJointDeclarationAsync(AppointmentId, "JDF", "jdf.pdf", "application/pdf", 20, Pdf())))
                .Code.ShouldBe(CaseEvaluationDomainErrorCodes.JdfRequiresAmeAppointment);
        });

        SavedBlobNames().ShouldBeEmpty();
    }

    [Fact]
    public async Task The_booking_attorney_files_a_joint_declaration_on_an_ame()
    {
        var ameAppointmentId = Guid.NewGuid();
        var types = GetRequiredService<IRepository<AppointmentType, Guid>>();
        // The booking attorney is recorded as the booker, which is the party the read-access guard
        // and the JDF gate both recognise. (ABP does not stamp CreatorId here: its audit setter
        // skips an entity whose tenant differs from the caller's, and a test principal has none.)
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                // The integration seed does not include the office's AME type, and the JDF gate
                // keys on its fixed id, so it is added here.
                if (await types.FindAsync(CaseEvaluationSeedIds.AppointmentTypes.Ame) == null)
                {
                    await types.InsertAsync(
                        new AppointmentType(CaseEvaluationSeedIds.AppointmentTypes.Ame, "AME"), autoSave: true);
                }

                await _appointmentRepository.InsertAsync(new Appointment(
                    id: ameAppointmentId,
                    patientId: PatientsTestData.Patient1Id,
                    identityUserId: IdentityUsersTestData.ApplicantAttorney1UserId,
                    appointmentTypeId: CaseEvaluationSeedIds.AppointmentTypes.Ame,
                    locationId: LocationsTestData.Location1Id,
                    doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                    appointmentDate: new DateTime(2030, 2, 1, 9, 0, 0, DateTimeKind.Utc),
                    requestConfirmationNumber: "A97001",
                    appointmentStatus: AppointmentStatusType.Approved)
                {
                    BookedByUserId = IdentityUsersTestData.ApplicantAttorney1UserId,
                }, autoSave: true);
            }
        });

        AppointmentDocumentDto result = null!;
        using (_currentTenant.Change(TenantsTestData.TenantARef))
        using (WithCurrentUser.Run(_principal, IdentityUsersTestData.ApplicantAttorney1UserId,
                   IdentityUsersTestData.ApplicantAttorneyRoleName))
        {
            result = await _documents.UploadJointDeclarationAsync(
                ameAppointmentId, " ", "jdf.pdf", "application/pdf", 20, Pdf());
        }

        result.DocumentName.ShouldBe("Joint Declaration Form");
        result.Status.ShouldBe(DocumentStatus.Uploaded);
        Published<AppointmentDocumentUploadedEto>().ShouldHaveSingleItem().IsJointDeclaration.ShouldBeTrue();
    }

    // ------------------------------------------------------------------ required documents

    [Fact]
    public async Task Required_documents_report_what_is_still_missing_for_the_appointment_type()
    {
        MissingRequiredDocumentsResultDto none = null!, some = null!;
        MissingRequiredDocumentsResult resolved = null!;
        var consent = Guid.NewGuid();
        var history = Guid.NewGuid();
        await AsStaff(async () =>
        {
            none = await _documents.GetMissingRequiredDocumentsAsync(AppointmentId);

            await WithUnitOfWorkAsync(async () =>
            {
                await _masterRepository.InsertAsync(new Document(consent, TenantsTestData.TenantARef, "Example consent", "docs/consent", "application/pdf"), autoSave: true);
                await _masterRepository.InsertAsync(new Document(history, TenantsTestData.TenantARef, "Example history", "docs/history", "application/pdf"), autoSave: true);
                var package = new PackageDetail(Guid.NewGuid(), TenantsTestData.TenantARef, "Example package", LocationsTestData.AppointmentType1Id);
                package.DocumentPackages.Add(new DocumentPackage(package.Id, consent));
                package.DocumentPackages.Add(new DocumentPackage(package.Id, history));
                await _packageRepository.InsertAsync(package, autoSave: true);
            });
            await InsertDocumentAsync(d =>
            {
                d.SourceDocumentId = consent;
                d.Status = DocumentStatus.Accepted;
            });

            some = await _documents.GetMissingRequiredDocumentsAsync(AppointmentId);
            resolved = await WithUnitOfWorkAsync(() => _resolver.ResolveAsync(AppointmentId));
        });

        none.RequiredCount.ShouldBe(0);
        none.Missing.ShouldBeEmpty();
        some.RequiredCount.ShouldBe(2);
        some.Missing.ShouldHaveSingleItem().DocumentId.ShouldBe(history);
        resolved.RequiredCount.ShouldBe(2);
        resolved.Missing.ShouldHaveSingleItem().DocumentId.ShouldBe(history);
    }

    [Fact]
    public async Task The_resolver_reports_nothing_for_an_empty_or_unknown_appointment_or_one_with_no_package()
    {
        await AsStaff(async () =>
        {
            (await _resolver.ResolveAsync(Guid.Empty)).ShouldBeSameAs(MissingRequiredDocumentsResult.Empty);
            (await WithUnitOfWorkAsync(() => _resolver.ResolveAsync(Guid.NewGuid())))
                .ShouldBeSameAs(MissingRequiredDocumentsResult.Empty);
            (await WithUnitOfWorkAsync(() => _resolver.ResolveAsync(AppointmentId)))
                .ShouldBeSameAs(MissingRequiredDocumentsResult.Empty);
        });
    }

    // ------------------------------------------------------------------ packets

    private Task<AppointmentPacket> InsertPacketAsync(PacketKind kind, string blobName, PacketGenerationStatus status) =>
        WithUnitOfWorkAsync(() => _packetRepository.InsertAsync(
            new AppointmentPacket(Guid.NewGuid(), TenantsTestData.TenantARef, AppointmentId, kind, blobName, status),
            autoSave: true));

    [Fact]
    public async Task Staff_see_every_packet_kind_in_kind_order()
    {
        List<AppointmentPacketDto> rows = null!;
        AppointmentPacketDto? patient = null, missing = null;
        await AsStaff(async () =>
        {
            missing = await _packets.GetByAppointmentAsync(AppointmentId);
            await InsertPacketAsync(PacketKind.AttorneyClaimExaminer, "p/atty.pdf", PacketGenerationStatus.Generated);
            await InsertPacketAsync(PacketKind.Patient, "p/patient.pdf", PacketGenerationStatus.Generated);
            rows = await _packets.GetListByAppointmentAsync(AppointmentId);
            patient = await _packets.GetByAppointmentAsync(AppointmentId);
        });

        missing.ShouldBeNull();
        rows.Select(r => r.Kind).ShouldBe(new[] { PacketKind.Patient, PacketKind.AttorneyClaimExaminer });
        patient.ShouldNotBeNull().Kind.ShouldBe(PacketKind.Patient);
    }

    [Fact]
    public async Task A_packet_downloads_only_once_generated_and_stored()
    {
        await AsStaff(async () =>
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => _packets.DownloadAsync(AppointmentId));
            await Should.ThrowAsync<EntityNotFoundException>(() => _packets.DownloadByKindAsync(AppointmentId, PacketKind.Doctor));

            var patient = await InsertPacketAsync(PacketKind.Patient, "p/patient.pdf", PacketGenerationStatus.Generating);
            var doctor = await InsertPacketAsync(PacketKind.Doctor, "p/doctor.pdf", PacketGenerationStatus.Generating);
            await Should.ThrowAsync<UserFriendlyException>(() => _packets.DownloadAsync(AppointmentId));
            await Should.ThrowAsync<UserFriendlyException>(() => _packets.DownloadByKindAsync(AppointmentId, PacketKind.Doctor));

            await WithUnitOfWorkAsync(async () =>
            {
                foreach (var id in new[] { patient.Id, doctor.Id })
                {
                    var packet = await _packetRepository.GetAsync(id);
                    packet.Status = PacketGenerationStatus.Generated;
                    await _packetRepository.UpdateAsync(packet, autoSave: true);
                }
            });
            _packetBlobs.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Stream)null!);
            await Should.ThrowAsync<UserFriendlyException>(() => _packets.DownloadAsync(AppointmentId));
            await Should.ThrowAsync<UserFriendlyException>(() => _packets.DownloadByKindAsync(AppointmentId, PacketKind.Doctor));
        });
    }

    [Fact]
    public async Task A_packet_download_is_named_for_the_appointment_and_typed_by_its_file()
    {
        DownloadResult latest = null!, byKind = null!;
        var patientStream = Pdf();
        var doctorStream = Pdf();
        await AsStaff(async () =>
        {
            await InsertPacketAsync(PacketKind.Patient, "p/patient.pdf", PacketGenerationStatus.Generated);
            await InsertPacketAsync(PacketKind.Doctor, "p/doctor.docx", PacketGenerationStatus.Generated);
            _packetBlobs.GetAsync("p/patient.pdf", Arg.Any<CancellationToken>()).Returns(patientStream);
            _packetBlobs.GetAsync("p/doctor.docx", Arg.Any<CancellationToken>()).Returns(doctorStream);

            latest = await _packets.DownloadAsync(AppointmentId);
            byKind = await _packets.DownloadByKindAsync(AppointmentId, PacketKind.Doctor);
        });

        latest.Content.ShouldBeSameAs(patientStream);
        latest.FileName.ShouldBe($"appointment-packet-{AppointmentId:N}.pdf");
        latest.ContentType.ShouldBe("application/pdf");
        byKind.Content.ShouldBeSameAs(doctorStream);
        byKind.FileName.ShouldContain(AppointmentsTestData.Appointment1RequestConfirmationNumber);
        byKind.FileName.ShouldEndWith(".docx");
        byKind.ContentType.ShouldBe("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    [Fact]
    public async Task A_patient_cannot_see_or_download_the_packets_meant_for_other_parties()
    {
        AppointmentPacketDto? forClaimExaminer = null;
        await AsStaff(async () =>
        {
            await InsertPacketAsync(PacketKind.Patient, "p/patient.pdf", PacketGenerationStatus.Generated);
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _packets.DownloadByKindAsync(AppointmentId, PacketKind.Doctor));
        }, IdentityUsersTestData.PatientRoleName);

        await AsStaff(async () =>
        {
            forClaimExaminer = await _packets.GetByAppointmentAsync(AppointmentId);
            await Should.ThrowAsync<AbpAuthorizationException>(() => _packets.DownloadAsync(AppointmentId));
        }, IdentityUsersTestData.ClaimExaminerRoleName);

        forClaimExaminer.ShouldBeNull();
    }
}
