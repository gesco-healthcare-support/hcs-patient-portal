using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// OLD-verbatim email subjects for the demo-critical lifecycle codes.
/// Subjects are short one-liners so a const-string map is the right
/// shape (versus the multi-line HTML bodies, which live as embedded
/// .html resources -- see <see cref="EmailBodyResources"/>).
///
/// <para>The bracketed patient-details prefix that OLD constructs at
/// <c>AppointmentDocumentDomain.cs</c>:921
/// (<c>"(" + patientName + injuryDetails + ")"</c>) is expressed via the
/// <c>##PatientDetailsSubject##</c> token so the dispatcher can do the
/// same substitution as OLD without us encoding the bracket-and-dash
/// concatenation logic per-template.</para>
///
/// <para><b>Bug-fix from OLD:</b> the registration subject in OLD reads
/// <c>"Your have registered successfully"</c> (sic). Corrected here to
/// <c>"You have registered successfully"</c> per CLAUDE.md
/// "Clear bug -- fix it" rule.</para>
/// </summary>
internal static class EmailSubjects
{
    /// <summary>
    /// OLD <c>UserDomain.cs</c>:321. Typo "Your" -> "You" fixed.
    /// </summary>
    public const string UserRegistered =
        "You have registered successfully - Patient Appointment portal";

    /// <summary>OLD <c>AppointmentDomain.cs</c>:926.</summary>
    public const string PatientAppointmentPending =
        "Patient Appointment Portal - ##PatientDetailsSubject## - Your appointment request has been Pending.";

    /// <summary>OLD <c>AppointmentDomain.cs</c>:940.</summary>
    public const string PatientAppointmentApproveReject =
        "Patient Appointment Portal - ##PatientDetailsSubject## - Approve or Reject New Appointment Request";

    /// <summary>OLD <c>AppointmentDomain.cs</c>:957.</summary>
    public const string PatientAppointmentApprovedInternal =
        "Patient Appointment Portal - ##PatientDetailsSubject## - Your appointment request has been approved successfully.";

    /// <summary>
    /// OLD <c>AppointmentDomain.cs</c>:970. Same string as the Internal-side
    /// variant -- OLD uses identical subject for both branches.
    /// </summary>
    public const string PatientAppointmentApprovedExt =
        "Patient Appointment Portal - ##PatientDetailsSubject## - Your appointment request has been approved successfully.";

    /// <summary>OLD <c>AppointmentDomain.cs</c>:985.</summary>
    public const string PatientAppointmentRejected =
        "Patient Appointment Portal - ##PatientDetailsSubject## - Your appointment request has been rejected by our clinic staff.";

    /// <summary>
    /// Single source of truth for the per-code subject lookup. The seed
    /// contributor and any future migration walk this map; codes without
    /// an entry fall back to a stub subject.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ByCode =
        new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            [NotificationTemplateConsts.Codes.UserRegistered] = UserRegistered,
            [NotificationTemplateConsts.Codes.PatientAppointmentPending] = PatientAppointmentPending,
            [NotificationTemplateConsts.Codes.PatientAppointmentApproveReject] = PatientAppointmentApproveReject,
            [NotificationTemplateConsts.Codes.PatientAppointmentApprovedInternal] = PatientAppointmentApprovedInternal,
            [NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt] = PatientAppointmentApprovedExt,
            [NotificationTemplateConsts.Codes.PatientAppointmentRejected] = PatientAppointmentRejected,
        };
}
