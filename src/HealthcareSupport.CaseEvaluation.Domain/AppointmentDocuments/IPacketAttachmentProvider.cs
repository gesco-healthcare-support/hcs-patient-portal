namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Email-side integration surface for the packet generator. The email
/// session calls this to fetch attachment bytes when sending an
/// appointment-approved email, and reports back via
/// <see cref="NotifySendCompletedAsync"/> once the send completes.
///
/// <para>Retention parity (2026-06-09): all three packet kinds -- Patient,
/// Doctor, and Attorney/Claim-Examiner -- persist after generation. They
/// are stored in the appointment-packets container, listed in the
/// appointment view, and downloadable subject to the per-role allow-list
/// in <c>PacketVisibility</c> (internal -> all three; patient -> Patient;
/// applicant/defense attorney + claim examiner -> Attorney/CE). The AttyCE
/// packet was previously pruned on a successful send; that prune was
/// removed so the packet stays available like the other two.</para>
/// </summary>
public interface IPacketAttachmentProvider
{
    /// <summary>
    /// Returns the rendered packet bytes for an (appointment, kind)
    /// pair. Returns <c>null</c> when the row does not exist or has not
    /// reached the Generated state. Caller is responsible for retrying
    /// or surfacing the wait-for-generation case.
    /// </summary>
    Task<PacketAttachment?> GetAttachmentAsync(Guid appointmentId, PacketKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Email session callback invoked after each send. No longer prunes any
    /// packet -- all three kinds are retained (see the interface remarks),
    /// so this is a no-op hook kept only to keep the send job's call site
    /// valid. Remove the call + this method in a dedicated cleanup if no
    /// per-send bookkeeping is ever added.
    /// </summary>
    Task NotifySendCompletedAsync(Guid packetId, bool success, CancellationToken cancellationToken = default);
}

/// <summary>
/// Email attachment payload. <see cref="ContentType"/> is the MIME type
/// (currently <c>application/vnd.openxmlformats-officedocument.wordprocessingml.document</c>
/// for Phase 1 DOCX output; Phase 2 PDF conversion swaps it to
/// <c>application/pdf</c>).
/// </summary>
public sealed record PacketAttachment(byte[] Bytes, string FileName, string ContentType);
