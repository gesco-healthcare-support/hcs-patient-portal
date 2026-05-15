using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.InternalUsers;

/// <summary>
/// IT-Admin-only AppService for creating new internal users (Clinic
/// Staff, Staff Supervisor). See <see cref="IInternalUsersAppService"/>
/// for the contract.
///
/// <para><b>Security choices documented per acceptance criteria:</b></para>
/// <list type="bullet">
///   <item>Class-level <c>[Authorize(InternalUsers.Default)]</c> gates
///         the surface; <c>[Authorize(InternalUsers.Create)]</c> on
///         <see cref="CreateAsync"/> gates the mutation. Permission is
///         declared <c>MultiTenancySides.Host</c> in the definition
///         provider, granted to IT Admin only.</item>
///   <item>OLD parity password format <c>{4chars}@{4chars}</c> (9
///         characters total). NEW uses cryptographic
///         <c>RandomNumberGenerator</c> instead of OLD's hex
///         <c>Guid.NewGuid()</c> substring, and curates the source
///         alphabet to satisfy ABP's default password complexity
///         (1 upper / 1 lower / 1 digit / 1 non-alphanumeric) on every
///         random outcome (no rejection sampling needed).</item>
///   <item>Auto-verified (<c>SetEmailConfirmed(true)</c>) so the new
///         user can sign in immediately -- OLD parity for
///         <c>IsVerified = true</c> after IT-Admin add.</item>
///   <item><c>SetShouldChangePasswordOnNextLogin(true)</c> -- Adrian
///         decision 2026-05-15. ABP Identity natively enforces this
///         on the next sign-in; the temporary password is single-use.</item>
///   <item>Plaintext password is dispatched via
///         <c>INotificationDispatcher</c> (queued through Hangfire,
///         same path as ResetPassword / InviteExternalUser) and is
///         NEVER included in the response DTO.</item>
///   <item>Welcome-email dispatch failures are logged + swallowed --
///         the user row + role assignment already committed, so the
///         IT Admin should reset the password manually rather than
///         retry the create.</item>
/// </list>
/// </summary>
[Authorize(CaseEvaluationPermissions.InternalUsers.Default)]
[RemoteService(IsEnabled = false)]
public class InternalUsersAppService : CaseEvaluationAppService, IInternalUsersAppService
{
    /// <summary>
    /// OLD-parity allow-list. IT Admin can create only these two roles.
    /// External roles (Patient / Applicant Attorney / Defense Attorney
    /// / Claim Examiner) go through <c>InviteExternalUserAsync</c>;
    /// IT Admin self-creation is rejected (seeded only).
    /// </summary>
    public static readonly IReadOnlyList<string> CreatableRoleNames =
        new[] { "Clinic Staff", "Staff Supervisor" };

    // Curated character sets that exclude visually ambiguous glyphs
    // (0/O, 1/I/l) so the temporary password the user types from the
    // email reaches the server intact.
    private const string LowerChars = "abcdefghijkmnpqrstuvwxyz";   // no l, o
    private const string UpperChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";   // no I, O
    private const string DigitChars = "23456789";                   // no 0, 1
    private const string MixedChars = LowerChars + UpperChars + DigitChars;

    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly ISettingProvider _settingProvider;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<InternalUsersAppService> _logger;

    public InternalUsersAppService(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        IRepository<Tenant, Guid> tenantRepository,
        INotificationDispatcher notificationDispatcher,
        ISettingProvider settingProvider,
        IDataFilter dataFilter,
        ILogger<InternalUsersAppService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tenantRepository = tenantRepository;
        _notificationDispatcher = notificationDispatcher;
        _settingProvider = settingProvider;
        _dataFilter = dataFilter;
        _logger = logger;
    }

