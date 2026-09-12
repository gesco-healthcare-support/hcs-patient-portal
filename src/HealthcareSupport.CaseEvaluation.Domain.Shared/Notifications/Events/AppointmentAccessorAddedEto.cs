using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// Issue #3 (2026-07-16) -- raised when an additional accessor who ALREADY has a
/// tenant account is added to an appointment (the <c>LinkExisting</c> /
/// <c>GrantRoleAndLink</c> outcomes of
/// <c>AppointmentAccessorManager.CreateOrLinkAsync</c>).
///
/// <para>Distinct from <see cref="AppointmentAccessorInvitedEto"/>, which fires
/// only for a brand-new account and carries a password-setup link. An existing
/// account needs no setup link -- it is simply notified it was added and can log
/// in to view the appointment. Fires for ALL appointment types, including
/// re-evaluation (carried-forward accessors are already provisioned, so they
/// always take the existing-account path and previously got nothing).</para>
/// </summary>
public class AppointmentAccessorAddedEto
{
    public Guid AppointmentId { get; set; }

    public Guid AccessorUserId { get; set; }

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
