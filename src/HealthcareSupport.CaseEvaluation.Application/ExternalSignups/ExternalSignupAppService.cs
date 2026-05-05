using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;
using HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Patients;
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
using Volo.Abp.EventBus.Local;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Settings;
using Volo.Saas.Tenants;

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
    // R1 follow-up (2026-05-05): publish UserRegisteredEto so the
    // notification handler can send OLD's "verify your email" message.
    private readonly ILocalEventBus _localEventBus;

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
        ILocalEventBus localEventBus)
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
        _localEventBus = localEventBus;
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
    /// Reads a bool ABP extension property from an IdentityUser. ABP stores
    /// extras as JSON objects so the property may surface as
    /// <c>System.Boolean</c>, <c>System.Text.Json.JsonElement</c>, or a
    /// stringified <c>"True" / "False"</c> depending on whether the value
    /// was just written or was round-tripped from the JSON column. This
    /// helper normalizes all three to a plain bool, defaulting to
    /// <c>false</c> when the property is missing.
    /// Internal so unit tests can verify without an IdentityUser.
    /// </summary>
    internal static bool ReadBoolExtensionProperty(
        Volo.Abp.Identity.IdentityUser user,
        string propertyName)
    {
        if (user == null || string.IsNullOrEmpty(propertyName))
        {
            return false;
        }
        var raw = user.GetProperty<object?>(propertyName);
        return CoerceBool(raw);
    }

    /// <summary>
    /// Coerces ABP's extra-property JSON-shaped value into a bool. Returns
    /// <c>false</c> for null, unrecognized strings, and unrecognized types.
    /// Internal for unit-test coverage.
    /// </summary>
    internal static bool CoerceBool(object? raw)
    {
        if (raw == null)
        {
            return false;
        }
        if (raw is bool b)
        {
            return b;
        }
        if (raw is string s)
        {
            return bool.TryParse(s, out var parsed) && parsed;
        }
        // JsonElement-shaped values: best-effort string parse; any other
        // numeric / object shape falls through to false.
        return bool.TryParse(raw.ToString(), out var parsedFallback) && parsedFallback;
    }

    [AllowAnonymous]
    public virtual async Task RegisterAsync(ExternalUserSignUpDto input)
    {
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
                throw new UserFriendlyException(L["Email address is already used: {0}", input.Email]);
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
                    dateOfBirth: DateTime.UtcNow.Date,
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

            // R1 follow-up (2026-05-05) -- publish UserRegisteredEto so the
            // UserRegisteredEmailHandler can dispatch OLD's "verify your email"
            // message. Mirrors OLD UserDomain.cs:332 SendMail call. The handler
            // generates the email-confirmation token + verify URL on the
            // consumer side so the Eto stays minimal (no token-leak risk in the
            // event payload). Published inside the `using (CurrentTenant.Change)`
            // block so the local event bus captures the right tenant context.
            await _localEventBus.PublishAsync(new UserRegisteredEto
            {
                UserId = user.Id,
                TenantId = CurrentTenant.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.Name ?? string.Empty,
                LastName = user.Surname ?? string.Empty,
                RoleName = roleName,
                OccurredAt = DateTime.UtcNow,
            });
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
        var applicantAttorney = await _applicantAttorneyRepository
            .FirstOrDefaultAsync(a => a.IdentityUserId == identityUserId);
        if (applicantAttorney == null)
        {
            // Should never happen because the AA registration branch creates
            // one above, but guard defensively rather than crashing the signup.
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
    /// D.2 (2026-04-30): admin-side invite for an external user. Restricted
    /// to internal-tier callers via role-based authorization (admin OR
    /// Staff Supervisor at tenant scope; IT Admin at host scope). Builds a
    /// tenant-specific `/Account/Register?__tenant=&lt;Name&gt;&amp;email=&lt;email&gt;&amp;
    /// role=&lt;roleName&gt;` URL using the `AuthServerBaseUrl` setting (S-6.1)
    /// and enqueues an invite email via the same Hangfire pipeline as 6.1's
    /// fan-out. Returns the URL in the response so the admin can copy and
    /// paste it manually -- the dev-stack swallows email silently when SMTP
    /// is unconfigured (S-5.7), and a Mailtrap-class sandbox will not
    /// deliver to real inboxes either, so showing the URL is non-negotiable
    /// for the demo flow.
    ///
    /// Internal roles (admin, Staff Supervisor, Clinic Staff, Doctor, IT
    /// Admin) are intentionally NOT invitable through this surface -- the
    /// `UserType` enum constrains the choice to the four external roles, and
    /// the JS overlay (1.6) further filters the role dropdown so a tampered
    /// URL with `?role=admin` cannot register as admin.
    /// </summary>
    [Authorize(Roles = "admin,Staff Supervisor,IT Admin")]
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

        var inviteUrl = BuildInviteUrl(
            authServerBaseUrl: authServerBaseUrl.TrimEnd('/'),
            tenantName: tenantName,
            email: input.Email.Trim(),
            roleName: roleName);

        var emailEnqueued = false;
        try
        {
            var subject = $"You have been invited to register at {tenantName}";
            var body = BuildInviteHtml(tenantName, roleName, inviteUrl);
            await _backgroundJobManager.EnqueueAsync(new SendAppointmentEmailArgs
            {
                To = input.Email.Trim(),
                Subject = subject,
                Body = body,
                IsBodyHtml = true,
                Context = $"Invite/{roleName}/{tenantId.Value}",
                Role = MapToRecipientRole(input.UserType),
                IsRegistered = false,
                TenantName = tenantName,
            });
            emailEnqueued = true;
        }
        catch (Exception)
        {
            // Email enqueue failure does not block the response; the admin
            // can still copy the URL manually. The Hangfire pipeline logs
            // the failure separately; we surface it via the bool flag.
            emailEnqueued = false;
        }

        return new InviteExternalUserResultDto
        {
            InviteUrl = inviteUrl,
            EmailEnqueued = emailEnqueued,
            Email = input.Email.Trim(),
            RoleName = roleName,
            TenantName = tenantName,
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

    private static string BuildInviteUrl(string authServerBaseUrl, string tenantName, string email, string roleName)
    {
        var query = new System.Text.StringBuilder("?");
        query.Append("__tenant=").Append(WebUtility.UrlEncode(tenantName)).Append('&');
        query.Append("email=").Append(WebUtility.UrlEncode(email)).Append('&');
        query.Append("role=").Append(WebUtility.UrlEncode(roleName));
        return $"{authServerBaseUrl}/Account/Register{query}";
    }

    private static string BuildInviteHtml(string tenantName, string roleName, string inviteUrl)
    {
        var encodedUrl = WebUtility.HtmlEncode(inviteUrl);
        var encodedTenant = WebUtility.HtmlEncode(tenantName);
        var encodedRole = WebUtility.HtmlEncode(roleName);
        return
            "<html><body style=\"font-family: Arial, sans-serif; color: #333;\">" +
            "<h2 style=\"color: #0d6efd;\">You have been invited to register</h2>" +
            $"<p><strong>{encodedTenant}</strong> has invited you to register a portal account as <strong>{encodedRole}</strong>.</p>" +
            "<p>Use the button below to open the registration page. The tenant and your email are pre-filled; pick a password and submit to finish.</p>" +
            "<p style=\"margin-top: 20px;\">" +
            $"<a href=\"{encodedUrl}\" style=\"background:#0d6efd;color:#fff;padding:10px 20px;text-decoration:none;border-radius:4px;\">" +
            $"Register at {encodedTenant}" +
            "</a></p>" +
            "<p style=\"color:#888;font-size:0.85em;\">If the button does not work, copy and paste this link into your browser:<br>" +
            $"<a href=\"{encodedUrl}\">{encodedUrl}</a></p>" +
            "<hr><p style=\"color: #888; font-size: 0.85em;\">If you were not expecting this invitation, you can safely ignore this email.</p>" +
            "</body></html>";
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
