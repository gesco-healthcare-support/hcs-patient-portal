namespace HealthcareSupport.CaseEvaluation.Uploads;

/// <summary>
/// Where the clamd daemon listens, bound from the <c>Clamd</c> configuration section (B11).
/// </summary>
/// <remarks>
/// <see cref="Host"/> has no default on purpose. Unset, every scan fails closed, and outside
/// Development <c>HostingConfigValidator</c> refuses to start the API at all.
/// </remarks>
public class ClamdOptions
{
    public const string SectionName = "Clamd";

    public string? Host { get; set; }

    public int Port { get; set; } = 3310;

    /// <summary>Whole-scan budget: connect, send, and read the verdict.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>INSTREAM chunk size. clamd accepts any chunk up to its StreamMaxLength.</summary>
    public int ChunkSizeBytes { get; set; } = 64 * 1024;
}
