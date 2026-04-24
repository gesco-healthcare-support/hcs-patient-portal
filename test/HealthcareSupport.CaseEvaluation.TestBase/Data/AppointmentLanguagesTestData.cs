namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs + names for the AppointmentLanguage host-scoped
/// lookup entity seeded by <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
///
/// AppointmentLanguage is `FullAuditedEntity&lt;Guid&gt;` (host-only -- NOT
/// IMultiTenant; NOT AggregateRoot). NameMaxLength = 50. 1 inbound FK
/// (Patient.AppointmentLanguageId, nullable SetNull). The CreateDto declares
/// a default value of "English" but the AppService/manager do not enforce it.
///
/// GUID prefix scheme: digit-only `5` (Tier-3 lookups, see StatesTestData).
/// </summary>
public static class AppointmentLanguagesTestData
{
    public static readonly System.Guid Language1Id = System.Guid.Parse("51111111-1111-1111-1111-111111111111");
    public const string Language1Name = "TEST-English";

    public static readonly System.Guid Language2Id = System.Guid.Parse("52222222-2222-2222-2222-222222222222");
    public const string Language2Name = "TEST-Spanish";
}
