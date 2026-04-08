namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

public static class AppointmentStatusConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentStatus." : string.Empty);
    }

    public const int NameMaxLength = 100;
}