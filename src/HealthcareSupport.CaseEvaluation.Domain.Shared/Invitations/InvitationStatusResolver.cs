using System;

namespace HealthcareSupport.CaseEvaluation.Invitations;

/// <summary>
/// Single source of truth for deriving an <see cref="InvitationStatus"/> from
/// an invitation's stored fields. Pure (no I/O, no ABP dependencies) so it is
/// unit-testable in isolation and gives the same answer on the entity, the
/// AppService projection, and any future caller.
///
/// <para>Precedence: Revoked (soft-deleted) wins over everything, then
/// Accepted, then Expired, else Pending. A revoked row is terminal even if it
/// had not yet expired; an accepted row reports Accepted regardless of
/// ExpiresAt.</para>
/// </summary>
public static class InvitationStatusResolver
{
    public static InvitationStatus Resolve(
        bool isDeleted,
        DateTime? acceptedAt,
        DateTime expiresAt,
        DateTime nowUtc)
    {
        if (isDeleted)
        {
            return InvitationStatus.Revoked;
        }
        if (acceptedAt.HasValue)
        {
            return InvitationStatus.Accepted;
        }
        if (expiresAt <= nowUtc)
        {
            return InvitationStatus.Expired;
        }
        return InvitationStatus.Pending;
    }
}
