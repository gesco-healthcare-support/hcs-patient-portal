namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

public static class AppointmentLanguageConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentLanguage." : string.Empty);
    }

    public const int NameMaxLength = 50;
}