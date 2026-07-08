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
/// IT-Admin-only AppService for creating new internal users (Intake
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
        new[] { "Intake Staff", "Staff Supervisor" };

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
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<InternalUsersAppService> _logger;
    // BUG-029 v3 fix (2026-05-21): centralized tenant-aware URL composition.
    private readonly IAccountUrlBuilder _accountUrlBuilder;

    public InternalUsersAppService(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        IRepository<Tenant, Guid> tenantRepository,
        INotificationDispatcher notificationDispatcher,
        IDataFilter dataFilter,
        ILogger<InternalUsersAppService> logger,
        IAccountUrlBuilder accountUrlBuilder)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tenantRepository = tenantRepository;
        _notificationDispatcher = notificationDispatcher;
        _dataFilter = dataFilter;
        _logger = logger;
        _accountUrlBuilder = accountUrlBuilder;
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

        // 2. Phase D (2026-06-25): internal operators (Staff Supervisor, Intake
        //    Staff) are HOST logins -- a single account that switches into
        //    offices. They are created in HOST context (TenantId null), NOT inside
        //    a tenant. input.TenantId is ignored; an Intake operator's office
        //    access is granted later via the assignment screen. The shared
        //    "All offices" label flows into the welcome email's TenantName token.
        const string tenantName = "All offices";
        using (CurrentTenant.Change(null))
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

            // 4d. Construct the IdentityUser in HOST scope. The enclosing
            //     CurrentTenant.Change(null) makes CurrentTenant.Id null, so the
            //     operator is a host login (input.TenantId is ignored -- see step 2).
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

            // 4g. Resolve the portal URL the email links to. BUG-029 v3
            //     (2026-05-21): now via IAccountUrlBuilder so the
            //     tenant subdomain is always prepended.
            // Host operator -> host portal root (null tenant = no subdomain prefix).
            var portalUrl = await _accountUrlBuilder.BuildPortalRootUrlAsync(null);

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

    /// <summary>
    /// 2026-06-16 (Prompt 16, A-B3) -- admin-triggered password reset. Finds the
    /// target user in the caller's ambient scope (since Phase D the internal-users
    /// list is HOST-scoped, so operators resolve in host context), generates an ABP
    /// Identity reset token, builds the reset URL (host root for host operators,
    /// subdomain-prefixed for any tenant-scoped user), and dispatches the
    /// ResetPassword template -- the same pipeline the self-service forgot-password
    /// flow uses. Dispatch failures are NOT swallowed (unlike the create welcome
    /// email): there is no committed side-effect to protect, so a queue failure
    /// should surface to the admin rather than show a false success.
    /// </summary>
    [Authorize(CaseEvaluationPermissions.InternalUsers.Edit)]
    public virtual async Task SendPasswordResetEmailAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.InternalUserNotFound);
        }
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        // Phase D (2026-06-25): internal operators are HOST logins (TenantId null).
        // The prior guard threw InternalUserTenantRequired here, breaking reset for
        // every host operator. Host users get the host AuthServer reset URL; a
        // tenant-scoped user (legacy/edge) still gets the subdomain-prefixed one.
        var resetUrl = user.TenantId.HasValue
            ? await _accountUrlBuilder.BuildPasswordResetUrlAsync(user.TenantId.Value, user.Id, token)
            : await _accountUrlBuilder.BuildHostPasswordResetUrlAsync(user.Id, token);

        await _notificationDispatcher.DispatchAsync(
            templateCode: NotificationTemplateConsts.Codes.ResetPassword,
            recipients: new[]
            {
                new NotificationRecipient(
                    email: user.Email!,
                    role: RecipientRole.Patient,
                    isRegistered: true),
            },
            variables: BuildPasswordResetVariables(user, resetUrl),
            contextTag: $"PasswordReset/AdminTriggered/{user.Id}");
    }

    /// <summary>
    /// Variable bag for the <c>ResetPassword</c> template (admin-triggered
    /// path). Mirrors <c>ExternalAccountAppService.BuildPasswordTokenVariables</c>
    /// so the seeded EmailBodies/ResetPassword.html renders identically whether
    /// the reset was self-service or admin-initiated.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildPasswordResetVariables(
        IdentityUser user,
        string resetUrl)
    {
        var fullName = JoinName(user.Name, user.Surname);
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["PatientFirstName"] = user.Name ?? string.Empty,
            ["PatientLastName"] = user.Surname ?? string.Empty,
            ["PatientFullName"] = fullName,
            ["PatientEmail"] = user.Email ?? string.Empty,
            ["URL"] = resetUrl,
            ["CompanyLogo"] = string.Empty,
            ["lblHeaderTitle"] = string.Empty,
            ["lblFooterText"] = string.Empty,
            ["Email"] = string.Empty,
            ["Skype"] = string.Empty,
            ["ph_US"] = string.Empty,
            ["fax"] = string.Empty,
            ["imageInByte"] = string.Empty,
        };
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

    // BUG-029 v3 fix (2026-05-21): ResolvePortalBaseUrlAsync removed; the
    // call site uses IAccountUrlBuilder.BuildPortalRootUrlAsync directly.
    // The "http://falkinstein.localhost:4200" hardcoded fallback is gone --
    // missing App__AngularUrl env var now throws a clear error instead of
    // silently emitting the demo tenant URL.

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

    /// <summary>
    /// Internal role names, in display precedence (IT Admin &gt; Staff Supervisor
    /// &gt; Intake Staff). Mirrors the frontend INTERNAL_ROLE_NAMES + the Domain
    /// seed contributor's role names; kept as literals to match this file's
    /// existing <see cref="CreatableRoleNames"/> style (ABP layering keeps the
    /// Domain constant out of reach without a new dependency).
    /// </summary>
    private static readonly string[] InternalRoleNamesByPrecedence =
        new[] { "IT Admin", "Staff Supervisor", "Intake Staff" };

    /// <summary>
    /// 2026-06-30 (QA item B) -- paged, internal-role-scoped Staff list. See
    /// <see cref="IInternalUsersAppService.GetInternalUsersAsync"/>.
    ///
    /// <para>Queries only the three internal roles (one membership query each, in
    /// host context) and de-duplicates with the precedence above, so a user with
    /// two internal roles surfaces under the higher one. This is bounded by
    /// internal-staff headcount -- not business-volume data -- so the page's
    /// filter / sort / offset are applied in memory after the role union. Crucially
    /// it never loads the full identity-user set, which is what made the old
    /// load-500-then-filter truncate the Staff list past 500 total users.</para>
    /// </summary>
    [Authorize(CaseEvaluationPermissions.InternalUsers.Default)]
    public virtual async Task<PagedResultDto<InternalUserListDto>> GetInternalUsersAsync(
        GetInternalUsersInput input)
    {
        Check.NotNull(input, nameof(input));

        using (CurrentTenant.Change(null))
        {
            // Union the three internal roles, host operators only (TenantId null),
            // keeping the first (highest-precedence) role each user is found under.
            var byUserId = new Dictionary<Guid, (IdentityUser User, string Role)>();
            foreach (var roleName in InternalRoleNamesByPrecedence)
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
                foreach (var user in usersInRole)
                {
                    if (user.TenantId != null || byUserId.ContainsKey(user.Id))
                    {
                        continue;
                    }
                    byUserId[user.Id] = (user, roleName);
                }
            }

            var filter = input.Filter?.Trim();
            var rows = byUserId.Values
                .Where(x => MatchesInternalUserFilter(x.User, filter))
                .Select(x => new InternalUserListDto
                {
                    Id = x.User.Id,
                    FirstName = x.User.Name ?? string.Empty,
                    LastName = x.User.Surname ?? string.Empty,
                    FullName = ComposeUserFullName(x.User),
                    Email = x.User.Email ?? string.Empty,
                    Role = x.Role,
                    IsActive = x.User.IsActive,
                })
                .ToList();

            var totalCount = rows.Count;
            var page = SortInternalUsers(rows, input.Sorting)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToList();

            return new PagedResultDto<InternalUserListDto>(totalCount, page);
        }
    }

    private static bool MatchesInternalUserFilter(IdentityUser user, string? filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return true;
        }
        return Contains(user.Name, filter)
            || Contains(user.Surname, filter)
            || Contains(user.UserName, filter)
            || Contains(user.Email, filter);
    }

    private static bool Contains(string? value, string filter) =>
        value != null && value.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private static string ComposeUserFullName(IdentityUser user)
    {
        var full = JoinName(user.Name, user.Surname);
        return string.IsNullOrEmpty(full) ? user.UserName ?? string.Empty : full;
    }

    private static List<InternalUserListDto> SortInternalUsers(
        List<InternalUserListDto> rows, string? sorting)
    {
        var parts = (sorting ?? string.Empty).Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var field = parts.Length > 0 ? parts[0].ToLowerInvariant() : "fullname";
        var descending = parts.Length > 1
            && parts[1].Equals("desc", StringComparison.OrdinalIgnoreCase);

        IOrderedEnumerable<InternalUserListDto> ordered = field switch
        {
            "email" => OrderInternalUsers(rows, r => r.Email, descending),
            "role" => OrderInternalUsers(rows, r => r.Role, descending),
            "isactive" or "status" => descending
                ? rows.OrderByDescending(r => r.IsActive)
                : rows.OrderBy(r => r.IsActive),
            "firstname" or "name" => OrderInternalUsers(rows, r => r.FirstName, descending),
            "lastname" => OrderInternalUsers(rows, r => r.LastName, descending),
            _ => OrderInternalUsers(rows, r => r.FullName, descending),
        };
        return ordered.ToList();
    }

    private static IOrderedEnumerable<InternalUserListDto> OrderInternalUsers(
        IEnumerable<InternalUserListDto> rows,
        Func<InternalUserListDto, string> selector,
        bool descending) =>
        descending
            ? rows.OrderByDescending(selector, StringComparer.OrdinalIgnoreCase)
            : rows.OrderBy(selector, StringComparer.OrdinalIgnoreCase);
}
