using HealthcareSupport.CaseEvaluation.Doctors;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using Volo.Abp.Threading;

namespace HealthcareSupport.CaseEvaluation.Users
{
    public class UserExtendedAppService : IdentityUserAppService
    {
        private readonly IDoctorRepository _doctorRepository;

        public UserExtendedAppService(IdentityUserManager userManager, IIdentityUserRepository userRepository, IIdentityRoleRepository roleRepository, IOrganizationUnitRepository organizationUnitRepository, IIdentityClaimTypeRepository identityClaimTypeRepository, IdentityProTwoFactorManager identityProTwoFactorManager, IOptions<IdentityOptions> identityOptions, IDistributedEventBus distributedEventBus, IOptions<AbpIdentityOptions> abpIdentityOptions, IPermissionChecker permissionChecker, IDistributedCache<IdentityUserDownloadTokenCacheItem, string> downloadTokenCache, IDistributedCache<ImportInvalidUsersCacheItem, string> importInvalidUsersCache, IdentitySessionManager identitySessionManager, IdentityUserTwoFactorChecker identityUserTwoFactorChecker, ICancellationTokenProvider cancellationTokenProvider, IDoctorRepository doctorRepository) : base(userManager, userRepository, roleRepository, organizationUnitRepository, identityClaimTypeRepository, identityProTwoFactorManager, identityOptions, distributedEventBus, abpIdentityOptions, permissionChecker, downloadTokenCache, importInvalidUsersCache, identitySessionManager, identityUserTwoFactorChecker, cancellationTokenProvider)
        {
            _doctorRepository = doctorRepository;
        }

        public override async Task<IdentityUserDto> UpdateAsync(Guid id, IdentityUserUpdateDto input)
        {
            var result = await base.UpdateAsync(id, input);

            var doctor = await _doctorRepository.FirstOrDefaultAsync(x => x.IdentityUserId == id);
            if (doctor == null)
            {
                return result;
            }

            if (!string.IsNullOrWhiteSpace(input.Name))
            {
                doctor.FirstName = input.Name;
            }

            if (!string.IsNullOrWhiteSpace(input.Surname))
            {
                doctor.LastName = input.Surname;
            }

            if (!string.IsNullOrWhiteSpace(input.Email))
            {
                doctor.Email = input.Email;
            }

            await _doctorRepository.UpdateAsync(doctor, autoSave: true);

            return result;
        }
    }
}