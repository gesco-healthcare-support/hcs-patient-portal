using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;
using Volo.Saas.Editions;
using Volo.Saas.Host;
using Volo.Saas.Host.Dtos;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Doctors
{
    public class DoctorTenantAppService : TenantAppService
    {
        private readonly ITenantRepository _tenantRepository;
        private readonly IConnectionStringChecker _connectionStringChecker;
        private readonly ITenantConnectionStringProvider _connectionStringProvider;
        private readonly IOfficeDatabaseProvisioner _officeProvisioner;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public DoctorTenantAppService(ITenantRepository tenantRepository,
            IEditionRepository editionRepository,
            ITenantManager tenantManager,
            IDataSeeder dataSeeder,
            ILocalEventBus _localEventBus,
            IDistributedEventBus distributedEventBus,
            IOptions<AbpDbConnectionOptions> dbConnectionOptions,
            IConnectionStringChecker connectionStringChecker,
            ITenantConnectionStringProvider connectionStringProvider,
            IOfficeDatabaseProvisioner officeProvisioner,
            IUnitOfWorkManager unitOfWorkManager)
            : base(tenantRepository, editionRepository, tenantManager, dataSeeder, _localEventBus, distributedEventBus, dbConnectionOptions, connectionStringChecker)
        {
            _tenantRepository = tenantRepository;
            _connectionStringChecker = connectionStringChecker;
            _connectionStringProvider = connectionStringProvider;
            _officeProvisioner = officeProvisioner;
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

            // Derive the office slug (subdomain + database name) from the name and build
            // its connection string up front, so a bad name or an unreachable SQL server
            // fails before any tenant row is written. The office name must be a single
            // DNS-safe token because it is also the subdomain (see TenantNaming).
            string slug;
            try
            {
                slug = TenantNaming.DeriveSlug(input.Name!);
            }
            catch (ArgumentException ex)
            {
                throw new UserFriendlyException(ex.Message);
            }

            var connectionString = _connectionStringProvider.BuildConnectionString(slug);

            var checkResult = await _connectionStringChecker.CheckAsync(connectionString);
            if (!checkResult.Connected)
            {
                throw new UserFriendlyException(
                    $"Cannot reach the database server for office '{input.Name}'. Verify the SQL server is running and the connection template is correct.");
            }

            // Register the tenant and store its connection string in one host-DB
            // transaction. The TenantCreatedEto is outbox-deferred to this commit, so a
            // failure here rolls the tenant back and never provisions an office database.
            SaasTenantDto tenant;
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                tenant = await base.CreateAsync(input);

                var tenantEntity = await _tenantRepository.GetAsync(tenant.Id);
                tenantEntity.SetDefaultConnectionString(connectionString);
                await _tenantRepository.UpdateAsync(tenantEntity);

                await uow.CompleteAsync();
            }

            // Provision the office database AFTER the host-DB commit: creating + seeding a
            // separate database cannot share the host transaction. Seeds catalogs, the admin
            // user, and the office doctor (B2) into the office DB. On failure the office row
            // and its connection string remain, so a retry completes it (idempotent seeders).
            try
            {
                await _officeProvisioner.ProvisionAsync(tenant.Id, input.AdminEmailAddress, input.AdminPassword);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Provisioning the office database for tenant {TenantId} failed after creation.",
                    tenant.Id);
                throw new UserFriendlyException(
                    $"Office '{input.Name}' was created but its database could not be fully provisioned. Re-run provisioning to complete setup.");
            }

            return tenant;
        }
    }
}
