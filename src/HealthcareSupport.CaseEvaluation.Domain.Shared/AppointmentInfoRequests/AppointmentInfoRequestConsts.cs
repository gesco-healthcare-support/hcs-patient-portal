namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// Max lengths for the Send Back / Request-more-information feature
/// (<c>AppointmentInfoRequest</c>). The staff Note is shown to the external
/// user verbatim; RequestedFields holds a JSON array of flagged field keys.
/// </summary>
public static class AppointmentInfoRequestConsts
{
    public const int NoteMaxLength = 1000;

    /// <summary>JSON array of flagged field keys (e.g. ["panelNumber","dob"]).</summary>
    public const int RequestedFieldsMaxLength = 4000;
}
