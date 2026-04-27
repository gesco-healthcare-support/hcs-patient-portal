namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs + names for the AppointmentStatus host-scoped
/// lookup entity seeded by <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
///
/// AppointmentStatus is `FullAuditedEntity&lt;Guid&gt;` (host-only -- NOT
/// IMultiTenant; NOT AggregateRoot). It is parallel to but distinct from the
/// `AppointmentStatusType` enum used on Appointment.AppointmentStatus -- there
/// is NO FK from Appointment to this entity. The Tier-3 plan encodes that
/// design split as a `[Fact(Skip="GAP:...")]` rather than fixing it.
///
/// Two statuses seeded so Tier-3 tests can exercise multi-row list +
/// FilterText filtering. Names use distinctive scratch labels (NOT real
/// `AppointmentStatusType` enum names) so DeleteAllAsync filter-scoped tests
/// can target scratch-only data without wiping Status1/Status2.
///
/// GUID prefix scheme: digit-only `4` (Tier-3 lookups, see StatesTestData).
/// </summary>
public static class AppointmentStatusesTestData
{
    public static readonly System.Guid Status1Id = System.Guid.Parse("41111111-1111-1111-1111-111111111111");
    public const string Status1Name = "TEST-PendingLabel";

    public static readonly System.Guid Status2Id = System.Guid.Parse("42222222-2222-2222-2222-222222222222");
    public const string Status2Name = "TEST-ApprovedLabel";
}
