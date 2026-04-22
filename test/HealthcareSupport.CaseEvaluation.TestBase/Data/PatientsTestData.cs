using System;
using Bogus;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs + Bogus-generated field values for Patient entities
/// seeded by <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
///
/// Tenant GUIDs previously lived here (as deterministic Guid.Parse constants).
/// They moved to <see cref="TenantsTestData"/> in the tenant-semantics cleanup
/// because tenants are now created via <c>ITenantManager.CreateAsync</c> and
/// their Ids are captured at seed time rather than hardcoded -- reflecting
/// production's tenant-provisioning path rather than a framework bypass.
///
/// Field values use Bogus seeded with the repo-wide deterministic seed in
/// <see cref="TestStringUtility"/>. SSN uses a hex-shaped synthetic value to stay
/// clear of the XXX-XX-XXXX pattern that PHI scanners match.
/// </summary>
public static class PatientsTestData
{
    public static readonly Guid Patient1Id = Guid.Parse("c1111111-1111-1111-1111-111111111111");
    public static readonly Guid Patient2Id = Guid.Parse("c2222222-2222-2222-2222-222222222222");

    // DOB fixed per .claude/rules/test-data.md — never a real birthday shape.
    public static readonly DateTime FixedDateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Gender / PhoneNumberType enum int values used by the seed to avoid
    // taking a dependency on the Enums namespace from TestBase consumers.
    // Gender.Male = 1; PhoneNumberType.Work = 28.
    public const int PatientGenderIdValue = 1;
    public const int PatientPhoneNumberTypeIdValue = 28;

    // Bogus-generated strings. Static readonly with initialization pulled from
    // the shared Faker; deterministic because TestStringUtility sets
    // Randomizer.Seed in its static constructor.
    public static readonly string Patient1FirstName;
    public static readonly string Patient1LastName;
    public static readonly string Patient1Email;
    public static readonly string Patient1Address;
    public static readonly string Patient1City;
    public static readonly string Patient1ZipCode;
    public static readonly string Patient1SocialSecurityNumber;

    public static readonly string Patient2FirstName;
    public static readonly string Patient2LastName;
    public static readonly string Patient2Email;

    static PatientsTestData()
    {
        // Touching TestStringUtility ensures Randomizer.Seed is applied
        // before we draw any values from the shared Faker.
        var faker = TestStringUtility.Faker;

        Patient1FirstName = faker.Name.FirstName();
        Patient1LastName = faker.Name.LastName();
        Patient1Email = $"TEST-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}@test.local";
        Patient1Address = faker.Address.StreetAddress();
        Patient1City = faker.Address.City();
        Patient1ZipCode = faker.Address.ZipCode("#####");
        Patient1SocialSecurityNumber = TestStringUtility.SyntheticSsnShaped();

        Patient2FirstName = faker.Name.FirstName();
        Patient2LastName = faker.Name.LastName();
        Patient2Email = $"TEST-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}@test.local";
    }
}
