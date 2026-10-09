namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// The editable letterhead fields of an office, as entered. Carries no defaults: a null or
/// blank field means "derive it" (see <see cref="OfficeLetterheadResolver"/>).
/// </summary>
public record OfficeLetterheadValues
{
    public string? LetterheadName { get; init; }
    public string? LetterheadTagline { get; init; }
    public string? PhysicianName { get; init; }
    public string? PracticeName { get; init; }
    public string? MailingStreet { get; init; }
    public string? MailingCity { get; init; }
    public string? MailingState { get; init; }
    public string? MailingZip { get; init; }
    public string? Phone { get; init; }
    public string? Fax { get; init; }
    public string? RecordsDeliveryAddress { get; init; }
    public string? RecordsReleaseAddress { get; init; }
    public decimal? MissedAppointmentFee { get; init; }
}
