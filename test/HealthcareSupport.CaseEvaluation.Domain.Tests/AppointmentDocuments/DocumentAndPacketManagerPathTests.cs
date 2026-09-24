using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// The paths of <see cref="AppointmentPacketManager"/> and <see cref="AppointmentDocumentManager"/>
/// that <c>AppointmentPacketManagerTests</c> does not reach, and the change-request document
/// entity's constructor.
/// </summary>
/// <remarks>
/// The existing tests build the managers with <c>new</c>, which leaves ABP's lazy service provider
/// unset, so any path that mints an id (<c>GuidGenerator</c>) was unreachable. Here the managers get
/// a substituted provider that answers with ABP's <see cref="SimpleGuidGenerator"/>. Values are
/// synthetic.
/// </remarks>
public class DocumentAndPacketManagerPathTests
{
    private static readonly Guid TenantId = new("7e57a000-0000-4000-9000-000000000001");
    private static readonly Guid AppointmentId = new("7e57a000-0000-4000-9000-000000000002");

    private static T WithGuids<T>(T service) where T : Volo.Abp.Domain.Services.DomainService
    {
        var lazy = Substitute.For<IAbpLazyServiceProvider>();
        lazy.LazyGetService<IGuidGenerator>(Arg.Any<IGuidGenerator>()).Returns(SimpleGuidGenerator.Instance);
        service.LazyServiceProvider = lazy;
        return service;
    }

