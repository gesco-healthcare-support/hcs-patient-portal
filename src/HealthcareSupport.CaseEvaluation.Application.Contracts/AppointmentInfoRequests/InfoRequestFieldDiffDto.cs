namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// One flagged scalar field's before-&gt;after change for the staff diff (Branch 2).
/// <see cref="Key"/> mirrors the frontend send-back-fields registry; the Angular
/// card maps it to a label. SSN values are already masked in the snapshot.
/// <see cref="Changed"/> is true only when an after-value exists and differs.
/// </summary>
public class InfoRequestFieldDiffDto
{
    public string Key { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public bool Changed { get; set; }
}
