namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>How a blank-form request ended; see <see cref="BlankPacketFormResult"/>.</summary>
public enum BlankPacketFormOutcome
{
    /// <summary>The PDF, with its ETag.</summary>
    Pdf = 1,

    /// <summary>The caller already holds the current version (If-None-Match matched): no body.</summary>
    NotModified = 2,

    /// <summary>The office is unknown or not switched on for the Case Tracker; answered like the feed does.</summary>
    NotEnabled = 3,
}

/// <summary>The answer to one blank-form request: the outcome, the ETag, and the bytes when there are any.</summary>
public sealed record BlankPacketFormResult(BlankPacketFormOutcome Outcome, string ETag, byte[]? Pdf)
{
    public static BlankPacketFormResult NotEnabled { get; } = new(BlankPacketFormOutcome.NotEnabled, string.Empty, null);
}
