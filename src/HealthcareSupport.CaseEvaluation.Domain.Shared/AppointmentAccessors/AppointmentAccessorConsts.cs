namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public static class AppointmentAccessorConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentAccessor." : string.Empty);
    }
}