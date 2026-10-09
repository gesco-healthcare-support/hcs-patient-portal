using System.ComponentModel.DataAnnotations;

namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Replaces the current office's packet letterhead. Every field is optional; a blank one
/// clears the stored value and the packet falls back to the derived default (see
/// <see cref="OfficeLetterheadDto"/>). Lengths are a first-line 400; the entity re-checks.
/// </summary>
public class UpdateOfficeLetterheadInput
{
    [StringLength(OfficeLetterheadConsts.NameMaxLength)]
    public string? LetterheadName { get; set; }

    [StringLength(OfficeLetterheadConsts.NameMaxLength)]
    public string? LetterheadTagline { get; set; }

    [StringLength(OfficeLetterheadConsts.NameMaxLength)]
    public string? PhysicianName { get; set; }

    [StringLength(OfficeLetterheadConsts.NameMaxLength)]
    public string? PracticeName { get; set; }

    [StringLength(OfficeLetterheadConsts.StreetMaxLength)]
    public string? MailingStreet { get; set; }

    [StringLength(OfficeLetterheadConsts.CityMaxLength)]
    public string? MailingCity { get; set; }

    [StringLength(OfficeLetterheadConsts.StateMaxLength)]
    public string? MailingState { get; set; }

    [StringLength(OfficeLetterheadConsts.ZipMaxLength)]
    public string? MailingZip { get; set; }

    [StringLength(OfficeLetterheadConsts.PhoneMaxLength)]
    public string? Phone { get; set; }

    [StringLength(OfficeLetterheadConsts.PhoneMaxLength)]
    public string? Fax { get; set; }

    [StringLength(OfficeLetterheadConsts.AddressMaxLength)]
    public string? RecordsDeliveryAddress { get; set; }

    [StringLength(OfficeLetterheadConsts.AddressMaxLength)]
    public string? RecordsReleaseAddress { get; set; }

    [Range(0, OfficeLetterheadConsts.MaxMissedAppointmentFee)]
    public decimal? MissedAppointmentFee { get; set; }
}
