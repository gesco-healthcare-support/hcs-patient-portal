using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.ClaimExaminers;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Invitations;
using HealthcareSupport.CaseEvaluation.Localization;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Account.Emailing;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Saas.Tenants;
using Microsoft.Extensions.Hosting;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

public class ExternalSignupAppService : CaseEvaluationAppService, IExternalSignupAppService
{
    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly PatientManager _patientManager;
    private readonly IPatientRepository _patientRepository;
    private readonly IClaimExaminerRepository _claimExaminerRepository;
    private readonly ClaimExaminerManager _claimExaminerManager;
    private readonly ApplicantAttorneyManager _applicantAttorneyManager;
    private readonly IApplicantAttorneyRepository _applicantAttorneyRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<IdentityRole, Guid> _identityRoleRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IAppointmentApplicantAttorneyRepository _appointmentApplicantAttorneyRepository;
    private readonly AppointmentApplicantAttorneyManager _appointmentApplicantAttorneyManager;
    private readonly IRepository<DefenseAttorney, Guid> _defenseAttorneyRepository;
    private readonly DefenseAttorneyManager _defenseAttorneyManager;
    private readonly IAppointmentDefenseAttorneyRepository _appointmentDefenseAttorneyRepository;
    private readonly AppointmentDefenseAttorneyManager _appointmentDefenseAttorneyManager;
    private readonly IBackgroundJobManager _backgroundJobManager;
    // 2026-05-15: tokenized invite flow. Manager owns token gen + hash +
    // accept; dispatcher routes the email through the per-tenant
    // InviteExternalUser NotificationTemplate (same path as
    // ResetPassword / PasswordChange).
    private readonly InvitationManager _invitationManager;
    // 2026-06-15 (B3): direct invitation-repository access for the internal
    // People hub's active-invited-email lookup (portal-status chip). The
    // AppService already queries several repositories directly, so keeping
    // the bulk-email filter inline is consistent with that style.
    private readonly IInvitationRepository _invitationRepository;
    private readonly INotificationDispatcher _notificationDispatcher;
    // 2026-05-06: dev-only test helpers (MarkEmailConfirmed / DeleteTestUsers)
    // gate on EnvironmentName so they cannot be invoked in production.
    private readonly IHostEnvironment _hostEnvironment;
    // 2026-05-06: cross-tenant queries in the dev helpers (find a user by
    // email regardless of which tenant they registered under) need to bypass
    // ABP's IMultiTenant filter. CurrentTenant.Change(null) only switches
    // to host context; the filter still applies and excludes tenant rows.
    // IDataFilter.Disable<IMultiTenant> turns the filter off entirely.
    private readonly IDataFilter _dataFilter;
    // 2026-05-18 (B-4): canonical ABP IAccountEmailer is the framework
    // contract for sending account-related links. Project's
    // CaseEvaluationAccountEmailer implements this and -- after the
    // 2026-05-18 relocation from AuthServer/Emailing/ to
    // Application/Emailing/ -- is registered in BOTH the AuthServer's
    // DI container AND the HttpApi.Host's DI container. Used in
    // RegisterAsync to auto-send the verification email on successful
    // registration.
    private readonly IAccountEmailer _accountEmailer;
    // BUG-029 v3 fix (2026-05-21): tenant-aware URL composition. The invite
    // URL is built via this service rather than concatenating the raw
    // setting value, so the tenant subdomain is always present.
    private readonly Notifications.IAccountUrlBuilder _accountUrlBuilder;
    // BUG-012 (2026-05-22): typed CaseEvaluationResource localizer for the
    // static ValidateRegistrationInput helper. The base class L property is
    // an instance-only IStringLocalizer; the validator stays `internal static`
    // (tests bypass DI), so we pass this through as an optional parameter --
    // tests call with null + assert against the English fallback string.
    private readonly IStringLocalizer<CaseEvaluationResource> _localizer;

