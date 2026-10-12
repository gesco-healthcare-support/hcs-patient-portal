using System.Linq;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Narrows an identity-user lookup to what the caller is entitled to see.
/// </summary>
/// <remarks>
/// The scaffolded <c>GetIdentityUserLookupAsync</c> methods return every account in the office,
/// paged by the caller and searchable by email substring. The permission guarding them
/// (<c>.Default</c> on the owning service) is held by the external roles too, so without this a
/// patient or attorney could harvest the office's user directory -- patient logins included.
///
/// Only internal staff (<see cref="BookingFlowRoles.IsInternalUserCaller"/>) see the whole
/// office; the one screen that calls a lookup is an internal one. Every other caller -- an
/// external role, or an account with no role at all -- sees only their own account. This is an
/// allow-list on purpose: an unrecognised role is treated as external, not as staff.
/// </remarks>
internal static class IdentityUserLookupScope
{
    internal static IQueryable<IdentityUser> ForCaller(IQueryable<IdentityUser> query, ICurrentUser currentUser)
    {
        if (BookingFlowRoles.IsInternalUserCaller(currentUser.Roles))
        {
            return query;
        }

        var selfId = currentUser.Id;
        return selfId.HasValue
            ? query.Where(u => u.Id == selfId.Value)
            : query.Where(u => false);
    }
}
