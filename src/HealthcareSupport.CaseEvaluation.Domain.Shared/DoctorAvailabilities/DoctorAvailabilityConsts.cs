namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public static class DoctorAvailabilityConsts
{
    private const string DefaultSorting = "{0}AvailableDate asc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "DoctorAvailability." : string.Empty);
    }
}