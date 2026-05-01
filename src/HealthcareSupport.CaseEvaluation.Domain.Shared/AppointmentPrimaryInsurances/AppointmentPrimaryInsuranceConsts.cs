namespace HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

public static class AppointmentPrimaryInsuranceConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentPrimaryInsurance." : string.Empty);
    }

    public const int NameMaxLength = 50;
    public const int InsuranceNumberMaxLength = 255;
    public const int AttentionMaxLength = 255;
    public const int PhoneNumberMaxLength = 12;
    public const int FaxNumberMaxLength = 20;
    public const int StreetMaxLength = 255;
    public const int CityMaxLength = 50;
    public const int ZipMaxLength = 10;
}
