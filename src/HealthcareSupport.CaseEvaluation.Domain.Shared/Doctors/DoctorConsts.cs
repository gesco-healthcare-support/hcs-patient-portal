namespace HealthcareSupport.CaseEvaluation.Doctors;

public static class DoctorConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "Doctor." : string.Empty);
    }

    public const int FirstNameMaxLength = 50;
    public const int LastNameMaxLength = 50;
    public const int EmailMaxLength = 49;
}