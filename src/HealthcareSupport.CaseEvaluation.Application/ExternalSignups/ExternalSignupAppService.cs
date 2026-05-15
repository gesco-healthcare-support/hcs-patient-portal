using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Invitations;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Settings;
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
    // D.2 (2026-04-30): wired for the admin invite endpoint.
    private readonly ISettingProvider _settingProvider;
    private readonly IBackgroundJobManager _backgroundJobManager;
    // 2026-05-15: tokenized invite flow. Manager owns token gen + hash +
    // accept; dispatcher routes the email through the per-tenant
    // InviteExternalUser NotificationTemplate (same path as
    // ResetPassword / PasswordChange).
    private readonly InvitationManager _invitationManager;
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

    public ExternalSignupAppService(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        IRepository<Tenant, Guid> tenantRepository,
        PatientManager patientManager,
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
        ISettingProvider settingProvider,
        IBackgroundJobManager backgroundJobManager,
        IHostEnvironment hostEnvironment,
        IDataFilter dataFilter,
        InvitationManager invitationManager,
        INotificationDispatcher notificationDispatcher)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tenantRepository = tenantRepository;
        _patientManager = patientManager;
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
        _settingProvider = settingProvider;
        _backgroundJobManager = backgroundJobManager;
        _hostEnvironment = hostEnvironment;
        _dataFilter = dataFilter;
        _invitationManager = invitationManager;
        _notificationDispatcher = notificationDispatcher;
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
        // Adrian (2026-04-30): only Patient and Applicant Attorney are exposed via this lookup.
        // Defense Attorney and Claim Examiner are intentionally excluded -- per D-2 in the
        // Wave-2 demo-lifecycle report, DA/CE register and login normally but their saved
        // profiles do not surface in any picker, dropdown, or autocomplete to other tenant users.
        var allowedRoleNames = new[]
        {
            "Patient",
            "Applicant Attorney",
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
            string.Equals(r, "Defense Attorney", StringComparison.OrdinalIgnoreCase)) ?? string.Empty;

        // Phase 9 (2026-05-03) -- surface IsExternalUser + IsAccessor
        // extension props registered in Phase 2.4 so the Angular SPA can
        // post-login route external-> /home / internal-> /dashboard
        // without a second roundtrip.
        var isExternalUser = ReadBoolExtensionProperty(
            user, CaseEvaluationModuleExtensionConfigurator.IsExternalUserPropertyName);
        var isAccessor = ReadBoolExtensionProperty(
            user, CaseEvaluationModuleExtensionConfigurator.IsAccessorPropertyName);

        return new ExternalUserProfileDto
        {
            IdentityUserId = user.Id,
            FirstName = user.Name ?? string.Empty,
            LastName = user.Surname ?? string.Empty,
            Email = user.Email ?? string.Empty,
            UserRole = userRole,
            IsExternalUser = isExternalUser,
            IsAccessor = isAccessor,
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
        }

        // Phase 8 (2026-05-03) -- OLD-parity validation:
        //   - ConfirmPassword must equal Password (UserDomain.cs:88)
        //   - FirmName required for ApplicantAttorney AND DefenseAttorney
        //     (OLD-bug-fix on UserDomain.cs:272 which checked PatientAttorney
        //     twice; intent was both attorney roles)
        ValidateRegistrationInput(input);

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
                Name = input.FirstName,
                Surname = input.LastName,
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
                // FirstName/LastName are not collected on the minimal register
                // form (Adrian, 2026-04-30). Normalize null to "" so the Patient
                // row is created with empty name fields; the booker fills these
                // in later via the booking form's patient section.
                await _patientManager.CreateAsync(
                    stateId: null,
                    appointmentLanguageId: null,
                    identityUserId: user.Id,
                    tenantId: CurrentTenant.Id,
                    firstName: input.FirstName ?? string.Empty,
                    lastName: input.LastName ?? string.Empty,
                    email: input.Email,
                    genderId: Gender.Male,
                    dateOfBirth: DateTime.MinValue,
                    phoneNumberTypeId: PhoneNumberType.Home
                );
            }
            else if (input.UserType == ExternalUserType.ApplicantAttorney)
            {
                // Adrian D-2 (2026-04-30): Applicant Attorney is the only non-Patient external
                // role that gets a saved profile. Creating an empty AA row here makes the
                // booker-side pre-fill ("Search by email" + lookup picker) discover this AA
                // on next booking, the tenant-admin AA management page surfaces them, and the
                // appointment-AA join can point at a real row.
                // Defense Attorney + Claim Examiner are intentionally NOT created -- per D-2,
                // their saved profiles are not exposed in any lookup or pre-fill surface.
                var existingApplicantAttorney = await _applicantAttorneyRepository
                    .FirstOrDefaultAsync(a => a.IdentityUserId == user.Id);
                if (existingApplicantAttorney == null)
                {
                    await _applicantAttorneyManager.CreateAsync(
                        stateId: null,
                        identityUserId: user.Id);
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
            }

            // 2026-05-06 (Adrian directive): the verification email is NOT sent
            // when the user submits the register form. Instead the SPA shows a
            // post-register page with a "Send verification email" button; the
            // email fires only when the user clicks that button (or when a
            // blocked-login error nudges them to resend). The trigger flows
            // through ABP's stock account flow into our IAccountEmailer override
            // (CaseEvaluationAccountEmailer.SendEmailConfirmationLinkAsync),
            // which dispatches the UserRegistered template through the same
            // template renderer + Hangfire queue used by every other email.
            // This lets registration finish silently if the user closes the
            // tab and removes the wasted SMTP send for users who never click
            // through.
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
    /// - Patient: not handled here -- the registration block already creates a
    ///   Patient row with the new user's IdentityUserId, but the appointment's
    ///   PatientId points at a different Patient row created via the booker's
    ///   get-or-create-by-email flow. Reconciling those two Patient rows is a
    ///   merge concern outside the scope of this hook; for fan-out (step 6.1)
    ///   the appointment-level PatientEmail column is sufficient.
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
        // Patient and ClaimExaminer: see method docstring for rationale.
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
    /// to IT Admin, Staff Supervisor, and Clinic Staff via the
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

        var authServerBaseUrl = await _settingProvider.GetOrNullAsync(
            CaseEvaluationSettings.NotificationsPolicy.AuthServerBaseUrl);
        if (string.IsNullOrWhiteSpace(authServerBaseUrl))
        {
            throw new UserFriendlyException(
                L["AuthServer base URL is not configured. Set CaseEvaluation.Notifications.AuthServerBaseUrl."]);
        }

        // CurrentUser is guaranteed set (perm-gated AppService).
        var invitedByUserId = CurrentUser.Id ?? Guid.Empty;
        var normalizedEmail = input.Email.Trim().ToLowerInvariant();

        var (invitation, rawToken) = await _invitationManager.IssueAsync(
            tenantId: tenantId.Value,
            email: normalizedEmail,
            userType: input.UserType,
            invitedByUserId: invitedByUserId);

        var inviteUrl = BuildInviteUrl(
            authServerBaseUrl: authServerBaseUrl.TrimEnd('/'),
            rawToken: rawToken);

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
                variables: BuildInvitationVariables(tenantName, roleName, inviteUrl, invitation.ExpiresAt),
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
        };
    }

    /// <summary>
    /// Variable bag for the <c>InviteExternalUser</c> NotificationTemplate.
    /// Tokens are referenced in
    /// <c>src/HealthcareSupport.CaseEvaluation.Domain/NotificationTemplates/EmailBodies/InviteExternalUser.html</c>
    /// as <c>##TenantName##</c>, <c>##RoleName##</c>, <c>##URL##</c>,
    /// <c>##ExpiresAt##</c>. <c>##PatientFullName##</c> is left blank --
    /// we do not collect a name at invite time.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> BuildInvitationVariables(
        string tenantName, string roleName, string inviteUrl, DateTime expiresAtUtc)
    {
        // Format expiry as a short human-readable UTC date so all tenants
        // see the same calendar day regardless of viewer locale; the
        // recipient does not need timezone precision to know "this link
        // works through Tuesday".
        var expiresAtLabel = expiresAtUtc.ToString("MMMM d, yyyy");
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["TenantName"] = tenantName,
            ["RoleName"] = roleName,
            ["URL"] = inviteUrl,
            ["ExpiresAt"] = expiresAtLabel,
            ["PatientFullName"] = string.Empty,
            // Defensive zero-fills for tokens the per-tenant edit UI may
            // reference even when the dispatcher doesn't supply them.
            ["PatientFirstName"] = string.Empty,
            ["PatientLastName"] = string.Empty,
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

    /// <summary>
    /// Builds the AuthServer register URL that carries the one-time
    /// invite token. Format:
    /// <c>{authServerBaseUrl}/Account/Register?inviteToken={url-encoded-raw-token}</c>.
    /// The base URL already includes the tenant subdomain (e.g.
    /// <c>http://falkinstein.localhost:44368</c>) per the
    /// <c>Notifications.AuthServerBaseUrl</c> setting; the JS overlay
    /// reads the token, validates it via
    /// <c>/api/public/external-signup/validate-invite</c>, and prefills
    /// the register form from the validation response (no email or role
    /// query params required).
    /// </summary>
    private static string BuildInviteUrl(string authServerBaseUrl, string rawToken)
    {
        return $"{authServerBaseUrl}/Account/Register?inviteToken={WebUtility.UrlEncode(rawToken)}";
    }

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
            ExternalUserType.Adjuster => "Adjuster",
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
    /// Internal so unit tests can verify without standing up ABP.
    /// </summary>
    internal static void ValidateRegistrationInput(ExternalUserSignUpDto input)
    {
        Check.NotNull(input, nameof(input));
        Check.NotNullOrWhiteSpace(input.Email, nameof(input.Email));
        Check.NotNullOrWhiteSpace(input.Password, nameof(input.Password));
        Check.NotNullOrWhiteSpace(input.ConfirmPassword, nameof(input.ConfirmPassword));

        if (!string.Equals(input.Password, input.ConfirmPassword, StringComparison.Ordinal))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.RegistrationConfirmPasswordMismatch);
        }

        if (IsAttorneyRole(input.UserType) && string.IsNullOrWhiteSpace(input.FirmName))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.RegistrationFirmNameRequired)
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
