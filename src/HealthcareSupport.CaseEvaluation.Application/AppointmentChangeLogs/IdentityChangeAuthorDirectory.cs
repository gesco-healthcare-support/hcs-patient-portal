using HealthcareSupport.CaseEvaluation.HostOperators;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeLogs;

/// <summary>
/// Looks authors up through the identity repository: the office user in the current (office)
/// scope, and a host operator in host scope. Names only; no emails leave this class.
/// </summary>
public class IdentityChangeAuthorDirectory : IChangeAuthorDirectory, ITransientDependency
{
    private readonly IIdentityUserRepository _users;
    private readonly ICurrentTenant _currentTenant;

    public IdentityChangeAuthorDirectory(IIdentityUserRepository users, ICurrentTenant currentTenant)
    {
        _users = users;
        _currentTenant = currentTenant;
    }

    public virtual async Task<ChangeAuthorUser?> FindOfficeUserAsync(Guid userId)
    {
        var user = await _users.FindAsync(userId, includeDetails: false);
        if (user == null)
        {
            return null;
        }
        var roles = await _users.GetRoleNamesAsync(userId);
        Guid? hostOperator = Guid.TryParse(
            user.GetProperty<string>(IntakeShadowUserProvisioner.OperatorIdProperty), out var stamp)
                ? stamp
                : null;
        return new ChangeAuthorUser(DisplayName(user), ChangeAuthorRoles.IsStaff(roles), hostOperator);
    }

    public virtual async Task<string?> FindHostOperatorNameAsync(Guid operatorId)
    {
        using (_currentTenant.Change(null))
        {
            var op = await _users.FindAsync(operatorId, includeDetails: false);
            return op == null ? null : DisplayName(op);
        }
    }

    private static string DisplayName(IdentityUser user)
    {
        var full = $"{user.Name} {user.Surname}".Trim();
        return full.Length > 0 ? full : user.UserName;
    }
}
