using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
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
/// Runtime seeding of external user accounts per tenant for demo / smoke
/// tests. Mirrors <see cref="InternalUsersDataSeedContributor"/> but emits
/// the four external roles defined in
/// <see cref="ExternalUserRoleDataSeedContributor"/>:
///   patient@&lt;tenantSlug&gt;.test            -&gt; Patient            + Patient domain row
///   adjuster@&lt;tenantSlug&gt;.test           -&gt; Claim Examiner     (no domain row -- per AppService)
///   applicant.attorney@&lt;tenantSlug&gt;.test -&gt; Applicant Attorney + ApplicantAttorney domain row
///   defense.attorney@&lt;tenantSlug&gt;.test   -&gt; Defense Attorney   (no domain row -- per AppService D-2)
///
/// The domain-entity creation mirrors <c>ExternalSignupAppService.RegisterAsync</c>:
/// only Patient and ApplicantAttorney get a saved profile row; ClaimExaminer
/// and DefenseAttorney are intentionally NOT created (their saved profiles
/// are not surfaced in any lookup or pre-fill UI).
///
/// Default password matches the internal seeder: <c>1q2w3E*r</c>. Accounts
/// are flagged <see cref="IdentityUser.SetEmailConfirmed"/> = true so the
/// demo skips ABP's <c>/Account/ConfirmUser</c> gate (production-only paths
/// remain unaffected because this contributor is Development-gated, just
/// like the internal one).
/// </summary>
public class ExternalUsersDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string DefaultPassword = "1q2w3E*r";

    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly PatientManager _patientManager;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly ApplicantAttorneyManager _applicantAttorneyManager;
    private readonly IRepository<ApplicantAttorney, Guid> _applicantAttorneyRepository;
    private readonly ILogger<ExternalUsersDataSeedContributor> _logger;

    public ExternalUsersDataSeedContributor(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        ICurrentTenant currentTenant,
        IRepository<Tenant, Guid> tenantRepository,
        PatientManager patientManager,
        IRepository<Patient, Guid> patientRepository,
        ApplicantAttorneyManager applicantAttorneyManager,
        IRepository<ApplicantAttorney, Guid> applicantAttorneyRepository,
        ILogger<ExternalUsersDataSeedContributor> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _currentTenant = currentTenant;
        _tenantRepository = tenantRepository;
        _patientManager = patientManager;
        _patientRepository = patientRepository;
        _applicantAttorneyManager = applicantAttorneyManager;
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (!IsDevelopment())
        {
            _logger.LogInformation(
                "ExternalUsersDataSeedContributor: skipping (not Development environment).");
            return;
        }

        if (context?.TenantId == null)
        {
            return;
        }

        await SeedTenantUsersAsync(context.TenantId.Value);
    }

    private static bool IsDevelopment()
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedTenantUsersAsync(Guid tenantId)
    {
        using (_currentTenant.Change(tenantId))
        {
            var tenant = await FindTenantAsync(tenantId);
            if (tenant == null)
            {
                _logger.LogWarning(
                    "ExternalUsersDataSeedContributor: tenant {TenantId} not found; skipping.",
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
                var user = await EnsureUserWithRoleAsync(
                    email: email,
                    userName: email,
                    roleName: roleName,
                    tenantId: tenantId);
                if (user == null)
                {
                    continue;
                }

                // Mirror ExternalSignupAppService.RegisterAsync: only Patient
                // and ApplicantAttorney get a saved profile. ClaimExaminer +
                // DefenseAttorney intentionally have no domain row (D-2,
                // 2026-04-30).
                if (roleName == "Patient")
                {
                    await EnsurePatientRowAsync(user, tenantId);
                }
                else if (roleName == "Applicant Attorney")
                {
                    await EnsureApplicantAttorneyRowAsync(user);
                }
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
                "ExternalUsersDataSeedContributor: role '{RoleName}' not found in tenant {TenantId}; skipping user {Email}.",
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
                    "ExternalUsersDataSeedContributor: failed to create {Email}: {Errors}",
                    email,
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return null;
            }
            _logger.LogInformation(
                "ExternalUsersDataSeedContributor: created user {Email} (tenant {TenantId}).",
                email, tenantId);
        }

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
                    "ExternalUsersDataSeedContributor: failed to assign role '{RoleName}' to {Email}: {Errors}",
                    roleName,
                    email,
                    string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
            }
            else
            {
                _logger.LogInformation(
                    "ExternalUsersDataSeedContributor: assigned role '{RoleName}' to {Email}.",
                    roleName, email);
            }
        }

        return user;
    }

    /// <summary>
    /// Idempotent. Creates a Patient domain row using the same hardcoded
    /// defaults that <c>ExternalSignupAppService.RegisterAsync</c> uses for a
    /// minimal-form signup. The Patient row is what the booking AppService
    /// looks up by IdentityUserId; without it, the booking page throws
    /// <c>EntityNotFoundException</c>.
    /// </summary>
    private async Task EnsurePatientRowAsync(IdentityUser user, Guid tenantId)
    {
        var existing = await _patientRepository
            .FirstOrDefaultAsync(p => p.IdentityUserId == user.Id);
        if (existing != null)
        {
            return;
        }

        await _patientManager.CreateAsync(
            stateId: null,
            appointmentLanguageId: null,
            identityUserId: user.Id,
            tenantId: tenantId,
            firstName: user.Name ?? string.Empty,
            lastName: user.Surname ?? string.Empty,
            email: user.Email ?? string.Empty,
            genderId: Gender.Male,
            dateOfBirth: DateTime.UtcNow.Date,
            phoneNumberTypeId: PhoneNumberType.Home);

        _logger.LogInformation(
            "ExternalUsersDataSeedContributor: created Patient domain row for {Email}.",
            user.Email);
    }

    /// <summary>
    /// Idempotent. Creates an empty ApplicantAttorney domain row so the
    /// booking-form's "Search by email" + lookup picker can discover the
    /// attorney on next booking. Mirrors RegisterAsync's D-2 logic.
    /// </summary>
    private async Task EnsureApplicantAttorneyRowAsync(IdentityUser user)
    {
        var existing = await _applicantAttorneyRepository
            .FirstOrDefaultAsync(a => a.IdentityUserId == user.Id);
        if (existing != null)
        {
            return;
        }

        await _applicantAttorneyManager.CreateAsync(
            stateId: null,
            identityUserId: user.Id);

        _logger.LogInformation(
            "ExternalUsersDataSeedContributor: created ApplicantAttorney domain row for {Email}.",
            user.Email);
    }

    private async Task<Tenant?> FindTenantAsync(Guid tenantId)
    {
        using (_currentTenant.Change(null))
        {
            return await _tenantRepository.FindAsync(tenantId);
        }
    }

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
