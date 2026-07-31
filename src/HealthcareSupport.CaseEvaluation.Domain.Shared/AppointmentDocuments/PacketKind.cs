namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Per-template generated packet kind. Mirrors OLD's actual production
/// runtime, which ships exactly 3 templates: PATIENT PACKET NEW (to the
/// patient), DOCTOR PACKET (storage-only, in-office printable), and the
/// unified ATTORNEY CLAIM EXAMINER PACKET (sent to every active claim
/// examiner + every defense attorney + every patient attorney on
/// PQME/AME-type appointments).
///
/// One persistence row per (TenantId, AppointmentId, Kind) tuple. The
/// AttorneyClaimExaminer row holds a single rendered DOCX that the
/// email session attaches to every atty/CE recipient send -- it is not
/// persisted per recipient.
///
/// Numeric values start at 1 to mirror PacketGenerationStatus and avoid
/// the C# default(int) = 0 trap that would map an unset enum to a valid
/// value.
/// </summary>
public enum PacketKind
{
    Patient = 1,
    Doctor = 2,
    AttorneyClaimExaminer = 3,
}
