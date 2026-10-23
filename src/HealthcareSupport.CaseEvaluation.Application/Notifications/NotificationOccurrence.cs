using System;
using System.Globalization;
using HealthcareSupport.CaseEvaluation.Timing;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Gives an email that can happen MORE THAN ONCE for the same person or office its own context tag
/// for each time it happens.
///
/// <para><b>Why this exists (2026-10-23).</b> The notification outbox collapses a send whose
/// idempotency key -- office, recipient and context tag -- matches an existing row, whatever that
/// row's state, so that a replayed unit of work cannot email the same thing twice. That is right
/// for an email that belongs to one event ("appointment A00012 was approved"). It is wrong for one
/// that names a KIND of email ("password reset for user X", "the daily digest for office Y"): the
/// first one is sent, and every later one silently collapses onto it and is never sent. Measured on
/// the test server: the daily pending digest had gone out exactly once, on its first day, and
/// every person's second password reset had sent nothing.</para>
///
/// <para>Each helper appends one occurrence part and keeps the collapse within that occurrence:
/// the same request, event or day replayed is still one email.</para>
///
/// <para>A guard test (<c>NotificationContextTagOccurrenceTests</c>) reads every context tag in the
/// source and fails on any that is neither built here nor listed as an email that happens once per
/// subject, so a new sender cannot bring the defect back.</para>
/// </summary>
public static class NotificationOccurrence
{
    /// <summary>
    /// One request a person made (asked for a reset or a new link, pressed "send test", changed a
    /// password): every request gets its own email. Call it ONCE per request and pass the result to
    /// every dispatch for that request.
    /// </summary>
    public static string PerRequest(string kind) =>
        Join(kind, "r" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

    /// <summary>
    /// One event, identified by the time it happened. A redelivery of the SAME event (same
    /// <paramref name="occurredAt"/>) still collapses to one email; the next occurrence does not.
    /// An event published without its time (the default value) is treated as its own occurrence,
    /// because a missing time would otherwise put every such event back on one shared tag.
    /// </summary>
    public static string At(string kind, DateTime occurredAt) =>
        occurredAt == default
            ? PerRequest(kind)
            : Join(kind, "t" + occurredAt.Ticks.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// One Pacific calendar day: a daily digest goes out at most once a day, and again the next day.
    /// </summary>
    public static string PerPacificDay(string kind, DateTime utcNow) =>
        Join(kind, PacificTime.TodayFrom(utcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static string Join(string kind, string occurrence)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException("A context tag needs a kind.", nameof(kind));
        }

        return kind.TrimEnd('/') + "/" + occurrence;
    }
}
