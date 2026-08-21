using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// Dev-only: seeds ONE demo Patient login (<c>patient@&lt;slug&gt;.test</c>) WITH a
/// linked <see cref="Patient"/> record, so the self-service My Profile page
/// resolves a populated profile. <c>GET /patients/me</c> keys off
/// <c>IdentityUserId == CurrentUser.Id</c> and does not auto-create, so both the
/// identity user (Patient role) and the Patient row are required.
///
/// Added 2026-06-13 to live-verify the redesigned My Profile patient variant.
/// The 2026-06-09 "no demo external users seeded" reset targeted demo realism
/// (so verification/invite emails fire during the demo); a stable Patient login
/// is still needed for dev UI testing, which is what this contributor provides.
///
/// Gated on Development. Idempotent: the identity user and the Patient row are
/// each created only when absent. Default password matches
/// <see cref="InternalUsersDataSeedContributor.DefaultPassword"/>.
/// </summary>
public class DemoPatientDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string RoleName = "Patient";

    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly PatientManager _patientManager;
    private readonly IPatientRepository _patientRepository;
    private readonly ILogger<DemoPatientDataSeedContributor> _logger;

    public DemoPatientDataSeedContributor(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        ICurrentTenant currentTenant,
        IRepository<Tenant, Guid> tenantRepository,
        PatientManager patientManager,
        IPatientRepository patientRepository,
        ILogger<DemoPatientDataSeedContributor> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _currentTenant = currentTenant;
        _tenantRepository = tenantRepository;
        _patientManager = patientManager;
        _patientRepository = patientRepository;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (!IsDevelopment())
        {
            return;
        }

        // The Patient + its identity user are tenant-scoped; the host context has
        // no external roles.
        if (context?.TenantId == null)
        {
            return;
        }

        await SeedTenantPatientAsync(context.TenantId.Value);
    }

    private async Task SeedTenantPatientAsync(Guid tenantId)
    {
        using (_currentTenant.Change(tenantId))
        {
            var tenant = await FindTenantAsync(tenantId);
            if (tenant == null)
            {
                _logger.LogWarning(
                    "DemoPatientDataSeedContributor: tenant {TenantId} not found; skipping.",
                    tenantId);
                return;
            }

            var slug = ToTenantSlug(tenant.Name);
            var email = $"patient@{slug}.test";

            var user = await EnsurePatientUserAsync(email, tenantId);
            if (user == null)
            {
                return;
            }

            // Idempotent: only create the Patient row when the login has none.
            var existing = await _patientRepository.FirstOrDefaultAsync(p => p.IdentityUserId == user.Id);
            if (existing != null)
            {
                return;
            }

            // Synthetic demographics (.claude/rules/test-data.md): 555-prefix
            // phones, fictional name + address. State / language left null so the
            // profile's lookup-backed selects start empty.
            await _patientManager.CreateAsync(
                stateId: null,
                appointmentLanguageId: null,
                identityUserId: user.Id,
                tenantId: tenantId,
                firstName: "Maria",
                lastName: "Santos",
                email: email,
                genderId: Gender.Female,
                dateOfBirth: new DateTime(1986, 5, 14),
                phoneNumberTypeId: PhoneNumberType.Home,
                middleName: null,
                phoneNumber: "555-010-0150",
                socialSecurityNumber: null,
                address: null,
                city: "Los Angeles",
                zipCode: "90013",
                cellPhoneNumber: "555-010-0151",
                street: "128 W 4th St",
                interpreterVendorName: null,
                apptNumber: "Apt 5",
                othersLanguageName: null);

            _logger.LogInformation(
                "DemoPatientDataSeedContributor: created demo patient {Email} (tenant {TenantId}).",
                email, tenantId);
        }
    }

    private async Task<IdentityUser?> EnsurePatientUserAsync(string email, Guid tenantId)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new IdentityUser(Guid.NewGuid(), email, email, tenantId)
            {
                Name = "Maria",
                Surname = "Santos",
            };
            user.SetPhoneNumber("555-010-0150", confirmed: true);
            var createResult = await _userManager.CreateAsync(
                user, InternalUsersDataSeedContributor.DefaultPassword);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning(
                    "DemoPatientDataSeedContributor: failed to create {Email}: {Errors}",
                    email,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return null;
            }
        }

        // Seeded demo accounts never receive a verification email; mark confirmed
        // so the G4 email-confirm gate does not block login (Development-gated).
        if (!user.EmailConfirmed)
        {
            user.SetEmailConfirmed(true);
            await _userManager.UpdateAsync(user);
        }

        if (!await _userManager.IsInRoleAsync(user, RoleName))
        {
            // Seed-order independence: the tenant's external roles are created by
            // ExternalUserRoleDataSeedContributor, which ABP may run AFTER this
            // dev-only contributor (contributor order is not guaranteed). Ensure
            // the Patient role exists first so a fresh seed never aborts on
            // "Role PATIENT does not exist". ExternalUserRole grants the role's
            // permissions unconditionally, so a role created here is still
            // permissioned correctly when that contributor runs.
            await EnsurePatientRoleExistsAsync(tenantId);

            var addRoleResult = await _userManager.AddToRoleAsync(user, RoleName);
            if (!addRoleResult.Succeeded)
            {
                _logger.LogWarning(
                    "DemoPatientDataSeedContributor: failed to assign '{RoleName}' to {Email}: {Errors}",
                    RoleName,
                    email,
                    string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
            }
        }

        return user;
    }

    /// <summary>Creates the tenant Patient role if it is absent (idempotent), so
    /// the demo patient can be assigned to it regardless of seed-contributor
    /// order. Permissions are granted by ExternalUserRoleDataSeedContributor.</summary>
    private async Task EnsurePatientRoleExistsAsync(Guid tenantId)
    {
        if (await _roleManager.FindByNameAsync(RoleName) != null)
        {
            return;
        }

        await _roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), RoleName, tenantId));
    }

    private async Task<Tenant?> FindTenantAsync(Guid tenantId)
    {
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
    /// URL/email-safe slug from a tenant name. Identical rule to the other demo
    /// seed contributors (kept local; the slug rule is tied to the subdomain
    /// pattern and these are its only callers).
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