    public ExternalSignupAppService(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        IRepository<Tenant, Guid> tenantRepository,
        PatientManager patientManager,
        IPatientRepository patientRepository,
        IClaimExaminerRepository claimExaminerRepository,
        ClaimExaminerManager claimExaminerManager,
        ApplicantAttorneyManager applicantAttorneyManager,
        IApplicantAttorneyRepository applicantAttorneyRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<IdentityRole, Guid> identityRoleRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        IAppointmentApplicantAttorneyRepository appointmentApplicantAttorneyRepository,
        AppointmentApplicantAttorneyManager appointmentApplicantAttorneyManager,
        IRepository<DefenseAttorney, Guid> defenseAttorneyRepository,
        DefenseAttorneyManager defenseAttorneyManager,
        IAppointmentDefenseAttorneyRepository appointmentDefenseAttorneyRepository,
        AppointmentDefenseAttorneyManager appointmentDefenseAttorneyManager,
        IBackgroundJobManager backgroundJobManager,
        IHostEnvironment hostEnvironment,
        IDataFilter dataFilter,
        InvitationManager invitationManager,
        IInvitationRepository invitationRepository,
        INotificationDispatcher notificationDispatcher,
        IAccountEmailer accountEmailer,
        Notifications.IAccountUrlBuilder accountUrlBuilder,
        IStringLocalizer<CaseEvaluationResource> localizer)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tenantRepository = tenantRepository;
        _patientManager = patientManager;
        _patientRepository = patientRepository;
        _claimExaminerRepository = claimExaminerRepository;
        _claimExaminerManager = claimExaminerManager;
        _applicantAttorneyManager = applicantAttorneyManager;
        _applicantAttorneyRepository = applicantAttorneyRepository;
        _identityUserRepository = identityUserRepository;
        _identityRoleRepository = identityRoleRepository;
        _appointmentRepository = appointmentRepository;
        _appointmentApplicantAttorneyRepository = appointmentApplicantAttorneyRepository;
        _appointmentApplicantAttorneyManager = appointmentApplicantAttorneyManager;
        _defenseAttorneyRepository = defenseAttorneyRepository;
        _defenseAttorneyManager = defenseAttorneyManager;
        _appointmentDefenseAttorneyRepository = appointmentDefenseAttorneyRepository;
        _appointmentDefenseAttorneyManager = appointmentDefenseAttorneyManager;
        _backgroundJobManager = backgroundJobManager;
        _hostEnvironment = hostEnvironment;
        _dataFilter = dataFilter;
        _invitationManager = invitationManager;
        _invitationRepository = invitationRepository;
        _notificationDispatcher = notificationDispatcher;
        _accountEmailer = accountEmailer;
        _accountUrlBuilder = accountUrlBuilder;
        _localizer = localizer;
    }

    /// <summary>
    /// 2026-06-15 (B3) -- returns the subset of <paramref name="emails"/>
    /// that currently have an ACTIVE invitation in the caller's tenant. See
    /// <see cref="IExternalSignupAppService.GetActiveInvitedEmailsAsync"/>.
    /// The internal People hub passes only the current page's record-only
    /// emails (those without a login), so the query-string list stays small.
    /// </summary>
    [Authorize(CaseEvaluationPermissions.UserManagement.InviteExternalUser)]
    public virtual async Task<List<string>> GetActiveInvitedEmailsAsync(List<string> emails)
    {
        if (emails == null || emails.Count == 0)
        {
            return new List<string>();
        }

        // Normalize + dedupe so the IN-clause is minimal and matches the
        // lowercased form invites are stored in (InviteExternalUserAsync
        // lowercases the email before IssueAsync).
        var normalized = emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
        if (normalized.Count == 0)
        {
            return new List<string>();
        }

        // Active = not accepted AND not expired. ABP's ISoftDelete filter
        // excludes revoked rows and the IMultiTenant filter scopes the query
        // to the caller's tenant, so this mirrors Invitation.IsActive(nowUtc)
        // as a server-side predicate.
        var nowUtc = Clock.Now.ToUniversalTime();
        var query = await _invitationRepository.GetQueryableAsync();
        var matched = await AsyncExecuter.ToListAsync(
            query
                .Where(i => i.AcceptedAt == null
                            && i.ExpiresAt > nowUtc
                            && normalized.Contains(i.Email.ToLower()))
                .Select(i => i.Email));

        return matched
            .Select(e => e.ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Dev-only: mark a user's email as confirmed by email lookup. Cross-
    /// tenant: switches to host context and finds the IdentityUser regardless
    /// of which tenant they registered under, so the demo can iterate
    /// without re-typing tenant ids. Throws if not Development.
    /// </summary>
    [AllowAnonymous]
    public virtual async Task MarkEmailConfirmedAsync(string email)
    {
        EnsureDevelopmentOnly(nameof(MarkEmailConfirmedAsync));
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new UserFriendlyException("Email is required.");
        }

        var normalized = email.Trim();
        // Cross-tenant lookup: disable the IMultiTenant filter so the
        // query returns rows from every tenant, not just the host. Switching
        // CurrentTenant to null is NOT enough -- the filter still applies
        // and excludes rows whose TenantId is non-null. See
        // memory/project_imultitenant-filter.md.
        Guid? foundTenantId;
        Guid foundUserId;
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var query = await _identityUserRepository.GetQueryableAsync();
            var user = await AsyncExecuter.FirstOrDefaultAsync(
                query.Where(u => u.Email != null && u.Email.ToLower() == normalized.ToLower()));
            if (user == null)
            {
                throw new UserFriendlyException($"User with email '{email}' not found.");
            }
            foundTenantId = user.TenantId;
            foundUserId = user.Id;
        }

        using (CurrentTenant.Change(foundTenantId))
        {
            var managed = await _userManager.GetByIdAsync(foundUserId);
            if (managed == null)
            {
                throw new UserFriendlyException($"User with email '{email}' not found in tenant scope.");
            }
            managed.SetEmailConfirmed(true);
            var result = await _userManager.UpdateAsync(managed);
            if (!result.Succeeded)
            {
                throw new UserFriendlyException(string.Join(", ", result.Errors.Select(x => x.Description)));
            }
        }
    }

    /// <summary>
    /// Dev-only: delete the IdentityUser rows matching the given emails plus
    /// any dependent Patient / ApplicantAttorney / DefenseAttorney profile
    /// rows. Lets the demo re-register the same emails repeatedly. Cross-
    /// tenant lookup. Throws if not Development.
    /// </summary>
    [AllowAnonymous]
    public virtual async Task<DeleteTestUsersResultDto> DeleteTestUsersAsync(IList<string> emails)
    {
        EnsureDevelopmentOnly(nameof(DeleteTestUsersAsync));
        var result = new DeleteTestUsersResultDto();
        if (emails == null || emails.Count == 0)
        {
            return result;
        }

        foreach (var rawEmail in emails)
        {
            if (string.IsNullOrWhiteSpace(rawEmail))
            {
                continue;
            }
            var email = rawEmail.Trim();

            // Cross-tenant lookup: disable the IMultiTenant filter so the
            // query sees rows from every tenant. See
            // memory/project_imultitenant-filter.md for why CurrentTenant.Change(null)
            // is not sufficient on its own.
            List<(Guid Id, Guid? TenantId)> targets;
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var query = await _identityUserRepository.GetQueryableAsync();
                var users = await AsyncExecuter.ToListAsync(
                    query.Where(u => u.Email != null && u.Email.ToLower() == email.ToLower())
                         .Select(u => new { u.Id, u.TenantId }));
                targets = users.Select(u => (u.Id, u.TenantId)).ToList();
            }

            if (targets.Count == 0)
            {
                result.NotFound.Add(email);
                continue;
            }

            foreach (var t in targets)
            {
                using (CurrentTenant.Change(t.TenantId))
                {
                    var managed = await _userManager.GetByIdAsync(t.Id);
                    if (managed != null)
                    {
                        var deleteResult = await _userManager.DeleteAsync(managed);
                        if (!deleteResult.Succeeded)
                        {
                            throw new UserFriendlyException(
                                $"Delete failed for {email}: " +
                                string.Join(", ", deleteResult.Errors.Select(x => x.Description)));
                        }
                    }
                }
            }

            result.Deleted.Add(email);
        }

        return result;
    }

    private void EnsureDevelopmentOnly(string operation)
    {
        if (!_hostEnvironment.IsDevelopment())
        {
            throw new UserFriendlyException(
                $"{operation} is only available in Development environment.");
        }
    }

    [AllowAnonymous]
    public virtual async Task<ListResultDto<LookupDto<Guid>>> GetTenantOptionsAsync(string? filter = null)
    {
        if (CurrentTenant.Id.HasValue)
        {
            return new ListResultDto<LookupDto<Guid>>(new List<LookupDto<Guid>>());
        }

        var query = await _tenantRepository.GetQueryableAsync();
        var items = query
            .WhereIf(!string.IsNullOrWhiteSpace(filter), x => x.Name != null && x.Name.Contains(filter!))
            .OrderBy(x => x.Name)
            .Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name! })
            .Take(200)
            .ToList();

        return new ListResultDto<LookupDto<Guid>>(items);
    }

    /// <summary>
    /// 1.6 (2026-04-30): host-scoped tenant lookup by name. Used by the
    /// `/Account/Register` JS overlay to resolve `?__tenant=&lt;Name&gt;` invite-link
    /// query strings to the GUID needed for the registration POST. Unlike
    /// `GetTenantOptionsAsync`, this method explicitly switches to host
    /// context so it works regardless of the caller's current tenant
    /// (the AuthServer cookie may have been set from a prior session). Case-
    /// insensitive exact-match on the tenant Name. Returns null on miss.
    /// </summary>
    [AllowAnonymous]
    public virtual async Task<LookupDto<Guid>?> ResolveTenantByNameAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        using (CurrentTenant.Change(null))
        {
            var query = await _tenantRepository.GetQueryableAsync();
            var trimmed = name.Trim();
            var tenant = await AsyncExecuter.FirstOrDefaultAsync(
                query.Where(x => x.Name != null && x.Name.ToLower() == trimmed.ToLower()));
            if (tenant == null)
            {
                return null;
            }
            return new LookupDto<Guid> { Id = tenant.Id, DisplayName = tenant.Name! };
        }
    }

    [Authorize]
    public virtual async Task<ListResultDto<ExternalUserLookupDto>> GetExternalUserLookupAsync(string? filter = null)
    {
        // R2-4 (2026-06-22): all 4 external roles surface in this lookup. The old D-2
        // restriction (Patient + Applicant Attorney only) is reversed -- the 4 roles are
        // capability-equal, so a booker/accessor picker must find a Defense Attorney or
        // Claim Examiner exactly like an Applicant Attorney.
        var allowedRoleNames = new[]
        {
            "Patient",
            "Applicant Attorney",
            "Defense Attorney",
            "Claim Examiner",
        };

        var roleQuery = await _identityRoleRepository.GetQueryableAsync();
        var roles = await AsyncExecuter.ToListAsync(
            roleQuery
                .Where(r => allowedRoleNames.Contains(r.Name!))
                .Select(r => new { r.Id, r.Name }));

        if (roles.Count == 0)
        {
            return new ListResultDto<ExternalUserLookupDto>(new List<ExternalUserLookupDto>());
        }

        var roleIds = roles.Select(r => r.Id).ToList();
        var roleNameMap = roles.ToDictionary(r => r.Id, r => r.Name!);

        var userQuery = await _identityUserRepository.GetQueryableAsync();
        var currentUserId = CurrentUser.Id;
        var usersWithRoleId = await AsyncExecuter.ToListAsync(
            userQuery
                .Where(u => u.Roles.Any(r => roleIds.Contains(r.RoleId))
                    && (!currentUserId.HasValue || u.Id != currentUserId.Value))
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Surname,
                    u.Email,
                    FirstRoleId = u.Roles.Where(r => roleIds.Contains(r.RoleId)).Select(r => r.RoleId).FirstOrDefault(),
                }));

        // Phase 1 / C2 / D4 (2026-06-11): the projection above omits extension
        // properties (FirmName lives in the AbpUserExtraProperties JSON column).
        // Materialize the matched users once and read FirmName so the picker can
        // display a firm account's firm name when First/Last are blank. The list
        // is tenant-scoped + role-filtered, so this is a small set.
        var matchedIds = usersWithRoleId.Select(u => u.Id).ToList();
        var firmNameById = new Dictionary<Guid, string>();
        if (matchedIds.Count > 0)
        {
            var fullUsers = await AsyncExecuter.ToListAsync(
                userQuery.Where(u => matchedIds.Contains(u.Id)));
            foreach (var fullUser in fullUsers)
            {
                firmNameById[fullUser.Id] = fullUser.GetProperty<string>(
                    CaseEvaluationModuleExtensionConfigurator.FirmNamePropertyName) ?? string.Empty;
            }
        }

        var items = new List<ExternalUserLookupDto>();
        foreach (var u in usersWithRoleId)
        {
            if (!MatchesExternalUserFilter(u.Name, u.Surname, u.Email, filter))
            {
                continue;
            }

            var userRole = u.FirstRoleId != Guid.Empty && roleNameMap.TryGetValue(u.FirstRoleId, out var name)
                ? name
                : allowedRoleNames[0];

            items.Add(new ExternalUserLookupDto
            {
                IdentityUserId = u.Id,
                FirstName = u.Name ?? string.Empty,
                LastName = u.Surname ?? string.Empty,
                Email = u.Email ?? string.Empty,
                UserRole = userRole,
                FirmName = firmNameById.TryGetValue(u.Id, out var firm) ? firm : string.Empty,
            });
        }

        items = items.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ToList();
        return new ListResultDto<ExternalUserLookupDto>(items);
    }

    private static bool MatchesExternalUserFilter(string? name, string? surname, string? email, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return true;
        }

        return (name != null && name.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
               (surname != null && surname.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
               (email != null && email.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    [Authorize]
    public virtual async Task<ExternalUserProfileDto> GetMyProfileAsync()
    {
        var userId = CurrentUser.Id;
        if (!userId.HasValue)
        {
            throw new Volo.Abp.Authorization.AbpAuthorizationException("Current user is not authenticated.");
        }

        var user = await _userManager.GetByIdAsync(userId.Value);
        if (user == null)
        {
            throw new Volo.Abp.Domain.Entities.EntityNotFoundException(typeof(Volo.Abp.Identity.IdentityUser), userId.Value);
        }

        var roleNames = await _userManager.GetRolesAsync(user);
        var userRole = roleNames.FirstOrDefault(r =>
            string.Equals(r, "Patient", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "Applicant Attorney", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "Defense Attorney", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "Claim Examiner", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

        // Phase 9 (2026-05-03) -- surface IsExternalUser + IsAccessor
        // extension props registered in Phase 2.4 so the Angular SPA can
        // post-login route external-> /home / internal-> /dashboard
        // without a second roundtrip.
        var isExternalUser = ReadBoolExtensionProperty(
            user, CaseEvaluationModuleExtensionConfigurator.IsExternalUserPropertyName);
        var isAccessor = ReadBoolExtensionProperty(
            user, CaseEvaluationModuleExtensionConfigurator.IsAccessorPropertyName);

        // Phase 1 / C2 / D4 (2026-06-11): surface FirmName so the SPA home
        // banner can fall back to it for a firm account (blank Name/Surname).
        // FirmName is a string extension property -- GetProperty<string> is
        // safe here (the JsonElement coercion bug only affects bool reads).
        var firmName = user.GetProperty<string>(
            CaseEvaluationModuleExtensionConfigurator.FirmNamePropertyName) ?? string.Empty;

        return new ExternalUserProfileDto
        {
            IdentityUserId = user.Id,
            FirstName = user.Name ?? string.Empty,
            LastName = user.Surname ?? string.Empty,
            Email = user.Email ?? string.Empty,
            UserRole = userRole,
            IsExternalUser = isExternalUser,
            IsAccessor = isAccessor,
            FirmName = firmName,
        };
    }

    /// <summary>
    /// B3 (2026-05-06): thin pass-through to the shared
    /// <see cref="HealthcareSupport.CaseEvaluation.Extensions.ExtraPropertyConverters.GetBoolOrDefault"/>
    /// helper. Kept here so unit tests and existing call sites do not
    /// need to update their import. Any new caller should target the
    /// shared helper directly.
    /// </summary>
    internal static bool ReadBoolExtensionProperty(
        Volo.Abp.Identity.IdentityUser user,
        string propertyName)
        => HealthcareSupport.CaseEvaluation.Extensions.ExtraPropertyConverters
            .GetBoolOrDefault(user, propertyName);

    /// <summary>
    /// B3 (2026-05-06): pass-through to the shared coercion helper.
    /// </summary>
    internal static bool CoerceBool(object? raw)
        => HealthcareSupport.CaseEvaluation.Extensions.ExtraPropertyConverters
            .CoerceBool(raw);

    [AllowAnonymous]
    public virtual async Task RegisterAsync(ExternalUserSignUpDto input)
    {
        Check.NotNull(input, nameof(input));

        // 2026-05-15 -- when an InviteToken is present, the server is the
        // source of truth for Email + UserType. Validate the token first
        // (throws InviteInvalid / InviteExpired / InviteAlreadyAccepted on
        // failure) and overwrite the input so the rest of the register
        // path runs against the server-resolved values. A tampered form
        // (different email or role than the invitation) cannot register
        // as a different identity.
        Invitation? acceptedInvitation = null;
        if (!string.IsNullOrWhiteSpace(input.InviteToken))
        {
            acceptedInvitation = await _invitationManager.ValidateAsync(input.InviteToken);
            input.Email = acceptedInvitation.Email;
            input.UserType = acceptedInvitation.UserType;
            // Force the tenant context too, so the register path runs
            // under the invitation's tenant regardless of any
            // ?__tenant= or cookie context on the request.
            input.TenantId = acceptedInvitation.TenantId;
            // #21 (2026-06-16): if the inviter pre-set a firm name and the
            // recipient left it blank, carry it through so the required
            // attorney firm-name validation passes with the invited value.
            if (string.IsNullOrWhiteSpace(input.FirmName)
                && !string.IsNullOrWhiteSpace(acceptedInvitation.FirmName))
            {
                input.FirmName = acceptedInvitation.FirmName;
            }
        }

        // Phase 8 (2026-05-03) -- OLD-parity validation:
        //   - ConfirmPassword must equal Password (UserDomain.cs:88)
        //   - FirmName required for ApplicantAttorney AND DefenseAttorney
        //     (OLD-bug-fix on UserDomain.cs:272 which checked PatientAttorney
        //     twice; intent was both attorney roles)
        ValidateRegistrationInput(input, _localizer);

        var tenantId = ResolveTenantId(input.TenantId);
        var roleName = ToRoleName(input.UserType);

        using (CurrentTenant.Change(tenantId))
        {
            await EnsureRoleAsync(roleName);

            var existingUser = await _userManager.FindByEmailAsync(input.Email);
            if (existingUser != null)
            {
                // 2026-05-13 -- generic message, no email echo (BUG-001
                // -- user-enumeration leak). UserFriendlyException is
                // used (not BusinessException) because BusinessException's
                // auto-localization via MapCodeNamespace is documented
                // as not resolving in this codebase -- see
                // AppointmentsAppService.EnsureCanReadAppointmentAsync.
                // UserFriendlyException passes its message argument
                // through verbatim. The error code is still carried so
                // CaseEvaluationAuthServerModule + HttpApi.HostModule can
                // remap to HTTP 400 via AbpExceptionHttpStatusCodeOptions
                // (BUG-003 -- prior 403 was semantically wrong).
                throw new UserFriendlyException(
                    message: L["Registration:DuplicateEmail"],
                    code: CaseEvaluationDomainErrorCodes.RegistrationDuplicateEmail);
            }

            var user = new IdentityUser(
                GuidGenerator.Create(),
                userName: input.Email,
                email: input.Email,
                tenantId: CurrentTenant.Id
            )
            {
                // UM1 (2026-06-04): recipient-typed name wins; fall back to the
                // name the inviter stored on the invitation so a blank register
                // form still produces a personalized account.
                Name = !string.IsNullOrWhiteSpace(input.FirstName)
                    ? input.FirstName
                    : acceptedInvitation?.FirstName,
                Surname = !string.IsNullOrWhiteSpace(input.LastName)
                    ? input.LastName
                    : acceptedInvitation?.LastName,
            };

            // Phase 8 (2026-05-03) -- mark this row as an external user
            // (replaces OLD's UserType.ExternalUser=7 column). Extension
            // properties registered in Phase 2.4 via
            // CaseEvaluationModuleExtensionConfigurator. Persist BEFORE
            // CreateAsync so the property write is part of the same
            // INSERT (extra-properties are part of the entity row).
            user.SetProperty(
                CaseEvaluationModuleExtensionConfigurator.IsExternalUserPropertyName, true);

            // Phase 8 (2026-05-03) -- OLD UserDomain.cs:104-108 persists
            // FirmName + auto-derives FirmEmail from EmailId.ToLower() for
            // attorneys. NEW respects an explicit FirmEmail when supplied;
            // otherwise auto-derives, matching OLD behavior.
            if (IsAttorneyRole(input.UserType))
            {
                user.SetProperty(
                    CaseEvaluationModuleExtensionConfigurator.FirmNamePropertyName,
                    input.FirmName!.Trim());
                user.SetProperty(
                    CaseEvaluationModuleExtensionConfigurator.FirmEmailPropertyName,
                    DeriveFirmEmail(input));
            }

            var createResult = await _userManager.CreateAsync(user, input.Password);
            if (!createResult.Succeeded)
            {
                throw new UserFriendlyException(string.Join(", ", createResult.Errors.Select(x => x.Description)));
            }

            if (!await _userManager.IsInRoleAsync(user, roleName))
            {
                var roleResult = await _userManager.AddToRoleAsync(user, roleName);
                if (!roleResult.Succeeded)
                {
                    throw new UserFriendlyException(string.Join(", ", roleResult.Errors.Select(x => x.Description)));
                }
            }

            if (input.UserType == ExternalUserType.Patient)
            {
                // Merge (2026-06-07): take main's IP6 (2026-06-05) link-by-email
                // -- booking creates a record-only Patient (null IdentityUserId),
                // so on self-register CLAIM that existing record instead of
                // creating a second row. KEEP the parity G-06-08 sentinel
                // (Gender.Unspecified, not main's fabricated Gender.Male) for the
                // create branch. Patient is IMultiTenant, so the query is
                // auto-scoped to CurrentTenant.
                var normalizedPatientEmail = input.Email.Trim().ToLower();
                var patientQuery = await _patientRepository.GetQueryableAsync();
                var unclaimedPatient = await AsyncExecuter.FirstOrDefaultAsync(
                    patientQuery.Where(p =>
                        p.IdentityUserId == null
                        && p.Email.ToLower() == normalizedPatientEmail));
                if (unclaimedPatient != null)
                {
                    unclaimedPatient.IdentityUserId = user.Id;
                    await _patientRepository.UpdateAsync(unclaimedPatient);
                }
                else
                {
                    // No prior booking record. FirstName/LastName are not
                    // collected on the minimal register form (Adrian, 2026-04-30);
                    // normalize null to "". G-06-08: do not fabricate a real
                    // gender -- Unspecified + MinValue are "not provided yet"
                    // sentinels; the booking form requires real values at booking.
                    await _patientManager.CreateAsync(
                        stateId: null,
                        appointmentLanguageId: null,
                        identityUserId: user.Id,
                        tenantId: CurrentTenant.Id,
                        firstName: input.FirstName ?? string.Empty,
                        lastName: input.LastName ?? string.Empty,
                        email: input.Email,
                        genderId: Gender.Unspecified,
                        dateOfBirth: DateTime.MinValue,
                        phoneNumberTypeId: PhoneNumberType.Home
                    );
                }
            }
            else if (input.UserType == ExternalUserType.ApplicantAttorney)
            {
                // Create an empty AA master so the booker-side pre-fill ("Search by email"
                // + lookup picker) discovers this AA on next booking, the tenant-admin AA
                // management page surfaces them, and the appointment-AA join can point at a
                // real row. R2-4 (2026-06-22) reverses the old D-2 asymmetry: Defense Attorney
                // + Claim Examiner now get the same master treatment in their branches below.
                var existingApplicantAttorney = await _applicantAttorneyRepository
                    .FirstOrDefaultAsync(a => a.IdentityUserId == user.Id);
                if (existingApplicantAttorney == null)
                {
                    await _applicantAttorneyManager.CreateAsync(
                        stateId: null,
                        identityUserId: user.Id);
                }
            }
            else if (input.UserType == ExternalUserType.DefenseAttorney)
            {
                // R2-4 (2026-06-22, D-R2-A reverses D-2): Defense Attorney now gets a
                // saved master at registration, exactly like Applicant Attorney -- so the
                // DA surfaces in the booker pre-fill + tenant-admin management page, the
                // appointment-DA join can point at a real row, and the self-edit profile
                // (MyAttorneyProfileAppService, which already supports DA) has a record to
                // edit. FirmName is stored on the DefenseAttorney entity (not only the
                // IdentityUser ExtraProperties), so /defense-attorneys shows the firm.
                var existingDefenseAttorney = await _defenseAttorneyRepository
                    .FirstOrDefaultAsync(a => a.IdentityUserId == user.Id);
                if (existingDefenseAttorney == null)
                {
                    await _defenseAttorneyManager.CreateAsync(
                        stateId: null,
                        identityUserId: user.Id,
                        firmName: input.FirmName?.Trim(),
                        email: input.Email,
                        firstName: user.Name,
                        lastName: user.Surname);
                }
            }
            else if (input.UserType == ExternalUserType.ClaimExaminer)
            {
                // R2-4 (2026-06-22): Claim Examiner is a full external user like the
                // others -- create its master at registration so the CE surfaces for
                // linking and has a record to self-edit. CE has no firm fields (its
                // schema differs by design); name + email come from the register form.
                var existingClaimExaminer = await _claimExaminerRepository
                    .FirstOrDefaultAsync(c => c.IdentityUserId == user.Id);
                if (existingClaimExaminer == null)
                {
                    await _claimExaminerManager.CreateAsync(
                        stateId: null,
                        identityUserId: user.Id,
                        email: input.Email,
                        firstName: user.Name,
                        lastName: user.Surname);
                }
            }

            // S-5.2: auto-link the new user to any pre-existing appointments where the
            // booker captured the matching party email at booking time (PatientEmail /
            // ApplicantAttorneyEmail / DefenseAttorneyEmail / ClaimExaminerEmail on the
            // Appointment row, populated by S-5.1). This means an AA / DA who was
            // emailed an "appointment requested -- register here" link can sign up later
            // and immediately see the appointments that named them, without anyone
            // having to re-enter their details.
            await AutoLinkAppointmentsForUserAsync(user, input.UserType);

            // 2026-05-15 -- mark the invitation accepted in the same UoW
            // as the user create. AcceptAsync re-runs ValidateAsync inside
            // the transaction; the aggregate's ConcurrencyStamp wins
            // races between two simultaneous accepts (second writer gets
            // AbpDbConcurrencyException which surfaces as 500 -- acceptable
            // because the user row is already created and the second
            // recipient will just need to sign in).
            if (acceptedInvitation != null && !string.IsNullOrWhiteSpace(input.InviteToken))
            {
                await _invitationManager.AcceptAsync(input.InviteToken, user.Id);

                // OBS-25 (IP6, 2026-06-05): the invite token already proved the
                // recipient owns this email, so confirm it here -- the claim is
                // one click, with no separate verification step. Non-invite
                // registrations still run the email-confirmation flow below.
                user.SetEmailConfirmed(true);
                await _userManager.UpdateAsync(user);
            }

            // 2026-05-18 (B-4, Adrian directive reverses 2026-05-06):
            // auto-send the verification email on successful registration.
            // Routes through the project's IAccountEmailer override
            // (CaseEvaluationAccountEmailer), which after the B-1 fix
            // builds {AuthServerBaseUrl}/Account/EmailConfirmation,
            // substitutes the per-tenant UserRegistered NotificationTemplate,
            // and enqueues a Hangfire SendAppointmentEmailJob.
            //
            // Cross-host DI: CaseEvaluationAccountEmailer was relocated
            // from the AuthServer project to the Application project
            // 2026-05-18 (B-4 v5) so its [Dependency(ReplaceServices)] +
            // [ExposeServices(typeof(IAccountEmailer))] attributes
            // register the override in BOTH the AuthServer and the
            // HttpApi.Host DI containers. Before that move, calling
            // IAccountEmailer from this AppService (which runs under
            // HttpApi.Host for /api/public/external-signup/register)
            // resolved to the stock framework AccountEmailer and
            // threw System.TypeLoadException on Scriban 7.1.0 (CVE pin).
            //
            // No try/catch: ABP's UoW interceptor relies on exception
            // propagation to trigger rollback. IBackgroundJobManager.EnqueueAsync
            // (inside CaseEvaluationAccountEmailer.DispatchAsync) writes
            // to Hangfire's SQL tables in this same UoW transaction
            // (effectively the outbox pattern), so the job row is
            // atomic with the user-create row -- both commit or both
            // rollback. The actual SMTP send happens in a separate
            // Hangfire worker process with its own retry + dead-letter
            // handling. If we wrap this in try/catch+swallow, a
            // Hangfire/Redis/settings outage would create an orphan
            // user with no verification path.
            //
            // appName="MVC" matches the AppUrlOptions config B-1 set on
            // Applications["MVC"].Urls[AccountUrlNames.EmailConfirmation].
            // The IAccountEmailer override ignores the parameter at
            // runtime (hardcodes URL via AuthServerBaseUrl setting), but
            // "MVC" is the right intent for any future framework code
            // that consults IAppUrlProvider.
            //
            // See docs/plans/2026-05-18-auto-send-verification-at-registration.md
            // for the v1->v5 design evolution (v5 = current).
            if (!string.IsNullOrWhiteSpace(user.Email) && !user.EmailConfirmed)
            {
                var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                await _accountEmailer.SendEmailConfirmationLinkAsync(
                    user,
                    confirmationToken,
                    appName: "MVC",
                    returnUrl: null,
                    returnUrlHash: null);
            }
        }
    }

    /// <summary>
    /// Backfills join rows for a freshly-registered external user against
    /// already-existing appointments where the booker had captured this user's
    /// email at booking time.
    ///
    /// Scope per role:
    /// - ApplicantAttorney: creates AppointmentApplicantAttorney link rows.
    /// - DefenseAttorney: creates a DefenseAttorney profile row if one does not
    ///   yet exist (the registration block does not create one per D-2; the
    ///   join row needs a DefenseAttorneyId, so we create the entity here. It
    ///   stays out of all lookup/pre-fill surfaces because GetExternalUserLookupAsync
    ///   excludes the DA role and there is no DA management UI at MVP).
    /// - Patient: handled via AutoLinkPatientAsync (IP6, 2026-06-05). The
    ///   register block now CLAIMS the record-only Patient row that booking
    ///   created (sets IdentityUserId by email) rather than creating a second
    ///   row; this hook then back-links that patient's appointments
    ///   (Appointment.IdentityUserId). Visibility keys off Patient.IdentityUserId.
    /// - ClaimExaminer: skipped -- there is no IdentityUser-bound join entity
    ///   for CE at MVP (AppointmentClaimExaminer hangs off AppointmentInjuryDetail
    ///   and has no IdentityUserId column). Step 6.1 fan-out reaches the CE via
    ///   Appointment.ClaimExaminerEmail directly.
    /// </summary>
    private async Task AutoLinkAppointmentsForUserAsync(IdentityUser user, ExternalUserType userType)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return;
        }

        var normalizedEmail = user.Email.Trim().ToLower();

        if (userType == ExternalUserType.ApplicantAttorney)
        {
            await AutoLinkApplicantAttorneyAsync(user.Id, normalizedEmail);
        }
        else if (userType == ExternalUserType.DefenseAttorney)
        {
            await AutoLinkDefenseAttorneyAsync(user.Id, normalizedEmail);
        }
        else if (userType == ExternalUserType.Patient)
        {
            await AutoLinkPatientAsync(user.Id);
        }
        else if (userType == ExternalUserType.ClaimExaminer)
        {
            await AutoLinkClaimExaminerAsync(user.Id, normalizedEmail);
        }
    }

    private async Task AutoLinkApplicantAttorneyAsync(Guid identityUserId, string normalizedEmail)
    {
        // Bonus issue (2026-05-07): claim any unlinked master AA rows that
        // were created by the booking flow with this email and a null
        // IdentityUserId. The booking AppService persists AA rows for
        // typed-but-unregistered attorneys; once the attorney registers we
        // patch IdentityUserId here so the existing IdentityUserId-keyed
        // lookup below finds them and the Appointment.ApplicantAttorneyEmail
        // scan stays applicable.
        var unlinkedMasterQuery = await _applicantAttorneyRepository.GetQueryableAsync();
        var unlinkedMasters = await AsyncExecuter.ToListAsync(
            unlinkedMasterQuery.Where(a =>
                a.IdentityUserId == null
                && a.Email != null
                && a.Email.ToLower() == normalizedEmail));
        foreach (var master in unlinkedMasters)
        {
            master.IdentityUserId = identityUserId;
            await _applicantAttorneyRepository.UpdateAsync(master);
        }
        if (unlinkedMasters.Count > 0)
        {
            // Patch any existing link rows that point at these masters so
            // the attorney's "My Appointments" list surfaces them via the
            // visibility filter on AppointmentApplicantAttorney.IdentityUserId.
            var masterIds = unlinkedMasters.Select(m => m.Id).ToHashSet();
            var unlinkedLinkQuery = await _appointmentApplicantAttorneyRepository.GetQueryableAsync();
            var unlinkedLinks = await AsyncExecuter.ToListAsync(
                unlinkedLinkQuery.Where(l =>
                    l.IdentityUserId == null && masterIds.Contains(l.ApplicantAttorneyId)));
            foreach (var link in unlinkedLinks)
            {
                link.IdentityUserId = identityUserId;
                await _appointmentApplicantAttorneyRepository.UpdateAsync(link);
            }
        }

        var applicantAttorney = await _applicantAttorneyRepository
            .FirstOrDefaultAsync(a => a.IdentityUserId == identityUserId);
        if (applicantAttorney == null)
        {
            // No master row exists yet (no booking captured this email).
            // Nothing further to backfill; the AA can still appear on
            // future bookings.
            return;
        }

        var appointmentQuery = await _appointmentRepository.GetQueryableAsync();
        var matchingAppointmentIds = await AsyncExecuter.ToListAsync(
            appointmentQuery
                .Where(a => a.ApplicantAttorneyEmail != null
                            && a.ApplicantAttorneyEmail.ToLower() == normalizedEmail)
                .Select(a => a.Id));

        foreach (var appointmentId in matchingAppointmentIds)
        {
            var existingLinkCount = await _appointmentApplicantAttorneyRepository
                .GetCountAsync(appointmentId: appointmentId);
            if (existingLinkCount > 0)
            {
                continue;
            }

            await _appointmentApplicantAttorneyManager.CreateAsync(
                appointmentId,
                applicantAttorney.Id,
                identityUserId);
        }
    }

    private async Task AutoLinkDefenseAttorneyAsync(Guid identityUserId, string normalizedEmail)
    {
        // Bonus issue (2026-05-07): mirror the AA path. Claim unlinked
        // master DA rows + their link rows by email before falling through
        // to the IdentityUserId-keyed lookup.
        var unlinkedMasterQuery = await _defenseAttorneyRepository.GetQueryableAsync();
        var unlinkedMasters = await AsyncExecuter.ToListAsync(
            unlinkedMasterQuery.Where(d =>
                d.IdentityUserId == null
                && d.Email != null
                && d.Email.ToLower() == normalizedEmail));
        foreach (var master in unlinkedMasters)
        {
            master.IdentityUserId = identityUserId;
            await _defenseAttorneyRepository.UpdateAsync(master);
        }
        if (unlinkedMasters.Count > 0)
        {
            var masterIds = unlinkedMasters.Select(m => m.Id).ToHashSet();
            var unlinkedLinkQuery = await _appointmentDefenseAttorneyRepository.GetQueryableAsync();
            var unlinkedLinks = await AsyncExecuter.ToListAsync(
                unlinkedLinkQuery.Where(l =>
                    l.IdentityUserId == null && masterIds.Contains(l.DefenseAttorneyId)));
            foreach (var link in unlinkedLinks)
            {
                link.IdentityUserId = identityUserId;
                await _appointmentDefenseAttorneyRepository.UpdateAsync(link);
            }
        }

        var defenseAttorney = await _defenseAttorneyRepository
            .FirstOrDefaultAsync(a => a.IdentityUserId == identityUserId);
        if (defenseAttorney == null)
        {
            defenseAttorney = await _defenseAttorneyManager.CreateAsync(
                stateId: null,
                identityUserId: identityUserId);
        }

        var appointmentQuery = await _appointmentRepository.GetQueryableAsync();
        var matchingAppointmentIds = await AsyncExecuter.ToListAsync(
            appointmentQuery
                .Where(a => a.DefenseAttorneyEmail != null
                            && a.DefenseAttorneyEmail.ToLower() == normalizedEmail)
                .Select(a => a.Id));

        foreach (var appointmentId in matchingAppointmentIds)
        {
            var existingLinkCount = await _appointmentDefenseAttorneyRepository
                .GetCountAsync(appointmentId: appointmentId);
            if (existingLinkCount > 0)
            {
                continue;
            }

            await _appointmentDefenseAttorneyManager.CreateAsync(
                appointmentId,
                defenseAttorney.Id,
                identityUserId);
        }
    }

    /// <summary>
    /// IP6 (2026-06-05): back-link a freshly-registered patient's appointments.
    /// The register block has already CLAIMED the record-only Patient row (set
    /// IdentityUserId by email); here we stamp Appointment.IdentityUserId on
    /// that patient's appointments that booking left null, so party/booker email
    /// resolution and any IdentityUserId-keyed reads stay coherent. Visibility
    /// itself keys off Patient.IdentityUserId, so it already works post-claim.
    /// </summary>
    private async Task AutoLinkPatientAsync(Guid identityUserId)
    {
        var patientQuery = await _patientRepository.GetQueryableAsync();
        var patientIds = await AsyncExecuter.ToListAsync(
            patientQuery
                .Where(p => p.IdentityUserId == identityUserId)
                .Select(p => p.Id));
        if (patientIds.Count == 0)
        {
            return;
        }

        var appointmentQuery = await _appointmentRepository.GetQueryableAsync();
        var appointments = await AsyncExecuter.ToListAsync(
            appointmentQuery.Where(a =>
                a.IdentityUserId == null && patientIds.Contains(a.PatientId)));
        foreach (var appointment in appointments)
        {
            appointment.IdentityUserId = identityUserId;
            await _appointmentRepository.UpdateAsync(appointment);
        }
    }

    /// <summary>
    /// UM3/UM4 (2026-06-05): claim any unlinked Claim Examiner master rows created
    /// by an admin with this email + a null IdentityUserId, so the freshly-
    /// registered CE owns their master record. The per-appointment
    /// AppointmentClaimExaminer is free-text (no IdentityUserId-keyed join), so
    /// there is nothing further to back-link.
    /// </summary>
    private async Task AutoLinkClaimExaminerAsync(Guid identityUserId, string normalizedEmail)
    {
        var unlinkedQuery = await _claimExaminerRepository.GetQueryableAsync();
        var unlinked = await AsyncExecuter.ToListAsync(
            unlinkedQuery.Where(c =>
                c.IdentityUserId == null
                && c.Email != null
                && c.Email.ToLower() == normalizedEmail));
        foreach (var master in unlinked)
        {
            master.IdentityUserId = identityUserId;
            await _claimExaminerRepository.UpdateAsync(master);
        }
    }

    /// <summary>
    /// 2026-05-15 (revised) -- admin-side invite for an external user.
    /// Replaces the prior unbounded-URL pattern with a one-time-use,
    /// 7-day-TTL token (see <see cref="InvitationManager"/>). The URL
    /// is <c>{authServerBaseUrl}/Account/Register?inviteToken=&lt;raw&gt;</c>;
    /// the AuthServer JS overlay validates the token, prefills + locks
    /// the email + role on the register form, and re-validates server-
    /// side at submit time.
    ///
    /// <para>Authorization is permission-based:
    /// <c>CaseEvaluation.UserManagement.InviteExternalUser</c> -- granted
    /// to IT Admin, Staff Supervisor, and Intake Staff via the
    /// internal-role seeder. External roles never receive this
    /// permission so a tampered URL cannot register as internal.</para>
    /// </summary>
    [Authorize(CaseEvaluationPermissions.UserManagement.InviteExternalUser)]
    public virtual async Task<InviteExternalUserResultDto> InviteExternalUserAsync(InviteExternalUserDto input)
    {
        if (input == null || string.IsNullOrWhiteSpace(input.Email))
        {
            throw new UserFriendlyException(L["Email is required."]);
        }

        if (!IsExternalRoleType(input.UserType))
        {
            throw new UserFriendlyException(L["Only external roles can be invited via this surface."]);
        }

        var roleName = ToRoleName(input.UserType);
        var tenantId = CurrentTenant.Id;
        if (!tenantId.HasValue)
        {
            throw new UserFriendlyException(L["Tenant context required for invite."]);
        }

        var tenantName = await ResolveCurrentTenantNameAsync(tenantId.Value);
        if (string.IsNullOrWhiteSpace(tenantName))
        {
            throw new UserFriendlyException(L["Could not resolve tenant name for invite."]);
        }

        // CurrentUser is guaranteed set (perm-gated AppService).
        var invitedByUserId = CurrentUser.Id ?? Guid.Empty;
        var normalizedEmail = input.Email.Trim().ToLowerInvariant();
        // Names are trimmed but NOT lowercased (display values).
        var firstName = string.IsNullOrWhiteSpace(input.FirstName) ? null : input.FirstName.Trim();
        var lastName = string.IsNullOrWhiteSpace(input.LastName) ? null : input.LastName.Trim();

        // #21 (2026-06-16): persist firm name for attorney invites only so
        // registration can pre-fill the firm; null for non-attorney roles.
        var firmName = IsAttorneyRole(input.UserType) && !string.IsNullOrWhiteSpace(input.FirmName)
            ? input.FirmName.Trim()
            : null;

        var (invitation, rawToken) = await _invitationManager.IssueAsync(
            tenantId: tenantId.Value,
            email: normalizedEmail,
            userType: input.UserType,
            invitedByUserId: invitedByUserId,
            firstName: firstName,
            lastName: lastName,
            firmName: firmName);

        // BUG-029 v3 fix (2026-05-21): invite URL now routes through
        // IAccountUrlBuilder, which composes the tenant subdomain.
        // The prior `BuildInviteUrl(authServerBaseUrl, rawToken)` static
        // concatenated the raw setting value with the path and dropped
        // the tenant prefix.
        var inviteUrl = await _accountUrlBuilder.BuildInviteUrlAsync(
            tenantId.Value, rawToken);

        // Dispatch through the per-tenant InviteExternalUser
        // NotificationTemplate. Failure is logged (by the dispatcher) but
        // does NOT bubble: the admin can always copy + share the inviteUrl
        // manually when SMTP is degraded.
        try
        {
            await _notificationDispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.InviteExternalUser,
                recipients: new[]
                {
                    new NotificationRecipient(
                        email: normalizedEmail,
                        role: MapToRecipientRole(input.UserType),
                        isRegistered: false),
                },
                variables: BuildInvitationVariables(tenantName, roleName, inviteUrl, invitation.ExpiresAt, firstName, lastName),
                contextTag: $"Invite/{roleName}/{tenantId.Value}/{invitation.Id}");
        }
        catch (Exception)
        {
            // Swallowed by design -- the dispatcher logs its own failures
            // and the admin always sees the inviteUrl in the response.
        }

        return new InviteExternalUserResultDto
        {
            InviteUrl = inviteUrl,
            Email = normalizedEmail,
            RoleName = roleName,
            TenantName = tenantName,
            ExpiresAt = invitation.ExpiresAt,
        };
    }

    /// <summary>
    /// 2026-06-16 (Prompt 16, A-B1) -- paged invite-management list for the
    /// internal "Pending Invites" surface. Returns EVERY invitation in the
    /// caller's tenant including revoked (soft-deleted) rows, with a derived
    /// <see cref="InvitationStatus"/> so the UI can facet client-side. The
    /// IMultiTenant filter still scopes the query to the caller's tenant.
    /// </summary>
    [Authorize(CaseEvaluationPermissions.UserManagement.InviteExternalUser)]
    public virtual async Task<PagedResultDto<InvitationDto>> GetInvitesAsync(GetInvitesInput input)
    {
        var nowUtc = Clock.Now.ToUniversalTime();
        var filter = input.Filter?.Trim();

        // Disable the soft-delete filter so revoked invitations still surface
        // (as Status = Revoked). The IMultiTenant filter is left on, so the
        // list stays scoped to the caller's tenant.
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var query = await _invitationRepository.GetQueryableAsync();
            query = query.WhereIf(
                !string.IsNullOrWhiteSpace(filter),
                i => i.Email.Contains(filter!));

            var totalCount = await AsyncExecuter.CountAsync(query);

            var items = await AsyncExecuter.ToListAsync(
                query.OrderByDescending(i => i.CreationTime)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount));

            var inviterNames = await ResolveInviterNamesAsync(
                items.Select(i => i.InvitedByUserId).Distinct().ToList());

            var dtos = items.Select(i => new InvitationDto
            {
                Id = i.Id,
                Email = i.Email,
                UserType = i.UserType,
                RoleName = ToRoleName(i.UserType),
                FirstName = i.FirstName,
                LastName = i.LastName,
                FirmName = i.FirmName,
                InvitedByUserId = i.InvitedByUserId,
                InvitedByName = inviterNames.TryGetValue(i.InvitedByUserId, out var inviter) ? inviter : null,
                CreationTime = i.CreationTime,
                ExpiresAt = i.ExpiresAt,
                AcceptedAt = i.AcceptedAt,
                Status = InvitationStatusResolver.Resolve(i.IsDeleted, i.AcceptedAt, i.ExpiresAt, nowUtc),
            }).ToList();

            return new PagedResultDto<InvitationDto>(totalCount, dtos);
        }
    }

    /// <summary>
    /// 2026-06-16 (A-B1) -- re-issues a pending invitation in place (fresh
    /// token + reset 7-day expiry, old token invalidated) and re-dispatches the
    /// invite email. Returns the new invite URL so the admin can copy it.
    /// Rejects an already-accepted invitation. GetAsync is tenant- and
    /// soft-delete-filtered, so a revoked or cross-tenant id surfaces as a clean
    /// EntityNotFoundException (404).
    /// </summary>
    [Authorize(CaseEvaluationPermissions.UserManagement.InviteExternalUser)]
    public virtual async Task<InviteExternalUserResultDto> ResendInviteAsync(Guid id)
    {
        var invitation = await _invitationRepository.GetAsync(id);
        if (invitation.AcceptedAt.HasValue)
        {
            throw new UserFriendlyException(L["Cannot resend an invitation that has already been accepted."]);
        }

        var tenantId = invitation.TenantId
            ?? throw new UserFriendlyException(L["Tenant context required for invite."]);
        var tenantName = await ResolveCurrentTenantNameAsync(tenantId)
            ?? throw new UserFriendlyException(L["Could not resolve tenant name for invite."]);
        var roleName = ToRoleName(invitation.UserType);

        var rawToken = await _invitationManager.ResendAsync(invitation);
        var inviteUrl = await _accountUrlBuilder.BuildInviteUrlAsync(tenantId, rawToken);

        // Dispatch mirrors InviteExternalUserAsync; failure is swallowed so the
        // admin can always copy + share the inviteUrl in the response.
        try
        {
            await _notificationDispatcher.DispatchAsync(
                templateCode: NotificationTemplateConsts.Codes.InviteExternalUser,
                recipients: new[]
                {
                    new NotificationRecipient(
                        email: invitation.Email,
                        role: MapToRecipientRole(invitation.UserType),
                        isRegistered: false),
                },
                variables: BuildInvitationVariables(tenantName, roleName, inviteUrl, invitation.ExpiresAt, invitation.FirstName, invitation.LastName),
                contextTag: $"InviteResend/{roleName}/{tenantId}/{invitation.Id}");
        }
        catch (Exception)
        {
            // Swallowed by design -- the dispatcher logs its own failures.
        }

        return new InviteExternalUserResultDto
        {
            InviteUrl = inviteUrl,
            Email = invitation.Email,
            RoleName = roleName,
            TenantName = tenantName,
            ExpiresAt = invitation.ExpiresAt,
        };
    }

    /// <summary>
    /// 2026-06-16 (A-B1) -- revokes (soft-deletes) a pending invitation so its
    /// token stops validating immediately (the ISoftDelete filter excludes it
    /// from FindByTokenHashAsync). Rejects an already-accepted invitation.
    /// </summary>
    [Authorize(CaseEvaluationPermissions.UserManagement.InviteExternalUser)]
    public virtual async Task RevokeInviteAsync(Guid id)
    {
        var invitation = await _invitationRepository.GetAsync(id);
        if (invitation.AcceptedAt.HasValue)
        {
            throw new UserFriendlyException(L["Cannot revoke an invitation that has already been accepted."]);
        }

        await _invitationRepository.DeleteAsync(invitation, autoSave: true);
    }

    /// <summary>
    /// Batched lookup of inviter display names for the invite list. Resolves in
    /// the caller's tenant scope (internal staff issue invites from within their
    /// tenant). Returns full name, falling back to username/email; an id that
    /// cannot be resolved (e.g. a host user) is simply absent from the map.
    /// </summary>
    private async Task<Dictionary<Guid, string>> ResolveInviterNamesAsync(List<Guid> userIds)
    {
        var result = new Dictionary<Guid, string>();
        if (userIds.Count == 0)
        {
            return result;
        }

        var query = await _identityUserRepository.GetQueryableAsync();
        var users = await AsyncExecuter.ToListAsync(
            query.Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Name, u.Surname, u.UserName, u.Email }));

        foreach (var u in users)
        {
            var display = $"{u.Name} {u.Surname}".Trim();
            if (string.IsNullOrWhiteSpace(display))
            {
                display = u.UserName ?? u.Email ?? string.Empty;
            }
            result[u.Id] = display;
        }

        return result;
    }

    /// <summary>
    /// 2026-05-15 -- anonymous validation endpoint for the JS overlay on
    /// <c>/Account/Register</c>. Throws <c>BusinessException</c> with one
    /// of <c>InviteInvalid</c> / <c>InviteExpired</c> /
    /// <c>InviteAlreadyAccepted</c> when the token is unusable; the
    /// overlay renders the appropriate banner per error code.
    /// </summary>
    [AllowAnonymous]
    public virtual async Task<InvitationValidationDto> ValidateInviteAsync(string token)
    {
        var invitation = await _invitationManager.ValidateAsync(token);

        // Resolve the tenant display name in host context (Tenant rows
        // are host-scoped; the invitation row's TenantId tells us which).
        var tenantId = invitation.TenantId
            ?? throw new BusinessException(CaseEvaluationDomainErrorCodes.InviteInvalid);
        var tenantName = await ResolveCurrentTenantNameAsync(tenantId)
            ?? throw new BusinessException(CaseEvaluationDomainErrorCodes.InviteInvalid);

        return new InvitationValidationDto
        {
            Email = invitation.Email,
            UserType = invitation.UserType,
            RoleName = ToRoleName(invitation.UserType),
            TenantName = tenantName,
            ExpiresAt = invitation.ExpiresAt,
            FirstName = invitation.FirstName,
            LastName = invitation.LastName,
            FirmName = invitation.FirmName,
        };
    }

    /// <summary>
    /// Variable bag for the <c>InviteExternalUser</c> NotificationTemplate.
    /// Tokens are referenced in
    /// <c>src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/InviteExternalUser.html</c>
    /// as <c>##TenantName##</c>, <c>##RoleName##</c>, <c>##URL##</c>,
    /// <c>##ExpiresAt##</c>, <c>##Greeting##</c>. UM1 (2026-06-04): the invite
    /// now optionally collects the recipient name; <c>##Greeting##</c> renders
    /// "Hi First Last," when a name is present and falls back to "Hello," when
    /// blank (fixes the OBS-27 empty "Hi ," greeting).
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildInvitationVariables(
        string tenantName, string roleName, string inviteUrl, DateTime expiresAtUtc,
        string? firstName = null, string? lastName = null)
    {
        // Format expiry as a short human-readable UTC date so all tenants
        // see the same calendar day regardless of viewer locale; the
        // recipient does not need timezone precision to know "this link
        // works through Tuesday".
        var expiresAtLabel = expiresAtUtc.ToString("MMMM d, yyyy");
        var fullName = $"{firstName} {lastName}".Trim();
        var greeting = string.IsNullOrWhiteSpace(fullName) ? "Hello," : $"Hi {fullName},";
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["TenantName"] = tenantName,
            ["RoleName"] = roleName,
            ["URL"] = inviteUrl,
            ["ExpiresAt"] = expiresAtLabel,
            ["Greeting"] = greeting,
            ["PatientFullName"] = fullName,
            ["PatientFirstName"] = firstName ?? string.Empty,
            ["PatientLastName"] = lastName ?? string.Empty,
            ["PatientEmail"] = string.Empty,
        };
    }

    private static bool IsExternalRoleType(ExternalUserType type) => type switch
    {
        ExternalUserType.Patient => true,
        ExternalUserType.ApplicantAttorney => true,
        ExternalUserType.DefenseAttorney => true,
        ExternalUserType.ClaimExaminer => true,
        _ => false,
    };

    private static RecipientRole MapToRecipientRole(ExternalUserType type) => type switch
    {
        ExternalUserType.Patient => RecipientRole.Patient,
        ExternalUserType.ApplicantAttorney => RecipientRole.ApplicantAttorney,
        ExternalUserType.DefenseAttorney => RecipientRole.DefenseAttorney,
        ExternalUserType.ClaimExaminer => RecipientRole.ClaimExaminer,
        _ => RecipientRole.Patient,
    };

    private async Task<string?> ResolveCurrentTenantNameAsync(Guid tenantId)
    {
        // Tenant rows live in the host scope; switch to host context so the
        // IMultiTenant filter does not exclude the row.
        using (CurrentTenant.Change(null))
        {
            var tenant = await _tenantRepository.FindAsync(tenantId);
            return tenant?.Name;
        }
    }

    // BUG-029 v3 fix (2026-05-21): BuildInviteUrl helper removed.
    // Invite URL composition lives in IAccountUrlBuilder (single
    // source of truth for tenant-prefixed account URLs).

    private Guid? ResolveTenantId(Guid? requestedTenantId)
    {
        if (CurrentTenant.Id.HasValue)
        {
            return CurrentTenant.Id;
        }

        if (!requestedTenantId.HasValue)
        {
            throw new UserFriendlyException("Tenant selection is required.");
        }

        return requestedTenantId.Value;
    }

    /// <summary>
    /// Maps the external-user-type enum to the role-name string seeded by
    /// <c>ExternalUserRoleDataSeedContributor</c>. Phase 8 (2026-05-03)
    /// added <c>Adjuster</c> per OLD parity (<c>Roles.cs:14-17</c>).
    /// <c>ClaimExaminer</c> is a NEW deviation flagged in audit gap G1
    /// and retained for Session A's tenant-invite flow compatibility.
    /// Internal so unit tests can verify without ABP infra.
    /// </summary>
    internal static string ToRoleName(ExternalUserType userType)
    {
        return userType switch
        {
            ExternalUserType.Patient => "Patient",
            ExternalUserType.ClaimExaminer => "Claim Examiner",
            ExternalUserType.ApplicantAttorney => "Applicant Attorney",
            ExternalUserType.DefenseAttorney => "Defense Attorney",
            _ => throw new UserFriendlyException("Invalid user type."),
        };
    }

    /// <summary>
    /// True for the two attorney roles. <c>FirmName</c> + <c>FirmEmail</c>
    /// extension props are persisted only for attorneys, mirroring OLD
    /// <c>UserDomain.cs:104-108</c>. OLD's source had a copy-paste bug
    /// at line 272 that checked <c>PatientAttorney</c> twice; NEW
    /// validates both attorney roles correctly (audit gap G6 / OLD-bug-fix).
    /// </summary>
    internal static bool IsAttorneyRole(ExternalUserType userType) =>
        userType == ExternalUserType.ApplicantAttorney
        || userType == ExternalUserType.DefenseAttorney;

    /// <summary>
    /// OLD-parity validation invoked at the top of <c>RegisterAsync</c>:
    /// <list type="bullet">
    ///   <item>Confirm-password match (OLD <c>UserDomain.cs:88</c>).</item>
    ///   <item>FirmName required for both attorney roles (OLD-bug-fix on
    ///         <c>UserDomain.cs:272</c> which checked PatientAttorney
    ///         twice).</item>
    /// </list>
    /// Internal so unit tests can verify without standing up ABP. Tests
    /// call with <paramref name="localizer"/>=null + assert against the
    /// English fallback strings below; production caller passes the
    /// AppService's injected <c>_localizer</c> so the SPA banner shows the
    /// localized text rather than the generic "An internal error occurred"
    /// ABP fallback (BUG-012, mirrors the BUG-014 / BUG-025 fix pattern).
    /// </summary>
    internal static void ValidateRegistrationInput(
        ExternalUserSignUpDto input,
        IStringLocalizer<CaseEvaluationResource>? localizer = null)
    {
        Check.NotNull(input, nameof(input));
        Check.NotNullOrWhiteSpace(input.Email, nameof(input.Email));
        Check.NotNullOrWhiteSpace(input.Password, nameof(input.Password));
        Check.NotNullOrWhiteSpace(input.ConfirmPassword, nameof(input.ConfirmPassword));

        if (!string.Equals(input.Password, input.ConfirmPassword, StringComparison.Ordinal))
        {
            // BUG-012 (2026-05-22): UserFriendlyException so the localized
            // message reaches the SPA banner. BusinessException with code
            // alone gets its Message replaced by ABP's generic fallback
            // (documented in this codebase at
            // AppointmentReadAccessGuard.cs:161-167 and at the
            // RegistrationDuplicateEmail call site in RegisterAsync). UFE
            // inherits from BusinessException, so the HTTP-status mapping
            // for the code still applies (400 BadRequest via
            // CaseEvaluationHttpApiHostModule + CaseEvaluationAuthServerModule).
            var mismatchMessage = localizer != null
                ? localizer["Registration:ConfirmPasswordMismatch"].Value
                : "Password and confirm password do not match.";
            throw new UserFriendlyException(
                message: mismatchMessage,
                code: CaseEvaluationDomainErrorCodes.RegistrationConfirmPasswordMismatch);
        }

        if (IsAttorneyRole(input.UserType) && string.IsNullOrWhiteSpace(input.FirmName))
        {
            // BUG-012 (2026-05-22): same UFE pattern as above. WithData
            // carries the UserType so a future per-role-tailored banner can
            // be added in the SPA without parsing the message string.
            var firmRequiredMessage = localizer != null
                ? localizer["Registration:FirmNameRequiredForAttorney"].Value
                : "Firm name is required for attorney roles.";
            throw new UserFriendlyException(
                    message: firmRequiredMessage,
                    code: CaseEvaluationDomainErrorCodes.RegistrationFirmNameRequired)
                .WithData("UserType", input.UserType.ToString());
        }
    }

    /// <summary>
    /// Returns the explicit <c>FirmEmail</c> when supplied (lowercased +
    /// trimmed), otherwise auto-derives from <c>Email</c> verbatim with
    /// OLD <c>UserDomain.cs:106</c>:
    ///   <c>user.FirmEmail = user.EmailId.ToLower().ToString().Trim()</c>.
    /// Caller guarantees <c>Email</c> is non-empty (validated upstream).
    /// Internal so unit tests can verify without ABP infra.
    /// </summary>
    internal static string DeriveFirmEmail(ExternalUserSignUpDto input)
    {
        var explicitEmail = input.FirmEmail?.Trim();
        if (!string.IsNullOrEmpty(explicitEmail))
        {
            return explicitEmail.ToLowerInvariant();
        }
        return input.Email.Trim().ToLowerInvariant();
    }

    private async Task EnsureRoleAsync(string roleName)
    {
        var existingRole = await _roleManager.FindByNameAsync(roleName);
        if (existingRole != null)
        {
            return;
        }

        var newRole = new IdentityRole(GuidGenerator.Create(), roleName, CurrentTenant.Id);
        var createRoleResult = await _roleManager.CreateAsync(newRole);
        if (!createRoleResult.Succeeded)
        {
            throw new UserFriendlyException(string.Join(", ", createRoleResult.Errors.Select(x => x.Description)));
        }
    }
}
