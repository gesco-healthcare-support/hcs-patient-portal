namespace HealthcareSupport.CaseEvaluation.UserQueries;

/// <summary>
/// Compile-time constants for the Submit-Query / Contact-Us feature.
/// Shared between Domain (entity construction) and EF Core (column length)
/// so the length budget stays in sync. Mirrors OLD spm.UserQueries.Message
/// nvarchar(500).
/// </summary>
public static class UserQueryConsts
{
    /// <summary>
    /// Max length of the free-text question. OLD enforced MaxLength(500) on
    /// the Message column and the submit modal mirrored it.
    /// </summary>
    public const int MessageMaxLength = 500;
}
