namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Tuning + column limits for the Case Tracker outbox. Mirrors
/// <c>NotificationOutboxConsts</c>; the values differ where the delivery policy does.
/// </summary>
public static class IntegrationOutboxConsts
{
    /// <summary>Relative request path, e.g. <c>api/intake/appointments</c>.</summary>
    public const int TargetPathMaxLength = 256;

    /// <summary>SHA-256 hex is 64 chars; the cap leaves room without being unbounded.</summary>
    public const int IdempotencyKeyMaxLength = 128;

    public const int LastErrorMaxLength = 500;

    /// <summary>
    /// FAIL FAST, unlike the email outbox's 5. Three attempts spaced by
    /// <see cref="RetryBackoffSeconds"/> dead-letter a row roughly 10 minutes after the
    /// first failure. Rationale: an appointment that has not reached the Case Tracker is a
    /// case nobody is working, and case timelines carry legal weight -- notifying staff
    /// early beats retrying quietly for hours. Transient blips still self-heal inside the
    /// window.
    /// </summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Flat backoff before a failed row is retried by the next drain. Flat (not
    /// exponential) so the proven <c>MarkFailed</c> mechanics are reused verbatim.
    /// </summary>
    public const int RetryBackoffSeconds = 300;

    /// <summary>
    /// Visibility timeout for a claimed row; must comfortably exceed one HTTP attempt so a
    /// crashed drain's row becomes reclaimable rather than stuck.
    /// </summary>
    public const int LeaseDurationSeconds = 120;

    /// <summary>Max rows a single drain claims per office per pass.</summary>
    public const int DrainBatchSize = 50;

    /// <summary>
    /// Rolling window the volume guard measures sends over, per office.
    ///
    /// <para><see cref="DrainBatchSize"/> bounds ONE drain invocation, not throughput: every enqueue
    /// schedules its own drain, so N queued rows become N drain jobs on parallel workers and the
    /// effective ceiling is unbounded. This window plus <see cref="VolumeThresholdPerWindow"/> is the
    /// actual ceiling.</para>
    /// </summary>
    public const int VolumeWindowMinutes = 60;

    /// <summary>
    /// Sends allowed per office per <see cref="VolumeWindowMinutes"/> before the guard holds delivery.
    ///
    /// <para>Chosen against physical capacity rather than guessed: an office runs roughly a dozen
    /// appointment slots a day, so organic approval traffic is single digits per hour. 100 leaves an
    /// order of magnitude of headroom for legitimate bursts -- a backlog released when an office is
    /// first enabled, or a patient edit fanning out across their appointments -- while still stopping
    /// a runaway at a fraction of the damage.</para>
    ///
    /// <para>Why a ceiling matters more here than for a typical rate limit: each intake becomes a CASE
    /// their staff must handle. A flood does not degrade a service, it fills another team's live queue
    /// with work they then unpick by hand. A withheld push is recoverable; a thousand delivered wrong
    /// ones are not.</para>
    /// </summary>
    public const int VolumeThresholdPerWindow = 100;
}
