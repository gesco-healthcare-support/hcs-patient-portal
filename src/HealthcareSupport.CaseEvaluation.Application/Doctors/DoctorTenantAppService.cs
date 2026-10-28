using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Practices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
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
        private readonly IRepository<OfficeBranding, Guid> _brandingRepository;
        private readonly IAccountActivationSender _activationSender;

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
            IUnitOfWorkManager unitOfWorkManager,
            IRepository<OfficeBranding, Guid> brandingRepository,
            IAccountActivationSender activationSender)
            : base(tenantRepository, editionRepository, tenantManager, dataSeeder, _localEventBus, distributedEventBus, dbConnectionOptions, connectionStringChecker)
        {
            _activationSender = activationSender;
            _tenantRepository = tenantRepository;
            _connectionStringChecker = connectionStringChecker;
            _connectionStringProvider = connectionStringProvider;
            _officeProvisioner = officeProvisioner;
            _unitOfWorkManager = unitOfWorkManager;
            _brandingRepository = brandingRepository;
        }

        // ADR-006 (2026-05-05) -- "admin" is reserved for the host-context
        // surface (admin.localhost). A tenant by that name would conflict
        // with the SPA's no-subdomain redirect target and break the URL =
        // tenant invariant. Match is case-insensitive on the trimmed name.
        public const string ReservedTenantNameAdmin = "admin";

        public override async Task<SaasTenantDto> CreateAsync(SaasTenantCreateDto input)
        {
            EnsureHostCaller();
            Check.NotNull(input, nameof(input));
            Check.NotNullOrWhiteSpace(input.Name, nameof(input.Name));
            Check.NotNullOrWhiteSpace(input.AdminPassword, nameof(input.AdminPassword));
            Check.NotNullOrWhiteSpace(input.AdminEmailAddress, nameof(input.AdminEmailAddress));
            RefuseSuppliedConnectionStrings(input.ConnectionStrings);

            // The office name IS the subdomain, so it must be a single DNS-safe token.
            var slug = DeriveSlugOrThrow(input.Name!);
            return await CreateTenantWithOfficeDatabaseAsync(input, slug, null, null, null);
        }

        /// <summary>
        /// The office's database as its row stores it: "Database=CaseEvaluation_{slug}", never a server
        /// or login. A row written by an older build is shown the same way, read through the same parser
        /// the resolver uses, so this screen never displays credentials. Named databases are not used by
        /// offices, so the list is always empty.
        /// </summary>
        [Authorize(SaasHostPermissions.Tenants.ManageConnectionStrings)]
        public override async Task<SaasTenantConnectionStringsDto> GetConnectionStringsAsync(Guid id)
        {
            EnsureHostCaller();
            var tenant = await _tenantRepository.GetAsync(id);
            return new SaasTenantConnectionStringsDto
            {
                Default = TenantNaming.TryGetSlugFromStoredConnectionString(tenant.FindDefaultConnectionString(), out var slug)
                    ? TenantNaming.BuildStoredConnectionString(slug)
                    : null,
                Databases = new List<SaasTenantDatabaseConnectionStringsDto>(),
            };
        }

        /// <summary>
        /// Refused. An office's database is fixed at creation and its server and login come from
        /// configuration, so there is nothing here an operator may change. Accepting a value would
        /// let this screen point an office at another database, including another office's.
        /// </summary>
        [Authorize(SaasHostPermissions.Tenants.ManageConnectionStrings)]
        public override Task UpdateConnectionStringsAsync(Guid id, SaasTenantConnectionStringsDto input)
        {
            EnsureHostCaller();
            throw new BusinessException(CaseEvaluationDomainErrorCodes.OfficeDatabaseSettingIsFixed);
        }

        // The stock create screen can carry connection strings. Refused rather than ignored, for the
        // reason above, and so that nothing it carries is ever stored on the row.
        private static void RefuseSuppliedConnectionStrings(SaasTenantConnectionStringsDto? supplied)
        {
            if (supplied != null &&
                (!supplied.Default.IsNullOrWhiteSpace() || (supplied.Databases?.Count ?? 0) > 0))
            {
                throw new BusinessException(CaseEvaluationDomainErrorCodes.OfficeDatabaseSettingIsFixed);
            }
        }

        /// <summary>
        /// Creates a COMPLETE practice: the SaaS tenant + its provisioned office database
        /// (catalogs + admin + the real owner doctor entered on the form) + host-side
        /// branding. This is the path the New Practice UI calls; the stock SaaS create
        /// (POST /api/saas/tenants) writes only a bare tenant row with no office database.
        /// The admin password is a server-generated throwaway nobody ever sees: the admin is sent
        /// an activate-your-account email and sets a real one from its link, which also confirms
        /// their email. Location(s) are added later on the Locations page.
        /// </summary>
        [Authorize(SaasHostPermissions.Tenants.Create)]
        public async Task<SaasTenantDto> CreatePracticeAsync(CreatePracticeInput input)
        {
            EnsureHostCaller();
            Check.NotNull(input, nameof(input));
            Check.NotNullOrWhiteSpace(input.Slug, nameof(input.Slug));
            Check.NotNullOrWhiteSpace(input.DoctorFirstName, nameof(input.DoctorFirstName));
            Check.NotNullOrWhiteSpace(input.DoctorLastName, nameof(input.DoctorLastName));
            Check.NotNullOrWhiteSpace(input.DoctorEmail, nameof(input.DoctorEmail));

            var slug = DeriveSlugOrThrow(input.Slug);
            var doctorEmail = input.DoctorEmail.Trim();

            var saasInput = new SaasTenantCreateDto
            {
                Name = slug,
                // One doctor per practice: the doctor's email is the office admin login too.
                AdminEmailAddress = doctorEmail,
                // Throwaway that meets the identity policy (upper/lower/digit/symbol); the
                // admin replaces it from the activation email, so it is never surfaced.
                AdminPassword = $"{GuidGenerator.Create():N}Aa1!",
            };

            var tenant = await CreateTenantWithOfficeDatabaseAsync(
                saasInput,
                slug,
                input.DoctorFirstName.Trim(),
                input.DoctorLastName.Trim(),
                doctorEmail);

            var displayName = input.DisplayName.IsNullOrWhiteSpace()
                ? PracticeNaming.DefaultDisplayName(input.DoctorFirstName, input.DoctorLastName)
                : input.DisplayName.Trim();
            await UpsertOfficeBrandingDisplayNameAsync(tenant.Id, displayName);

            return tenant;
        }

        // Reserved-name guard + DNS-safe slug derivation, surfaced as a user-friendly
        // error (TenantNaming validates rather than transforms, so a non-slug name fails).
        private static string DeriveSlugOrThrow(string name)
        {
            if (string.Equals(name?.Trim(), ReservedTenantNameAdmin, StringComparison.OrdinalIgnoreCase))
            {
                throw new UserFriendlyException(
                    $"Tenant name '{ReservedTenantNameAdmin}' is reserved for the host-context surface and cannot be used.");
            }

            try
            {
                return TenantNaming.DeriveSlug(name!);
            }
            catch (ArgumentException ex)
            {
                throw new UserFriendlyException(ex.Message);
            }
        }

        // Shared create used by both the stock override and the New Practice flow.
        // Reachability-check the full office connection string up front (held in memory only),
        // register the tenant + store its DATABASE NAME in one host transaction (the TenantCreatedEto is
        // outbox-deferred to this commit, so a failure rolls the tenant back and never
        // provisions a database), then provision the office database out-of-band (a
        // separate database cannot share the host transaction). Doctor fields flow to the
        // office doctor seed when supplied (New Practice), else null (stock create). On a
        // provisioning failure the office row + connection string remain, so a retry
        // completes it (idempotent seeders).
        private async Task<SaasTenantDto> CreateTenantWithOfficeDatabaseAsync(
            SaasTenantCreateDto input,
            string slug,
            string? doctorFirstName,
            string? doctorLastName,
            string? doctorEmail)
        {
            var connectionString = _connectionStringProvider.BuildConnectionString(slug);

            var checkResult = await _connectionStringChecker.CheckAsync(connectionString);
            if (!checkResult.Connected)
            {
                throw new UserFriendlyException(
                    $"Cannot reach the database server for office '{input.Name}'. Verify the SQL server is running and the connection template is correct.");
            }

            SaasTenantDto tenant;
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                tenant = await base.CreateAsync(input);

                // The row stores the database name only; OfficeConnectionStringResolver composes the
                // server and login from configuration on every request.
                var tenantEntity = await _tenantRepository.GetAsync(tenant.Id);
                tenantEntity.SetDefaultConnectionString(TenantNaming.BuildStoredConnectionString(slug));
                await _tenantRepository.UpdateAsync(tenantEntity);

                await uow.CompleteAsync();
            }

            try
            {
                await _officeProvisioner.ProvisionAsync(
                    tenant.Id,
                    input.AdminEmailAddress,
                    input.AdminPassword,
                    doctorFirstName,
                    doctorLastName,
                    doctorEmail);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex,
                    "Provisioning the office database for tenant {TenantId} failed after creation.",
                    tenant.Id);
                throw new UserFriendlyException(
                    $"Office '{input.Name}' was created but its database could not be fully provisioned. Re-run provisioning to complete setup.");
            }

            // The office admin is the one account an office is born with, created unconfirmed with a
            // password nobody knows. Without this email nobody could get in until someone found the
            // resend-verification page. The office exists by now, so a failure here is logged, not
            // thrown; the admin can still ask for a new link from the office's sign-in page.
            await _activationSender.TrySendToOfficeAdminAsync(tenant.Id, doctorFirstName);

            return tenant;
        }

        // 2026-10-28: the rest of the practice registry, inherited from the SaaS module. Each override only adds
        // the host check below and keeps the module's own permission (an override inherits its [Authorize]).
        public override Task<SaasTenantDto> GetAsync(Guid id)
        {
            EnsureHostCaller();
            return base.GetAsync(id);
        }

        public override Task<PagedResultDto<SaasTenantDto>> GetListAsync(GetTenantsInput input)
        {
            EnsureHostCaller();
            return base.GetListAsync(input);
        }

        public override Task<SaasTenantDto> UpdateAsync(Guid id, SaasTenantUpdateDto input)
        {
            EnsureHostCaller();
            return base.UpdateAsync(id, input);
        }

        public override Task DeleteAsync(Guid id)
        {
            EnsureHostCaller();
            return base.DeleteAsync(id);
        }

        public override Task<SaasTenantDatabasesDto> GetDatabasesAsync()
        {
            EnsureHostCaller();
            return base.GetDatabasesAsync();
        }

        public override Task ApplyDatabaseMigrationsAsync(Guid id)
        {
            EnsureHostCaller();
            return base.ApplyDatabaseMigrationsAsync(id);
        }

        public override Task<List<EditionLookupDto>> GetEditionLookupAsync()
        {
            EnsureHostCaller();
            return base.GetEditionLookupAsync();
        }

        public override Task<bool> CheckConnectionStringAsync(string connectionString)
        {
            EnsureHostCaller();
            return base.CheckConnectionStringAsync(connectionString);
        }

        public override Task SetPasswordAsync(Guid id, SaasTenantSetPasswordDto input)
        {
            EnsureHostCaller();
            return base.SetPasswordAsync(id, input);
        }

        /// <summary>
        /// 2026-10-28 -- refuses a caller who is inside an office, before anything is read or written. The practice
        /// registry lists, creates and changes every office, so it belongs to the host alone. The SaaS permissions on
        /// these methods are host-side and already stop an office caller at the authorization interceptor; this
        /// check keeps the refusal in place even if a permission's side is ever widened.
        /// </summary>
        private void EnsureHostCaller()
        {
            if (CurrentTenant.IsAvailable)
            {
                throw new AbpAuthorizationException("Practices are managed from the host, not from inside an office.");
            }
        }

        // Host-side branding row (display name) for the new office. Runs in host scope
        // because OfficeBranding is host-only (never IMultiTenant) -- it is read pre-auth
        // by subdomain, so it cannot live in the office database.
        private async Task UpsertOfficeBrandingDisplayNameAsync(Guid officeId, string displayName)
        {
            using (CurrentTenant.Change(null))
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                var branding = await _brandingRepository.FirstOrDefaultAsync(b => b.OfficeId == officeId);
                if (branding == null)
                {
                    branding = new OfficeBranding(GuidGenerator.Create(), officeId);
                    branding.SetDisplayName(displayName);
                    await _brandingRepository.InsertAsync(branding, autoSave: true);
                }
                else
                {
                    branding.SetDisplayName(displayName);
                    await _brandingRepository.UpdateAsync(branding, autoSave: true);
                }

                await uow.CompleteAsync();
            }
        }
    }
}
