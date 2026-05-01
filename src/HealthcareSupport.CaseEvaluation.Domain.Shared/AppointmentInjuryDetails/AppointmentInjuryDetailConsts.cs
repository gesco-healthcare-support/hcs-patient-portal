namespace HealthcareSupport.CaseEvaluation.AppointmentInjuryDetails;

public static class AppointmentInjuryDetailConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentInjuryDetail." : string.Empty);
    }

    public const int ClaimNumberMaxLength = 50;
    public const int WcabAdjMaxLength = 50;
    public const int BodyPartsSummaryMaxLength = 500;
}
