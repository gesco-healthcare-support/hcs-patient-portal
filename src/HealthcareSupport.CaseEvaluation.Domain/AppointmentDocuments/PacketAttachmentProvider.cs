using System.Globalization;
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
/// <c>{ConfirmationNumber}_{KindName}_{ddMMyyyy_hhmmss}.pdf</c>.
///
/// <para>OLD source pattern: <c>AppointmentDocumentDomain.cs:519, :612</c>
/// (Patient + Doctor) where the rendered DOCX is saved to disk with
/// <c>RequestConfirmationNumber + "_Patient Packet_" + ddMMyyyy_hhmmss + ".docx"</c>.
/// Phase 2 (2026-05-11) replaced the DOCX output with PDF after Gotenberg
/// conversion; the filename pattern stays OLD-verbatim except the
/// extension. Recipients see the same naming, just immutable PDFs
/// instead of editable DOCX.</para>
/// </summary>
public class PacketAttachmentProvider : IPacketAttachmentProvider, ITransientDependency
{
    /// <summary>PDF MIME type. Phase 2 (2026-05-11) replaced the DOCX
    /// output after Gotenberg DOCX -> PDF conversion landed -- the
    /// packet blob is now always a PDF.</summary>
    public const string PdfContentType = "application/pdf";

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

        return new PacketAttachment(bytes, fileName, PdfContentType);
    }

    public virtual Task NotifySendCompletedAsync(Guid packetId, bool success, CancellationToken cancellationToken = default)
    {
        // Retention parity (2026-06-09): the Attorney/Claim-Examiner packet is
        // now retained exactly like the Patient and Doctor packets -- stored in
        // the appointment-packets blob container, listed in the appointment
        // view, and downloadable by every viewer. This callback previously
        // pruned the AttyCE row + blob on a successful send (transient-by-design
        // per the earlier "AttyCE-on-failure-only" retention rule). That prune
        // is removed; all three packet kinds now persist, so the callback is
        // intentionally a no-op. It stays on the interface because
        // SendAppointmentEmailJob still invokes it after each send -- removing
        // the call + the whole notify mechanism is a separate cleanup.
        return Task.CompletedTask;
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
        return $"{confirmation}_{kindName}_{timestamp}.pdf";
    }
}
