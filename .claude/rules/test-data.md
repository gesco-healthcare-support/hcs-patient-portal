---
paths:
  - "test/**/*.cs"
---
# Test Data Rules (HIPAA)

When writing or modifying test files:

- ALL test data MUST be synthetic — never use data that could resemble real patients.
- NEVER copy data from production, staging, or demo environments.
- NEVER use patterns resembling real government-issued ID numbers (e.g., `XXX-XX-XXXX` SSNs).

## Two approved synthetic-data sources

Both are HIPAA Safe-Harbor compliant when used as documented below. Choose based on the entity shape.

### 1. Hex-string pattern (legacy convention)

Use for entities where test data is asserted literally (e.g., existing Doctor seed values the test suite already asserts against) or when the PHI scanner hook requires obviously-synthetic string shapes.

- String values: random hex strings matching max-length constraints.
- Email values: `"{randomhex}@{randomhex}.com"` format.
- Names: random hex strings (NOT realistic names).
- Identifier values: `TEST-` or `FAKE-` prefix (e.g., `TEST-staff-admin`).

Helpers:

- `TestStringUtility.RandomHex(maxLength)` in `test/HealthcareSupport.CaseEvaluation.TestBase/Data/TestStringUtility.cs`.
- `TestStringUtility.SyntheticSsnShaped()` for SSN-shaped hex values.

### 2. Bogus / Bogus.Healthcare (preferred for new seed contributors)

Added in Phase B-6 PR-0 (see `docs/plans/2026-04-20-phase-b6-tier1.md`). Use for new Tier-1+ entity seed contributors where richer, more realistic-shape synthetic data helps debugging.

- `Bogus` + `Bogus.Healthcare` NuGet packages (v35.6.5, Apache-2.0 license, bchavez).
- MUST use the deterministic seed: `Randomizer.Seed = new Random(20260420)` — wired in `TestStringUtility`'s static constructor. Changing the seed invalidates tests asserting on Bogus-generated values.
- Safe Harbor compliance: Bogus generates names/addresses/emails that are stochastically synthetic. They do NOT reference real patients and do NOT match any of HIPAA's 18 identifier categories as identifying data for a real person.
- The PHI scanner hook still enforces the 18-category rule at tool-use boundaries — if a scan false-positives on a specific Bogus pattern, tune the hook rather than abandon Bogus.

Usage example:

```csharp
var faker = TestStringUtility.Faker;
var firstName = faker.Name.FirstName();
var email = faker.Internet.Email();
var address = faker.Address.StreetAddress();
```

## Always applies

- Phone numbers: use 555 prefix reserved range only — `TestStringUtility.SyntheticPhoneNumber()`.
- Date values: obviously fake dates like `1990-01-01` (never today's date, never a real birthday shape).
- GUIDs: hardcoded deterministic values in per-entity `TestData` static classes (e.g., `DoctorsTestData.Doctor1Id`). Tests assert against these; changing them breaks the assertion.
- The PHI scanner hook will block real-looking patterns in tool input.

## Related

- Seed-orchestrator reference: `test/HealthcareSupport.CaseEvaluation.TestBase/Data/CaseEvaluationIntegrationTestSeedContributor.cs`.
- Test-data constants: `test/HealthcareSupport.CaseEvaluation.TestBase/Data/{Entity}TestData.cs`.
- Test convention rules: `test/CLAUDE.md`.
