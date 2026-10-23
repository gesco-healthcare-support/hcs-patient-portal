using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.ExternalAccount;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
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
///   <item>2026-10-23: no temporary password. The account is created
///         unconfirmed with a password nobody sees
///         (<see cref="UnusablePassword"/>), and the person is sent an
///         activate-your-account link (<see cref="IAccountActivationSender"/>);
///         choosing a password from it confirms the email. A password in an
///         email can be read by anyone the email reaches, and the old
///         auto-confirmed account gave appointment access on an address
///         nobody had proved.</item>
///   <item>Activation-email dispatch failures are logged + swallowed --
///         the user row + role assignment already committed, so the
///         reset button on the user's row (which sends a fresh activation
///         link to an account not yet active) is the recovery, not a retry
///         of the create.</item>
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

    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IDataFilter _dataFilter;
    private readonly ILogger<InternalUsersAppService> _logger;
    // BUG-029 v3 fix (2026-05-21): centralized tenant-aware URL composition.
    private readonly IAccountUrlBuilder _accountUrlBuilder;
    private readonly IAccountActivationSender _activationSender;

    public InternalUsersAppService(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        IRepository<Tenant, Guid> tenantRepository,
        INotificationDispatcher notificationDispatcher,
        IDataFilter dataFilter,
        ILogger<InternalUsersAppService> logger,
        IAccountUrlBuilder accountUrlBuilder,
        IAccountActivationSender activationSender)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tenantRepository = tenantRepository;
        _notificationDispatcher = notificationDispatcher;
        _dataFilter = dataFilter;
        _logger = logger;
        _accountUrlBuilder = accountUrlBuilder;
        _activationSender = activationSender;
    }

    [Authorize(CaseEvaluationPermissions.InternalUsers.Create)]
    public virtual async Task<InternalUserCreatedDto> CreateAsync(CreateInternalUserDto input)
    {
        EnsureHostCaller();
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
        //    "Management" label flows into the create-result DTO's TenantName (shown in
        //    the create-result UI).
        const string tenantName = "Management";
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

            // 4c. Construct the IdentityUser in HOST scope. The enclosing
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

            // 4d. Created UNCONFIRMED, with a password nobody ever sees: the person chooses their
            //     own from the activation link, and choosing it is what confirms the email
            //     (2026-10-23; this used to auto-confirm and email a temporary password).
            var createResult = await _userManager.CreateAsync(user, UnusablePassword.Generate());
            if (!createResult.Succeeded)
            {
                throw new BusinessException(
                    CaseEvaluationDomainErrorCodes.InternalUserCreateFailed)
                    .WithData("Errors", string.Join("; ", createResult.Errors.Select(e => e.Description)));
            }

            // 4e. Assign role. On failure, delete the user so the
            //     account doesn't sit role-less and unusable.
            var roleResult = await _userManager.AddToRoleAsync(user, input.RoleName);
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                throw new BusinessException(
                    CaseEvaluationDomainErrorCodes.InternalUserRoleAssignFailed)
                    .WithData("Errors", string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            }

            // 4f. Queue the activation email. Issued LAST, after every change to the user, because
            //     any later change to the security stamp would kill the link. A failure to queue is
            //     logged and reported through WelcomeEmailQueued but does NOT roll back the create.
            var welcomeEmailQueued = await TrySendActivationEmailAsync(user);

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
    /// flow uses. Dispatch failures are NOT swallowed (unlike the create's activation
    /// email): there is no committed side-effect to protect, so a queue failure
    /// should surface to the admin rather than show a false success.
    ///
    /// <para>2026-10-23: an account that has not activated yet (its email is not
    /// confirmed) is sent a fresh ACTIVATION link instead. A reset link would let
    /// that person set a password and then be refused at sign-in for an unconfirmed
    /// email. A switched-off account that has not activated is refused with a
    /// message, because its activation link would be dead on arrival.</para>
    /// </summary>
    [Authorize(CaseEvaluationPermissions.InternalUsers.Edit)]
    public virtual async Task SendPasswordResetEmailAsync(Guid userId)
    {
        EnsureHostCaller();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.InternalUserNotFound);
        }

        if (!user.EmailConfirmed)
        {
            if (!user.IsActive)
            {
                throw new BusinessException(CaseEvaluationDomainErrorCodes.ActivationAccountInactive);
            }

            await _activationSender.SendAsync(user, NotificationOccurrence.PerRequest($"AccountActivation/AdminTriggered/{user.Id}"));
            return;
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
            contextTag: NotificationOccurrence.PerRequest($"PasswordReset/AdminTriggered/{user.Id}"));
    }

    /// <summary>
    /// Variable bag for the <c>ResetPassword</c> template (admin-triggered
    /// path). Mirrors <c>ExternalAccountAppService.BuildPasswordTokenVariables</c>
    /// so the seeded EmailBodies/ResetPassword.html renders identically whether
    /// the reset was self-service or admin-initiated.
    /// </summary>
    private static Dictionary<string, object?> BuildPasswordResetVariables(
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

    // Gated by the class-level [Authorize(InternalUsers.Default)]. This method used to carry
    // [AllowAnonymous], and a method-level [AllowAnonymous] wins over the class attribute (ABP
    // 10.0.2 MethodInvocationAuthorizationService returns before reading it), so the office list
    // was readable without signing in -- and from every host, because the lookup below leaves
    // the caller's office for host context itself.
    //
    // HOST callers only (2026-10-20), like every method here: the internal-users form and the
    // shell's office switcher at host scope (IT Admin, Staff Supervisor, admin). Inside an office
    // the switcher asks IntakeAssignments.GetSwitchableOffices instead, which reads the host
    // operator behind the session from the impersonation claim. Pinned by
    // InternalUsersHostOnlyGateTests, which runs the real permission check.
    public virtual async Task<ListResultDto<LookupDto<Guid>>> GetTenantOptionsAsync(
        string? filter = null)
    {
        EnsureHostCaller();

        // The list spans every office, so it is read in host context whichever office the
        // caller is signed in to.
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

    private async Task<bool> TrySendActivationEmailAsync(IdentityUser user)
    {
        try
        {
            await _activationSender.SendAsync(user, NotificationOccurrence.PerRequest($"AccountActivation/InternalUserCreated/{user.Id}"));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "InternalUsersAppService.TrySendActivationEmailAsync: dispatch failed " +
                "for user {UserId}. Account is created; the reset button on the user's " +
                "row sends a fresh activation link.",
                user.Id);
            return false;
        }
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
        EnsureHostCaller();
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

    /// <summary>
    /// Refuses a caller who is inside an office, before anything is read or written. Internal users
    /// are host operators: every method here works in host context, so it belongs to the host alone.
    /// The Host-only permissions already stop an office caller at the authorization interceptor; this
    /// keeps the refusal in place even if a permission's side is ever widened again, or an office
    /// role is granted one of them.
    /// </summary>
    private void EnsureHostCaller()
    {
        if (CurrentTenant.IsAvailable)
        {
            throw new AbpAuthorizationException(
                "Internal users are managed from the host, not from inside an office.");
        }
    }
}
