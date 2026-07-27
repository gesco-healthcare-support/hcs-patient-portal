using System;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the document/packet wire mapping and -- more importantly -- the fetchability rule.
/// Publishing a row that has no object in MinIO would hand the Case Tracker a key that 404s, so what
/// gets OMITTED matters as much as what gets sent. All fixture data is synthetic.
/// </summary>
public class DocumentEntryMapperTests
{
    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");
    private static readonly Guid UploaderId = new("11111111-2222-3333-4444-555555555555");

    private static AppointmentDocument NewDocument(
        string blobName = "tenantseg/apptseg/f97796c9365b4ad3a16408f72981cae3",
        DocumentStatus status = DocumentStatus.Accepted,
        string? contentType = "application/pdf",
        string fileName = "records.pdf") =>
        new(
            Guid.NewGuid(),
            TenantId,
            AppointmentId,
            documentName: "Medical records",
            fileName: fileName,
            blobName: blobName,
            contentType: contentType,
            fileSize: 1024,
            uploadedByUserId: UploaderId)
        { Status = status };

    private static AppointmentPacket NewPacket(
        PacketKind kind = PacketKind.Patient,
        PacketGenerationStatus status = PacketGenerationStatus.Generated) =>
        new(
            Guid.NewGuid(),
            TenantId,
            AppointmentId,
            kind,
            blobName: "tenantseg/apptseg/packet/patient/228d6bed62e04be7b1146e58629bf901.pdf",
            status: status);

    // -- Fetchability ------------------------------------------------------

    [Fact]
    public void AcceptedDocument_WithARealBlob_IsFetchable()
    {
        DocumentEntryMapper.IsFetchable(NewDocument()).ShouldBeTrue();
    }

    [Fact]
    public void PendingDocument_IsNotFetchable()
    {
        // Queued at booking as a placeholder; there is no object until the patient uploads.
        var queued = AppointmentDocument.CreateQueued(
            Guid.NewGuid(), TenantId, AppointmentId, "Consent form", Guid.NewGuid());

        DocumentEntryMapper.IsFetchable(queued).ShouldBeFalse();
    }

    [Fact]
    public void DocumentWithThePlaceholderBlob_IsNotFetchable()
    {
        var doc = NewDocument(blobName: DocumentEntryMapper.PendingUploadPlaceholder);

        DocumentEntryMapper.IsFetchable(doc).ShouldBeFalse();
    }

    [Fact]
    public void RejectedDocument_IsStillFetchable()
    {
        // A rejected document has real bytes; the receiver is told the status and decides.
        DocumentEntryMapper.IsFetchable(NewDocument(status: DocumentStatus.Rejected)).ShouldBeTrue();
    }

    [Theory]
    [InlineData(PacketGenerationStatus.Generating)]
    [InlineData(PacketGenerationStatus.Failed)]
    public void PacketNotYetGenerated_IsNotFetchable(PacketGenerationStatus status)
    {
        DocumentEntryMapper.IsFetchable(NewPacket(status: status)).ShouldBeFalse();
    }

    [Fact]
    public void GeneratedPacket_IsFetchable()
    {
        DocumentEntryMapper.IsFetchable(NewPacket()).ShouldBeTrue();
    }

    // -- Mapping -----------------------------------------------------------

    [Fact]
    public void FromDocument_MapsTheWireShape_AndQualifiesTheObjectKey()
    {
        var doc = NewDocument();

        var entry = DocumentEntryMapper.FromDocument(doc, "Medical Records", TenantId);

        entry.Id.ShouldBe(doc.Id);
        entry.Source.ShouldBe("document");
        entry.Kind.ShouldBeNull();
        entry.FileName.ShouldBe("records.pdf");
        entry.ContentType.ShouldBe("application/pdf");
        entry.FileSize.ShouldBe(1024);
        entry.Status.ShouldBe("Accepted");
        entry.DocumentType.ShouldBe("Medical Records");
        entry.ObjectKey.ShouldStartWith("tenants/b8844bba-414c-e238-4a71-3a22841f21af/");
        entry.UpdatedAt.ShouldEndWith("Z");
    }

    [Fact]
    public void FromDocument_PassesThroughANullContentType()
    {
        // Content type is client-supplied on upload, so it can legitimately be absent.
        var entry = DocumentEntryMapper.FromDocument(NewDocument(contentType: null), null, TenantId);

        entry.ContentType.ShouldBeNull();
        entry.DocumentType.ShouldBeNull();
    }

    [Fact]
    public void FromDocument_MapsAnImageUpload()
    {
        // Uploads are restricted to pdf/jpg/jpeg/png -- not everything is a PDF.
        var entry = DocumentEntryMapper.FromDocument(
            NewDocument(contentType: "image/jpeg", fileName: "scan.jpg"), null, TenantId);

        entry.ContentType.ShouldBe("image/jpeg");
        entry.FileName.ShouldBe("scan.jpg");
    }

    [Fact]
    public void FromPacket_AlwaysClaimsPdf_AndOmitsFileSize()
    {
        var packet = NewPacket();

        var entry = DocumentEntryMapper.FromPacket(packet, "A00065", TenantId);

        entry.Source.ShouldBe("packet");
        entry.Kind.ShouldBe("Patient");
        entry.ContentType.ShouldBe("application/pdf");
        entry.FileSize.ShouldBeNull();
        entry.Status.ShouldBe("Generated");
        entry.DocumentName.ShouldBe("Patient Packet");
        entry.FileName.ShouldStartWith("A00065_Patient Packet_");
        entry.FileName.ShouldEndWith(".pdf");
    }

    [Theory]
    [InlineData(PacketKind.Patient, "Patient Packet")]
    [InlineData(PacketKind.Doctor, "Doctor Packet")]
    [InlineData(PacketKind.AttorneyClaimExaminer, "Attorney Claim Examiner Packet")]
    public void PacketLabel_MatchesTheEmailAttachmentNaming(PacketKind kind, string expected)
    {
        DocumentEntryMapper.PacketLabel(kind).ShouldBe(expected);
    }
}
