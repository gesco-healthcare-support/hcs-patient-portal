using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.InternalUsers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using Volo.Abp.Threading;

namespace HealthcareSupport.CaseEvaluation.Users
{
    /// <summary>
    /// Extends ABP's <see cref="IdentityUserAppService"/>. Per OLD spec (Phase 0.1, 2026-05-01)
    /// Doctor is a non-user reference entity, so nothing syncs identity updates into a Doctor row.
    ///
    /// <para>The one thing this service adds is a restriction on who may change privileged
    /// accounts. Staff Supervisor holds <c>AbpIdentity.Users.Update</c> so the internal-users hub
    /// can activate and deactivate staff, but that permission alone lets a caller change roles and
    /// passwords of ANY user. So every method that can change a user's roles, credentials, status
    /// or existence is guarded in code: a caller who is not IT Admin or admin may not touch an
    /// account that holds one of those roles, may only assign or remove the staff roles in
    /// <see cref="InternalUsersAppService.CreatableRoleNames"/>, and may not change their own
    /// roles. IT Admin and admin are unrestricted.</para>
    ///
    /// <para>It is registered as a REPLACEMENT for the framework service. Volo publishes
    /// <c>/api/identity/users</c> from <see cref="IIdentityUserAppService"/>, and without the
    /// replacement that route would resolve to the framework class and bypass every guard here.</para>
    /// </summary>
    [Dependency(ReplaceServices = true)]
    [ExposeServices(typeof(IIdentityUserAppService), typeof(IdentityUserAppService), typeof(UserExtendedAppService))]
    public class UserExtendedAppService : IdentityUserAppService
    {
        public UserExtendedAppService(IdentityUserManager userManager, IIdentityUserRepository userRepository, IIdentityRoleRepository roleRepository, IOrganizationUnitRepository organizationUnitRepository, IIdentityClaimTypeRepository identityClaimTypeRepository, IdentityProTwoFactorManager identityProTwoFactorManager, IOptions<IdentityOptions> identityOptions, IDistributedEventBus distributedEventBus, IOptions<AbpIdentityOptions> abpIdentityOptions, IPermissionChecker permissionChecker, IDistributedCache<IdentityUserDownloadTokenCacheItem, string> downloadTokenCache, IDistributedCache<ImportInvalidUsersCacheItem, string> importInvalidUsersCache, IdentitySessionManager identitySessionManager, IdentityUserTwoFactorChecker identityUserTwoFactorChecker, ICancellationTokenProvider cancellationTokenProvider) : base(userManager, userRepository, roleRepository, organizationUnitRepository, identityClaimTypeRepository, identityProTwoFactorManager, identityOptions, distributedEventBus, abpIdentityOptions, permissionChecker, downloadTokenCache, importInvalidUsersCache, identitySessionManager, identityUserTwoFactorChecker, cancellationTokenProvider)
        {
        }

        /// <summary>
        /// Roles whose holders are privileged. "IT Admin" is the host technical role and "admin"
        /// the framework's office/host administrator role.
        /// </summary>
        private static readonly string[] PrivilegedRoleNames =
            { InternalUserRoleDataSeedContributor.ItAdminRoleName, "admin" };

        private bool CallerIsPrivileged() => PrivilegedRoleNames.Any(r => CurrentUser.IsInRole(r));

        private static bool IsPrivilegedRole(string role) =>
            PrivilegedRoleNames.Contains(role, StringComparer.OrdinalIgnoreCase);

        private static bool IsStaffRole(string role) =>
            InternalUsersAppService.CreatableRoleNames.Contains(role, StringComparer.OrdinalIgnoreCase);

        private static BusinessException Refusal() =>
            new(CaseEvaluationDomainErrorCodes.PrivilegedUserChangeNotAllowed);

        /// <summary>
        /// Refuses a change to an account that holds a privileged role, unless the caller is
        /// privileged. An unknown id is left to the base service, which reports it as not found.
        /// </summary>
        private async Task EnsureTargetNotPrivilegedAsync(Guid id)
        {
            if (CallerIsPrivileged())
            {
                return;
            }

            var target = await UserManager.FindByIdAsync(id.ToString());
            if (target == null)
            {
                return;
            }

            var held = await UserManager.GetRolesAsync(target);
            if (held.Any(IsPrivilegedRole))
            {
                throw Refusal();
            }
        }

