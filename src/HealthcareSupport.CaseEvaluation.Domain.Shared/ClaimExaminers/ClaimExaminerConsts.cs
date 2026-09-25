namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

// UM3/UM4 (2026-06-05): field max-lengths for the firm-less Claim Examiner master
// (OBS-8: no firm fields). Mirrors ApplicantAttorneyConsts minus firm/web.
public static class ClaimExaminerConsts
{
    public const int FirstNameMaxLength = 50;
    public const int LastNameMaxLength = 50;
    public const int EmailMaxLength = 100;
    public const int PhoneNumberMaxLength = 20;
    public const int FaxNumberMaxLength = 19;
    public const int StreetMaxLength = 255;
    public const int CityMaxLength = 50;
    public const int ZipCodeMaxLength = 10;

    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "claimExaminer." : string.Empty);
    }
}