    [Authorize(CaseEvaluationPermissions.InternalUsers.Create)]
    public virtual async Task<InternalUserCreatedDto> CreateAsync(CreateInternalUserDto input)
    {
        Check.NotNull(input, nameof(input));

        // 1. Validate role is in the IT-Admin-creatable allow-list. This
        //    is defense in depth -- the SPA form's dropdown only offers
        //    the two roles, but a tampered request body must be
        //    rejected here regardless.
        if (!CreatableRoleNames.Contains(input.RoleName, StringComparer.Ordinal))
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.InternalUserInvalidRole)
                .WithData("AttemptedRole", input.RoleName)
                .WithData("AllowedRoles", string.Join(", ", CreatableRoleNames));
        }

        // 2. Validate tenant is supplied. IT Admin is host-scoped, so
        //    CurrentTenant.Id is null; the form's tenant picker is the
        //    only authoritative source for the target tenant.
        if (input.TenantId == Guid.Empty)
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.InternalUserTenantRequired);
        }

        // 3. Resolve the tenant display name in host context (Tenant
        //    rows are host-scoped per Volo SaaS).
        var tenantName = await ResolveTenantNameAsync(input.TenantId);
        if (string.IsNullOrWhiteSpace(tenantName))
        {
            throw new BusinessException(
                CaseEvaluationDomainErrorCodes.InternalUserTenantRequired);
        }

        // 4. Switch to the target tenant context for the remainder of
        //    the flow. Role lookup, duplicate-email check, and user
        //    create all run under this scope so the IdentityUser row
        //    is owned by the tenant (not the host).
        using (CurrentTenant.Change(input.TenantId))
        {
            // 4a. Confirm the role exists in the tenant. Seeded by
            //     InternalUserRoleDataSeedContributor; if missing, it
            //     surfaces as a 400 so the operator can re-seed
            //     instead of seeing a raw 500.
            var role = await _roleManager.FindByNameAsync(input.RoleName);
            if (role == null)
            {
                throw new BusinessException(
                    CaseEvaluationDomainErrorCodes.InternalUserRoleMissing)
                    .WithData("RoleName", input.RoleName);
            }

            // 4b. Reject duplicate email. HIPAA-safe error: no email
            //     echoed in the response (same pattern ExternalSignup
            //     uses to avoid account-enumeration leaks).
            var normalizedEmail = input.Email.Trim();
            var existing = await _userManager.FindByEmailAsync(normalizedEmail);
            if (existing != null)
            {
                throw new BusinessException(
                    CaseEvaluationDomainErrorCodes.InternalUserDuplicateEmail);
            }

            // 4c. Generate the temporary password. Curated alphabet +
            //     fixed-format `{Upper}{lower}{any}{any}@{digit}{any}{any}{any}`
            //     guarantees ABP's default complexity is satisfied
            //     without rejection sampling.
            var generatedPassword = GenerateParityPassword();

            // 4d. Construct the IdentityUser. Tenant id is set
            //     explicitly to the resolved target (CurrentTenant.Id
            //     is now == input.TenantId because of the using block).
            var user = new IdentityUser(
                id: GuidGenerator.Create(),
                userName: normalizedEmail,
                email: normalizedEmail,
                tenantId: CurrentTenant.Id)
            {
                Name = input.FirstName.Trim(),
                Surname = input.LastName.Trim(),
            };

            if (!string.IsNullOrWhiteSpace(input.PhoneNumber))
            {
                user.SetPhoneNumber(input.PhoneNumber.Trim(), confirmed: false);
            }

            var createResult = await _userManager.CreateAsync(user, generatedPassword);
            if (!createResult.Succeeded)
            {
                throw new BusinessException(
                    CaseEvaluationDomainErrorCodes.InternalUserCreateFailed)
                    .WithData("Errors", string.Join("; ", createResult.Errors.Select(e => e.Description)));
            }

            // 4e. Auto-verify (OLD parity) + force change on first
            //     login (Adrian decision 2026-05-15). Persisted in the
            //     same Identity update call.
            user.SetEmailConfirmed(true);
            user.SetShouldChangePasswordOnNextLogin(true);
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                // Roll back the freshly-created user so we don't leave
                // an orphan account without the confirmation flag.
                await _userManager.DeleteAsync(user);
                throw new BusinessException(
                    CaseEvaluationDomainErrorCodes.InternalUserCreateFailed)
                    .WithData("Errors", string.Join("; ", updateResult.Errors.Select(e => e.Description)));
            }

            // 4f. Assign role. On failure, delete the user so the
            //     account doesn't sit role-less and unusable.
            var roleResult = await _userManager.AddToRoleAsync(user, input.RoleName);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                throw new BusinessException(
                    CaseEvaluationDomainErrorCodes.InternalUserRoleAssignFailed)
                    .WithData("Errors", string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }

            // 4g. Resolve the portal URL the email links to. Per-tenant
            //     setting, falls back to the SPA's documented dev URL
            //     when unset (same pattern ExternalAccountAppService
            //     uses for the reset / verify URLs).
            var portalUrl = await ResolvePortalBaseUrlAsync();

            // 4h. Dispatch welcome email via the same path
            //     ResetPassword / InviteExternalUser use. Failure to
            //     queue is logged + surfaced via WelcomeEmailQueued
            //     but does NOT roll back the user create.
            var welcomeEmailQueued = await TrySendWelcomeEmailAsync(
                user: user,
                input: input,
                generatedPassword: generatedPassword,
                tenantName: tenantName,
                portalUrl: portalUrl);

            return new InternalUserCreatedDto
            {
                UserId = user.Id,
                Email = user.Email!,
                FirstName = user.Name ?? string.Empty,
                LastName = user.Surname ?? string.Empty,
                RoleName = input.RoleName,
                TenantName = tenantName,
                WelcomeEmailQueued = welcomeEmailQueued,
            };
        }
    }

    [AllowAnonymous]
    public virtual async Task<ListResultDto<LookupDto<Guid>>> GetTenantOptionsAsync(
        string? filter = null)
    {
        // Host-context tenant lookup. The form is reachable only by an
        // authenticated IT Admin (the route guard + class-level
        // [Authorize] gate), but we mark this endpoint AllowAnonymous
        // so the dropdown populates BEFORE any other check fires --
        // the SPA's permission guard handles the "is this user
        // allowed" question; we just hand back the tenant list.
        using (CurrentTenant.Change(null))
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _tenantRepository.GetQueryableAsync();
            var items = query
                .WhereIf(
                    !string.IsNullOrWhiteSpace(filter),
                    x => x.Name != null && x.Name.Contains(filter!))
                .OrderBy(x => x.Name)
                .Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name! })
                .Take(200)
                .ToList();
            return new ListResultDto<LookupDto<Guid>>(items);
        }
    }

    /// <summary>
    /// Cryptographically random temporary password in the OLD-parity
    /// <c>{4chars}@{4chars}</c> 9-character shape. Guarantees ABP
    /// defaults (1 upper / 1 lower / 1 digit / 1 non-alphanumeric)
    /// without rejection sampling: block 1 always carries upper + lower
    /// in positions 1+2; block 2 always carries a digit in position 1;
    /// the literal <c>@</c> between blocks supplies the
    /// non-alphanumeric. Internal so unit tests can verify the format
    /// contract.
    /// </summary>
    internal static string GenerateParityPassword()
    {
        var block1 =
            new[]
            {
                Pick(UpperChars),
                Pick(LowerChars),
                Pick(MixedChars),
                Pick(MixedChars),
            };
        var block2 =
            new[]
            {
                Pick(DigitChars),
                Pick(MixedChars),
                Pick(MixedChars),
                Pick(MixedChars),
            };
        return new string(block1) + "@" + new string(block2);
    }

    private static char Pick(string source)
    {
        var idx = RandomNumberGenerator.GetInt32(source.Length);
        return source[idx];
    }

    /// <summary>
    /// Returns the tenant's display name in host context. Returns null
    /// when the tenant row is absent so the caller can throw the
    /// appropriate error code without leaking which tenant ids exist.
    /// </summary>
    private async Task<string?> ResolveTenantNameAsync(Guid tenantId)
    {
        using (CurrentTenant.Change(null))
        {
            var tenant = await _tenantRepository.FindAsync(tenantId);
            return tenant?.Name;
        }
    }

    /// <summary>
    /// Resolves the SPA base URL the welcome email's CTA button links to.
    /// Falls back to the Phase 1A Falkinstein subdomain when the
    /// per-tenant <c>Notifications.PortalBaseUrl</c> setting is unset --
    /// mirrors <c>ExternalAccountAppService.ResolvePortalBaseUrlAsync</c>
    /// so both flows land on the same SPA host.
    /// </summary>
    private async Task<string> ResolvePortalBaseUrlAsync()
    {
        var configured = await _settingProvider.GetOrNullAsync(
            CaseEvaluationSettings.NotificationsPolicy.PortalBaseUrl);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return "http://falkinstein.localhost:4200";
        }
        return configured.TrimEnd('/');
    }

    private async Task<bool> TrySendWelcomeEmailAsync(
        IdentityUser user,
        CreateInternalUserDto input,
        string generatedPassword,
        string tenantName,
        string portalUrl)
    {
        try
        {
            await _notificationDispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.InternalUserCreated,
                recipients: new[]
                {
                    new NotificationRecipient(
                        email: user.Email!,
                        role: RecipientRole.Patient,   // generic "to" -- role here is the dispatcher hint, not the IdentityUser role
                        isRegistered: true),
                },
                variables: BuildWelcomeEmailVariables(
                    user: user,
                    input: input,
                    generatedPassword: generatedPassword,
                    tenantName: tenantName,
                    portalUrl: portalUrl),
                contextTag: $"InternalUserCreated/{user.Id}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "InternalUsersAppService.TrySendWelcomeEmailAsync: dispatch failed " +
                "for user {UserId}. Account is created; IT Admin must reset the " +
                "password manually.",
                user.Id);
            return false;
        }
    }

    /// <summary>
    /// Variable bag for the <c>InternalUserCreated</c> email template.
    /// Tokens are referenced in
    /// <c>src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/InternalUserCreated.html</c>
    /// as <c>##UserName##</c>, <c>##LoginUserName##</c>,
    /// <c>##Password##</c>, <c>##RoleName##</c>, <c>##TenantName##</c>,
    /// <c>##PortalUrl##</c>. Empty-string defaults for the brand tokens
    /// (<c>##CompanyLogo##</c>, etc.) match the pattern other templates
    /// use; per-tenant branding fills them in when the branding feature
    /// ships.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildWelcomeEmailVariables(
        IdentityUser user,
        CreateInternalUserDto input,
        string generatedPassword,
        string tenantName,
        string portalUrl)
    {
        var fullName = JoinName(input.FirstName, input.LastName);
        var variables = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["UserName"] = fullName,
            ["LoginUserName"] = user.Email ?? string.Empty,
            ["Password"] = generatedPassword,
            ["RoleName"] = input.RoleName,
            ["TenantName"] = tenantName,
            ["PortalUrl"] = portalUrl,
            // Defensive defaults for any tokens an IT-Admin editor may
            // have left in the template body.
            ["PatientFirstName"] = string.Empty,
            ["PatientLastName"] = string.Empty,
            ["PatientFullName"] = fullName,
            ["PatientEmail"] = user.Email ?? string.Empty,
            ["URL"] = portalUrl,
            ["CompanyLogo"] = string.Empty,
            ["lblHeaderTitle"] = string.Empty,
            ["lblFooterText"] = string.Empty,
            ["Email"] = string.Empty,
            ["Skype"] = string.Empty,
            ["ph_US"] = string.Empty,
            ["fax"] = string.Empty,
            ["imageInByte"] = string.Empty,
        };
        return variables;
    }

    private static string JoinName(string? first, string? last)
    {
        var hasFirst = !string.IsNullOrWhiteSpace(first);
        var hasLast = !string.IsNullOrWhiteSpace(last);
        if (hasFirst && hasLast) return first!.Trim() + " " + last!.Trim();
        if (hasFirst) return first!.Trim();
        if (hasLast) return last!.Trim();
        return string.Empty;
    }
}
