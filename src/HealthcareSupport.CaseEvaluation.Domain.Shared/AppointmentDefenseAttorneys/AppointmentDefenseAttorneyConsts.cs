namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

public static class AppointmentDefenseAttorneyConsts
{
    private const string DefaultSorting = "{0}Id asc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentDefenseAttorney." : string.Empty);
    }
}
