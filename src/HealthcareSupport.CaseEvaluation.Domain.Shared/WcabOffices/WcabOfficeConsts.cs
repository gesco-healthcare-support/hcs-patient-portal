namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public static class WcabOfficeConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "WcabOffice." : string.Empty);
    }

    public const int NameMaxLength = 50;
    public const int AbbreviationMaxLength = 50;
    public const int AddressMaxLength = 100;
    public const int CityMaxLength = 50;
    public const int ZipCodeMaxLength = 15;
}