using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using IdentityRole = Volo.Abp.Identity.IdentityRole;

namespace HealthcareSupport.CaseEvaluation.ExternalLogins;

/// <summary>
/// 2026-11-13 -- a practice's staff supervisor (or admin) lists the outside users' logins and switches one off
/// or back on. The trigger was a law office that made a login on a misspelled email domain, which only the
/// office admin could switch off.
///
/// <para>NARROW ON PURPOSE. The holder is NOT given <c>AbpIdentity.Users.Update</c>: that would also let them
/// set any patient's or attorney's password (an account takeover). This service can do exactly two things to
/// an outside user's account: list it and flip <c>IsActive</c>. Who counts as an outside user is decided in
/// <see cref="ExternalLoginTargets"/>, and enforced on the list (SQL) and on the change.</para>
///
/// <para>WHAT "OFF" DOES (read from the ABP 10.0.2 token endpoint, <c>TokenController.HandleRefreshTokenAsync</c>
/// then <c>PreSignInCheckAsync</c>): every refresh-token grant re-reads the user and refuses with
/// <c>invalid_grant</c> when <c>IsActive</c> is false, and so does the password sign-in
/// (<c>CanSignInAsync</c>). So switching off blocks every NEW token. An access token already issued stays
/// valid until it expires (a few minutes) unless the session is revoked, so this also rotates the security
/// stamp and revokes the user's identity sessions. Switching ON only flips the flag: it never touches
/// <c>EmailConfirmed</c>, so an unconfirmed login stays unconfirmed.</para>
///
/// <para>Switching a login off does NOT stop the emails a case sends to that address; that is the "Stopped
/// emails" screen (email stops).</para>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.UserManagement.SwitchOffExternalUser)]
public class ExternalLoginsAppService : CaseEvaluationAppService, IExternalLoginsAppService
{
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IRepository<IdentityRole, Guid> _roleRepository;
    private readonly IdentityUserManager _userManager;
    private readonly IdentitySessionManager _sessionManager;

    public ExternalLoginsAppService(
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<IdentityRole, Guid> roleRepository,
        IdentityUserManager userManager,
        IdentitySessionManager sessionManager)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userManager = userManager;
        _sessionManager = sessionManager;
    }

    public virtual async Task<PagedResultDto<ExternalLoginDto>> GetListAsync(GetExternalLoginsInput input)
    {
        Check.NotNull(input, nameof(input));
        EnsureOffice();

        var roles = await _roleRepository.GetListAsync();
        var outsideRoleIds = roles
            .Where(r => ExternalLoginTargets.IsOutsideRole(r.Name))
            .Select(r => r.Id)
            .ToList();
        var roleNameById = roles.ToDictionary(r => r.Id, r => r.Name);

        var users = await _userRepository.WithDetailsAsync(u => u.Roles);
        // At least one role, and every role is an outside role.
        var query = users.Where(u => u.Roles.Any() && !u.Roles.Any(r => !outsideRoleIds.Contains(r.RoleId)));
        if (input.IsActive.HasValue)
        {
            var active = input.IsActive.Value;
            query = query.Where(u => u.IsActive == active);
        }
        var text = input.FilterText?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            query = query.Where(u =>
                (u.Name != null && u.Name.Contains(text))
                || (u.Surname != null && u.Surname.Contains(text))
                || (u.Email != null && u.Email.Contains(text))
                || u.UserName.Contains(text));
        }

        var total = await AsyncExecuter.CountAsync(query);
        var page = await AsyncExecuter.ToListAsync(
            query.OrderBy(u => u.Surname).ThenBy(u => u.Name).ThenBy(u => u.Email).ThenBy(u => u.Id)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        // A shadow login of a host operator never appears here (its roles are staff roles, so the query already
        // leaves it out); the stamp check is the same belt as on the change.
        var items = page
            .Where(u => !ExternalLoginTargets.IsHostOperatorShadow(u))
            .Select(u => ToDto(u, u.Roles
                .Select(r => roleNameById.TryGetValue(r.RoleId, out var name) ? name : null)
                .OfType<string>()))
            .ToList();
        return new PagedResultDto<ExternalLoginDto>(total, items);
    }

    public virtual async Task<ExternalLoginDto> SetActiveAsync(Guid id, ExternalLoginSetActiveDto input)
    {
        Check.NotNull(input, nameof(input));
        EnsureOffice();

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            throw new EntityNotFoundException(typeof(IdentityUser), id);
        }

        if (CurrentUser.Id == id)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ExternalLoginCannotChangeSelf);
        }

        var heldRoles = await _userManager.GetRolesAsync(user);
        if (!ExternalLoginTargets.IsOutsideUser(heldRoles.ToList()) || ExternalLoginTargets.IsHostOperatorShadow(user))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ExternalLoginNotAnOutsideUser);
        }

        if (user.IsActive != input.IsActive)
        {
            user.SetIsActive(input.IsActive);
            (await _userManager.UpdateAsync(user)).CheckErrors();

            if (!input.IsActive)
            {
                // Rotate the stamp and drop the sessions so the person is signed out now, not when the access
                // token they hold runs out. New tokens are already refused by the IsActive check.
                (await _userManager.UpdateSecurityStampAsync(user)).CheckErrors();
                await _sessionManager.RevokeAllAsync(user.Id);
            }

            // Ids and the new state only: never an email or a name in a log line.
            Logger.LogInformation(
                "ExternalLogins: user {TargetUserId} set active={IsActive} by {CallerUserId}.",
                user.Id, input.IsActive, CurrentUser.Id);
        }

        return ToDto(user, heldRoles);
    }

    private void EnsureOffice()
    {
        if (CurrentTenant.Id == null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ExternalLoginOfficeOnly);
        }
    }

    private static ExternalLoginDto ToDto(IdentityUser user, IEnumerable<string> roleNames) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Surname = user.Surname,
        Email = user.Email,
        Roles = roleNames.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList(),
        FirmName = user.GetProperty<string>(CaseEvaluationModuleExtensionConfigurator.FirmNamePropertyName),
        IsActive = user.IsActive,
        EmailConfirmed = user.EmailConfirmed,
        CreationTime = user.CreationTime,
    };
}
