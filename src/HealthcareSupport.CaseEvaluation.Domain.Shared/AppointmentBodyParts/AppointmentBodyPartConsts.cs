namespace HealthcareSupport.CaseEvaluation.AppointmentBodyParts;

public static class AppointmentBodyPartConsts
{
    private const string DefaultSorting = "{0}CreationTime asc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentBodyPart." : string.Empty);
    }

    public const int BodyPartDescriptionMaxLength = 500;
}
