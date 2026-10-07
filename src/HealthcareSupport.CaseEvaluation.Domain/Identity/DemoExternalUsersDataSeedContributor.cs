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
/// Seeds NO external demo users. The seed plan below is an empty array, so the loop never runs
/// and this contributor currently creates nothing. Patient, Claim Examiner, Applicant Attorney
/// and Defense Attorney accounts come from the real registration and invite flows, so the
/// verification and invite emails actually fire (Adrian, 2026-06-09).
///
/// The <c>patient@&lt;slug&gt;.test</c> login seen on a fresh dev stack is NOT from here: it is
/// created by <see cref="DemoPatientDataSeedContributor"/>.
///
/// Kept as the place a demo external user would be added again. If it is, it stays gated on the
/// Development environment so production never gets demo logins, and it stays idempotent
/// (existing users by email are left alone; the role is re-applied only when missing).
/// </summary>
public class DemoExternalUsersDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    /// <summary>
    /// Issue #119 (2026-05-13) -- the four real-inbox external identities, one per external role,
    /// for end-to-end testing. NOT seeded: they are registered self-service so the verification
    /// email lands in the real inbox (see the comments in <c>SeedAsync</c>). Nothing in
    /// <c>src/</c> or <c>test/</c> reads this array today; it survives only as the canonical
    /// email-to-role mapping, and is a removal candidate.
    ///
    /// Mailbox-side notes from the demo-readiness pass live in
    /// docs/demo-readiness/2026-05-11-pre-demo.md (item B) -- the SoftwareFour inbox routes some
    /// mail to Junk; not a code issue.
    /// </summary>
    public static readonly (string Email, string RoleName, string First, string Last, string Phone)[] InboxedExternalUsers =
    {
        ("SoftwareThree@gesco.com", "Patient",            "Software", "Three", "555-020-0101"),
        ("SoftwareFour@gesco.com",  "Applicant Attorney", "Software", "Four",  "555-020-0102"),
        ("SoftwareFive@gesco.com",  "Defense Attorney",   "Software", "Five",  "555-020-0103"),
        ("SoftwareSix@gesco.com",   "Claim Examiner",     "Software", "Six",   "555-020-0104"),
    };

    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly ILogger<DemoExternalUsersDataSeedContributor> _logger;
    private readonly IDemoSeedEnvironment _demoSeedEnvironment;

    public DemoExternalUsersDataSeedContributor(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        ICurrentTenant currentTenant,
        IRepository<Tenant, Guid> tenantRepository,
        ILogger<DemoExternalUsersDataSeedContributor> logger,
        IDemoSeedEnvironment demoSeedEnvironment)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _currentTenant = currentTenant;
        _tenantRepository = tenantRepository;
        _logger = logger;
        _demoSeedEnvironment = demoSeedEnvironment;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (!DemoSeedGate.IsAllowed(_demoSeedEnvironment))
        {
            _logger.LogInformation(
                "DemoExternalUsersDataSeedContributor: skipping ({Reason}).",
                DemoSeedGate.ClosedReason(_demoSeedEnvironment));
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

            // Issue 1.2 (2026-05-12): synthetic First/Last/Phone per
            // demo user so admin pages + welcome banner aren't blank.
            // Synthetic 555-prefix per .claude/rules/test-data.md.
            // 2026-06-09 (Adrian, demo reset): NO external demo users are seeded.
            // Patient / Claim Examiner / Applicant + Defense Attorney accounts are
            // created via the real registration + invite flows during the demo so
            // the verification/invite emails actually fire. The InboxedExternalUsers
            // constant above is unreferenced and kept only as the email-to-role mapping.
            var seedPlan = Array.Empty<(string EmailPrefix, string RoleName, string First, string Last, string Phone)>();

            foreach (var (prefix, roleName, first, last, phone) in seedPlan)
            {
                var email = $"{prefix}@{slug}.test";
                await EnsureUserWithRoleAsync(
                    email: email,
                    userName: email,
                    roleName: roleName,
                    tenantId: tenantId,
                    firstName: first,
                    lastName: last,
                    phoneNumber: phone);
            }

            // 2026-05-13 (SEED-1) -- the four @gesco.com real-inbox
            // identities are NO LONGER seeded automatically. Per Adrian
            // they are intended for end-to-end real-user tests (the
            // tester registers them self-service from the form so the
            // verification email actually fires and lands in the real
            // inbox). The InboxedExternalUsers array constant (above)
            // is kept as the canonical email->role mapping (nothing
            // references it today); only the seed-side loop has
            // been removed. Issue #119 reverted on the seeding side;
            // the constant itself is kept for self-service tests.
        }
    }

    private async Task<IdentityUser?> EnsureUserWithRoleAsync(
        string email,
        string userName,
        string roleName,
        Guid? tenantId,
        string firstName,
        string lastName,
        string phoneNumber)
    {
        var role = await _roleManager.FindByNameAsync(roleName);
        if (role == null)
        {
            _logger.LogWarning(
                "DemoExternalUsersDataSeedContributor: role '{RoleName}' not found in tenant {TenantId}; skipping user.",
                roleName, tenantId);
            return null;
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new IdentityUser(Guid.NewGuid(), userName, email, tenantId);
            user.Name = firstName;
            user.Surname = lastName;
            user.SetPhoneNumber(phoneNumber, confirmed: true);
            var createResult = await _userManager.CreateAsync(user, InternalUsersDataSeedContributor.DefaultPassword);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning(
                    "DemoExternalUsersDataSeedContributor: failed to create user: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
                return null;
            }
            _logger.LogWarning(
                "DemoExternalUsersDataSeedContributor: DEMO SEED created a user (tenant {TenantId}) with the published default password.",
                tenantId);
        }
        else
        {
            // Idempotent backfill: prior seeds left Name/Surname/Phone
            // blank. Fill in so admin pages don't show empty cells.
            var changed = false;
            if (string.IsNullOrWhiteSpace(user.Name)) { user.Name = firstName; changed = true; }
            if (string.IsNullOrWhiteSpace(user.Surname)) { user.Surname = lastName; changed = true; }
            if (string.IsNullOrWhiteSpace(user.PhoneNumber))
            {
                user.SetPhoneNumber(phoneNumber, confirmed: true);
                changed = true;
            }
            if (!user.EmailConfirmed) { user.SetEmailConfirmed(true); changed = true; }
            if (changed) await _userManager.UpdateAsync(user);
        }

        if (!await _userManager.IsInRoleAsync(user, roleName))
        {
            var addRoleResult = await _userManager.AddToRoleAsync(user, roleName);
            if (!addRoleResult.Succeeded)
            {
                _logger.LogWarning(
                    "DemoExternalUsersDataSeedContributor: failed to assign role '{RoleName}' to user {UserId}: {Errors}",
                    roleName,
                    user.Id,
                    string.Join(", ", addRoleResult.Errors.Select(e => e.Description)));
            }
            else
            {
                _logger.LogInformation(
                    "DemoExternalUsersDataSeedContributor: assigned role '{RoleName}' to user {UserId}.",
                    roleName, user.Id);
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
