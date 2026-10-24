using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using HealthcareSupport.CaseEvaluation.Identity;
using HealthcareSupport.CaseEvaluation.InternalUsers;
using HealthcareSupport.CaseEvaluation.Notifications;
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
    /// <para>2026-10-23 -- it also owns how an account made here is switched on. A new account is
    /// ALWAYS created unconfirmed, whatever the form's "Email confirmed" box says, and is sent the
    /// activate-your-account email: choosing a password from that link is what confirms it. An edit
    /// can never turn confirmation on, and changing the address turns it off. The reason is
    /// appointment access: a party sees an appointment when their CONFIRMED email matches it, so a
    /// confirmed flag set by whoever filled in the form -- on an address that may be somebody
    /// else's -- would hand that person's appointments to the account.</para>
    ///
    /// <para>It is registered as a REPLACEMENT for the framework service. Volo publishes
    /// <c>/api/identity/users</c> from <see cref="IIdentityUserAppService"/>, and without the
    /// replacement that route would resolve to the framework class and bypass every guard here.</para>
    /// </summary>
    [Dependency(ReplaceServices = true)]
    [ExposeServices(typeof(IIdentityUserAppService), typeof(IdentityUserAppService), typeof(UserExtendedAppService))]
    public class UserExtendedAppService : IdentityUserAppService
    {
        private readonly IAccountActivationSender _activationSender;

        public UserExtendedAppService(IdentityUserManager userManager, IIdentityUserRepository userRepository, IIdentityRoleRepository roleRepository, IOrganizationUnitRepository organizationUnitRepository, IIdentityClaimTypeRepository identityClaimTypeRepository, IdentityProTwoFactorManager identityProTwoFactorManager, IOptions<IdentityOptions> identityOptions, IDistributedEventBus distributedEventBus, IOptions<AbpIdentityOptions> abpIdentityOptions, IPermissionChecker permissionChecker, IDistributedCache<IdentityUserDownloadTokenCacheItem, string> downloadTokenCache, IDistributedCache<ImportInvalidUsersCacheItem, string> importInvalidUsersCache, IdentitySessionManager identitySessionManager, IdentityUserTwoFactorChecker identityUserTwoFactorChecker, ICancellationTokenProvider cancellationTokenProvider, IAccountActivationSender activationSender) : base(userManager, userRepository, roleRepository, organizationUnitRepository, identityClaimTypeRepository, identityProTwoFactorManager, identityOptions, distributedEventBus, abpIdentityOptions, permissionChecker, downloadTokenCache, importInvalidUsersCache, identitySessionManager, identityUserTwoFactorChecker, cancellationTokenProvider)
        {
            _activationSender = activationSender;
        }

        /// <summary>Privileged roles and callers: see <see cref="PrivilegedAccounts"/>.</summary>
        private bool CallerIsPrivileged() => PrivilegedAccounts.CallerIsPrivileged(CurrentUser);

        private static bool IsPrivilegedRole(string role) => PrivilegedAccounts.IsPrivilegedRole(role);

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
        public override async Task<IdentityUserDto> CreateAsync(IdentityUserCreateDto input)
        {
            if (!CallerIsPrivileged() && (input.RoleNames ?? Array.Empty<string>()).Any(r => !IsStaffRole(r)))
            {
                throw Refusal();
            }

            // Never confirmed at birth, and never the framework's own bare confirmation mail: the
            // activation email below is the one this account gets.
            input.EmailConfirmed = false;
            input.SendConfirmationEmail = false;

            // Nobody types this account's password: the person chooses it from the activation
            // link. Whatever the form sent (the screen sends a random value it never shows, because
            // the framework's create contract requires one) is replaced by a value nobody ever
            // sees, so not even the creator, reading the request, knows a password for it.
            input.Password = UnusablePassword.Generate();
            input.ShouldChangePasswordOnNextLogin = false;

            var created = await base.CreateAsync(input);

            // Same unit of work as the create, so the account and its email land together or not at
            // all. A failure to queue it is NOT swallowed: an account that exists but was never sent
            // its link is exactly the dead end this replaces.
            var user = await UserManager.GetByIdAsync(created.Id);
            await _activationSender.SendAsync(user, NotificationOccurrence.PerRequest($"AccountActivation/IdentityUserCreated/{user.Id}"));

            return created;
        }

        [Authorize(IdentityPermissions.Users.Update)]
        public override async Task<IdentityUserDto> UpdateAsync(Guid id, IdentityUserUpdateDto input)
        {
            await EnsureTargetNotPrivilegedAsync(id);
            await EnsureRoleChangeAllowedAsync(id, input.RoleNames);

            var existing = await UserManager.FindByIdAsync(id.ToString());
            if (existing != null)
            {
                input.EmailConfirmed = ConfirmedAfterEdit(existing.EmailConfirmed, existing.Email, input.Email);
            }

            return await base.UpdateAsync(id, input);
        }

        /// <summary>
        /// What an edit leaves the email-confirmed flag as. An edit can only keep confirmation, and
        /// only when the address is unchanged; it can never turn it on. Confirmation comes from the
        /// mailbox (an activation or confirmation link), never from a form.
        /// </summary>
        internal static bool ConfirmedAfterEdit(bool storedConfirmed, string? storedEmail, string? requestedEmail) =>
            storedConfirmed
            && string.Equals(storedEmail?.Trim(), requestedEmail?.Trim(), StringComparison.OrdinalIgnoreCase);

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

        // 2026-10-23: user import is switched off (CaseEvaluationPermissionDefinitionProvider.DisableUserImport
        // disables the permission, so these are refused before they run). The overrides keep it off if that
        // permission is ever enabled again: an import takes passwords typed into the file and roles from it,
        // sends no activation email, and skips CreateAsync above.
        [Authorize(IdentityPermissions.Users.Import)]
        public override Task<ImportUsersFromFileOutput> ImportUsersFromFileAsync(ImportUsersFromFileInputWithStream input)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.UserImportDisabled);
        }

        [Authorize(IdentityPermissions.Users.Import)]
        public override Task<IdentityUserDto> ImportExternalUserAsync(ImportExternalUserInput input)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.UserImportDisabled);
        }
    }
}
