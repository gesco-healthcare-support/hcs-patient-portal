namespace HealthcareSupport.CaseEvaluation.Invitations;

/// <summary>
/// Lifecycle status of an <see cref="Invitation"/> as surfaced to the
/// internal "Pending Invites" management list. Derived (never stored) from
/// the row's IsDeleted / AcceptedAt / ExpiresAt fields by
/// <see cref="InvitationStatusResolver"/>.
/// </summary>
public enum InvitationStatus
{
    /// <summary>Issued, not yet accepted, not expired, not revoked.</summary>
    Pending = 0,

    /// <summary>The recipient completed registration (AcceptedAt set).</summary>
    Accepted = 1,

    /// <summary>Passed its ExpiresAt without being accepted.</summary>
    Expired = 2,

    /// <summary>Soft-deleted by staff via revoke; the token no longer validates.</summary>
    Revoked = 3,
}