        /// <summary>
        /// A non-privileged caller may only add or remove roles in
        /// <see cref="InternalUsersAppService.CreatableRoleNames"/>, and may never change their
        /// own. A request that resends the roles the user already holds changes nothing and
        /// passes, which is what the activate/deactivate toggle sends.
        /// </summary>
        private async Task EnsureRoleChangeAllowedAsync(Guid id, IEnumerable<string>? requested)
        {
            if (requested == null || CallerIsPrivileged())
            {
                return;
            }

            var target = await UserManager.FindByIdAsync(id.ToString());
            if (target == null)
            {
                return;
            }

            var held = new HashSet<string>(await UserManager.GetRolesAsync(target), StringComparer.OrdinalIgnoreCase);
            var wanted = new HashSet<string>(requested, StringComparer.OrdinalIgnoreCase);
            var changed = held.Except(wanted).Concat(wanted.Except(held)).ToList();
            if (changed.Count == 0)
            {
                return;
            }

            if (CurrentUser.Id == id || !changed.All(IsStaffRole))
            {
                throw Refusal();
            }
        }

        [Authorize(IdentityPermissions.Users.Create)]
        public override Task<IdentityUserDto> CreateAsync(IdentityUserCreateDto input)
        {
            if (!CallerIsPrivileged() && (input.RoleNames ?? Array.Empty<string>()).Any(r => !IsStaffRole(r)))
            {
                throw Refusal();
            }

            return base.CreateAsync(input);
        }

        [Authorize(IdentityPermissions.Users.Update)]
        public override async Task<IdentityUserDto> UpdateAsync(Guid id, IdentityUserUpdateDto input)
        {
            await EnsureTargetNotPrivilegedAsync(id);
            await EnsureRoleChangeAllowedAsync(id, input.RoleNames);
            return await base.UpdateAsync(id, input);
        }

        [Authorize(IdentityPermissions.Users.Update)]
        public override async Task UpdateRolesAsync(Guid id, IdentityUserUpdateRolesDto input)
        {
            await EnsureTargetNotPrivilegedAsync(id);
            await EnsureRoleChangeAllowedAsync(id, input.RoleNames);
            await base.UpdateRolesAsync(id, input);
        }

        [Authorize(IdentityPermissions.Users.Delete)]
        public override async Task DeleteAsync(Guid id)
        {
            await EnsureTargetNotPrivilegedAsync(id);
            await base.DeleteAsync(id);
        }

        [Authorize(IdentityPermissions.Users.Update)]
        public override async Task UpdateClaimsAsync(Guid id, List<IdentityUserClaimDto> input)
        {
            await EnsureTargetNotPrivilegedAsync(id);
            await base.UpdateClaimsAsync(id, input);
        }

        [Authorize(IdentityPermissions.Users.Update)]
        public override async Task LockAsync(Guid id, DateTime lockoutEnd)
        {
            await EnsureTargetNotPrivilegedAsync(id);
            await base.LockAsync(id, lockoutEnd);
        }

        [Authorize(IdentityPermissions.Users.Update)]
        public override async Task UnlockAsync(Guid id)
        {
            await EnsureTargetNotPrivilegedAsync(id);
            await base.UnlockAsync(id);
        }

        [Authorize(IdentityPermissions.Users.Update)]
        public override async Task UpdatePasswordAsync(Guid id, IdentityUserUpdatePasswordInput input)
        {
            await EnsureTargetNotPrivilegedAsync(id);
            await base.UpdatePasswordAsync(id, input);
        }

        [Authorize(IdentityPermissions.Users.Update)]
        public override async Task SetTwoFactorEnabledAsync(Guid id, bool enabled)
        {
            await EnsureTargetNotPrivilegedAsync(id);
            await base.SetTwoFactorEnabledAsync(id, enabled);
        }
    }
}
