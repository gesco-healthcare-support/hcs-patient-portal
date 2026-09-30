using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;

/// <summary>
/// B12 -- the admin password handed to the seeder for the CURRENT tenant scope.
///
/// <para><b>Why the store is only asked when the admin is missing.</b> ABP's seeder creates the
/// admin only when no account by that name exists, and otherwise ignores the password it is given.
/// Asking the store unconditionally wrote an entry for every database on every run, including the
/// ones whose admin already had a password the entry did not match: every office made through the
/// New Practice screen (seeded with its own throwaway), every office created with a typed
/// password, and any admin already changed by hand. An operator reading one of those files would
/// hand over a password the account does not have.</para>
///
/// <para>So an entry now exists only where it was actually used: here, when this seeding pass is
/// about to create the admin, or in <see cref="AdminPasswordRotator"/>, when it moves an admin off
/// a published default.</para>
///
/// <para>When the admin exists, the value returned is a fresh generated password that is never
/// stored. The seeder ignores it; generating one rather than passing null keeps a published
/// default out of the seed context entirely, since ABP's contributor falls back to its own default
/// when the property is missing.</para>
/// </summary>
public class AdminSeedPasswordResolver : ITransientDependency
{
    private readonly IdentityUserManager _userManager;
    private readonly IAdminPasswordStore _adminPasswordStore;

    public AdminSeedPasswordResolver(IdentityUserManager userManager, IAdminPasswordStore adminPasswordStore)
    {
        _userManager = userManager;
        _adminPasswordStore = adminPasswordStore;
    }

    /// <summary>
    /// The stored password when the admin does not exist yet, otherwise an unstored throwaway.
    /// Must be called inside the tenant scope of the database being seeded.
    /// </summary>
    public virtual async Task<string> ResolveAsync(Guid? tenantId)
    {
        var admin = await _userManager.FindByNameAsync(CaseEvaluationConsts.AdminUserName);
        if (admin != null)
        {
            return AdminPasswordPolicy.Generate();
        }

        return await _adminPasswordStore.GetOrCreateAsync(tenantId);
    }
}
