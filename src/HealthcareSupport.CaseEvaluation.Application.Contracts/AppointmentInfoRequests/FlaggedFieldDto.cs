namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// One field the staff flagged for the external user to fix, with an optional
/// hint shown next to that field on the fix-it page. <see cref="Key"/> is a
/// stable field identifier the Angular fix-it page maps to an input.
/// </summary>
public class FlaggedFieldDto
{
    public string Key { get; set; } = string.Empty;

    public string? Hint { get; set; }
}
