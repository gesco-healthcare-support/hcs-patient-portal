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

    /// <summary>
    /// 2026-05-06 -- additional per-tenant admin emails seeded for the
    /// end-to-end demo scripts. These get the same `admin` role as
    /// `admin@&lt;tenantSlug&gt;.test` and the same password. They are
    /// intended for the appointment-lifecycle test plan where two human
    /// testers each need their own admin account in the same tenant
    /// (Falkinstein) so they can independently book + approve / reject
    /// appointments without trampling each other's session state.
    /// Development-gated like the rest of the seeder.
    /// </summary>
    public static readonly string[] ExtraTenantAdminEmails =
    {
        "SoftwareOne@evaluators.com",
        "SoftwareTwo@evaluators.com",
    };

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

            // 2026-05-06 (Adrian directive): also seed the extra demo
            // admin emails into every tenant. Idempotent -- if an
            // already-registered external user happens to share the same
            // email, EnsureUserWithRoleAsync will leave the row alone and
            // just add the admin role. (For the test plan we delete the
            // matching external rows beforehand via the dev API so the
            // seeder creates a fresh admin user.)
            foreach (var extraEmail in ExtraTenantAdminEmails)
            {
                await EnsureUserWithRoleAsync(
                    email: extraEmail,
                    userName: extraEmail,
                    roleName: "admin",
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

        // B10 (2026-05-06): seed internal users with a Name/Surname pair so
        // the SPA welcome banner shows "First Last" instead of falling back
        // to the email address. Mapping is hardcoded for the small set of
        // seeded internal-admin emails; anything else gets a generic Test
        // User pair so the banner is at least non-email.
        // Issue 1.2 (2026-05-12): also seed a synthetic phone number so the
        // user profile and admin pages don't display blank phone cells.
        // Synthetic 555-prefix per .claude/rules/test-data.md.
        var (firstName, lastName) = BuildInternalUserDisplayName(email);
        var seedPhone = BuildSeedPhoneNumber(email);

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new IdentityUser(Guid.NewGuid(), userName, email, tenantId);
            user.Name = firstName;
            user.Surname = lastName;
            user.SetPhoneNumber(seedPhone, confirmed: true);
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
        else
        {
            // Idempotent backfill: previously-seeded users were created
            // without Name/Surname (B10 root cause). Fill in here so the
            // banner shows correctly without requiring a fresh tenant.
            // Issue 1.2 (2026-05-12) extends this to PhoneNumber too.
            var changed = false;
            if (string.IsNullOrWhiteSpace(user.Name))
            {
                user.Name = firstName;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(user.Surname))
            {
                user.Surname = lastName;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                user.SetPhoneNumber(seedPhone, confirmed: true);
                changed = true;
            }
            if (changed)
            {
                await _userManager.UpdateAsync(user);
            }
        }

        // Demo flow gate: G4's email-confirm requirement (commit 682093c)
        // forces /Account/ConfirmUser after login when EmailConfirmed=false.
        // Seeded demo accounts never receive a real verification email,
        // so we mark them confirmed at seed time. Production-only paths
        // are not affected because this contributor is Development-gated.
        if (!user.EmailConfirmed)
        {
            user.SetEmailConfirmed(true);
            await _userManager.UpdateAsync(user);
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
    /// B10 (2026-05-06): map a seeded internal-admin email to a Name +
    /// Surname pair so the SPA welcome banner has something to render
    /// other than the email address. The mapping is hardcoded for the
    /// known seeded emails; everything else falls back to a generic
    /// "Test User" pair (still better than the email).
    /// </summary>
    private static (string FirstName, string LastName) BuildInternalUserDisplayName(string email)
    {
        var prefix = (email ?? string.Empty).Split('@')[0].ToLowerInvariant();
        return prefix switch
        {
            "admin" => ("Tenant", "Administrator"),
            "supervisor" => ("Staff", "Supervisor"),
            "staff" => ("Clinic", "Staff"),
            "it.admin" => ("IT", "Administrator"),
            "softwareone" => ("Software", "One"),
            "softwaretwo" => ("Software", "Two"),
            _ => ("Test", "User"),
        };
    }

    /// <summary>
    /// Issue 1.2 (2026-05-12): synthetic 555-prefix phone number per
    /// seeded user. Synthetic data only (.claude/rules/test-data.md).
    /// Deterministic so a reseed produces the same numbers and the test
    /// fixture asserts don't drift.
    /// </summary>
    private static string BuildSeedPhoneNumber(string email)
    {
        var prefix = (email ?? string.Empty).Split('@')[0].ToLowerInvariant();
        return prefix switch
        {
            "admin" => "555-010-0001",
            "supervisor" => "555-010-0002",
            "staff" => "555-010-0003",
            "it.admin" => "555-010-0004",
            "softwareone" => "555-010-0005",
            "softwaretwo" => "555-010-0006",
            _ => "555-010-0099",
        };
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
