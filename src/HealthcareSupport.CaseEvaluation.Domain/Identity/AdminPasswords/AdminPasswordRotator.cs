using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;

/// <summary>
/// B12 decision D1 -- outside Development, an admin still holding a published default password is
/// moved onto the stored generated one, and made to change it at next sign-in.
///
/// <para><b>Why rotation and not just better seeding.</b> Seeding only helps a database created
/// AFTER this change. Every database that already exists -- the host and every office on the
/// in-house server, plus anything restored from a backup -- was seeded with a password published in
/// the framework source and in this public repository, and would keep it forever. Rotation is what
/// makes "no manual step on the existing box" true.</para>
///
/// <para>The check is a real password verification, not a flag or a marker column. Nothing records
/// that an account is on a default, and an installation that has already changed its password by
/// hand must not be disturbed -- so the only honest test is whether the published value still
/// authenticates.</para>
///
/// <para>Nothing here logs a password, either the old one or the new one. The log line names the
/// database.</para>
/// </summary>
public class AdminPasswordRotator : ITransientDependency
{
    private readonly IdentityUserManager _userManager;
    private readonly IAdminPasswordStore _adminPasswordStore;
    private readonly IHostEnvironment _hostEnvironment;

    public AdminPasswordRotator(
        IdentityUserManager userManager,
        IAdminPasswordStore adminPasswordStore,
        IHostEnvironment hostEnvironment)
    {
        _userManager = userManager;
        _adminPasswordStore = adminPasswordStore;
        _hostEnvironment = hostEnvironment;

        Logger = NullLogger<AdminPasswordRotator>.Instance;
    }

    public ILogger<AdminPasswordRotator> Logger { get; set; }

    /// <summary>
    /// Rotates the admin of the CURRENT tenant scope if it still authenticates with a published
    /// default. Returns true when a rotation happened.
    ///
    /// <para>No-op in Development, which is the control: a local clone keeps the documented
    /// credentials and every runbook, onboarding page and docker guide stays correct.</para>
    /// </summary>
    public virtual async Task<bool> RotateIfOnAKnownDefaultAsync(Guid? tenantId, string databaseName)
    {
        Check.NotNullOrWhiteSpace(databaseName, nameof(databaseName));

        if (_hostEnvironment.IsDevelopment())
        {
            return false;
        }

        var admin = await _userManager.FindByNameAsync(CaseEvaluationConsts.AdminUserName);
        if (admin == null)
        {
            // Not an error. A database may legitimately have no account by that name -- it is only
            // created by the seeder, and an installation may have renamed or removed it.
            return false;
        }

        if (!await IsOnAKnownDefaultAsync(admin))
        {
            return false;
        }

        await ResetToStoredPasswordAsync(admin, tenantId);

        Logger.LogWarning("Rotated the admin password of {Database}.", databaseName);
        return true;
    }

    private async Task<bool> IsOnAKnownDefaultAsync(IdentityUser admin)
    {
        foreach (var known in AdminPasswordPolicy.KnownDefaults)
        {
            if (await _userManager.CheckPasswordAsync(admin, known))
            {
                return true;
            }
        }

        return false;
    }

    private async Task ResetToStoredPasswordAsync(IdentityUser admin, Guid? tenantId)
    {
        var replacement = await _adminPasswordStore.GetOrCreateAsync(tenantId);

        // Through a reset token rather than RemovePassword + AddPassword. The two-step form leaves
        // the account with NO password in between, so a process that dies there leaves an account
        // that cannot be signed into and whose recovery needs database access.
        var token = await _userManager.GeneratePasswordResetTokenAsync(admin);
        (await _userManager.ResetPasswordAsync(admin, token, replacement)).CheckErrors();

        // The operator reads the generated password out of the store to hand it over, so it is
        // known to more than one person by construction. Forcing a change makes that handover
        // credential rather than the account's lasting password.
        admin.SetShouldChangePasswordOnNextLogin(true);
        (await _userManager.UpdateAsync(admin)).CheckErrors();
    }
}
