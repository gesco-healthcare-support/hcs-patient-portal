namespace HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

public static class ApplicantAttorneyConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "ApplicantAttorney." : string.Empty);
    }

    public const int FirstNameMaxLength = 50;
    public const int LastNameMaxLength = 50;
    // 2026-10-27: was 50. Real firm names ("Law Offices of ... & Associates, A Professional Corporation")
    // run longer, and the invite and sign-up already take 256, so an invited firm over 50 failed sign-up.
    // The attorney record and the appointment's copy of it are widened together (both migration sets).
    public const int FirmNameMaxLength = 256;
    public const int FirmAddressMaxLength = 100;
    public const int WebAddressMaxLength = 100;
    public const int PhoneNumberMaxLength = 20;
    public const int FaxNumberMaxLength = 19;
    public const int StreetMaxLength = 255;
    public const int CityMaxLength = 50;
    public const int ZipCodeMaxLength = 10;
    public const int EmailMaxLength = 100;
}