using HealthcareSupport.CaseEvaluation.AppointmentDocuments;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 4 (Category 4, 2026-05-10) -- published by
/// <c>GenerateAppointmentPacketJob</c> after each <see cref="PacketKind"/>
/// reaches <c>PacketGenerationStatus.Generated</c>. Subscribers:
/// <c>PatientPacketEmailHandler</c> (Patient kind) and
/// <c>AttyCEPacketEmailHandler</c> (AttorneyClaimExaminer kind).
///
/// <para>Mirrors OLD <c>AppointmentDocumentDomain.AddAppointmentDocumentsAndSendDocumentToEmail</c>
/// fan-out at <c>:463-859</c>. Each handler resolves its own recipient
/// set and dispatches <c>AppointmentDocumentAddWithAttachment</c> per
/// recipient with the rendered DOCX as attachment.</para>
///
/// <para>One event fires per (appointment, kind) tuple, so a single
/// appointment that gets all 3 kinds (PQME/AME) publishes 3 events.
/// Doctor kind is published too even though no handler subscribes -- the
/// row + blob remain for staff to print from the portal, matching OLD's
/// "Doctor packet is generated and stored, but NOT emailed" asymmetry at
/// <c>AppointmentDocumentDomain.cs:561-634</c>.</para>
/// </summary>
public class PacketGeneratedEto
{
    public System.Guid AppointmentId { get; set; }

    public System.Guid? TenantId { get; set; }

    public System.Guid PacketId { get; set; }

    public PacketKind Kind { get; set; }

    public System.DateTime OccurredAt { get; set; }
}
