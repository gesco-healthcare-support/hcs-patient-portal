using System;
using Bogus;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Synthetic test data generation for Phase B-6 seed contributors and test methods.
/// All generated values are HIPAA Safe-Harbor compliant — random, deterministically
/// seeded, and not derived from real patient data. Bogus.Randomizer.Seed is set once
/// in the static constructor for reproducible test runs.
/// </summary>
public static class TestStringUtility
{
    // Deterministic date-derived seed so CI and local runs produce identical data.
    // 2026-04-20 = Phase-B closure date; changing this seed would invalidate tests
    // that assert on specific Bogus-generated values.
    private const int BogusRandomizerSeed = 20260420;

    static TestStringUtility()
    {
        Randomizer.Seed = new Random(BogusRandomizerSeed);
    }

    /// <summary>
    /// Shared Faker for simple value lookups. Tests needing dedicated locale or
    /// ruleset should construct their own Faker&lt;T&gt; with the same Randomizer.Seed.
    /// </summary>
    public static readonly Faker Faker = new Faker();

    /// <summary>
    /// Legacy hex-string helper — preserved for existing Doctor seed values
    /// that predated the Bogus adoption. New code should prefer Bogus where
    /// possible; use this only when a field requires an obviously-synthetic
    /// value that cannot resemble a realistic name / email / SSN.
    /// </summary>
    public static string RandomHex(int maxLength)
    {
        if (maxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "maxLength must be positive.");
        }

        var bytes = new byte[(maxLength + 1) / 2];
        Faker.Random.Bytes(bytes.Length).CopyTo(bytes, 0);
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return hex.Length > maxLength ? hex[..maxLength] : hex;
    }

    /// <summary>
    /// 555-prefixed phone number. Reserved-for-fiction range per NANP guidelines;
    /// matches the convention in .claude/rules/test-data.md.
    /// </summary>
    public static string SyntheticPhoneNumber()
    {
        return $"555{Faker.Random.Number(1000000, 9999999)}";
    }

    /// <summary>
    /// Hex-shaped value that cannot be mistaken for a real SSN.
    /// Avoids the XXX-XX-XXXX pattern that PHI scanners commonly match.
    /// </summary>
    public static string SyntheticSsnShaped()
    {
        return RandomHex(9);
    }
}
