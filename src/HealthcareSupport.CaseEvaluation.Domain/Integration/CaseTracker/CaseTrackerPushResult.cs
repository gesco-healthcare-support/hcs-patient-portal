namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>How the drain must treat the outcome of one push attempt.</summary>
public enum CaseTrackerPushOutcome
{
    /// <summary>Confirmed accepted; mark the row Sent.</summary>
    Success = 1,

    /// <summary>Might succeed later; reschedule with backoff until the attempt cap.</summary>
    Retryable = 2,

    /// <summary>Cannot succeed however often we retry; dead-letter now and alert a human.</summary>
    Fatal = 3,
}

/// <summary>
/// Classifies one push attempt per the delivery status matrix in
/// <c>docs/integration/case-tracker-api-contract.md</c> section I.
///
/// <para>Kept as a pure, side-effect-free factory so the matrix is unit-testable without HTTP:
/// this decision determines whether a case is retried or dead-lettered, and both wrong answers
/// are costly. Retrying a permanent failure delays the alert a human needs; giving up on a
/// transient one loses a case until someone notices.</para>
/// </summary>
public sealed class CaseTrackerPushResult
{
    private CaseTrackerPushResult(CaseTrackerPushOutcome outcome, int? statusCode, string? error)
    {
        Outcome = outcome;
        StatusCode = statusCode;
        Error = error;
    }

    public CaseTrackerPushOutcome Outcome { get; }

    /// <summary>HTTP status observed; null when the request never got a response.</summary>
    public int? StatusCode { get; }

    /// <summary>Bounded, non-PHI failure reason for the ledger. Null on success.</summary>
    public string? Error { get; }

    public bool IsSuccess => Outcome == CaseTrackerPushOutcome.Success;

    /// <summary>
    /// Maps an HTTP status to an outcome:
    /// 2xx succeeds; 404 / 408 / 429 / 5xx are transient; every other 4xx (401 bad token,
    /// 400 and 415 malformed request) is permanent. Anything unexpected is treated as permanent
    /// so an unknown response cannot drive a retry storm.
    /// </summary>
    public static CaseTrackerPushResult FromStatusCode(int statusCode)
    {
        var outcome = statusCode switch
        {
            >= 200 and <= 299 => CaseTrackerPushOutcome.Success,
            404 or 408 or 429 => CaseTrackerPushOutcome.Retryable,
            >= 500 and <= 599 => CaseTrackerPushOutcome.Retryable,
            _ => CaseTrackerPushOutcome.Fatal,
        };

        var error = outcome == CaseTrackerPushOutcome.Success
            ? null
            : $"Case Tracker responded {statusCode}.";

        return new CaseTrackerPushResult(outcome, statusCode, error);
    }

    /// <summary>
    /// The request never produced a response (connection refused, DNS failure, timeout, TLS
    /// handshake failure). Always retryable -- the far side being unreachable says nothing about
    /// whether the message is valid.
    /// </summary>
    public static CaseTrackerPushResult FromTransportFailure(string error)
    {
        return new CaseTrackerPushResult(CaseTrackerPushOutcome.Retryable, statusCode: null, error: error);
    }
}
