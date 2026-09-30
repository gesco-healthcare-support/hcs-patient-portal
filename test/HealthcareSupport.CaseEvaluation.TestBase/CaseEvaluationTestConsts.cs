using System;

namespace HealthcareSupport.CaseEvaluation;

public static class CaseEvaluationTestConsts
{
    /// <summary>
    /// Name of the single serial xUnit collection every EF Core test class used to join. #1034
    /// removed it: each test builds its own application and its own in-memory SQLite database,
    /// so classes now run in parallel, one collection per class. The constant is kept only so that
    /// a leftover <c>[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]</c> (for
    /// example in a pull request branched before #1034) fails to compile with the fix in the
    /// message, instead of quietly re-forming a serial group. Delete it once no open branch uses it.
    /// </summary>
    [Obsolete(
        "The shared EF Core test collection was removed by #1034: delete this [Collection] attribute. "
        + "Each test has its own database, so test classes run in parallel.",
        error: true)]
    public const string CollectionDefinitionName = "CaseEvaluation collection";
}
