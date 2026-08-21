namespace HealthcareSupport.CaseEvaluation.AppointmentClaimExaminers;

public static class AppointmentClaimExaminerConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentClaimExaminer." : string.Empty);
    }

    public const int NameMaxLength = 50;
    // Issue 2.3 (2026-05-12): renamed from ClaimExaminerNumberMaxLength.
    public const int SuiteMaxLength = 255;
    public const int EmailMaxLength = 255;
    public const int PhoneNumberMaxLength = 20;
    public const int FaxMaxLength = 15;
    public const int StreetMaxLength = 100;
    public const int CityMaxLength = 50;
    public const int ZipMaxLength = 10;
}
