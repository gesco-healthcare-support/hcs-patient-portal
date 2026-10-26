using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Takes an Intake operator off an office: removes the assignment row (in the host database) and locks the
/// per-office shadow login the assignment provisioned. Staff Assignments' Unassign uses it for one office;
/// changing a person's role from Intake Staff to Staff Supervisor uses it for every office they were
/// assigned (2026-10-26), so both leave exactly the same state behind. The caller has checked who may do
/// this.
/// </summary>
public class IntakeAssignmentRevoker : ITransientDependency
{
    private readonly IRepository<IntakeOfficeAssignment, Guid> _assignments;
    private readonly IIntakeShadowUserProvisioner _shadows;
    private readonly ICurrentTenant _currentTenant;

    public IntakeAssignmentRevoker(
        IRepository<IntakeOfficeAssignment, Guid> assignments,
        IIntakeShadowUserProvisioner shadows,
        ICurrentTenant currentTenant)
    {
        _assignments = assignments;
        _shadows = shadows;
        _currentTenant = currentTenant;
    }

    /// <summary>Takes the operator off one office. Harmless when they were not assigned to it.</summary>
    public virtual async Task RevokeAsync(Guid operatorUserId, Guid officeId)
    {
        using (_currentTenant.Change(null))
        {
            var row = await _assignments.FirstOrDefaultAsync(
                x => x.OperatorUserId == operatorUserId && x.OfficeId == officeId);
            if (row != null)
            {
                await _assignments.DeleteAsync(row, autoSave: true);
            }
        }

        // Defense in depth: lock the shadow user (the removed row already blocks the gate).
        await _shadows.DisableShadowUserAsync(officeId, operatorUserId);
    }

    /// <summary>Takes the operator off every office they are assigned to. Returns how many.</summary>
    public virtual async Task<int> RevokeAllAsync(Guid operatorUserId)
    {
        Guid[] officeIds;
        using (_currentTenant.Change(null))
        {
            officeIds = (await _assignments.GetListAsync(x => x.OperatorUserId == operatorUserId))
                .Select(x => x.OfficeId)
                .Distinct()
                .ToArray();
        }

        foreach (var officeId in officeIds)
        {
            await RevokeAsync(operatorUserId, officeId);
        }

        return officeIds.Length;
    }
}
