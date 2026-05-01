using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Doctors;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// D.1 / W-UI-16 (2026-04-30): runtime seeding of internal user accounts
/// per tenant + role assignment, gated on `ASPNETCORE_ENVIRONMENT=Development`
/// so production never gets test logins.
///
/// Adrian Q-S-* answers (2026-04-30):
///   - Q-S-a: NO external users seeded -- the register flow stays exercised.
///   - Q-S-b: Emails parameterised per tenant: `&lt;role&gt;@&lt;tenantSlug&gt;.test`.
///   - Q-S-c: Gated to Development.
///   - Q-S-d: Doctor user is linked to the tenant's Doctor entity by setting
///     Doctor.IdentityUserId so the future "own appointments only" filter
///     (W-DOC-1) has a stable join key.
///
/// Per tenant the seeder ensures four users with the matching role:
///   admin@&lt;tenantSlug&gt;.test       -> admin
///   supervisor@&lt;tenantSlug&gt;.test  -> Staff Supervisor
///   staff@&lt;tenantSlug&gt;.test       -> Clinic Staff
///   doctor@&lt;tenantSlug&gt;.test      -> Doctor (and linked to the Doctor entity)
///
/// Plus one host-side user: `it.admin@hcs.test` with the IT Admin role.
///
/// Default password for every seeded account: `1q2w3E*` (matches ABP's stock
/// password policy: upper / lower / digit / special). The seeder is idempotent
/// -- if a user with the email already exists, it is left alone (the existing
/// admin user created by `DoctorTenantAppService.CreateAsync` is reached by
/// the `admin@...` slot and only re-keyed to that email if it does not exist
/// already).
/// </summary>
public class InternalUsersDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string DefaultPassword = "1q2w3E*";
    public const string ItAdminEmail = "it.admin@hcs.test";

    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly IRepository<Doctor, Guid> _doctorRepository;
    private readonly ILogger<InternalUsersDataSeedContributor> _logger;

    public InternalUsersDataSeedContributor(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        ICurrentTenant currentTenant,
        IRepository<Tenant, Guid> tenantRepository,
        IRepository<Doctor, Guid> doctorRepository,
        ILogger<InternalUsersDataSeedContributor> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _currentTenant = currentTenant;
        _tenantRepository = tenantRepository;
        _doctorRepository = doctorRepository;
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

            // Per-tenant role -> email-prefix map. Order matters: admin first
            // so the existing tenant-admin user (if any) is reachable by the
            // admin@... slot before we look at the doctor link below.
            var seedPlan = new (string EmailPrefix, string RoleName)[]
            {
                ("admin",      "admin"),
                ("supervisor", InternalUserRoleDataSeedContributor.StaffSupervisorRoleName),
                ("staff",      InternalUserRoleDataSeedContributor.ClinicStaffRoleName),
                ("doctor",     InternalUserRoleDataSeedContributor.DoctorRoleName),
            };

            IdentityUser? doctorUser = null;
            foreach (var (prefix, roleName) in seedPlan)
            {
                var email = $"{prefix}@{slug}.test";
                var user = await EnsureUserWithRoleAsync(
                    email: email,
                    userName: email,
                    roleName: roleName,
                    tenantId: tenantId);
                if (prefix == "doctor")
                {
                    doctorUser = user;
                }
            }

            if (doctorUser != null)
            {
                await LinkDoctorEntityAsync(doctorUser);
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

    /// <summary>
    /// Q-S-d: link the seeded doctor user to the tenant's Doctor entity so
    /// the future "own appointments only" filter (W-DOC-1) has an
    /// IdentityUserId join key. The tenant always has a Doctor row -- it is
    /// auto-created by `DoctorTenantAppService.CreateAsync` during tenant
    /// provisioning (see Doctors CLAUDE.md, business rule 5). When the row
    /// initially links to the admin user, this re-keys it to the doctor user.
    /// If multiple Doctor rows exist (schema permits N:1 even though the
    /// product intent is 1:1), the first row is updated and the rest are
    /// left alone -- a clean fix would coalesce them, but that is out of
    /// scope for the seeder.
    /// </summary>
    private async Task LinkDoctorEntityAsync(IdentityUser doctorUser)
    {
        var queryable = await _doctorRepository.GetQueryableAsync();
        var doctor = queryable.OrderBy(x => x.CreationTime).FirstOrDefault();
        if (doctor == null)
        {
            _logger.LogInformation(
                "InternalUsersDataSeedContributor: tenant has no Doctor entity; skipping doctor-link step.");
            return;
        }

        var emailMatches = string.Equals(doctor.Email, doctorUser.Email, StringComparison.OrdinalIgnoreCase);
        if (doctor.IdentityUserId == doctorUser.Id && emailMatches)
        {
            return;
        }

        doctor.IdentityUserId = doctorUser.Id;
        // W-NEW-6 (2026-05-01): keep Doctor.Email aligned with the linked
        // IdentityUser's email. `DoctorsAppService.UpdateAsync` syncs IdentityUser
        // email FROM the Doctor's email on every save, so leaving the Doctor's
        // email at the original tenant-admin address (set by
        // `DoctorTenantAppService.CreateAsync` at provisioning time) causes
        // every subsequent UI-driven Doctor save to throw `DuplicateEmail`
        // because the original admin user already owns that address. Pre-empt
        // the conflict by re-keying Doctor.Email to match the doctor user.
        if (!string.IsNullOrWhiteSpace(doctorUser.Email))
        {
            doctor.Email = doctorUser.Email;
        }
        await _doctorRepository.UpdateAsync(doctor);
        _logger.LogInformation(
            "InternalUsersDataSeedContributor: re-linked Doctor entity {DoctorId} to user {Email}.",
            doctor.Id, doctorUser.Email);
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
