using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Ensures / disables the per-office limited shadow Intake user. Runs inside
/// <c>CurrentTenant.Change(officeId)</c> so every user read/write lands in that
/// office's physical database (proven via the impersonation spike). The shadow
/// user mirrors the operator's email + name, holds the per-tenant Intake Staff
/// role, and is auto-confirmed with an undisclosed random password (never used
/// for direct login -- it is purely an impersonation target).
///
/// <para>2026-10-27: a shadow is identified by the operator's host user id, stamped on it as
/// <see cref="OperatorIdProperty"/>, not by an address. Address lookups remain only to find
/// shadows made before the stamp existed, which are stamped the first time they are found.
/// Every ensure also brings the shadow's address and name up to date and leaves it holding
/// exactly the requested role, so a person moved between tiers is never two of them at once.</para>
/// </summary>
public class IntakeShadowUserProvisioner : DomainService, IIntakeShadowUserProvisioner
{
    /// <summary>The extra property on a shadow that holds its operator's host user id ("D" format).</summary>
    public const string OperatorIdProperty = "HostOperatorUserId";

    private readonly IdentityUserManager _userManager;
    private readonly ITenantRepository _tenantRepository;

    public IntakeShadowUserProvisioner(IdentityUserManager userManager, ITenantRepository tenantRepository)
    {
        _userManager = userManager;
        _tenantRepository = tenantRepository;
    }

    public async Task<Guid> EnsureShadowUserAsync(Guid officeId, Guid operatorUserId, string roleName)
    {
        Check.NotNullOrWhiteSpace(roleName, nameof(roleName));
        var op = await ResolveOperatorAsync(operatorUserId);

        using (CurrentTenant.Change(officeId))
        {
            var existing = await FindShadowAsync(operatorUserId, op.Email);
            if (existing != null)
            {
                await EnsureAdoptableAsync(existing, officeId, operatorUserId);

                // Idempotent: re-activate (a prior unassign may have disabled it), stamp a legacy
                // shadow, follow the operator's current address, and hold exactly the requested role.
                existing.SetIsActive(true);
                Stamp(existing, operatorUserId);
                await SyncAddressAsync(existing, op, officeId);
                await SetOnlyShadowRoleAsync(existing, roleName);
                (await _userManager.UpdateAsync(existing)).CheckErrors();
                return existing.Id;
            }

            // The address is already an account here that is not this operator's shadow (an office
            // administrator, or a shadow stamped for someone else, both of which the lookup never adopts):
            // refuse as EnsureAdoptableAsync does, rather than fail on a duplicate username or email.
            var holder = await _userManager.FindByNameAsync(op.Email) ?? await _userManager.FindByEmailAsync(op.Email);
            if (holder != null)
            {
                Logger.LogWarning(
                    "IntakeShadowUserProvisioner: refused to make a shadow for operator {OperatorUserId} in office "
                    + "{OfficeId}; account {AccountId} there already uses the operator's address.",
                    operatorUserId,
                    officeId,
                    holder.Id);
                throw new BusinessException(CaseEvaluationDomainErrorCodes.InternalUserDuplicateEmail);
            }

            var shadow = new IdentityUser(
                GuidGenerator.Create(),
                userName: op.Email,
                email: op.Email,
                tenantId: officeId)
            {
                Name = op.Name,
                Surname = op.Surname,
            };
            Stamp(shadow, operatorUserId);

            (await _userManager.CreateAsync(shadow, GenerateUndisclosedPassword())).CheckErrors();
            shadow.SetEmailConfirmed(true);
            (await _userManager.UpdateAsync(shadow)).CheckErrors();
            (await _userManager.AddToRoleAsync(shadow, roleName)).CheckErrors();

            return shadow.Id;
        }
    }

    public async Task DisableShadowUserAsync(Guid officeId, Guid operatorUserId)
    {
        var op = await ResolveOperatorAsync(operatorUserId);

        using (CurrentTenant.Change(officeId))
        {
            await DisableInCurrentOfficeAsync(officeId, operatorUserId, op.Email, warnWhenMissing: true);
        }
    }

