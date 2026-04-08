namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public static class AppointmentEmployerDetailConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentEmployerDetail." : string.Empty);
    }

    public const int EmployerNameMaxLength = 255;
    public const int OccupationMaxLength = 255;
    public const int PhoneNumberMaxLength = 12;
    public const int StreetMaxLength = 255;
    public const int CityMaxLength = 255;
    public const int ZipCodeMaxLength = 10;
}