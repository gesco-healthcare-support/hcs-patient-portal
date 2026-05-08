using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using Microsoft.Extensions.Logging;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Default implementation. Reads the (appointment, kind) row, streams
/// the blob bytes, and builds the OLD-style filename
/// <c>{ConfirmationNumber}_{KindName}_{ddMMyyyy_hhmmss}.docx</c>.
///
/// <para>OLD source pattern: <c>AppointmentDocumentDomain.cs:519, :612</c>
/// (Patient + Doctor) where the rendered DOCX is saved to disk with
/// <c>RequestConfirmationNumber + "_Patient Packet_" + ddMMyyyy_hhmmss + ".docx"</c>.
/// We preserve the verbatim filename pattern so recipients see the
/// same naming as OLD.</para>
/// </summary>
public class PacketAttachmentProvider : IPacketAttachmentProvider, ITransientDependency
{
    /// <summary>DOCX MIME type per RFC 4288.</summary>
    public const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IBlobContainer<AppointmentPacketsContainer> _packetsContainer;
    private readonly ILogger<PacketAttachmentProvider> _logger;

    public PacketAttachmentProvider(
        IRepository<AppointmentPacket, Guid> packetRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IBlobContainer<AppointmentPacketsContainer> packetsContainer,
        ILogger<PacketAttachmentProvider> logger)
    {
        _packetRepository = packetRepository;
        _appointmentRepository = appointmentRepository;
        _packetsContainer = packetsContainer;
        _logger = logger;
    }

    public virtual async Task<PacketAttachment?> GetAttachmentAsync(Guid appointmentId, PacketKind kind, CancellationToken cancellationToken = default)
    {
        var queryable = await _packetRepository.GetQueryableAsync();
        var packet = queryable.FirstOrDefault(p => p.AppointmentId == appointmentId && p.Kind == kind);
        if (packet == null || packet.Status != PacketGenerationStatus.Generated)
        {
            return null;
        }

        var bytes = await _packetsContainer.GetAllBytesOrNullAsync(packet.BlobName);
        if (bytes == null)
        {
            _logger.LogWarning(
                "PacketAttachmentProvider: blob missing for packet {PacketId} ({BlobName}) -- DB row reports Generated but blob is gone.",
                packet.Id, packet.BlobName);
            return null;
        }

        var appointment = await _appointmentRepository.FindAsync(appointmentId, cancellationToken: cancellationToken);
        var confirmation = appointment?.RequestConfirmationNumber ?? appointmentId.ToString("N");
        var fileName = BuildFileName(confirmation, kind, packet.GeneratedAt);

        return new PacketAttachment(bytes, fileName, DocxContentType);
    }

    public virtual async Task NotifySendCompletedAsync(Guid packetId, bool success, CancellationToken cancellationToken = default)
    {
        var packet = await _packetRepository.FindAsync(packetId, cancellationToken: cancellationToken);
        if (packet == null)
        {
            return;
        }

        // Patient + Doctor rows always persist -- they back the patient's
        // documents UI and the staff's per-kind download list.
        if (packet.Kind != PacketKind.AttorneyClaimExaminer)
        {
            return;
        }

        // AttyCE rows persist ONLY when send fails -- gives the office a
        // manual-resend fallback. On success, prune the row + blob to
        // match Adrian's retention rule.
        if (!success)
        {
            return;
        }

        try
        {
            await _packetsContainer.DeleteAsync(packet.BlobName);
        }
        catch (Exception ex)
        {
            // Entity is the source of truth; orphan blobs are a janitor
            // concern. Log and continue with the row delete.
            _logger.LogWarning(ex,
                "PacketAttachmentProvider: failed to delete blob {BlobName} for sent AttyCE packet {PacketId}; orphan blob left for cleanup.",
                packet.BlobName, packet.Id);
        }

        await _packetRepository.DeleteAsync(packet, autoSave: true, cancellationToken: cancellationToken);
        _logger.LogInformation(
            "PacketAttachmentProvider: pruned AttyCE packet {PacketId} after successful email send.",
            packetId);
    }

    /// <summary>
    /// Builds OLD's verbatim filename pattern. KindName has spaces and
    /// matches OLD's hand-written strings at <c>AppointmentDocumentDomain.cs:520, :613</c>.
    /// </summary>
    private static string BuildFileName(string confirmation, PacketKind kind, DateTime generatedAt)
    {
        var kindName = kind switch
        {
            PacketKind.Patient => "Patient Packet",
            PacketKind.Doctor => "Doctor Packet",
            PacketKind.AttorneyClaimExaminer => "Attorney Claim Examiner Packet",
            _ => kind.ToString(),
        };
        var timestamp = generatedAt.ToString("ddMMyyyy_hhmmss", CultureInfo.InvariantCulture);
        return $"{confirmation}_{kindName}_{timestamp}.docx";
    }
}
