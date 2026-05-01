using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs + Bogus-generated field values for
/// AppointmentEmployerDetail entities seeded by
/// <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
///
/// AppointmentEmployerDetail is IMultiTenant. Two rows seeded across the two
/// tenants so Tier-2 tests can exercise isolation + nav-prop hydration through
/// the optional StateId join:
///   Detail1 -- TenantA, Appointment1, StateId = State1Id (populated nav join)
///   Detail2 -- TenantB, Appointment2, StateId = null (null FK branch)
/// The entity's ctor sets EmployerName + Occupation (both required, max 255);
/// the 4 optional strings (PhoneNumber, Street, City, ZipCode) are assigned
/// post-construction -- same shape as ApplicantAttorneysTestData.
///
/// Field values draw from TestStringUtility.Faker so the deterministic
/// Randomizer.Seed applies; touching Faker in the static ctor guarantees the
/// seed is in place before any value is drawn.
/// </summary>
public static class AppointmentEmployerDetailsTestData
{
    public static readonly Guid Detail1Id = Guid.Parse("e1111111-1111-1111-1111-111111111111");
    public static readonly Guid Detail2Id = Guid.Parse("e2222222-2222-2222-2222-222222222222");

    // Constructor-path fields (required).
    public static readonly string Detail1EmployerName;
    public static readonly string Detail1Occupation;
    public static readonly string Detail2EmployerName;
    public static readonly string Detail2Occupation;

    // Post-construction optional fields.
    public static readonly string Detail1PhoneNumber;
    public static readonly string Detail1Street;
    public static readonly string Detail1City;
    public static readonly string Detail1ZipCode;
    public static readonly string Detail2PhoneNumber;
    public static readonly string Detail2Street;
    public static readonly string Detail2City;
    public static readonly string Detail2ZipCode;

    static AppointmentEmployerDetailsTestData()
    {
        var faker = TestStringUtility.Faker;

        Detail1EmployerName = $"TEST-Emp-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}";
        Detail1Occupation = $"TEST-Occ-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}";
        Detail1PhoneNumber = TestStringUtility.SyntheticPhoneNumber();
        Detail1Street = faker.Address.StreetAddress();
        Detail1City = faker.Address.City();
        Detail1ZipCode = faker.Address.ZipCode("#####");

        Detail2EmployerName = $"TEST-Emp-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}";
        Detail2Occupation = $"TEST-Occ-{faker.Random.AlphaNumeric(8).ToLowerInvariant()}";
        Detail2PhoneNumber = TestStringUtility.SyntheticPhoneNumber();
        Detail2Street = faker.Address.StreetAddress();
        Detail2City = faker.Address.City();
        Detail2ZipCode = faker.Address.ZipCode("#####");
    }
}
