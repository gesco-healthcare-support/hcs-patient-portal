namespace HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

public static class ApplicantAttorneyConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "ApplicantAttorney." : string.Empty);
    }

    public const int FirmNameMaxLength = 50;
    public const int FirmAddressMaxLength = 100;
    public const int WebAddressMaxLength = 100;
    public const int PhoneNumberMaxLength = 20;
    public const int FaxNumberMaxLength = 19;
    public const int StreetMaxLength = 255;
    public const int CityMaxLength = 50;
    public const int ZipCodeMaxLength = 10;
}