using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.ExternalSignups;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Shared;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

public class ExternalSignupAppService : CaseEvaluationAppService, IExternalSignupAppService
{
    private readonly IdentityUserManager _userManager;
    private readonly IdentityRoleManager _roleManager;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly PatientManager _patientManager;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<IdentityRole, Guid> _identityRoleRepository;

    public ExternalSignupAppService(
        IdentityUserManager userManager,
        IdentityRoleManager roleManager,
        IRepository<Tenant, Guid> tenantRepository,
        PatientManager patientManager,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<IdentityRole, Guid> identityRoleRepository)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _tenantRepository = tenantRepository;
        _patientManager = patientManager;
        _identityUserRepository = identityUserRepository;
        _identityRoleRepository = identityRoleRepository;
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

    public virtual async Task<ListResultDto<ExternalUserLookupDto>> GetExternalUserLookupAsync(string? filter = null)
    {
        var allowedRoleNames = new[]
        {
            "Patient",
            "Applicant Attorney",
            "Defense Attorney",
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
            var userRole = u.FirstRoleId != Guid.Empty && roleNameMap.TryGetValue(u.FirstRoleId, out var name)
                ? name
                : allowedRoleNames[0];

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var f = filter.ToLowerInvariant();
                var matches = (u.Name != null && u.Name.Contains(f, StringComparison.OrdinalIgnoreCase)) ||
                             (u.Surname != null && u.Surname.Contains(f, StringComparison.OrdinalIgnoreCase)) ||
                             (u.Email != null && u.Email.Contains(f, StringComparison.OrdinalIgnoreCase));
                if (!matches)
                {
                    continue;
                }
            }

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

        return new ExternalUserProfileDto
        {
            IdentityUserId = user.Id,
            FirstName = user.Name ?? string.Empty,
            LastName = user.Surname ?? string.Empty,
            Email = user.Email ?? string.Empty,
            UserRole = userRole,
        };
    }

    [AllowAnonymous]
    public virtual async Task RegisterAsync(ExternalUserSignUpDto input)
    {
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
                await _patientManager.CreateAsync(
                    stateId: null,
                    appointmentLanguageId: null,
                    identityUserId: user.Id,
                    tenantId: CurrentTenant.Id,
                    firstName: input.FirstName,
                    lastName: input.LastName,
                    email: input.Email,
                    genderId: Gender.Male,
                    dateOfBirth: DateTime.UtcNow.Date,
                    phoneNumberTypeId: PhoneNumberType.Home
                );
            }
        }
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

    private static string ToRoleName(ExternalUserType userType)
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
