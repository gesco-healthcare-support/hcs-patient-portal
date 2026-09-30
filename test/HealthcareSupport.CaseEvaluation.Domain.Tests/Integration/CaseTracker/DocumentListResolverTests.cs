using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// <see cref="DocumentListResolver"/>: which uploaded documents are published, and how each one's
/// category label is chosen. The category repository stub applies the resolver's own predicate to a
/// set that always includes a decoy category, so "only the referenced categories are read" is shown
/// against one that could have been picked up. All data is synthetic.
/// </summary>
public class DocumentListResolverTests
{
    private static readonly Guid Office = new("72839aa4-b5c6-48d7-a2e3-7e8f90a1b2c9");
    private static readonly Guid AppointmentId = new("8394abb5-c6d7-49e8-b3f4-8f90a1b2c3da");
    private static readonly Guid MedicalRecordsTypeId = new("94a5bcc6-d7e8-4af9-84a5-90a1b2c3d4eb");
    private static readonly Guid DecoyTypeId = new("a5b6cdd7-e8f9-4b0a-95b6-a1b2c3d4e5fc");

    private static readonly List<AppointmentDocumentType> Categories = new()
    {
        new AppointmentDocumentType(MedicalRecordsTypeId, "TEST-Medical Records"),
        new AppointmentDocumentType(DecoyTypeId, "TEST-Decoy category"),
    };

    private sealed class Harness
    {
        public DocumentListResolver Resolver { get; init; } = null!;
        public IRepository<AppointmentDocumentType, Guid> Types { get; init; } = null!;
    }

    private static AppointmentDocument Document(
        Guid? typeId = null,
        string? otherTypeName = null,
        DocumentStatus status = DocumentStatus.Uploaded) =>
        new(
            Guid.NewGuid(),
            Office,
            AppointmentId,
            documentName: "TEST-Document",
            fileName: "TEST-document.pdf",
            blobName: $"docs/{Guid.NewGuid():N}.pdf",
            contentType: "application/pdf",
            fileSize: 1024,
            uploadedByUserId: new Guid("b6c7dee8-f90a-4c1b-a6c7-b2c3d4e5f60d"),
            appointmentDocumentTypeId: typeId,
            otherDocumentTypeName: otherTypeName)
        {
            Status = status,
        };

    private static Harness Build(params AppointmentDocument[] documents)
    {
        var documentRepository = Substitute.For<IRepository<AppointmentDocument, Guid>>();
        documentRepository.FindAsync(Arg.Any<Guid>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(documents.FirstOrDefault(d => d.Id == ci.ArgAt<Guid>(0))));
        documentRepository.GetListAsync(
                Arg.Any<Expression<Func<AppointmentDocument, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(documents.Where(ci.ArgAt<Expression<Func<AppointmentDocument, bool>>>(0).Compile()).ToList()));

        var packetRepository = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        packetRepository.GetListAsync(
                Arg.Any<Expression<Func<AppointmentPacket, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<AppointmentPacket>()));

        var typeRepository = Substitute.For<IRepository<AppointmentDocumentType, Guid>>();
        typeRepository.GetListAsync(
                Arg.Any<Expression<Func<AppointmentDocumentType, bool>>>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Categories.Where(ci.ArgAt<Expression<Func<AppointmentDocumentType, bool>>>(0).Compile()).ToList()));

        return new Harness
        {
            Resolver = new DocumentListResolver(documentRepository, packetRepository, typeRepository),
            Types = typeRepository,
        };
    }

    private static Appointment TheAppointment() =>
        new(
            AppointmentId,
            patientId: new Guid("c7d8eff9-0a1b-4d2c-b7d8-c3d4e5f6071e"),
            identityUserId: null,
            appointmentTypeId: new Guid("d8e9f00a-1b2c-4e3d-88e9-d4e5f607182f"),
            locationId: new Guid("e9f0011b-2c3d-4f4e-99f0-e5f607182930"),
            doctorAvailabilityId: new Guid("f001122c-3d4e-405f-aa01-f60718293a41"),
            appointmentDate: new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A90091",
            appointmentStatus: AppointmentStatusType.Approved,
            panelNumber: "TEST-PANEL")
        {
            TenantId = Office,
        };

    [Fact]
    public async Task OneCategorisedDocument_IsLabelledWithItsCategory_UnderItsOffice()
    {
        var document = Document(typeId: MedicalRecordsTypeId, otherTypeName: "TEST-ignored free text");
        var h = Build(document);

        var entry = await h.Resolver.ResolveDocumentAsync(document.Id, Office);

        entry.ShouldNotBeNull();
        entry.Id.ShouldBe(document.Id);
        entry.DocumentType.ShouldBe("TEST-Medical Records");
        entry.ObjectKey.ShouldBe(ObjectKeyBuilder.BuildFullyQualifiedKey(Office, document.BlobName));
    }

    [Fact]
    public async Task OneDocumentFiledUnderOther_UsesItsFreeTextLabel_WithoutReadingCategories()
    {
        var document = Document(otherTypeName: "TEST-Other label");
        var h = Build(document);

        var entry = await h.Resolver.ResolveDocumentAsync(document.Id, Office);

        entry.ShouldNotBeNull();
        entry.DocumentType.ShouldBe("TEST-Other label");
        await h.Types.DidNotReceiveWithAnyArgs().GetListAsync(default!, default, default);
    }

    [Fact]
    public async Task OneDocumentWhoseCategoryIsGone_FallsBackToItsFreeTextLabel()
    {
        var document = Document(typeId: Guid.NewGuid(), otherTypeName: "TEST-fallback label");
        var h = Build(document);

        var entry = await h.Resolver.ResolveDocumentAsync(document.Id, Office);

        entry.ShouldNotBeNull();
        entry.DocumentType.ShouldBe("TEST-fallback label");
    }

    [Fact]
    public async Task AnUnknownDocument_IsNotPublished()
    {
        // Positive control: the first Fact, the same harness with the document present.
        var h = Build(Document(typeId: MedicalRecordsTypeId));

        (await h.Resolver.ResolveDocumentAsync(Guid.NewGuid(), Office)).ShouldBeNull();
    }

    [Fact]
    public async Task APendingPlaceholder_IsNotPublished_AndNoCategoryIsRead()
    {
        // A required document is queued as a Pending row with no object behind it; publishing it
        // would hand the receiver a key that 404s.
        var placeholder = Document(typeId: MedicalRecordsTypeId, status: DocumentStatus.Pending);
        var h = Build(placeholder);

        (await h.Resolver.ResolveDocumentAsync(placeholder.Id, Office)).ShouldBeNull();
        await h.Types.DidNotReceiveWithAnyArgs().GetListAsync(default!, default, default);
    }

    [Fact]
    public async Task TheFullList_LabelsEveryCategorisedDocument_FromOneCategoryRead_AndSkipsPlaceholders()
    {
        var first = Document(typeId: MedicalRecordsTypeId);
        var second = Document(typeId: MedicalRecordsTypeId);
        var placeholder = Document(typeId: DecoyTypeId, status: DocumentStatus.Pending);
        var h = Build(first, second, placeholder);

        var entries = await h.Resolver.ResolveAsync(TheAppointment());

        entries.Select(e => e.Id).ShouldBe(new[] { first.Id, second.Id }, ignoreOrder: true);
        entries.ShouldAllBe(e => e.DocumentType == "TEST-Medical Records");
        await h.Types.Received(1).GetListAsync(
            Arg.Any<Expression<Func<AppointmentDocumentType, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
