using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Who may be an appointment's Responsible User (2026-10-26): an account of the current office that is
/// switched ON and holds one of <see cref="BookingFlowRoles.InternalUserRoles"/>. One rule for both the
/// Approve dialog's list and the approval itself, so the server never stores someone the list would not
/// offer. Whoever is stored receives the appointment's "you are responsible" mail and its user queries,
/// and a switched-off account is typically an unassigned operator's office login, which keeps its role.
/// </summary>
public class ResponsibleUserCandidates : ITransientDependency
{
    private readonly IdentityUserManager _userManager;

    public ResponsibleUserCandidates(IdentityUserManager userManager)
    {
        _userManager = userManager;
    }

    /// <summary>The eligible accounts, once each (an admin-tier user may hold several internal roles).</summary>
    public virtual async Task<List<IdentityUser>> GetAsync()
    {
        var byId = new Dictionary<Guid, IdentityUser>();
        foreach (var roleName in BookingFlowRoles.InternalUserRoles)
        {
            foreach (var user in await _userManager.GetActiveUsersInRoleAsync(roleName))
            {
                byId[user.Id] = user;
            }
        }

        return byId.Values.ToList();
    }

    /// <summary>
    /// Refuses <paramref name="userId"/> unless <see cref="GetAsync"/> would offer it. An id from another
    /// office, a switched-off account, an external role and an unknown id all get the same refusal.
    /// </summary>
    public virtual async Task EnsureEligibleAsync(Guid userId)
    {
        if (!(await GetAsync()).Exists(user => user.Id == userId))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentResponsibleUserNotEligible);
        }
    }
}
