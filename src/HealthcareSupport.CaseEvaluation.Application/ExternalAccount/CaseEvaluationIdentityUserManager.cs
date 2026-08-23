using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using Volo.Abp.Identity.Settings;
using Volo.Abp.Security.Claims;
using Volo.Abp.Settings;
using Volo.Abp.Threading;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Replaces ABP's <see cref="IdentityUserManager"/> to make account lockout PROGRESSIVE instead of a
/// flat hour. Item D (2026-08-22).
///
/// <para><b>Why an override.</b> Lockout duration is decided inside
/// <c>UserManager.AccessFailedAsync</c>, which sets
/// <c>LockoutEnd = now + DefaultLockoutTimeSpan</c> once the attempt threshold is reached. There is
/// no setting or event that can vary it per user, so the only seam is the method itself. This is the
/// first <c>IdentityUserManager</c> subclass in the codebase; the override is kept deliberately thin
/// and every decision lives in the pure <see cref="LockoutBackoff"/> helper, which is unit-tested.
/// The authentication path is not somewhere to put logic that cannot be tested.</para>
///
/// <para><b>Why a separate cycle counter.</b> The base <c>AccessFailedAsync</c> RESETS
/// <c>AccessFailedCount</c> to 0 at the moment it locks the account, so that field cannot tell a
/// first lockout from a fifth. The counter lives in <c>AbpUsers.ExtraProperties</c>
/// (<see cref="CaseEvaluationModuleExtensionConfigurator.LockoutCyclePropertyName"/>), which needs no
/// migration in either migration set.</para>
///
/// <para><b>Failure mode this must avoid.</b> If the counter is written but never reset, every user's
/// second lockout onward would be the maximum -- strictly worse than the flat hour it replaces. It is
/// zeroed on a successful sign-in (Identity calls <c>ResetAccessFailedCountAsync</c>) and on a
/// completed password reset (<c>ExternalAccountAppService.ResetPasswordAsync</c> calls the same
/// method), which are the only two ways a user legitimately regains access.</para>
///
/// <para>Registered via <c>ReplaceServices</c> in the Application layer rather than the AuthServer,
/// because the AuthServer resolves it from there -- the same arrangement
/// <c>CaseEvaluationAccountEmailer</c> already relies on.</para>
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IdentityUserManager))]
public class CaseEvaluationIdentityUserManager : IdentityUserManager
{
    /// <summary>Fallback matching the seeded default, used only if the setting cannot be read.</summary>
    private const int DefaultLockoutSeconds = 3600;

    private readonly ISettingProvider _settingProvider;

    public CaseEvaluationIdentityUserManager(
        IdentityUserStore store,
        IIdentityRoleRepository roleRepository,
        IIdentityUserRepository userRepository,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<IdentityUser> passwordHasher,
        IEnumerable<IUserValidator<IdentityUser>> userValidators,
        IEnumerable<IPasswordValidator<IdentityUser>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<IdentityUserManager> logger,
        ICancellationTokenProvider cancellationTokenProvider,
        IOrganizationUnitRepository organizationUnitRepository,
        ISettingProvider settingProvider,
        IDistributedEventBus distributedEventBus,
        IIdentityLinkUserRepository identityLinkUserRepository,
        IDistributedCache<AbpDynamicClaimCacheItem> dynamicClaimCache)
        : base(
            store,
            roleRepository,
            userRepository,
            optionsAccessor,
            passwordHasher,
            userValidators,
            passwordValidators,
            keyNormalizer,
            errors,
            services,
            logger,
            cancellationTokenProvider,
            organizationUnitRepository,
            settingProvider,
            distributedEventBus,
            identityLinkUserRepository,
            dynamicClaimCache)
    {
        _settingProvider = settingProvider;
    }

    /// <summary>
    /// Lets the base class count the failure and decide whether to lock, then -- only if it DID lock
    /// -- replaces the flat duration with this user's next rung on the ladder.
    ///
    /// <para>Hooks AFTER base rather than reimplementing the counting: if anything here throws, the
    /// worst case is ABP's stock lockout behaviour, never a bypass.</para>
    /// </summary>
    public override async Task<IdentityResult> AccessFailedAsync(IdentityUser user)
    {
        var result = await base.AccessFailedAsync(user);

        // A failed base call means nothing was counted; leave it entirely alone.
        if (!result.Succeeded)
        {
            return result;
        }

        // Below the threshold the base call does not touch LockoutEnd, and neither may we.
        if (!await IsLockedOutAsync(user))
        {
            return result;
        }

        var nextCycle = ExtraPropertyConverters.GetIntOrDefault(
            user,
            CaseEvaluationModuleExtensionConfigurator.LockoutCyclePropertyName) + 1;

        // Set the counter BEFORE SetLockoutEndDateAsync so that call's own persist carries it, rather
        // than needing a second write that could half-succeed.
        user.SetProperty(
            CaseEvaluationModuleExtensionConfigurator.LockoutCyclePropertyName,
            nextCycle);

        var configuredMaximum = TimeSpan.FromSeconds(
            await _settingProvider.GetAsync<int>(
                IdentitySettingNames.Lockout.LockoutDuration,
                DefaultLockoutSeconds));

        await SetLockoutEndDateAsync(
            user,
            DateTimeOffset.UtcNow + LockoutBackoff.DurationForCycle(nextCycle, configuredMaximum));

        return result;
    }

    /// <summary>
    /// Zeroes the escalation counter whenever the failure count is reset -- which Identity does on a
    /// successful sign-in, and which the password-reset path calls explicitly.
    ///
    /// <para>Without this the ladder ratchets and never comes back down, making every later lockout
    /// the maximum. The counter is set BEFORE the base call so the base class's own persist writes
    /// it.</para>
    /// </summary>
    public override Task<IdentityResult> ResetAccessFailedCountAsync(IdentityUser user)
    {
        user.SetProperty(CaseEvaluationModuleExtensionConfigurator.LockoutCyclePropertyName, 0);
        return base.ResetAccessFailedCountAsync(user);
    }
}
