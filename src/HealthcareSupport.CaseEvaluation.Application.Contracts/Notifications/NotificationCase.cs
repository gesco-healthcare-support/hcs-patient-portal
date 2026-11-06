using System;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// 2026-11-05 -- which appointment an email is about, passed explicitly to every dispatch so the email-stop
/// filter can honour a stop made "for this case only". Every caller must decide: <see cref="Of"/> for an
/// email about a case, <see cref="None"/> for one that is not (staff summaries, technical alerts, account
/// mail). An email sent with <see cref="None"/> still honours an all-cases stop; only the self-service
/// account templates are never stopped, and that is decided by the template code, not by this value.
///
/// <para>A source guard test fails when a new dispatch call passes <see cref="None"/> from a file that is
/// not on its reviewed list, so a future case email cannot slip past a per-case stop unnoticed.</para>
/// </summary>
public readonly record struct NotificationCase
{
    private NotificationCase(Guid appointmentId)
    {
        AppointmentId = appointmentId;
    }

    /// <summary>The appointment the email is about; null when it is not about a case.</summary>
    public Guid? AppointmentId { get; }

    /// <summary>An email about this appointment.</summary>
    public static NotificationCase Of(Guid appointmentId) => new(appointmentId);

    /// <summary>An email that is not about a single case.</summary>
    public static NotificationCase None => default;
}
