using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs + Bogus-generated field values for the Location
/// host-scoped entity seeded by
/// <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
///
/// One <c>State</c> and one <c>AppointmentType</c> are also declared here because
/// Location's nav-prop join tests need populated related entities; both lookups
/// are host-scoped and their seed lives under the orchestrator's Location phase.
///
/// String values draw from the shared Faker in <see cref="TestStringUtility"/>,
/// which sets <c>Randomizer.Seed</c> deterministically in its static constructor.
/// Touching <c>TestStringUtility.Faker</c> here ensures the seed is applied
/// before any value is drawn.
/// </summary>
public static class LocationsTestData
{
    // --- State seed (1 row: populated nav-join + FilterByStateId) ---
    public static readonly Guid State1Id = Guid.Parse("e1111111-1111-1111-1111-111111111111");
    public const string State1Name = "TEST-California";

    // --- AppointmentType seed (1 row: populated nav-join) ---
    public static readonly Guid AppointmentType1Id = Guid.Parse("f1111111-1111-1111-1111-111111111111");
    public const string AppointmentType1Name = "TEST-IME-Eval";

    // --- Location seeds (3 rows: varied ParkingFee / IsActive / nav FKs) ---
    public static readonly Guid Location1Id = Guid.Parse("a1111111-1111-1111-1111-111111111111");
    public static readonly Guid Location2Id = Guid.Parse("a2222222-2222-2222-2222-222222222222");
    public static readonly Guid Location3Id = Guid.Parse("a3333333-3333-3333-3333-333333333333");

    // ParkingFee values (0 / 5 / 15) support the range-filter test.
    public const decimal Location1ParkingFee = 0.00m;
    public const decimal Location2ParkingFee = 5.00m;
    public const decimal Location3ParkingFee = 15.00m;

    // IsActive spread (2 active, 1 inactive) supports the bool-filter test.
    public const bool Location1IsActive = true;
    public const bool Location2IsActive = true;
    public const bool Location3IsActive = false;

    // Bogus-generated strings, deterministic through TestStringUtility's seed.
    public static readonly string Location1Name;
    public static readonly string Location1Address;
    public static readonly string Location1City;
    public static readonly string Location1ZipCode;

    public static readonly string Location2Name;
    public static readonly string Location2Address;
    public static readonly string Location2City;
    public static readonly string Location2ZipCode;

    public static readonly string Location3Name;
    public static readonly string Location3Address;
    public static readonly string Location3City;
    public static readonly string Location3ZipCode;

    static LocationsTestData()
    {
        // Touching TestStringUtility first guarantees Randomizer.Seed is applied
        // before we draw any value -- matches the pattern in PatientsTestData.
        var faker = TestStringUtility.Faker;

        Location1Name = $"TEST-Loc-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}";
        Location1Address = faker.Address.StreetAddress();
        Location1City = faker.Address.City();
        Location1ZipCode = faker.Address.ZipCode("#####");

        Location2Name = $"TEST-Loc-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}";
        Location2Address = faker.Address.StreetAddress();
        Location2City = faker.Address.City();
        Location2ZipCode = faker.Address.ZipCode("#####");

        Location3Name = $"TEST-Loc-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}";
        Location3Address = faker.Address.StreetAddress();
        Location3City = faker.Address.City();
        Location3ZipCode = faker.Address.ZipCode("#####");
    }
}
