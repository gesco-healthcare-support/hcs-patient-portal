using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Serializes every multi-office isolation test class into one xUnit collection. They
/// share the process-wide named SQLite databases and the two seeded offices, so running
/// them in parallel would race on that shared state (and on the one-time seed). No
/// fixture object is needed -- the shared state lives in static holders.
/// </summary>
[CollectionDefinition(Name)]
public class MultiOfficeCollection
{
    public const string Name = "MultiOffice isolation collection";
}
