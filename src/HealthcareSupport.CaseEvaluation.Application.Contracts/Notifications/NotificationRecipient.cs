using HealthcareSupport.CaseEvaluation.Appointments.Notifications;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Phase 18 (2026-05-04) -- one row in
/// <see cref="INotificationDispatcher.DispatchAsync"/>'s recipient list.
/// Carries the wire address (email + optional phone) plus context the
/// renderer / handler may branch on (role, registered status).
///
/// <para>Modeled after the existing
/// <c>SendAppointmentEmailArgs</c> shape so the dispatcher can hand off
/// to the existing Hangfire pipeline without an extra translation layer.</para>
/// </summary>
public class NotificationRecipient
{
    public string Email { get; init; } = string.Empty;

    /// <summary>Optional E.164 phone for the SMS leg; null skips SMS.</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Hint for handlers / templates that branch on recipient role.</summary>
    public RecipientRole? Role { get; init; }

    /// <summary>True when the recipient already has an IdentityUser account.</summary>
    public bool IsRegistered { get; init; }

    public NotificationRecipient()
    {
    }

    public NotificationRecipient(
        string email,
        string? phoneNumber = null,
        RecipientRole? role = null,
        bool isRegistered = false)
    {
        Email = email;
        PhoneNumber = phoneNumber;
        Role = role;
        IsRegistered = isRegistered;
    }
}
