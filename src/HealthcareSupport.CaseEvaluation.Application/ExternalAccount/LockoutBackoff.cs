using System;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// The progressive lockout ladder. Item D (2026-08-22).
///
/// <para><b>What it replaces.</b> A flat one-hour lockout after 10 failed sign-ins, whose duration
/// was also hard-coded into the LockedOut page's text. One mistyped password cost an hour.</para>
///
/// <para><b>Why a ladder.</b> NIST SP 800-63B recommends a delay that increases as an account
/// approaches its limit, and OWASP's Authentication Cheat Sheet recommends exponential backoff over a
/// flat duration; OWASP WSTG records 10-15 minutes as normal observed practice, with one hour
/// appearing only in legacy material. So the attempt threshold (10) was already conservative and the
/// flat hour was the outlier. A first offence now costs a minute; a repeat offender still reaches the
/// configured maximum.</para>
///
/// <para><b>The top rung is READ, not hard-coded.</b> It comes from the
/// <c>Abp.Identity.Lockout.LockoutDuration</c> setting, so the ladder cannot drift from the
/// configured policy the way the page text did. That drift is part of why this item exists.</para>
///
/// <para><c>internal static</c> and pure so it is unit-testable without ABP DI, via
/// <c>InternalsVisibleTo</c> -- the same shape as <see cref="PasswordResetGate"/>. This matters here
/// because the only other home would be the replacement <c>IdentityUserManager</c>, which sits on the
/// authentication path and is awkward to exercise directly.</para>
/// </summary>
internal static class LockoutBackoff
{
    /// <summary>
    /// Rungs for cycles 1..3. Cycle 4 and beyond use the configured maximum, so the ceiling never
    /// rises above today's behaviour -- this change can only ever make a lockout shorter.
    /// </summary>
    private static readonly TimeSpan[] Ladder =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
    ];

    /// <summary>
    /// How long a lockout should last, given which cycle this is (1-based: the user's first lockout
    /// is cycle 1) and the configured maximum.
    ///
    /// <para>A cycle below 1 is treated as 1 rather than throwing: the counter is read from a JSON
    /// extension property, so an absent or malformed value must degrade to the shortest lockout, not
    /// break the sign-in path.</para>
    ///
    /// <para>The result is always clamped to <paramref name="configuredMaximum"/>. If an
    /// administrator sets the maximum below a ladder rung, the maximum wins -- the setting is the
    /// policy, and the ladder must never exceed it.</para>
    /// </summary>
    internal static TimeSpan DurationForCycle(int cycle, TimeSpan configuredMaximum)
    {
        var effectiveCycle = cycle < 1 ? 1 : cycle;

        var rung = effectiveCycle <= Ladder.Length
            ? Ladder[effectiveCycle - 1]
            : configuredMaximum;

        return rung < configuredMaximum ? rung : configuredMaximum;
    }
}
