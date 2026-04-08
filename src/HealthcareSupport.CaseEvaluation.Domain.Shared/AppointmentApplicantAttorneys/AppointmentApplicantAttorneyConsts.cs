namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public static class AppointmentApplicantAttorneyConsts
{
    private const string DefaultSorting = "{0}Id asc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentApplicantAttorney." : string.Empty);
    }
}