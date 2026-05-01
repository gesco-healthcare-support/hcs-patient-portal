namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs + field values for the WcabOffice host-scoped
/// lookup entity seeded by <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
///
/// WcabOffice is `FullAuditedAggregateRoot&lt;Guid&gt;` (host-only -- NOT
/// IMultiTenant). 7 fields: Name (required, max 50), Abbreviation (required,
/// max 50), Address (optional, max 100), City (optional, max 50), ZipCode
/// (optional, max 15), IsActive (bool), StateId (nullable Guid FK -> State,
/// SetNull on delete).
///
/// Two offices seeded so Tier-3 tests can exercise both branches of the
/// nullable StateId nav-prop (populated AND null) and a meaningful IsActive
/// filter exclusion case:
///   Office1 -- TEST-LosAngelesWcab, all 7 fields populated, StateId=State1Id,
///              IsActive=true.
///   Office2 -- TEST-FresnoWcab, only required fields populated (Name +
///              Abbreviation), StateId=null, IsActive=false.
///
/// Excel export (`GetListAsExcelFileAsync`) is OUT OF SCOPE for Tier 3 per
/// Decision T3-5; the `[AllowAnonymous]` security gap on that endpoint is
/// documented in `src/.../Domain/WcabOffices/CLAUDE.md` and deferred to a
/// follow-up PR after B-6 closure.
///
/// GUID prefix scheme: digit-only `6` (Tier-3 lookups; see StatesTestData).
/// </summary>
public static class WcabOfficesTestData
{
    public static readonly System.Guid Office1Id = System.Guid.Parse("61111111-1111-1111-1111-111111111111");
    public const string Office1Name = "TEST-LosAngelesWcab";
    public const string Office1Abbreviation = "TEST-LAO";
    public const string Office1Address = "TEST-300 Synthetic Wcab Way";
    public const string Office1City = "TEST-LosAngeles";
    public const string Office1ZipCode = "90013";
    public const bool Office1IsActive = true;

    public static readonly System.Guid Office2Id = System.Guid.Parse("62222222-2222-2222-2222-222222222222");
    public const string Office2Name = "TEST-FresnoWcab";
    public const string Office2Abbreviation = "TEST-FNO";
    public const bool Office2IsActive = false;
}