    public async Task DisableAllShadowUsersAsync(Guid operatorUserId)
    {
        var op = await ResolveOperatorAsync(operatorUserId);

        foreach (var officeId in await GetOfficeIdsAsync())
        {
            using (CurrentTenant.Change(officeId))
            {
                // Most offices hold no shadow for any given operator, so a miss here is normal.
                await DisableInCurrentOfficeAsync(officeId, operatorUserId, op.Email, warnWhenMissing: false);
            }
        }
    }

    public async Task SyncShadowUsersAsync(Guid operatorUserId, string? previousEmail)
    {
        var op = await ResolveOperatorAsync(operatorUserId);

        foreach (var officeId in await GetOfficeIdsAsync())
        {
            using (CurrentTenant.Change(officeId))
            {
                // A shadow made before the stamp still carries the address the operator had then.
                var shadow = await FindShadowAsync(operatorUserId, previousEmail, op.Email);
                if (shadow == null || (await GetNonShadowRolesAsync(shadow)).Count > 0)
                {
                    continue;
                }

                Stamp(shadow, operatorUserId);
                await SyncAddressAsync(shadow, op, officeId);
                (await _userManager.UpdateAsync(shadow)).CheckErrors();
            }
        }
    }

    private async Task DisableInCurrentOfficeAsync(Guid officeId, Guid operatorUserId, string email, bool warnWhenMissing)
    {
        var shadow = await FindShadowAsync(operatorUserId, email);

        // #610: these two cases used to share one silent `return`, and they are not
        // the same thing.
        //
        // ALREADY INACTIVE is the normal idempotent path -- revoke called twice, or
        // an operator unassigned from an office they were already out of. Nothing to
        // say about it.
        //
        // NOT FOUND, when one office was asked about, is a failure: revoke could not
        // locate the shadow, so an unassigned operator may keep working access to that
        // office. Since 2026-10-27 the lookup is by the operator's id, so the remaining
        // cause is a shadow made before the stamp under an address the operator no longer
        // has (see FindShadowAsync).
        //
        // Logged rather than thrown deliberately. Throwing here would fail the whole
        // unassign operation over a shadow that may legitimately never have existed
        // (an operator assigned and unassigned before ever signing in), and the
        // caller cannot tell those apart either. A warning that names the office and
        // the operator makes the silence visible without inventing a failure.
        if (shadow == null)
        {
            if (warnWhenMissing)
            {
                Logger.LogWarning(
                    "IntakeShadowUserProvisioner: no shadow user found to revoke for operator "
                    + "{OperatorUserId} in office {OfficeId}. A shadow made before 2026-10-27 under a "
                    + "previous address of that operator may remain ACTIVE (#610).",
                    operatorUserId,
                    officeId);
            }
            return;
        }

        if (!shadow.IsActive)
        {
            return;
        }

        // The address matched an account that is not a shadow (see EnsureAdoptableAsync), so it
        // belongs to somebody else. Deactivating it would lock that person out of the office.
        var foreignRoles = await GetNonShadowRolesAsync(shadow);
        if (foreignRoles.Count > 0)
        {
            Logger.LogWarning(
                "IntakeShadowUserProvisioner: left account {AccountId} in office {OfficeId} active "
                + "on revoke for operator {OperatorUserId}; it holds non-shadow roles {Roles}.",
                shadow.Id,
                officeId,
                operatorUserId,
                foreignRoles);
            return;
        }
        shadow.SetIsActive(false);
        (await _userManager.UpdateAsync(shadow)).CheckErrors();
    }

    /// <summary>
    /// Finds this operator's shadow in the CURRENT office: the account stamped with the operator's
    /// id first, then, for a shadow made before the stamp existed, an unstamped (or same-stamped)
    /// account whose username or email is one of <paramref name="legacyAddresses"/>.
    ///
    /// <para>The legacy lookup is by ADDRESS ONLY, never by the operator's host username. The
    /// System owner's host username is <c>admin</c>, which is also every office admin's username,
    /// so a username lookup would hand the System owner the office's own admin account.</para>
    ///
    /// <para>History (#610, 2026-08-22): the address lookup is username first, email second,
    /// because a shadow whose email had diverged from its username was invisible to an email-only
    /// lookup, and the caller then tried to create a duplicate username. Keying on the id
    /// (2026-10-27) is what makes a changed operator address safe: before it, the next switch-in
    /// created a SECOND shadow under the new address and revoke left the first one active.</para>
    /// </summary>
    private async Task<IdentityUser?> FindShadowAsync(Guid operatorUserId, params string?[] legacyAddresses)
    {
        var key = operatorUserId.ToString("D");
        foreach (var role in ShadowRoleNames)
        {
            var stamped = (await _userManager.GetUsersInRoleAsync(role)).FirstOrDefault(u => StampOf(u) == key);
            if (stamped != null)
            {
                return stamped;
            }
        }

        foreach (var address in legacyAddresses.Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var byName = await _userManager.FindByNameAsync(address!);
            if (byName != null && await IsLegacyShadowCandidateAsync(byName, key))
            {
                return byName;
            }

            var byEmail = await _userManager.FindByEmailAsync(address!);
            if (byEmail != null && await IsLegacyShadowCandidateAsync(byEmail, key))
            {
                return byEmail;
            }
        }

        return null;
    }

