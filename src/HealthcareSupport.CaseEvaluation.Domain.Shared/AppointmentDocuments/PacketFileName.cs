using System;
using System.Globalization;
using HealthcareSupport.CaseEvaluation.Timing;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// The packet file name, built in ONE place (#621).
/// </summary>
/// <remarks>
/// <para>Three call sites produced this name independently and were required to
/// agree: <c>PacketAttachmentProvider</c> (the email attachment),
/// <c>AppointmentPacketsAppService</c> (the download), and
/// <c>DocumentEntryMapper</c> (the Case Tracker intake payload). The last one
/// said so explicitly -- "mirrors PacketAttachmentProvider.BuildFileName
/// verbatim ... so a packet downloaded from an email and the same packet
/// referenced through the integration carry the same name" -- which is a
/// requirement enforced by three people remembering it.</para>
///
/// <para>THE TIMESTAMP IS PACIFIC, NOT UTC. <c>AppointmentPacket.GeneratedAt</c>
/// is <c>DateTime.UtcNow</c>, and the name was formatted straight off it. From
/// 5pm Pacific onwards the UTC date is already tomorrow, so a packet generated
/// on the evening of the 15th was named <c>..._16062026_..._</c> -- a date the
/// office never saw, on a document that reaches the patient, the doctor,
/// opposing counsel and the WCAB.</para>
///
/// <para>THE 12-HOUR <c>hh</c> IS DELIBERATE AND IS LEFT ALONE. It is OLD-app
/// parity (<c>RequestConfirmationNumber + "_Patient Packet_" + ddMMyyyy_hhmmss</c>),
/// and it means two packets generated twelve hours apart collide in name. That
/// is a real defect inherited from OLD, but changing it is a parity decision
/// rather than a timezone fix, so it is flagged rather than silently corrected
/// -- see the bug-and-deviation policy in CLAUDE.md. Moving to Pacific narrows
/// the collision in practice, because packets are generated during office hours
/// and a single Pacific day now maps to a single date stamp.</para>
/// </remarks>
public static class PacketFileName
{
    /// <summary>Human label for a packet kind.</summary>
    public static string Label(PacketKind kind) => kind switch
    {
        PacketKind.Patient => "Patient Packet",
        PacketKind.Doctor => "Doctor Packet",
        PacketKind.AttorneyClaimExaminer => "Attorney Claim Examiner Packet",
        _ => kind.ToString(),
    };

    /// <summary>
    /// <c>{confirmation}_{Kind Label}_{ddMMyyyy_hhmmss}.pdf</c>, stamped in
    /// Pacific.
    /// </summary>
    /// <param name="confirmation">The appointment confirmation number.</param>
    /// <param name="kind">Which packet.</param>
    /// <param name="generatedAtUtc">
    /// The UTC instant the packet was generated (<c>AppointmentPacket.GeneratedAt</c>).
    /// Converted here; callers pass the raw instant rather than converting first,
    /// so there is exactly one place the conversion can be forgotten.
    /// </param>
    public static string Build(
        string confirmation,
        PacketKind kind,
        DateTime generatedAtUtc,
        string extension = "pdf")
    {
        var pacific = PacificTime.FromUtc(generatedAtUtc);
        var timestamp = pacific.ToString("ddMMyyyy_hhmmss", CultureInfo.InvariantCulture);
        return $"{confirmation}_{Label(kind)}_{timestamp}.{extension}";
    }

    /// <summary>
    /// The extension for a stored packet, derived from the blob rather than
    /// assumed. Packets have rendered as PDF since 2026-06-10, but rows written
    /// before that are DOCX, and a download named <c>.pdf</c> carrying DOCX bytes
    /// is worse than either.
    /// </summary>
    public static string ExtensionFor(string blobName) =>
        blobName != null && blobName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
            ? "docx"
            : "pdf";
}
