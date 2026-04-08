namespace HealthcareSupport.CaseEvaluation.Patients;

public static class PatientConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "Patient." : string.Empty);
    }

    public const int FirstNameMaxLength = 50;
    public const int LastNameMaxLength = 50;
    public const int MiddleNameMaxLength = 50;
    public const int EmailMaxLength = 50;
    public const int PhoneNumberMaxLength = 20;
    public const int SocialSecurityNumberMaxLength = 20;
    public const int AddressMaxLength = 100;
    public const int CityMaxLength = 50;
    public const int ZipCodeMaxLength = 15;
    public const int RefferedByMaxLength = 50;
    public const int CellPhoneNumberMaxLength = 12;
    public const int StreetMaxLength = 255;
    public const int InterpreterVendorNameMaxLength = 255;
    public const int ApptNumberMaxLength = 100;
    public const int OthersLanguageNameMaxLength = 100;
}