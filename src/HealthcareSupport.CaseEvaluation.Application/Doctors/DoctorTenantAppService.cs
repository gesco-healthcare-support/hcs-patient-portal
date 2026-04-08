using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.Uow;
using Volo.Saas.Editions;
using Volo.Saas.Host;
using Volo.Saas.Host.Dtos;
using Volo.Saas.Tenants;
using static Volo.Abp.Identity.Settings.IdentitySettingNames;
using static Volo.Abp.UI.Navigation.DefaultMenuNames.Application;
using static Volo.Saas.Host.SaasHostPermissions;

namespace HealthcareSupport.CaseEvaluation.Doctors
{
    public class DoctorTenantAppService : TenantAppService
    {
        private readonly IdentityUserManager _userManager;
        private readonly IdentityRoleManager _roleManager;
        private readonly IRepository<Doctor, Guid> _doctorRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        public DoctorTenantAppService(ITenantRepository tenantRepository,
            IEditionRepository editionRepository,
            ITenantManager tenantManager,
            IDataSeeder dataSeeder,
            ILocalEventBus _localEventBus,
            IDistributedEventBus distributedEventBus,
            IOptions<AbpDbConnectionOptions> dbConnectionOptions,
            IConnectionStringChecker connectionStringChecker,
            IdentityUserManager userManager,
            IdentityRoleManager roleManager,
            IRepository<Doctor, Guid> doctorRepository,
            IUnitOfWorkManager unitOfWorkManager)
            : base(tenantRepository, editionRepository, tenantManager, dataSeeder, _localEventBus, distributedEventBus, dbConnectionOptions, connectionStringChecker)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _doctorRepository = doctorRepository;
            _unitOfWorkManager = unitOfWorkManager;
        }
        public override async Task<SaasTenantDto> CreateAsync(SaasTenantCreateDto input)
        {
            Check.NotNull(input, nameof(input));
            Check.NotNullOrWhiteSpace(input.Name, nameof(input.Name));
            Check.NotNullOrWhiteSpace(input.AdminPassword, nameof(input.AdminPassword));
            Check.NotNullOrWhiteSpace(input.AdminEmailAddress, nameof(input.AdminEmailAddress));

            SaasTenantDto tenant;
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
            {
                tenant = await base.CreateAsync(input);
                await uow.CompleteAsync();
            }
            using (CurrentTenant.Change(tenant.Id))
            {
                var adminUser = await CreateDoctorUserAsync(input);
                //Create Doctor record
                await CreateDoctorProfileAsync(adminUser, input);
                //Create doctor role
                await EnsureRoleAsync("Doctor");
            }
            return tenant;
        }
        private async Task<IdentityUser> CreateDoctorUserAsync(SaasTenantCreateDto input)
        {
            var existingUser = await _userManager.FindByEmailAsync(input.AdminEmailAddress);
            if (existingUser != null)
            {
                existingUser.Name = input.Name;
                var updateResult = await _userManager.UpdateAsync(existingUser);
                if (!updateResult.Succeeded)
                {
                    throw new UserFriendlyException("Failed to update admin user: " +
                        string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                }

                return existingUser;
            }
            var adminUser = new IdentityUser(
                GuidGenerator.Create(),
                userName: input.AdminEmailAddress,
                email: input.AdminEmailAddress,
                CurrentTenant.Id
            )
            {
                Name = input.Name
            };

            // Create with default password
            var result = await _userManager.CreateAsync(adminUser, input.AdminPassword);

            if (!result.Succeeded)
            {
                throw new UserFriendlyException("Failed to create admin user: " +
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            return adminUser;
        }
        private async Task EnsureRoleAsync(string roleName)
        {
            var newRole = await _roleManager.FindByNameAsync(roleName);
            if (newRole != null)
            {
                return;
            }

            newRole = new IdentityRole(GuidGenerator.Create(), roleName, CurrentTenant.Id);
            var roleResult = await _roleManager.CreateAsync(newRole);
            if (!roleResult.Succeeded)
            {
                throw new UserFriendlyException("Failed to create admin role: " +
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
            }
        }
        private async Task CreateDoctorProfileAsync(IdentityUser user, SaasTenantCreateDto input)
        {
            var existingDoctor = await _doctorRepository.FirstOrDefaultAsync(x => x.IdentityUserId == user.Id);
            if (existingDoctor != null)
            {
                existingDoctor.FirstName = input.Name;
                existingDoctor.Email = user.Email;
                await _doctorRepository.UpdateAsync(existingDoctor, autoSave: true);
                return;
            }

            var doctor = new Doctor(
                id: GuidGenerator.Create(),
                identityUserId: user.Id,
                firstName: input.Name,
                lastName: "",
                email: user.Email,
                gender: Gender.Male
            );

            await _doctorRepository.InsertAsync(doctor, autoSave: true);
        }
    }
}