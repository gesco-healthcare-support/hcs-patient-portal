namespace HealthcareSupport.CaseEvaluation.Locations;

public static class LocationConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "Location." : string.Empty);
    }

    public const int NameMaxLength = 50;
    public const int AddressMaxLength = 100;
    public const int CityMaxLength = 50;
    public const int ZipCodeMaxLength = 15;
}