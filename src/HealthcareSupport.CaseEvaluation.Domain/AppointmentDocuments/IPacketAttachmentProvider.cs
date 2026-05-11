namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Email-side integration surface for the packet generator. The email
/// session calls this to fetch attachment bytes when sending an
/// appointment-approved email, and reports back via
/// <see cref="NotifySendCompletedAsync"/> so the AttorneyClaimExaminer
/// rows can be pruned on success per Adrian's "AttyCE-on-failure-only"
/// retention rule.
///
/// <para>Patient + Doctor packets persist forever (the patient sees the
/// rendered Patient Packet in their documents list, and internal staff
/// can re-print the Doctor Packet at any time). The atty/CE packet is
/// transient -- once email send succeeds it is removed; if the send
/// fails, the row stays so the office can re-trigger or download the
/// missed attachment manually.</para>
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
    /// Email session callback. On (kind=AttorneyClaimExaminer, success=true)
    /// the underlying row + blob are deleted. On any other combination
    /// (Patient/Doctor regardless of success, or AttyCE on failure) the
    /// call is a no-op so the row remains available for retry / manual
    /// re-download.
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
