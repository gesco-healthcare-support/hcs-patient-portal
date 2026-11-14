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

    // Whole-case entities (the per-case page only; the global list keeps scanning the intake five).
    public const string EmployerDetail = "HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails.AppointmentEmployerDetail";
    public const string ApplicantAttorneyLink = "HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys.AppointmentApplicantAttorney";
    public const string DefenseAttorneyLink = "HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys.AppointmentDefenseAttorney";
    public const string Accessor = "HealthcareSupport.CaseEvaluation.AppointmentAccessors.AppointmentAccessor";
    public const string Document = "HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentDocument";
    public const string Packet = "HealthcareSupport.CaseEvaluation.AppointmentDocuments.AppointmentPacket";
    public const string InfoRequest = "HealthcareSupport.CaseEvaluation.AppointmentInfoRequests.AppointmentInfoRequest";
    public const string ChangeRequest = "HealthcareSupport.CaseEvaluation.AppointmentChangeRequests.AppointmentChangeRequest";
    public const string ChangeRequestDocument = "HealthcareSupport.CaseEvaluation.AppointmentChangeRequests.AppointmentChangeRequestDocument";
    public const string ConsentRound = "HealthcareSupport.CaseEvaluation.AppointmentChangeRequests.ChangeRequestConsentRound";

    private static readonly Dictionary<string, string> Labels = new()
    {
        [Appointment] = "Appointment",
        [InjuryDetail] = "Injury Detail",
        [BodyPart] = "Body Part",
        [ClaimExaminer] = "Claim Examiner",
        [PrimaryInsurance] = "Primary Insurance",
        [EmployerDetail] = "Employer Detail",
        [ApplicantAttorneyLink] = "Applicant Attorney",
        [DefenseAttorneyLink] = "Defense Attorney",
        [Accessor] = "Authorized User",
        [Document] = "Document",
        [Packet] = "Packet",
        [InfoRequest] = "Information Request",
        [ChangeRequest] = "Change Request",
        [ChangeRequestDocument] = "Change Request Document",
        [ConsentRound] = "Consent Round",
    };

    /// <summary>All audited intake entity type names, for the global cross-type scan.</summary>
    public static IReadOnlyList<string> All { get; } =
        new[] { Appointment, InjuryDetail, BodyPart, ClaimExaminer, PrimaryInsurance };

    /// <summary>
    /// Every audited entity that belongs to one appointment, for the per-case page: the intake five plus
    /// documents, change requests, consent rounds, info requests, authorized users and the party rows.
    /// </summary>
    public static IReadOnlyList<string> PerCase { get; } =
        new[]
        {
            Appointment, InjuryDetail, BodyPart, ClaimExaminer, PrimaryInsurance, EmployerDetail,
            ApplicantAttorneyLink, DefenseAttorneyLink, Accessor, Document, Packet, InfoRequest,
            ChangeRequest, ChangeRequestDocument, ConsentRound,
        };

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
