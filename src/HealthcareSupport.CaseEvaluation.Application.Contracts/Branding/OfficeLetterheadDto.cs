namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// The current office's packet letterhead for the editor: what the office stored (null =
/// not set) plus the three DERIVED defaults a blank field falls back to, so the editor can
/// show what will actually print instead of an empty box.
/// </summary>
public class OfficeLetterheadDto
{
    public string? LetterheadName { get; set; }
    public string? LetterheadTagline { get; set; }
    public string? PhysicianName { get; set; }
    public string? PracticeName { get; set; }
    public string? MailingStreet { get; set; }
    public string? MailingCity { get; set; }
    public string? MailingState { get; set; }
    public string? MailingZip { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? RecordsDeliveryAddress { get; set; }
    public string? RecordsReleaseAddress { get; set; }
    public decimal? MissedAppointmentFee { get; set; }

    /// <summary>Printed when <see cref="PhysicianName"/> is blank: "Dr. {First} {Last}" from the office's doctor.</summary>
    public string DefaultPhysicianName { get; set; } = string.Empty;

    /// <summary>Printed when <see cref="LetterheadName"/> is blank: the effective physician name.</summary>
    public string DefaultLetterheadName { get; set; } = string.Empty;

    /// <summary>Printed when <see cref="PracticeName"/> is blank: the office display name.</summary>
    public string DefaultPracticeName { get; set; } = string.Empty;
}
