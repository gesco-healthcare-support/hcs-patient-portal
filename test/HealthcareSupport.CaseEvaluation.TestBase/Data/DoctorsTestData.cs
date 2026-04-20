using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs for Doctor entities seeded by
/// CaseEvaluationIntegrationTestSeedContributor. Tests assert against these
/// specific IDs; changing them breaks existing Doctor AppService / Repository tests.
/// </summary>
public static class DoctorsTestData
{
    public static readonly Guid Doctor1Id = Guid.Parse("63b171d1-b8d1-4a84-98c2-435381633f67");
    public static readonly Guid Doctor2Id = Guid.Parse("b6d53903-5956-47fe-a12d-02982664ed4f");

    // Hex-string field values preserved from the pre-B-6 seed contributor so
    // existing DoctorApplicationTests / DoctorRepositoryTests continue to assert
    // against the same values without behaviour change in PR-0.
    public const string Doctor1FirstName = "551551e068be423cb150129a2fb3fd1f0c6bc2ecc74145619f";
    public const string Doctor1LastName = "221de0f2b24843429fbb2b7101ced2cbcca103583b4d4cd89c";
    public const string Doctor1Email = "7c7fa4aa54e94b09adf79@07d1fd7ead804f659d7d5.com";

    public const string Doctor2FirstName = "b032f90ee6b14bec8ce85eb2c239d6779b0a5be0ee7a4dc2be";
    public const string Doctor2LastName = "1967da12b041453b9280d4befe7d582fe8e72d7b5a13447291";
    public const string Doctor2Email = "eb5b574cbd18458f84700@a4260fb508044a75afd13.com";
}
