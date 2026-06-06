namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// The audited entities that make up an appointment's intake, plus friendly labels
/// for the change-log "entity" column. Injury details FK directly to the appointment;
/// body parts / claim examiners / primary insurance FK to an injury detail.
/// </summary>
public static class AppointmentAuditedEntities
{
    public const string Appointment = "HealthcareSupport.CaseEvaluation.Appointments.Appointment";
    public const string InjuryDetail = "HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails.AppointmentInjuryDetail";
    public const string BodyPart = "HealthcareSupport.CaseEvaluation.AppointmentBodyParts.AppointmentBodyPart";
    public const string ClaimExaminer = "HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers.AppointmentClaimExaminer";
    public const string PrimaryInsurance = "HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances.AppointmentPrimaryInsurance";

    private static readonly Dictionary<string, string> Labels = new()
    {
        [Appointment] = "Appointment",
        [InjuryDetail] = "Injury Detail",
        [BodyPart] = "Body Part",
        [ClaimExaminer] = "Claim Examiner",
        [PrimaryInsurance] = "Primary Insurance",
    };

    /// <summary>All audited intake entity type names, for the global cross-type scan.</summary>
    public static IReadOnlyList<string> All { get; } =
        new[] { Appointment, InjuryDetail, BodyPart, ClaimExaminer, PrimaryInsurance };

    /// <summary>Friendly label for a type, falling back to the simple class name.</summary>
    public static string Label(string entityTypeFullName)
    {
        if (string.IsNullOrEmpty(entityTypeFullName))
        {
            return string.Empty;
        }
        if (Labels.TryGetValue(entityTypeFullName, out var label))
        {
            return label;
        }
        var lastDot = entityTypeFullName.LastIndexOf('.');
        return lastDot >= 0 ? entityTypeFullName[(lastDot + 1)..] : entityTypeFullName;
    }
}
