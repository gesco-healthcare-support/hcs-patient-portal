using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Caching;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using Volo.Abp.Threading;

namespace HealthcareSupport.CaseEvaluation.Users
{
    /// <summary>
    /// Extends ABP's <see cref="IdentityUserAppService"/> for project-specific
    /// identity hooks. Per OLD spec (Phase 0.1, 2026-05-01) Doctor is a non-user
    /// reference entity; this service no longer syncs IdentityUser updates into
    /// the Doctor row. Kept as the seam for future extension hooks.
    /// </summary>
    public class UserExtendedAppService : IdentityUserAppService
    {
        public UserExtendedAppService(IdentityUserManager userManager, IIdentityUserRepository userRepository, IIdentityRoleRepository roleRepository, IOrganizationUnitRepository organizationUnitRepository, IIdentityClaimTypeRepository identityClaimTypeRepository, IdentityProTwoFactorManager identityProTwoFactorManager, IOptions<IdentityOptions> identityOptions, IDistributedEventBus distributedEventBus, IOptions<AbpIdentityOptions> abpIdentityOptions, IPermissionChecker permissionChecker, IDistributedCache<IdentityUserDownloadTokenCacheItem, string> downloadTokenCache, IDistributedCache<ImportInvalidUsersCacheItem, string> importInvalidUsersCache, IdentitySessionManager identitySessionManager, IdentityUserTwoFactorChecker identityUserTwoFactorChecker, ICancellationTokenProvider cancellationTokenProvider) : base(userManager, userRepository, roleRepository, organizationUnitRepository, identityClaimTypeRepository, identityProTwoFactorManager, identityOptions, distributedEventBus, abpIdentityOptions, permissionChecker, downloadTokenCache, importInvalidUsersCache, identitySessionManager, identityUserTwoFactorChecker, cancellationTokenProvider)
        {
        }
    }
}