    private static IRepository<AppointmentPacket, Guid> PacketRepo(params AppointmentPacket[] rows)
    {
        var list = rows.ToList();
        var repo = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        repo.GetQueryableAsync().Returns(_ => list.AsQueryable());
        repo.GetAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => list.First(p => p.Id == ci.ArgAt<Guid>(0)));
        repo.InsertAsync(Arg.Any<AppointmentPacket>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<AppointmentPacket>()));
        return repo;
    }

    // ------------------------------------------------------------------ packets

    [Fact]
    public async Task A_first_generation_inserts_a_new_generating_packet_for_that_kind()
    {
        var repo = PacketRepo();

        var packet = await WithGuids(new AppointmentPacketManager(repo))
            .EnsureGeneratingAsync(TenantId, AppointmentId, PacketKind.Doctor, "blob/first.docx");

        packet.Id.ShouldNotBe(Guid.Empty);
        packet.Status.ShouldBe(PacketGenerationStatus.Generating);
        packet.Kind.ShouldBe(PacketKind.Doctor);
        packet.BlobName.ShouldBe("blob/first.docx");
        await repo.Received(1).InsertAsync(packet, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Marking_generated_stamps_the_first_generation_then_later_ones_as_regenerations()
    {
        var packet = new AppointmentPacket(Guid.NewGuid(), TenantId, AppointmentId, PacketKind.Patient, "blob/v1.pdf")
        {
            ErrorMessage = "TEST earlier failure",
        };
        var manager = new AppointmentPacketManager(PacketRepo(packet));

        await manager.MarkGeneratedAsync(packet.Id, "blob/v2.pdf");
        var firstGeneratedAt = packet.GeneratedAt;
        await manager.MarkGeneratedAsync(packet.Id);

        packet.Status.ShouldBe(PacketGenerationStatus.Generated);
        packet.ErrorMessage.ShouldBeNull();
        packet.BlobName.ShouldBe("blob/v2.pdf", "a regeneration with no new blob keeps the stored one");
        packet.GeneratedAt.ShouldBe(firstGeneratedAt);
        packet.RegeneratedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Marking_failed_records_the_error_cut_to_the_column_length()
    {
        var shortFail = new AppointmentPacket(Guid.NewGuid(), TenantId, AppointmentId, PacketKind.Patient, "blob/a.pdf");
        var longFail = new AppointmentPacket(Guid.NewGuid(), TenantId, AppointmentId, PacketKind.Doctor, "blob/b.pdf");
        var manager = new AppointmentPacketManager(PacketRepo(shortFail, longFail));

        await manager.MarkFailedAsync(shortFail.Id, "TEST renderer down");
        await manager.MarkFailedAsync(longFail.Id, new string('x', AppointmentPacketConsts.ErrorMessageMaxLength + 25));

        shortFail.Status.ShouldBe(PacketGenerationStatus.Failed);
        shortFail.ErrorMessage.ShouldBe("TEST renderer down");
        longFail.ErrorMessage!.Length.ShouldBe(AppointmentPacketConsts.ErrorMessageMaxLength);
    }

    // ------------------------------------------------------------------ documents

    [Fact]
    public async Task An_upload_needs_an_appointment_and_a_file_within_the_size_cap()
    {
        var repo = Substitute.For<IRepository<AppointmentDocument, Guid>>();
        var manager = WithGuids(new AppointmentDocumentManager(repo));

        Task Create(Guid appointmentId, long size) => manager.CreateAsync(
            TenantId, appointmentId, "TEST Intake", "intake.pdf", "blob/intake.pdf", "application/pdf", size, Guid.NewGuid());

        await Should.ThrowAsync<UserFriendlyException>(() => Create(Guid.Empty, 10));
        await Should.ThrowAsync<UserFriendlyException>(() => Create(AppointmentId, 0));
        await Should.ThrowAsync<UserFriendlyException>(() => Create(AppointmentId, AppointmentDocumentConsts.MaxFileSizeBytes + 1));

        await repo.DidNotReceiveWithAnyArgs().InsertAsync(default!, default, default);
    }

    [Fact]
    public async Task A_queued_document_is_created_pending_with_a_verification_code_and_needs_an_appointment_and_a_name()
    {
        var repo = Substitute.For<IRepository<AppointmentDocument, Guid>>();
        repo.InsertAsync(Arg.Any<AppointmentDocument>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(ci.Arg<AppointmentDocument>()));
        var manager = WithGuids(new AppointmentDocumentManager(repo));
        var source = Guid.NewGuid();

        var queued = await manager.CreateQueuedAsync(TenantId, AppointmentId, "TEST Medical Records", sourceDocumentId: source);

        queued.Status.ShouldBe(DocumentStatus.Pending);
        queued.VerificationCode.ShouldNotBeNull();
        queued.AppointmentId.ShouldBe(AppointmentId);
        queued.DocumentName.ShouldBe("TEST Medical Records");
        await Should.ThrowAsync<UserFriendlyException>(() => manager.CreateQueuedAsync(TenantId, Guid.Empty, "TEST x"));
        await Should.ThrowAsync<UserFriendlyException>(() => manager.CreateQueuedAsync(TenantId, AppointmentId, "  "));
        await repo.Received(1).InsertAsync(Arg.Any<AppointmentDocument>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------ change-request documents

    [Fact]
    public void A_change_request_document_keeps_its_file_details_and_needs_a_name_file_and_blob()
    {
        var uploader = Guid.NewGuid();
        var changeRequest = Guid.NewGuid();

        var document = new AppointmentChangeRequestDocument(
            Guid.NewGuid(), TenantId, changeRequest, "TEST Consent", "consent.pdf", "blob/consent.pdf", "application/pdf", 2048, uploader);

        document.TenantId.ShouldBe(TenantId);
        document.AppointmentChangeRequestId.ShouldBe(changeRequest);
        document.DocumentName.ShouldBe("TEST Consent");
        document.FileName.ShouldBe("consent.pdf");
        document.BlobName.ShouldBe("blob/consent.pdf");
        document.ContentType.ShouldBe("application/pdf");
        document.FileSize.ShouldBe(2048);
        document.UploadedByUserId.ShouldBe(uploader);

        foreach (var (name, file, blob) in new[] { (" ", "f.pdf", "b"), ("n", " ", "b"), ("n", "f.pdf", " ") })
        {
            Should.Throw<ArgumentException>(() => new AppointmentChangeRequestDocument(
                Guid.NewGuid(), TenantId, changeRequest, name, file, blob, null, 1, uploader));
        }
    }
}
