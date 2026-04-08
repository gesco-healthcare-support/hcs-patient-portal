namespace HealthcareSupport.CaseEvaluation.AppointmentTypes;

public static class AppointmentTypeConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentType." : string.Empty);
    }

    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 200;
}