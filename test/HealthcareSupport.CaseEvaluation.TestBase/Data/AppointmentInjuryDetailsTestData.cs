namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs + field values for AppointmentInjuryDetail entities seeded by
/// <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
///
/// AppointmentInjuryDetail is <c>FullAuditedAggregateRoot&lt;Guid&gt;</c> and IMultiTenant. Two rows
/// seeded across the two tenants, mirroring AppointmentEmployerDetailsTestData:
///   Detail1 -- TenantA, Appointment1, WcabOfficeId = WcabOffice1Id (populated nav join)
///   Detail2 -- TenantB, Appointment2, WcabOfficeId = null (null FK branch)
///
/// It is also the PARENT of AppointmentBodyPart (which keys on AppointmentInjuryDetailId), which is
/// why this class is shared in TestBase rather than built inline in one test file: two services in
/// this tranche need it and later ones will too.
///
/// THE CONSTRUCTOR'S OPTIONAL PARAMETERS ARE NOT ALL OPTIONAL. <c>wcabAdj</c> is declared
/// <c>string? wcabAdj = null</c>, and the ctor then runs
/// <c>Check.NotNullOrWhiteSpace(wcabAdj, nameof(wcabAdj))</c> on it -- so the default value the
/// signature advertises throws. Any fixture or test constructing this entity MUST pass a WcabAdj.
/// Recorded here because the signature is the first thing a reader consults and it is misleading;
/// logged to docs/backlog.md rather than changed, since production code is out of scope for this
/// tranche.
///
/// Max lengths from AppointmentInjuryDetailConsts: ClaimNumber 50, WcabAdj 50,
/// BodyPartsSummary 500. The values below sit well inside all three so they are never the reason
/// a test fails; a test asserting the length guard supplies its own over-long string.
///
/// GUID prefix scheme: digit-only `7`. Hex letters a-f are claimed by Tier-1/2 files
/// (a=Locations/Accessors, b=ApplicantAttorneys, c=Appointments+Joins, d=Slots, e=Employer/State1,
/// f=AppointmentType1) and digits 2-6 are already taken by Tier-3 lookups (2=States,
/// 3=AppointmentTypes, 4=AppointmentStatuses, 5=AppointmentLanguages, 6=Doctors/WcabOffices).
/// See StatesTestData for the scheme itself.
/// </summary>
public static class AppointmentInjuryDetailsTestData
{
    public static readonly Guid Detail1Id = Guid.Parse("71111111-1111-1111-1111-111111111111");
    public static readonly Guid Detail2Id = Guid.Parse("72222222-2222-2222-2222-222222222222");

    // Synthetic and TEST- prefixed. A claim number is a real-world identifier, so these must never
    // resemble one -- see the HIPAA rule on test data.
    public const string Detail1ClaimNumber = "TEST-CLAIM-0001";
    public const string Detail2ClaimNumber = "TEST-CLAIM-0002";

    public const string Detail1WcabAdj = "TEST-ADJ-0001";
    public const string Detail2WcabAdj = "TEST-ADJ-0002";

    public const string Detail1BodyPartsSummary = "TEST-lower back, left shoulder";
    public const string Detail2BodyPartsSummary = "TEST-right knee";

    public const bool Detail1IsCumulativeInjury = false;
    public const bool Detail2IsCumulativeInjury = true;

    // Fixed rather than relative-to-now, so a test asserting on a date cannot pass or fail
    // according to when it runs.
    public static readonly DateTime Detail1DateOfInjury = new DateTime(2026, 3, 14, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime Detail2DateOfInjury = new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc);

    // Detail2 exercises the cumulative-injury shape, which is the branch that carries a range.
    public static readonly DateTime Detail2ToDateOfInjury = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
}
