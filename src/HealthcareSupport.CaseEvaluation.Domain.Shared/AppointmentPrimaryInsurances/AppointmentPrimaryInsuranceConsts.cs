namespace HealthcareSupport.CaseEvaluation.AppointmentPrimaryInsurances;

public static class AppointmentPrimaryInsuranceConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentPrimaryInsurance." : string.Empty);
    }

    public const int NameMaxLength = 50;
    // Issue 2.3 (2026-05-12): renamed from InsuranceNumberMaxLength.
    public const int SuiteMaxLength = 255;
    public const int AttentionMaxLength = 255;
    public const int PhoneNumberMaxLength = 12;
    public const int FaxNumberMaxLength = 20;
    public const int StreetMaxLength = 255;
    public const int CityMaxLength = 50;
    public const int ZipMaxLength = 10;
}