    /// <summary>
    /// Whether an account matched by address may be taken as this operator's shadow: one stamped for this
    /// operator always; one stamped for anybody else never; an unstamped one only if it is not an office
    /// administrator.
    ///
    /// <para>An office's own administrator can carry an address a host operator also has (#593 lets one
    /// address exist in the host and in an office), as its email, or as its username when it was made on the
    /// office's Users screen. Adopting it would sign the operator in AS that office's administrator, strip its
    /// admin role on the next ensure, and switch it off when the operator is switched off. So it is never
    /// adopted; the operator gets a refusal instead (see <see cref="EnsureShadowUserAsync"/>).</para>
    ///
    /// <para>The cost: an IT Admin's shadow made before 2026-10-27 holds the admin role and no stamp, so it is
    /// refused too and needs a manual fix (stamp it, or remove it so a new one is made). Measured on the Azure
    /// server that day: none exists.</para>
    /// </summary>
    private async Task<bool> IsLegacyShadowCandidateAsync(IdentityUser user, string key)
    {
        var stamp = StampOf(user);
        if (stamp != null)
        {
            return stamp == key;
        }

        return !await _userManager.IsInRoleAsync(user, CaseEvaluationIdentityDataSeedContributor.AdminRoleName);
    }

    private static string? StampOf(IdentityUser user) =>
        user.GetProperty<string>(OperatorIdProperty)?.ToLowerInvariant();

    /// <summary>
    /// 2026-10-27 -- whether <paramref name="user"/> is an office login made for a host staff member (it carries
    /// the operator stamp). Such a login is reached only through "Enter practice"; the sign-in page and
    /// forgot-password refuse it. A shadow made before the stamp is covered once it is stamped, which happens on
    /// the operator's next entry or assignment.
    /// </summary>
    public static bool IsStampedShadow(IdentityUser user) => StampOf(user) != null;

    private static void Stamp(IdentityUser shadow, Guid operatorUserId) =>
        shadow.SetProperty(OperatorIdProperty, operatorUserId.ToString("D"));

    /// <summary>
    /// Brings the shadow's username, email and name up to the operator's current host values, so
    /// office mail sent to the shadow reaches the address the person has now. When the new address
    /// already belongs to a different account in this office, the shadow keeps its old one: the id
    /// stamp still finds it, and the warning says why its mail has not moved.
    /// </summary>
    private async Task SyncAddressAsync(IdentityUser shadow, OperatorDetails op, Guid officeId)
    {
        shadow.Name = op.Name;
        shadow.Surname = op.Surname;

        var sameUserName = string.Equals(shadow.UserName, op.Email, StringComparison.OrdinalIgnoreCase);
        var sameEmail = string.Equals(shadow.Email, op.Email, StringComparison.OrdinalIgnoreCase);
        if (sameUserName && sameEmail)
        {
            return;
        }

        var holder = await _userManager.FindByNameAsync(op.Email) ?? await _userManager.FindByEmailAsync(op.Email);
        if (holder != null && holder.Id != shadow.Id)
        {
            Logger.LogWarning(
                "IntakeShadowUserProvisioner: shadow {ShadowId} in office {OfficeId} keeps its previous address; "
                + "the operator's current one is held by account {AccountId} there.",
                shadow.Id,
                officeId,
                holder.Id);
            return;
        }

        if (!sameUserName)
        {
            (await _userManager.SetUserNameAsync(shadow, op.Email)).CheckErrors();
        }
        if (!sameEmail)
        {
            (await _userManager.SetEmailAsync(shadow, op.Email)).CheckErrors();
            shadow.SetEmailConfirmed(true);
        }
    }

