using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;

namespace HealthcareSupport.CaseEvaluation.HostOperators;

/// <summary>
/// Ensures / disables the per-office limited shadow Intake user. Runs inside
/// <c>CurrentTenant.Change(officeId)</c> so every user read/write lands in that
/// office's physical database (proven via the impersonation spike). The shadow
/// user mirrors the operator's email + name, holds the per-tenant Intake Staff
/// role, and is auto-confirmed with an undisclosed random password (never used
/// for direct login -- it is purely an impersonation target).
/// </summary>
public class IntakeShadowUserProvisioner : DomainService, IIntakeShadowUserProvisioner
{
    private readonly IdentityUserManager _userManager;

    public IntakeShadowUserProvisioner(IdentityUserManager userManager)
    {
        _userManager = userManager;
    }

    public async Task<Guid> EnsureShadowUserAsync(Guid officeId, Guid operatorUserId, string roleName)
    {
        Check.NotNullOrWhiteSpace(roleName, nameof(roleName));
        var (email, name, surname) = await ResolveOperatorAsync(operatorUserId);

        using (CurrentTenant.Change(officeId))
        {
            var existing = await FindShadowAsync(email);
            if (existing != null)
            {
                await EnsureAdoptableAsync(existing, officeId, operatorUserId);

                // Idempotent: re-activate (a prior unassign may have disabled it)
                // and guarantee the requested per-office role, then return.
                var changed = false;
                if (!existing.IsActive)
                {
                    existing.SetIsActive(true);
                    changed = true;
                }
                if (!await _userManager.IsInRoleAsync(existing, roleName))
                {
                    (await _userManager.AddToRoleAsync(existing, roleName)).CheckErrors();
                    changed = true;
                }
                if (changed)
                {
                    (await _userManager.UpdateAsync(existing)).CheckErrors();
                }
                return existing.Id;
            }

            var shadow = new IdentityUser(
                GuidGenerator.Create(),
                userName: email,
                email: email,
                tenantId: officeId)
            {
                Name = name,
                Surname = surname,
            };

            (await _userManager.CreateAsync(shadow, GenerateUndisclosedPassword())).CheckErrors();
            shadow.SetEmailConfirmed(true);
            (await _userManager.UpdateAsync(shadow)).CheckErrors();
            (await _userManager.AddToRoleAsync(shadow, roleName)).CheckErrors();

            return shadow.Id;
        }
    }

    public async Task DisableShadowUserAsync(Guid officeId, Guid operatorUserId)
    {
        var (email, _, _) = await ResolveOperatorAsync(operatorUserId);

        using (CurrentTenant.Change(officeId))
        {
            var shadow = await FindShadowAsync(email);

            // #610: these two cases used to share one silent `return`, and they are not
            // the same thing.
            //
            // ALREADY INACTIVE is the normal idempotent path -- revoke called twice, or
            // an operator unassigned from an office they were already out of. Nothing to
            // say about it.
            //
            // NOT FOUND is a failure. Revoke was asked to disable a shadow and could not
            // locate one, so it returns having done nothing, and an unassigned operator
            // keeps working access to that office. The known cause is an operator whose
            // host email CHANGED after their shadow was provisioned: the lookup is by the
            // new address, the shadow still carries the old one in both username and
            // email, so it misses. See FindShadowAsync for why keying on the operator's
            // user id is the real fix.
            //
            // Logged rather than thrown deliberately. Throwing here would fail the whole
            // unassign operation over a shadow that may legitimately never have existed
            // (an operator assigned and unassigned before ever signing in), and the
            // caller cannot tell those apart either. A warning that names the office and
            // the operator makes the silence visible without inventing a failure.
            if (shadow == null)
            {
                Logger.LogWarning(
                    "IntakeShadowUserProvisioner: no shadow user found to revoke for operator "
                    + "{OperatorUserId} in office {OfficeId}. If that operator's host email has "
                    + "changed since the shadow was provisioned, an ACTIVE shadow may remain "
                    + "under the previous address (#610).",
                    operatorUserId,
                    officeId);
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
    }

    /// <summary>
    /// Finds this operator's shadow in the CURRENT office, by USERNAME first and email second.
    ///
    /// <para>Username is the key that actually identifies a shadow. <see cref="EnsureShadowUserAsync"/>
    /// creates it with the operator's host email in BOTH fields, and username is what Identity enforces
    /// as unique. Looking up by email alone was the defect: a shadow whose email had diverged from its
    /// username was invisible here, so the caller fell through to create a user whose username was
    /// already taken and got <c>DuplicateUserName</c> back, which surfaced as a 403 from the token
    /// endpoint and locked the operator out of the office. The revoke path had the same defect and
    /// failed SILENTLY, leaving an unassigned operator's shadow active.</para>
    ///
    /// <para><b>Correction (2026-08-22).</b> An earlier version of this comment blamed the pre-Phase-D
    /// per-tenant seed for producing that divergence. That was wrong. The rows that exposed it had been
    /// repointed by hand in a local database (emails moved to <c>@example.test</c> so a dev stack would
    /// stop mailing real colleagues); the seed itself writes the same value to both fields.</para>
    ///
    /// <para><b>The case this does NOT fix.</b> If an operator CHANGES their host email, this method is
    /// asked for the new address while their existing shadow still carries the old one in both fields,
    /// so it misses, and the caller creates a SECOND shadow. Revoke then silently leaves the first one
    /// active. Keying the shadow on the operator's user id rather than any address is the real fix;
    /// see <c>docs/backlog.md</c>.</para>
    /// </summary>
    private async Task<IdentityUser?> FindShadowAsync(string email) =>
        await _userManager.FindByNameAsync(email)
        ?? await _userManager.FindByEmailAsync(email);

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

    private async Task<(string Email, string? Name, string? Surname)> ResolveOperatorAsync(Guid operatorUserId)
    {
        using (CurrentTenant.Change(null))
        {
            var op = await _userManager.FindByIdAsync(operatorUserId.ToString());
            if (op == null)
            {
                throw new BusinessException(CaseEvaluationDomainErrorCodes.InternalUserNotFound)
                    .WithData("UserId", operatorUserId);
            }
            return (op.Email!, op.Name, op.Surname);
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
