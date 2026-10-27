using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// 2026-10-27 -- keeps a host operator's per-office shadow logins in step with their host account.
/// Office staff email goes to the shadow (a role lookup inside the office finds it), so a shadow that
/// outlives the account's state keeps mailing a person who was switched off, removed, moved to another
/// role, or given a new address. Each method here is the office-side consequence of one host change;
/// the caller has checked who may make it.
/// </summary>
public class HostOperatorOfficeLogins : ITransientDependency
{
    private readonly IIntakeShadowUserProvisioner _shadows;
    private readonly IntakeAssignmentRevoker _revoker;
    private readonly IRepository<IntakeOfficeAssignment, Guid> _assignments;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;

    public HostOperatorOfficeLogins(
        IIntakeShadowUserProvisioner shadows,
        IntakeAssignmentRevoker revoker,
        IRepository<IntakeOfficeAssignment, Guid> assignments,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant)
    {
        _shadows = shadows;
        _revoker = revoker;
        _assignments = assignments;
        _userManager = userManager;
        _currentTenant = currentTenant;
    }

    /// <summary>The operator was switched off or is about to be deleted: no office login stays usable.</summary>
    public virtual Task SwitchedOffAsync(Guid operatorUserId) => _shadows.DisableAllShadowUsersAsync(operatorUserId);

    /// <summary>
    /// The operator was switched back on. An Intake operator's offices are the ones they are assigned to,
    /// so those logins wake up now and office mail resumes at once. A Supervisor or IT Admin has no
    /// assignments: their login in an office wakes up the next time they enter it, as it was first made.
    /// </summary>
    public virtual async Task SwitchedOnAsync(Guid operatorUserId)
    {
        Guid[] officeIds;
        using (_currentTenant.Change(null))
        {
            var user = await _userManager.FindByIdAsync(operatorUserId.ToString());
            if (user == null || !await _userManager.IsInRoleAsync(user, InternalUserRoleDataSeedContributor.IntakeStaffRoleName))
            {
                return;
            }

            officeIds = (await _assignments.GetListAsync(x => x.OperatorUserId == operatorUserId))
                .Select(x => x.OfficeId)
                .Distinct()
                .ToArray();
        }

        foreach (var officeId in officeIds)
        {
            await _shadows.EnsureShadowUserAsync(officeId, operatorUserId, InternalUserRoleDataSeedContributor.IntakeStaffRoleName);
        }
    }

    /// <summary>
    /// The operator's address changed: every office login moves to the new one, so office mail follows
    /// the person rather than the address they used to have.
    /// </summary>
    public virtual Task AddressChangedAsync(Guid operatorUserId, string? previousEmail) =>
        _shadows.SyncShadowUsersAsync(operatorUserId, previousEmail);

    /// <summary>
    /// The operator's role changed. Leaving Intake Staff takes them off every office they were assigned
    /// to (the assignment rows go, as Staff Assignments' Unassign leaves it); and whatever the move, every
    /// office login they have is disabled, because it holds the OLD tier's office role. The next entry
    /// (Supervisor, IT Admin) or assignment (Intake) makes it again with the new one. Returns how many
    /// office assignments were removed.
    /// </summary>
    public virtual async Task<int> RoleChangedAsync(Guid operatorUserId, string previousRole)
    {
        var revoked = string.Equals(previousRole, InternalUserRoleDataSeedContributor.IntakeStaffRoleName, StringComparison.OrdinalIgnoreCase)
            ? await _revoker.RevokeAllAsync(operatorUserId)
            : 0;

        await _shadows.DisableAllShadowUsersAsync(operatorUserId);
        return revoked;
    }
}
