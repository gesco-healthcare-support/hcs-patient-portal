namespace HealthcareSupport.CaseEvaluation.Appointments;

public static class AppointmentConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "Appointment." : string.Empty);
    }

    public const int PanelNumberMaxLength = 50;
    public const int RequestConfirmationNumberMaxLength = 50;
    public const int InternalUserCommentsMaxLength = 250;
    public const int PartyEmailMaxLength = 256;
}