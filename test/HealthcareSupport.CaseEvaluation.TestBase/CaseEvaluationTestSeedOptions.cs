namespace HealthcareSupport.CaseEvaluation;

/// <summary>
/// Controls the full <c>IDataSeeder</c> run that <see cref="CaseEvaluationTestBaseModule"/> performs
/// when a test application starts.
/// </summary>
public class CaseEvaluationTestSeedOptions
{
    /// <summary>
    /// When true, the start-up seed is skipped because the database already holds the seeded data.
    /// The EF Core test module sets this when it hands the application a copy of the once-per-process
    /// template database (#1033). Defaults to false, so every other harness seeds as before.
    /// </summary>
    public bool SkipInitialSeed { get; set; }
}
