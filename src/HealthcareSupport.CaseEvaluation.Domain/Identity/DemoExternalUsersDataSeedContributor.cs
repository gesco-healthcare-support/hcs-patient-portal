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
/// Seeds one demo user per external role per tenant. Mirrors
/// <see cref="InternalUsersDataSeedContributor"/> for the external side.
///
/// Per OLD role taxonomy (verified at
/// <c>P:\PatientPortalOld\PatientAppointment.Models\Enums\Roles.cs</c>) and
/// the role-naming reconciliation captured in
/// <see cref="ExternalUserRoleDataSeedContributor"/>:
///   patient@&lt;slug&gt;.test            -> Patient
///   adjuster@&lt;slug&gt;.test           -> Claim Examiner   (OLD "Adjuster" renamed)
///   applicant.attorney@&lt;slug&gt;.test -> Applicant Attorney
///   defense.attorney@&lt;slug&gt;.test   -> Defense Attorney
///
/// Default password matches <see cref="InternalUsersDataSeedContributor.DefaultPassword"/>
/// so a single env-var or doc reference covers every dev login.
///
/// Gated on Development environment to keep production free of demo logins.
/// Idempotent: existing users by email are left alone; the role assignment
/// is re-applied only when missing.
/// </summary>
public class DemoExternalUsersDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly ILogger<DemoExternalUsersDataSeedContributor> _logger;

    public DemoExternalUsersDataSeedContributor(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        ICurrentTenant currentTenant,
        IRepository<Tenant, Guid> tenantRepository,
        ILogger<DemoExternalUsersDataSeedContributor> logger)
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
                "DemoExternalUsersDataSeedContributor: skipping (not Development environment).");
            return;
        }

        // Demo external users are tenant-scoped; host context has no external roles.
        if (context?.TenantId == null)
        {
            return;
        }

        await SeedTenantUsersAsync(context.TenantId.Value);
    }

    private async Task SeedTenantUsersAsync(Guid tenantId)
    {
        using (_currentTenant.Change(tenantId))
        {
            var tenant = await FindTenantAsync(tenantId);
            if (tenant == null)
            {
                _logger.LogWarning(
                    "DemoExternalUsersDataSeedContributor: tenant {TenantId} not found; skipping.",
                    tenantId);
                return;
            }

            var slug = ToTenantSlug(tenant.Name);

            var seedPlan = new (string EmailPrefix, string RoleName)[]
            {
                ("patient",            "Patient"),
                ("adjuster",           "Claim Examiner"),
                ("applicant.attorney", "Applicant Attorney"),
                ("defense.attorney",   "Defense Attorney"),
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
                "DemoExternalUsersDataSeedContributor: role '{RoleName}' not found in tenant {TenantId}; skipping user {Email}.",
                roleName, tenantId, email);
            return null;
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new IdentityUser(Guid.NewGuid(), userName, email, tenantId);
            var createResult = await _userManager.CreateAsync(user, InternalUsersDataSeedContributor.DefaultPassword);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning(
                    "DemoExternalUsersDataSeedContributor: failed to create {Email}: {Errors}",
                    email,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return null;
            }
            _logger.LogInformation(
                "DemoExternalUsersDataSeedContributor: created user {Email} (tenant {TenantId}).",
                email, tenantId);
        }

        if (!await _userManager.IsInRoleAsync(user, roleName))
        {
            var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!addRoleResult.Succeeded)
            {
                _logger.LogWarning(
                    "DemoExternalUsersDataSeedContributor: failed to assign role '{RoleName}' to {Email}: {Errors}",
                    roleName,
                    email,
                    string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
            }
            else
            {
                _logger.LogInformation(
                    "DemoExternalUsersDataSeedContributor: assigned role '{RoleName}' to {Email}.",
                    roleName, email);
            }
        }

        return user;
    }

    private async Task<Tenant?> FindTenantAsync(Guid tenantId)
    {
        // Tenant rows live in host scope; switch to host context for the lookup
        // so the IMultiTenant filter does not exclude the row.
        using (_currentTenant.Change(null))
        {
            return await _tenantRepository.FindAsync(tenantId);
        }
    }

    private static bool IsDevelopment()
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Identical algorithm to <see cref="InternalUsersDataSeedContributor"/>'s
    /// private slug helper; kept duplicated rather than extracted to a shared
    /// utility because the two contributors are the only callers and the slug
    /// rule is intentionally narrow (tied to the tenant subdomain pattern).
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
