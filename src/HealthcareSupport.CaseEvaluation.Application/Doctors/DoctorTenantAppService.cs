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
            IRepository<Doctor, Guid> doctorRepository,
            IUnitOfWorkManager unitOfWorkManager)
            : base(tenantRepository, editionRepository, tenantManager, dataSeeder, _localEventBus, distributedEventBus, dbConnectionOptions, connectionStringChecker)
        {
            _userManager = userManager;
            _doctorRepository = doctorRepository;
            _unitOfWorkManager = unitOfWorkManager;
        }
        // ADR-006 (2026-05-05) -- "admin" is reserved for the host-context
        // surface (admin.localhost). A tenant by that name would conflict
        // with the SPA's no-subdomain redirect target and break the URL =
        // tenant invariant. Match is case-insensitive on the trimmed name.
        public const string ReservedTenantNameAdmin = "admin";

        public override async Task<SaasTenantDto> CreateAsync(SaasTenantCreateDto input)
        {
            Check.NotNull(input, nameof(input));
            Check.NotNullOrWhiteSpace(input.Name, nameof(input.Name));
            Check.NotNullOrWhiteSpace(input.AdminPassword, nameof(input.AdminPassword));
            Check.NotNullOrWhiteSpace(input.AdminEmailAddress, nameof(input.AdminEmailAddress));

            if (string.Equals(input.Name?.Trim(), ReservedTenantNameAdmin, StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFriendlyException(
                    $"Tenant name '{ReservedTenantNameAdmin}' is reserved for the host-context surface and cannot be used.");
            }

            // Single transactional UoW wraps SaasTenant + IdentityUser + Doctor.
            // Failure anywhere rolls back the SaasTenant insert and suppresses the
            // TenantCreatedEto distributed event (ABP outbox defers to UoW commit),
            // so the tenant DB never gets provisioned on failure either.
            //
            // Doctor is a non-user reference entity per OLD spec (Phase 0.1, 2026-05-01):
            // Staff Supervisor manages the Doctor on its behalf. The tenant admin user
            // gets the "admin" role only.
            SaasTenantDto tenant;
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                tenant = await base.CreateAsync(input);
                using (CurrentTenant.Change(tenant.Id))
                {
                    var adminUser = await CreateAdminUserAsync(input);
                    await CreateDoctorProfileAsync(adminUser, input);
                }
                await uow.CompleteAsync();
            }
            return tenant;
        }
        private async Task<IdentityUser> CreateAdminUserAsync(SaasTenantCreateDto input)
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

            var result = await _userManager.CreateAsync(adminUser, input.AdminPassword);

            if (!result.Succeeded)
            {
                throw new UserFriendlyException("Failed to create admin user: " +
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }

            return adminUser;
        }
        private async Task CreateDoctorProfileAsync(IdentityUser user, SaasTenantCreateDto input)
        {
            var existingDoctor = await _doctorRepository.FirstOrDefaultAsync(x => x.Email == user.Email);
            if (existingDoctor != null)
            {
                existingDoctor.FirstName = input.Name;
                existingDoctor.Email = user.Email;
                await _doctorRepository.UpdateAsync(existingDoctor, autoSave: true);
                return;
            }

            var doctor = new Doctor(
                id: GuidGenerator.Create(),
                firstName: input.Name,
                lastName: "",
                email: user.Email,
                gender: Gender.Male
            );

            await _doctorRepository.InsertAsync(doctor, autoSave: true);
        }
    }
}