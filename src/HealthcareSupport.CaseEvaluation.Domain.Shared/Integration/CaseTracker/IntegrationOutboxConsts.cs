using System.Collections.Generic;

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
    /// A BACKSTOP on attempts for rows created since #917, not the rule. The rule is
    /// <see cref="RetryWindowHours"/>: keep retrying for 24 hours from the first failure. The wait
    /// schedule (<see cref="RetryWaitMinutes"/>, then <see cref="SteadyRetryWaitMinutes"/>) allows about
    /// 50 attempts in that window, so this cap is only reached if something retries faster than the
    /// schedule permits.
    ///
    /// <para>Rows created before #917 keep the 3 stored on them (agreed rule 7): they dead-letter at 3 as
    /// they always did, and the bulk retry recovers anything that dead-lettered under the old rule.</para>
    ///
    /// <para>Why the old fail-fast rule changed (#917, agreed with the Case Tracker side 2026-09-15): three
    /// attempts dead-lettered every queued change during any outage longer than a few minutes, and
    /// recovery was one row at a time. The legal-timeline reason for failing fast is kept by the
    /// early-warning email on the second failure (<see cref="EarlyWarningAfterAttempts"/>): staff are told
    /// in about ten minutes, while the row keeps trying. The old comment here said three attempts took
    /// "roughly 10 minutes"; in practice they took 20-30, because the next attempt waited for a sweep.</para>
    /// </summary>
    public const int MaxAttempts = 100;

    /// <summary>How long a failing row keeps being retried, from its FIRST failure, before it dead-letters.</summary>
    public const int RetryWindowHours = 24;

    /// <summary>
    /// The waits after the first, second and third failures, in minutes. After that every wait is
    /// <see cref="SteadyRetryWaitMinutes"/>.
    /// </summary>
    public static readonly IReadOnlyList<int> RetryWaitMinutes = new[] { 5, 10, 20 };

    /// <summary>The wait between attempts once the growing waits are used up.</summary>
    public const int SteadyRetryWaitMinutes = 30;

    /// <summary>
    /// A row that has failed this many times and is STILL retrying triggers the early-warning email,
    /// once. That is the moment the old fail-fast rule would have given up, so staff hear about a
    /// problem exactly as early as they used to.
    /// </summary>
    public const int EarlyWarningAfterAttempts = 2;

    /// <summary>
    /// Visibility timeout for a claimed row; must comfortably exceed one HTTP attempt so a
    /// crashed drain's row becomes reclaimable rather than stuck.
    /// </summary>
    public const int LeaseDurationSeconds = 120;

    /// <summary>Max rows a single drain claims per office per pass.</summary>
    public const int DrainBatchSize = 50;

    /// <summary>
    /// How long an intake or document enqueue waits for the per-appointment ordering lock (#931)
    /// before failing loudly. The lock is held only for one enqueue's transaction, so a wait anywhere
    /// near this long means something is stuck, not busy.
    /// </summary>
    public const int AppointmentLockTimeoutMilliseconds = 30_000;

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
