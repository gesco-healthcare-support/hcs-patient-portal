using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// Runtime seeding of internal user accounts per tenant + role assignment,
/// gated on `ASPNETCORE_ENVIRONMENT=Development` so production never gets
/// test logins.
///
/// Per OLD spec (Phase 0.1, 2026-05-01) Doctor is a non-user reference entity
/// managed by Staff Supervisor; no Doctor user role exists. Per-tenant seeded
/// users are:
///   admin@&lt;tenantSlug&gt;.test       -> admin
///   supervisor@&lt;tenantSlug&gt;.test  -> Staff Supervisor
///   staff@&lt;tenantSlug&gt;.test       -> Clinic Staff
///
/// Plus one host-side user: `it.admin@hcs.test` with the IT Admin role.
///
/// Default password for every seeded account: `1q2w3E*r` (8 chars; satisfies
/// the Phase 2 policy of digit + non-alphanumeric + RequiredLength=8).
/// The seeder is idempotent -- if a user with the email already exists, it
/// is left alone.
/// </summary>
public class InternalUsersDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string DefaultPassword = "1q2w3E*r";
    public const string ItAdminEmail = "it.admin@hcs.test";

    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly ILogger<InternalUsersDataSeedContributor> _logger;

    public InternalUsersDataSeedContributor(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        ICurrentTenant currentTenant,
        IRepository<Tenant, Guid> tenantRepository,
        ILogger<InternalUsersDataSeedContributor> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _currentTenant = currentTenant;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (!IsDevelopment())
        {
            _logger.LogInformation(
                "InternalUsersDataSeedContributor: skipping (not Development environment).");
            return;
        }

        if (context?.TenantId == null)
        {
            await SeedHostUsersAsync();
        }
        else
        {
            await SeedTenantUsersAsync(context.TenantId.Value);
        }
    }

    private static bool IsDevelopment()
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedHostUsersAsync()
    {
        using (_currentTenant.Change(null))
        {
            await EnsureUserWithRoleAsync(
                email: ItAdminEmail,
                userName: ItAdminEmail,
                roleName: InternalUserRoleDataSeedContributor.ItAdminRoleName,
                tenantId: null);
        }
    }

    private async Task SeedTenantUsersAsync(Guid tenantId)
    {
        using (_currentTenant.Change(tenantId))
        {
            var tenant = await FindTenantAsync(tenantId);
            if (tenant == null)
            {
                _logger.LogWarning(
                    "InternalUsersDataSeedContributor: tenant {TenantId} not found; skipping.",
                    tenantId);
                return;
            }

            var slug = ToTenantSlug(tenant.Name);

            // Per-tenant role -> email-prefix map. Doctor is non-user per OLD spec.
            var seedPlan = new (string EmailPrefix, string RoleName)[]
            {
                ("admin",      "admin"),
                ("supervisor", InternalUserRoleDataSeedContributor.StaffSupervisorRoleName),
                ("staff",      InternalUserRoleDataSeedContributor.ClinicStaffRoleName),
            };

            foreach (var (prefix, roleName) in seedPlan)
            {
                var email = $"{prefix}@{slug}.test";
                await EnsureUserWithRoleAsync(
                    email: email,
                    userName: email,
                    roleName: roleName,
                    tenantId: tenantId);
            }
        }
    }

    /// <summary>
    /// Idempotent user + role assignment. If the user does not exist, create
    /// it with the default password. Whether new or existing, ensure the
    /// requested role is assigned (skipped if already a member). Returns the
    /// resolved user so callers (e.g. the Doctor entity-link step) can chain.
    /// </summary>
    private async Task<IdentityUser?> EnsureUserWithRoleAsync(
        string email,
        string userName,
        string roleName,
        Guid? tenantId)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role == null)
        {
            _logger.LogWarning(
                "InternalUsersDataSeedContributor: role '{RoleName}' not found in tenant {TenantId}; skipping user {Email}.",
                roleName, tenantId, email);
            return null;
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new IdentityUser(Guid.NewGuid(), userName, email, tenantId);
            var createResult = await _userManager.CreateAsync(user, DefaultPassword);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning(
                    "InternalUsersDataSeedContributor: failed to create {Email}: {Errors}",
                    email,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return null;
            }
            _logger.LogInformation(
                "InternalUsersDataSeedContributor: created user {Email} (tenant {TenantId}).",
                email, tenantId);
        }

        if (!await _userManager.IsInRoleAsync(user, roleName))
        {
            var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!addRoleResult.Succeeded)
            {
                _logger.LogWarning(
                    "InternalUsersDataSeedContributor: failed to assign role '{RoleName}' to {Email}: {Errors}",
                    roleName,
                    email,
                    string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
            }
            else
            {
                _logger.LogInformation(
                    "InternalUsersDataSeedContributor: assigned role '{RoleName}' to {Email}.",
                    roleName, email);
            }
        }

        return user;
    }

    private async Task<Tenant?> FindTenantAsync(Guid tenantId)
    {
        // Tenant rows live in the host scope; switch to host context for the
        // lookup so the IMultiTenant filter does not exclude the row.
        using (_currentTenant.Change(null))
        {
            return await _tenantRepository.FindAsync(tenantId);
        }
    }

    /// <summary>
    /// Build a URL/email-safe slug from a tenant name. Lowercases, strips
    /// non-alphanumeric characters except '-', collapses runs of '-', and
    /// trims leading/trailing dashes. Falls back to "tenant" if the result
    /// is empty (e.g. tenant name was all whitespace -- should not happen
    /// at the SaaS level but is defensive).
    /// </summary>
    private static string ToTenantSlug(string? tenantName)
    {
        if (string.IsNullOrWhiteSpace(tenantName))
        {
            return "tenant";
        }

        var lowered = tenantName.Trim().ToLowerInvariant();
        var builder = new System.Text.StringBuilder(lowered.Length);
        char last = '\0';
        foreach (var ch in lowered)
        {
            char emit;
            if (char.IsLetterOrDigit(ch))
            {
                emit = ch;
            }
            else if (ch == '-' || char.IsWhiteSpace(ch))
            {
                emit = '-';
            }
            else
            {
                continue;
            }

            if (emit == '-' && last == '-')
            {
                continue;
            }
            builder.Append(emit);
            last = emit;
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrEmpty(slug) ? "tenant" : slug;
    }
}
