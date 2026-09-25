namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

public static class AppointmentDocumentTypeConsts
{
    private const string DefaultSorting = "{0}Name asc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "AppointmentDocumentType." : string.Empty);
    }

    public const int NameMaxLength = 100;

    /// <summary>
    /// Seeded name of the reserved system category auto-applied to generated
    /// packet documents (G-03-01 decision 4). IsSystem rows are hidden from the
    /// upload picker and cannot be edited or deleted by admins.
    /// </summary>
    public const string GeneratedPacketName = "Generated Packet";

    /// <summary>
    /// I15 (2026-06-08): seeded label that designates a document as the PQME
    /// panel strike list. The upload path sets <c>IsPanelStrikeList</c> when a
    /// document is tagged with this category, and the approval gate requires
    /// such a document before a PQME can be approved.
    /// </summary>
    public const string PanelStrikeListName = "Panel Strike List";
}
