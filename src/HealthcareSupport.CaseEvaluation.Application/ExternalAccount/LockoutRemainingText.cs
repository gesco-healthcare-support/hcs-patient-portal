using System;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Turns "how long until this lockout expires" into something a person can act on. Item D
/// (2026-08-22).
///
/// <para><b>Why this exists.</b> The LockedOut page said "Try again in 1 hour", hard-coded, and its
/// own comment admitted the duration had to be kept in sync with the settings by hand -- as did the
/// settings provider's comment, from the other side. With a progressive ladder that sentence is wrong
/// for most lockouts: a first offence is one minute. A page that overstates the wait by 59 minutes
/// pushes people to phone the clinic for something that has already expired.</para>
///
/// <para><b>Never precise, always safe.</b> Rounds UP to a whole unit, so the stated time never
/// expires before the real one -- being told to wait slightly too long is harmless, being told to
/// retry too early is not. A missing, zero or negative remainder degrades to "shortly" rather than
/// rendering "in -3 minutes"; the framework's redirect carries no user id, so an unknown remainder is
/// a normal case here, not an error.</para>
///
/// <para>Public and static so the AuthServer Razor page can call it while the unit tests live in
/// Application.Tests -- the AuthServer has no test project of its own.</para>
///
/// <para><b>Every return value is a bare noun phrase, and the CALLER supplies the preposition.</b>
/// This is a contract, not a detail: the page renders "You can sign in again in {phrase}", so a
/// phrase that carried its own "in" would double it. It is also the bug this note exists to prevent
/// -- the page originally read "Try again {phrase}", which was written around the old fallback
/// ("shortly") and rendered "Try again about 5 minutes" once the ladder started producing real
/// durations. Keep the fallback a noun phrase too, so it reads correctly after the same "in".</para>
/// </summary>
public static class LockoutRemainingText
{
    /// <summary>
    /// Used when the remaining time is unknown, elapsed, or not positive. A noun phrase like every
    /// other return value, so "Try again in {this}" reads correctly.
    /// </summary>
    public const string Unknown = "a short while";

    /// <summary>
    /// Describes <paramref name="remaining"/> as a rounded-up phrase: "about 1 minute",
    /// "about 5 minutes", "about 1 hour", "about 2 hours", or <see cref="Unknown"/>. Never includes
    /// a leading preposition -- see the type remarks.
    /// </summary>
    public static string Describe(TimeSpan? remaining)
    {
        if (remaining == null || remaining.Value <= TimeSpan.Zero)
        {
            return Unknown;
        }

        // Round up so the advice is never early: 1s..60s all read as one minute.
        var minutes = (int)Math.Ceiling(remaining.Value.TotalMinutes);

        // Promote to hours once the rounded minutes reach 60, so exactly one hour reads as
        // "about 1 hour" rather than "about 60 minutes", and 59m59s rounds to the hour rather than
        // to an awkward 60. Rounding up across the boundary keeps the never-early property.
        if (minutes < 60)
        {
            return minutes <= 1 ? "about 1 minute" : $"about {minutes} minutes";
        }

        var hours = (int)Math.Ceiling(minutes / 60d);
        return hours <= 1 ? "about 1 hour" : $"about {hours} hours";
    }
}
