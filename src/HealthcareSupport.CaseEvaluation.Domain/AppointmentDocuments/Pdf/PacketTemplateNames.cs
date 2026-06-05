namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;

/// <summary>
/// The template-name CONTRACT between <see cref="Jobs.GenerateAppointmentPacketJob"/> and the
/// packet-renderer sidecar. These strings are the keys the sidecar exposes (see
/// <c>docker/packet-renderer/app.py</c> <c>_TEMPLATE_FILES</c>); keep both sides in sync.
/// AttorneyClaimExaminer has two notice variants selected by appointment type.
/// </summary>
public static class PacketTemplateNames
{
    public const string Doctor = "doctor";
    public const string Patient = "patient";

    /// <summary>Shared AME / IME attorney+patient notice (the default AttorneyCE notice).</summary>
    public const string AttorneyAme = "attorney-ame";

    /// <summary>Panel QME notice -- the AME/IME notices plus the DWC QME Appointment Notification Form.</summary>
    public const string AttorneyPqme = "attorney-pqme";
}
