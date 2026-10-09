namespace HealthcareSupport.CaseEvaluation.Branding;

/// <summary>
/// Field lengths for an office's packet letterhead. In Domain.Shared (not on the entity,
/// unlike the older branding lengths) because the update DTO in Application.Contracts
/// validates against the same numbers and cannot reference the Domain project.
/// </summary>
public static class OfficeLetterheadConsts
{
    public const int NameMaxLength = 128;
    public const int StreetMaxLength = 128;
    public const int CityMaxLength = 64;
    public const int StateMaxLength = 32;
    public const int ZipMaxLength = 16;
    public const int PhoneMaxLength = 32;
    public const int AddressMaxLength = 256;

    /// <summary>Upper bound on the missed-appointment fee, a guard against a stray keystroke.</summary>
    public const double MaxMissedAppointmentFee = 100000;
}
