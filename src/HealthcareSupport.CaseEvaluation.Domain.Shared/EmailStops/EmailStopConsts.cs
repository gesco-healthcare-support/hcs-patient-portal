namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>2026-11-05 -- field limits shared by the two email-stop tables and their DTOs.</summary>
public static class EmailStopConsts
{
    public const int EmailMaxLength = 256;
    public const int NoteMaxLength = 256;
    public const int NameMaxLength = 256;
    public const int OfficeNameMaxLength = 128;

    /// <summary>Upper bound on the "other cases" list of the all-cases consent warning.</summary>
    public const int ConsentImpactMaxOtherCases = 20;
}
