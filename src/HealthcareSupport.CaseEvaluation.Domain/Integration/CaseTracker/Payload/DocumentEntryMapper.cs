using System;
using System.Globalization;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Pure mapping between portal document/packet rows and their wire entries, plus the
/// fetchability rule. Separated from <see cref="DocumentListResolver"/>'s repository I/O so the
/// decisions that actually matter -- what to omit, what content type a packet claims -- are
/// unit-testable without a database.
/// </summary>
public static class DocumentEntryMapper
{
    public const string DocumentSource = "document";
    public const string PacketSource = "packet";

    /// <summary>Packets are always rendered PDFs, whatever two legacy read paths still claim.</summary>
    public const string PacketContentType = "application/pdf";

    /// <summary>
    /// Placeholder blob written by <c>AppointmentDocument.CreateQueued</c> for a required document
    /// that has not been uploaded yet. Duplicated here because it is a local const there; the
    /// status check below is the primary signal, this is belt-and-braces.
    /// </summary>
    public const string PendingUploadPlaceholder = "(pending-upload)";

    /// <summary>
    /// True when the row has real bytes in MinIO. A <see cref="DocumentStatus.Pending"/> row is a
    /// queued placeholder with no object, so publishing it would hand the receiver a key that
    /// 404s. Rejected rows ARE fetchable and are published with their status so the receiver can
    /// decide what to show.
    /// </summary>
    public static bool IsFetchable(AppointmentDocument document)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        return document.Status != DocumentStatus.Pending
            && !string.IsNullOrWhiteSpace(document.BlobName)
            && !string.Equals(document.BlobName, PendingUploadPlaceholder, StringComparison.Ordinal);
    }

    /// <summary>True only once the render finished; earlier states have no object to fetch.</summary>
    public static bool IsFetchable(AppointmentPacket packet)
    {
        if (packet is null)
        {
            throw new ArgumentNullException(nameof(packet));
        }

        return packet.Status == PacketGenerationStatus.Generated
            && !string.IsNullOrWhiteSpace(packet.BlobName);
    }

    /// <summary>Human label for a packet kind. Mirrors <c>PacketAttachmentProvider</c>'s strings.</summary>
    public static string PacketLabel(PacketKind kind) => kind switch
    {
        PacketKind.Patient => "Patient Packet",
        PacketKind.Doctor => "Doctor Packet",
        PacketKind.AttorneyClaimExaminer => "Attorney Claim Examiner Packet",
        _ => kind.ToString(),
    };

    /// <summary>
    /// Synthesizes a packet file name; the portal stores none. Mirrors
    /// <c>PacketAttachmentProvider.BuildFileName</c> verbatim (including its 12-hour <c>hhmmss</c>
    /// stamp) so a packet downloaded from an email and the same packet referenced through the
    /// integration carry the same name.
    /// </summary>
    public static string PacketFileName(string confirmationNumber, PacketKind kind, DateTime generatedAt)
    {
        var timestamp = generatedAt.ToString("ddMMyyyy_hhmmss", CultureInfo.InvariantCulture);
        return $"{confirmationNumber}_{PacketLabel(kind)}_{timestamp}.pdf";
    }

    /// <summary>Maps an uploaded document. <paramref name="documentType"/> is the resolved category label.</summary>
    public static IntakeDocumentEntry FromDocument(AppointmentDocument document, string? documentType, Guid? tenantId)
    {
        if (document is null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        return new IntakeDocumentEntry
        {
            Id = document.Id,
            Source = DocumentSource,
            Kind = null,
            DocumentName = document.DocumentName,
            FileName = document.FileName,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            Status = document.Status.ToString(),
            ObjectKey = ObjectKeyBuilder.BuildFullyQualifiedKey(tenantId, document.BlobName),
            CreatedAtUtc = IntegrationTimestamp.ToIsoUtc(document.CreationTime),
            UpdatedAt = IntegrationTimestamp.ToIsoUtc(document.LastModificationTime ?? document.CreationTime),
            DocumentType = documentType,
        };
    }

    /// <summary>
    /// Maps a generated packet. <c>FileSize</c> is null because <c>AppointmentPacket</c> stores no
    /// size and stat-ing MinIO for a display field is not worth a round trip per packet.
    /// </summary>
    public static IntakeDocumentEntry FromPacket(AppointmentPacket packet, string confirmationNumber, Guid? tenantId)
    {
        if (packet is null)
        {
            throw new ArgumentNullException(nameof(packet));
        }

        return new IntakeDocumentEntry
        {
            Id = packet.Id,
            Source = PacketSource,
            Kind = packet.Kind.ToString(),
            DocumentName = PacketLabel(packet.Kind),
            FileName = PacketFileName(confirmationNumber, packet.Kind, packet.GeneratedAt),
            ContentType = PacketContentType,
            FileSize = null,
            Status = packet.Status.ToString(),
            ObjectKey = ObjectKeyBuilder.BuildFullyQualifiedKey(tenantId, packet.BlobName),
            CreatedAtUtc = IntegrationTimestamp.ToIsoUtc(packet.GeneratedAt),
            UpdatedAt = IntegrationTimestamp.ToIsoUtc(
                packet.RegeneratedAt ?? packet.LastModificationTime ?? packet.GeneratedAt),
            DocumentType = null,
        };
    }
}
