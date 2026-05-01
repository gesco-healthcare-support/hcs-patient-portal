using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs + Bogus-generated field values for ApplicantAttorney
/// entities seeded by <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
///
/// ApplicantAttorney is IMultiTenant. Two attorneys seeded to exercise tenant
/// isolation (the contrast case vs Patient's non-IMultiTenant leak at
/// docs/issues/INCOMPLETE-FEATURES.md FEAT-09):
///   - Attorney1: TenantId = TenantARef, IdentityUserId = ApplicantAttorney1UserId
///   - Attorney2: TenantId = TenantBRef, IdentityUserId = DefenseAttorney1UserId
/// IdentityUserId just needs a valid IdentityUser; cross-tenant semantics is
/// irrelevant at the DB FK level.
///
/// The entity's constructor sets 3 string fields (FirmName, FirmAddress,
/// PhoneNumber). ApplicantAttorneyManager assigns the remaining 5 fields
/// (WebAddress, FaxNumber, Street, City, ZipCode) post-construction. Both paths
/// are exercised by the tests.
///
/// String values draw from the shared Faker in TestStringUtility, which sets
/// Randomizer.Seed deterministically in its static ctor. Touching
/// TestStringUtility.Faker below ensures the seed applies before any value is drawn.
/// </summary>
public static class ApplicantAttorneysTestData
{
    public static readonly Guid Attorney1Id = Guid.Parse("b1111111-1111-1111-1111-111111111111");
    public static readonly Guid Attorney2Id = Guid.Parse("b2222222-2222-2222-2222-222222222222");

    // Constructor-path fields (set via ApplicantAttorney ctor args).
    public static readonly string Attorney1FirmName;
    public static readonly string Attorney1FirmAddress;
    public static readonly string Attorney1PhoneNumber;

    public static readonly string Attorney2FirmName;
    public static readonly string Attorney2FirmAddress;
    public static readonly string Attorney2PhoneNumber;

    // Manager-post-construction fields (assigned directly on the entity after ctor).
    public static readonly string Attorney1WebAddress;
    public static readonly string Attorney1FaxNumber;
    public static readonly string Attorney1Street;
    public static readonly string Attorney1City;
    public static readonly string Attorney1ZipCode;

    public static readonly string Attorney2WebAddress;
    public static readonly string Attorney2FaxNumber;
    public static readonly string Attorney2Street;
    public static readonly string Attorney2City;
    public static readonly string Attorney2ZipCode;

    static ApplicantAttorneysTestData()
    {
        // Touching TestStringUtility first guarantees Randomizer.Seed is applied
        // before we draw any value -- same pattern as PatientsTestData.
        var faker = TestStringUtility.Faker;

        Attorney1FirmName = $"TEST-Firm-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}";
        Attorney1FirmAddress = faker.Address.StreetAddress();
        Attorney1PhoneNumber = TestStringUtility.SyntheticPhoneNumber();
        Attorney1WebAddress = $"https://TEST-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}.test.local";
        Attorney1FaxNumber = TestStringUtility.SyntheticPhoneNumber();
        Attorney1Street = faker.Address.StreetAddress();
        Attorney1City = faker.Address.City();
        Attorney1ZipCode = faker.Address.ZipCode("#####");

        Attorney2FirmName = $"TEST-Firm-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}";
        Attorney2FirmAddress = faker.Address.StreetAddress();
        Attorney2PhoneNumber = TestStringUtility.SyntheticPhoneNumber();
        Attorney2WebAddress = $"https://TEST-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}.test.local";
        Attorney2FaxNumber = TestStringUtility.SyntheticPhoneNumber();
        Attorney2Street = faker.Address.StreetAddress();
        Attorney2City = faker.Address.City();
        Attorney2ZipCode = faker.Address.ZipCode("#####");
    }
}