    /// <summary>One person, one role: drops any other shadow role and adds the requested one.</summary>
    private async Task SetOnlyShadowRoleAsync(IdentityUser shadow, string roleName)
    {
        var held = await _userManager.GetRolesAsync(shadow);
        var others = held
            .Where(r => ShadowRoleNames.Contains(r, StringComparer.OrdinalIgnoreCase)
                        && !string.Equals(r, roleName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (others.Count > 0)
        {
            (await _userManager.RemoveFromRolesAsync(shadow, others)).CheckErrors();
        }
        if (!held.Contains(roleName, StringComparer.OrdinalIgnoreCase))
        {
            (await _userManager.AddToRoleAsync(shadow, roleName)).CheckErrors();
        }
    }

    private async Task<List<Guid>> GetOfficeIdsAsync()
    {
        using (CurrentTenant.Change(null))
        {
            return (await _tenantRepository.GetListAsync()).Select(t => t.Id).ToList();
        }
    }

    /// <summary>
    /// The only roles this provisioner ever grants a shadow: Intake Staff on assignment, and Staff
    /// Supervisor or the office admin role from the office-access grant.
    /// </summary>
    private static readonly string[] ShadowRoleNames =
    {
        InternalUserRoleDataSeedContributor.IntakeStaffRoleName,
        InternalUserRoleDataSeedContributor.StaffSupervisorRoleName,
        CaseEvaluationIdentityDataSeedContributor.AdminRoleName,
    };

    /// <summary>
    /// Refuses to adopt an office account that is not a shadow.
    ///
    /// <para>The lookup matches on the operator's address alone, and an office can hold an
    /// unrelated account carrying that address (#593 lets one address exist in the host and in an
    /// office). Adopting it would ADD a staff or admin role to, say, a patient or attorney account,
    /// and the office-access grant would then sign the operator in as that account. So an existing
    /// account is reused only when every role it holds is one this provisioner grants. An account
    /// with no roles is still adopted: that is the legacy shape the username lookup exists for.</para>
    ///
    /// <para>Thrown, not logged and skipped: the alternative is creating a second account under a
    /// username Identity already holds, which fails anyway. The message is the generic duplicate
    /// one and names no address.</para>
    /// </summary>
    private async Task EnsureAdoptableAsync(IdentityUser existing, Guid officeId, Guid operatorUserId)
    {
        var foreignRoles = await GetNonShadowRolesAsync(existing);
        if (foreignRoles.Count == 0)
        {
            return;
        }

        Logger.LogWarning(
            "IntakeShadowUserProvisioner: refused to adopt account {AccountId} in office {OfficeId} "
            + "as the shadow of operator {OperatorUserId}; it holds non-shadow roles {Roles}.",
            existing.Id,
            officeId,
            operatorUserId,
            foreignRoles);
        throw new BusinessException(CaseEvaluationDomainErrorCodes.InternalUserDuplicateEmail);
    }

    private async Task<List<string>> GetNonShadowRolesAsync(IdentityUser user) =>
        (await _userManager.GetRolesAsync(user))
            .Where(role => !ShadowRoleNames.Contains(role, StringComparer.OrdinalIgnoreCase))
            .ToList();

    private sealed record OperatorDetails(string Email, string? Name, string? Surname);

    private async Task<OperatorDetails> ResolveOperatorAsync(Guid operatorUserId)
    {
        using (CurrentTenant.Change(null))
        {
            var op = await _userManager.FindByIdAsync(operatorUserId.ToString());
            if (op == null)
            {
                throw new BusinessException(CaseEvaluationDomainErrorCodes.InternalUserNotFound)
                    .WithData("UserId", operatorUserId);
            }
            return new OperatorDetails(op.Email!, op.Name, op.Surname);
        }
    }

    /// <summary>
    /// A long random password that satisfies ABP's default complexity. It is
    /// never disclosed -- the shadow user is reached only via impersonation, so
    /// no human ever types it. The fixed "Aa1!" prefix guarantees the
    /// upper/lower/digit/symbol classes regardless of the random tail.
    /// </summary>
    private static string GenerateUndisclosedPassword()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return "Aa1!" + Convert.ToHexString(bytes);
    }
}
