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
///   staff@&lt;tenantSlug&gt;.test       -> Intake Staff
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
    public const string ItAdminEmail = "adriang@gesco.com";

    /// <summary>
    /// Host operator logins seeded in Development (alongside the real IT Admin in
    /// <see cref="ItAdminEmail"/>). Each entry is (email, role, forceReset):
    ///   - REAL accounts (forceReset = true): the actual go-live operators. Seeded so the
    ///     stack mirrors production, but force a password change on first login (the dev
    ///     default is well-known) -- and Adrian does NOT use these for day-to-day testing
    ///     so their real inboxes never receive test emails.
    ///   - SYNTHETIC test accounts (forceReset = false): kept until the prod cutover so
    ///     Adrian can test host-operator + assignment-gate flows with the shared dev
    ///     password and no reset step, without touching real people's accounts.
    /// Idempotent: existing rows are left alone.
    /// </summary>
    public static readonly (string Email, string RoleName, bool ForceReset)[] ExtraSeededUsers =
    {
        // Real go-live operators: 2 Staff Supervisors + 4 Intake operators. Intake
        // operators are gated to one office each via IntakeOfficeAssignment (seed via the
        // host-central assignment UI, or a follow-up assignment seeder):
        //   jocelynh -> hekmat, jonatanb -> longacre, myrkas -> pelton, genevieveg -> falkinstein.
        ("teresal@socalpm.com",     InternalUserRoleDataSeedContributor.StaffSupervisorRoleName, true),
        ("karenm@gesco.com",        InternalUserRoleDataSeedContributor.StaffSupervisorRoleName, true),
        ("jocelynh@socalpm.com",    InternalUserRoleDataSeedContributor.IntakeStaffRoleName,      true),
        ("jonatanb@socalpm.com",    InternalUserRoleDataSeedContributor.IntakeStaffRoleName,      true),
        ("myrkas@socalpm.com",      InternalUserRoleDataSeedContributor.IntakeStaffRoleName,      true),
        ("genevieveg@socalpm.com",  InternalUserRoleDataSeedContributor.IntakeStaffRoleName,      true),

        // Synthetic test accounts (kept until prod): a synthetic IT Admin + 2 Supervisors
        // + 2 Intake operators, usable directly with the dev password (no force-reset).
        ("it.admin@hcs.test",   InternalUserRoleDataSeedContributor.ItAdminRoleName,          false),
        ("stafsuper1@gesco.com", InternalUserRoleDataSeedContributor.StaffSupervisorRoleName, false),
        ("stafsuper2@gesco.com", InternalUserRoleDataSeedContributor.StaffSupervisorRoleName, false),
        ("clistaff1@gesco.com",  InternalUserRoleDataSeedContributor.IntakeStaffRoleName,      false),
        ("clistaff2@gesco.com",  InternalUserRoleDataSeedContributor.IntakeStaffRoleName,      false),
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
            // Real IT Admin (Adrian) -- force a reset on first login.
            await EnsureUserWithRoleAsync(
                email: ItAdminEmail,
                userName: ItAdminEmail,
                roleName: InternalUserRoleDataSeedContributor.ItAdminRoleName,
                tenantId: null,
                forceResetOnFirstLogin: true);

            // Phase D (2026-06-25): Staff Supervisor + Intake Staff are HOST operators
            // (a host login each that switches into offices). Real go-live operators
            // force-reset; synthetic test accounts do not (see ExtraSeededUsers).
            foreach (var (email, roleName, forceReset) in ExtraSeededUsers)
            {
                await EnsureUserWithRoleAsync(
                    email: email,
                    userName: email,
                    roleName: roleName,
                    tenantId: null,
                    forceResetOnFirstLogin: forceReset);
            }
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
            // 2026-06-09 (Adrian, demo reset): seed ONLY the tenant `admin` here.
            // The Staff Supervisor + Intake Staff demo logins are provided by
            // ExtraSeededUsers (stafsuper1 / clistaff1); the generic
            // supervisor@/staff@<slug>.test accounts are no longer seeded.
            var seedPlan = new (string EmailPrefix, string RoleName)[]
            {
                ("admin",      "admin"),
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

            // Phase D (2026-06-25): the Staff Supervisor + Intake Staff demo
            // logins (stafsuper*/clistaff*) are NO LONGER seeded per tenant --
            // they are HOST operators now (see SeedHostUsersAsync). The only
            // per-office internal user seeded here is the office `admin` (the
            // impersonation target for a switching Supervisor). The limited
            // per-office Intake shadow users are provisioned on assignment, not
            // seeded.
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
        Guid? tenantId,
        bool forceResetOnFirstLogin = false)
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

            // Real accounts force a password change on first login (the shared dev
            // default is well-known); synthetic test accounts skip this so they stay
            // usable with the dev password for repeated testing.
            if (forceResetOnFirstLogin)
            {
                user.SetShouldChangePasswordOnNextLogin(true);
                await _userManager.UpdateAsync(user);
            }
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
            // Real host operators (2026-06-26): IT Admin + 2 Supervisors + 4 Intake.
            "adriang" => ("Adrian", "Gambhir"),
            "teresal" => ("Teresa", "Lopez"),
            "karenm" => ("Karen", "Muratalla"),
            "jocelynh" => ("Jocelyn", "Heredia"),
            "jonatanb" => ("Jonatan", "Barbero"),
            "myrkas" => ("Myrka", "Solis"),
            "genevieveg" => ("Genevieve", "Garcia"),
            // Synthetic test accounts (kept until prod). Names are synthetic.
            "it.admin" => ("IT", "Administrator"),
            "stafsuper1" => ("Patrick", "O'Neal"),
            "stafsuper2" => ("Denise", "Alvarez"),
            "clistaff1" => ("Rachel", "Kim"),
            "clistaff2" => ("Marcus", "Webb"),
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
        // Synthetic 555-prefix placeholders (no real phone numbers seeded).
        return prefix switch
        {
            "admin" => "555-010-0001",
            "adriang" => "555-010-0004",
            "teresal" => "555-010-0005",
            "karenm" => "555-010-0006",
            "jocelynh" => "555-010-0011",
            "jonatanb" => "555-010-0012",
            "myrkas" => "555-010-0013",
            "genevieveg" => "555-010-0014",
            "it.admin" => "555-010-0004",
            "stafsuper1" => "555-010-0005",
            "stafsuper2" => "555-010-0007",
            "clistaff1" => "555-010-0006",
            "clistaff2" => "555-010-0008",
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
