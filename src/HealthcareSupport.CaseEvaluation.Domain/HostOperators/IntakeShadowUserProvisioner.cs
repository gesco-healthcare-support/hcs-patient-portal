using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
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
            if (shadow == null || !shadow.IsActive)
            {
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
