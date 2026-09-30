using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// Serializes every real-authorization test class into one xUnit collection. They share
/// the process-wide named SQLite databases and the one seeded office, so running them in
/// parallel would race on that shared state (and on the one-time seed). No fixture object
/// is needed -- the shared state lives in static holders.
/// </summary>
[CollectionDefinition(Name)]
public class RealAuthorizationCollection
{
    public const string Name = "Real authorization collection";
}
