using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Phase 18 (2026-05-04) -- raised when a booking flow auto-creates an
/// accessor IdentityUser (verification-code reset path). Mirrors OLD
/// <c>EmailTemplate.AccessorAppointmentBooked</c> trigger.
///
/// <para>Phase 11 (Booking) emits this from the booking
/// <c>AppointmentAccessorManager.CreateOrLinkAsync</c> path when a NEW user
/// row is created -- existing users do NOT trigger an invite, only
/// <c>AppointmentAccessor</c> link creation. The temp password / reset
/// token is rendered into the invitation body via the standard
/// <c>##URL##</c> template variable.</para>
/// </summary>
public class AppointmentAccessorInvitedEto
{
    public Guid AppointmentId { get; set; }

    public Guid InvitedUserId { get; set; }

    public Guid? TenantId { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>Localized role name (Patient / Applicant Attorney / etc.).</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// AccessTypeId from the appointment-accessor link
    /// (<c>AccessType.View = 23</c> / <c>AccessType.Edit = 24</c>).
    /// </summary>
    public int AccessTypeId { get; set; }

    public DateTime OccurredAt { get; set; }
}
