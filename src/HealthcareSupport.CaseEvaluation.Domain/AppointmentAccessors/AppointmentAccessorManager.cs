using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public class AppointmentAccessorManager : DomainService
{
    protected IAppointmentAccessorRepository _appointmentAccessorRepository;
    // Phase 11i (2026-05-04) -- additional collaborators for the
    // CreateOrLinkAsync flow. Older constructor consumers continue to
    // resolve via DI; the test base + ABP module register every type
    // automatically.
    protected IdentityUserManager? _userManager;
    protected IIdentityRoleRepository? _roleRepository;
    protected ILocalEventBus? _localEventBus;

    public AppointmentAccessorManager(IAppointmentAccessorRepository appointmentAccessorRepository)
    {
        _appointmentAccessorRepository = appointmentAccessorRepository;
    }

    public AppointmentAccessorManager(
        IAppointmentAccessorRepository appointmentAccessorRepository,
        IdentityUserManager userManager,
        IIdentityRoleRepository roleRepository,
        ILocalEventBus localEventBus)
        : this(appointmentAccessorRepository)
    {
        _userManager = userManager;
        _roleRepository = roleRepository;
        _localEventBus = localEventBus;
    }

    public virtual async Task<AppointmentAccessor> CreateAsync(Guid identityUserId, Guid appointmentId, AccessType accessTypeId)
    {
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNull(accessTypeId, nameof(accessTypeId));
        var appointmentAccessor = new AppointmentAccessor(GuidGenerator.Create(), identityUserId, appointmentId, accessTypeId);
        return await _appointmentAccessorRepository.InsertAsync(appointmentAccessor);
    }

    public virtual async Task<AppointmentAccessor> UpdateAsync(Guid id, Guid identityUserId, Guid appointmentId, AccessType accessTypeId)
    {
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNull(appointmentId, nameof(appointmentId));
        Check.NotNull(accessTypeId, nameof(accessTypeId));
        var appointmentAccessor = await _appointmentAccessorRepository.GetAsync(id);
        appointmentAccessor.IdentityUserId = identityUserId;
        appointmentAccessor.AppointmentId = appointmentId;
        appointmentAccessor.AccessTypeId = accessTypeId;
        return await _appointmentAccessorRepository.UpdateAsync(appointmentAccessor);
    }

    /// <summary>
    /// Phase 11i (2026-05-04) -- OLD-parity Add-accessor flow from
    /// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>
    /// lines 186-196 (validation) + 222 / 290+ (Add path).
    ///
    /// Looks up an IdentityUser by email; if missing, creates one with
    /// a random temp password, grants the requested role, links the
    /// accessor row, and publishes
    /// <see cref="AppointmentAccessorInvitedEto"/> so the notification
    /// pipeline can send an invitation email. If the user already
    /// exists with the requested role, only the link row is created.
    /// If the user exists with a conflicting recognised external role,
    /// throws <see cref="BusinessException"/> with code
    /// <c>AppointmentAccessorRoleMismatch</c> (verbatim OLD message).
    /// </summary>
    public virtual async Task<AppointmentAccessor> CreateOrLinkAsync(
        Guid appointmentId,
        string email,
        string requestedRoleName,
        AccessType accessTypeId,
        Guid? tenantId,
        string? firstName = null,
        string? lastName = null)
    {
        Check.NotDefaultOrNull<Guid>(appointmentId, nameof(appointmentId));
        Check.NotNullOrWhiteSpace(email, nameof(email));
        Check.NotNullOrWhiteSpace(requestedRoleName, nameof(requestedRoleName));
        if (_userManager == null || _roleRepository == null || _localEventBus == null)
        {
            throw new InvalidOperationException(
                "AppointmentAccessorManager.CreateOrLinkAsync requires the full DI ctor; resolve via the container or pass the additional collaborators.");
        }

        var normalizedEmail = email.Trim();
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);

        IList<string>? existingRoles = existingUser == null
            ? null
            : await _userManager.GetRolesAsync(existingUser);

        var outcome = AppointmentAccessorRules.ResolveOutcome(
            userExists: existingUser != null,
            userRoles: existingRoles,
            requestedRole: requestedRoleName);

        Guid identityUserId;
        switch (outcome)
        {
            case AccessorLinkOutcome.RoleMismatch:
                throw new BusinessException(CaseEvaluationDomainErrorCodes.AppointmentAccessorRoleMismatch)
                    .WithData("email", normalizedEmail)
                    .WithData("requestedRole", requestedRoleName)
                    .WithData("existingRoles", string.Join(",", existingRoles ?? new List<string>()));

            case AccessorLinkOutcome.LinkExisting:
                identityUserId = existingUser!.Id;
                break;

            case AccessorLinkOutcome.GrantRoleAndLink:
                identityUserId = existingUser!.Id;
                await EnsureRoleExistsAsync(requestedRoleName, tenantId);
                await _userManager.AddToRoleAsync(existingUser, requestedRoleName);
                break;

            case AccessorLinkOutcome.CreateUserAndLink:
                await EnsureRoleExistsAsync(requestedRoleName, tenantId);
                var newUser = new IdentityUser(
                    GuidGenerator.Create(),
                    userName: normalizedEmail,
                    email: normalizedEmail,
                    tenantId: tenantId)
                {
                    Name = firstName,
                    Surname = lastName,
                };
                var tempPassword = GenerateTempPassword();
                var createResult = await _userManager.CreateAsync(newUser, tempPassword);
                if (!createResult.Succeeded)
                {
                    throw new UserFriendlyException(string.Join(", ", createResult.Errors.Select(e => e.Description)));
                }
                await _userManager.AddToRoleAsync(newUser, requestedRoleName);
                identityUserId = newUser.Id;

                await _localEventBus.PublishAsync(new AppointmentAccessorInvitedEto
                {
                    AppointmentId = appointmentId,
                    InvitedUserId = newUser.Id,
                    TenantId = tenantId,
                    Email = normalizedEmail,
                    RoleName = requestedRoleName,
                    AccessTypeId = (int)accessTypeId,
                    OccurredAt = DateTime.UtcNow,
                });
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown AccessorLinkOutcome.");
        }

        return await CreateAsync(identityUserId, appointmentId, accessTypeId);
    }

    private async Task EnsureRoleExistsAsync(string roleName, Guid? tenantId)
    {
        if (_roleRepository == null)
        {
            return;
        }
        // Per-tenant role row -- mirrors the PatientsAppService pattern.
        var existing = await _roleRepository.FindByNormalizedNameAsync(roleName.Trim().ToUpperInvariant());
        if (existing != null)
        {
            return;
        }
        var newRole = new IdentityRole(GuidGenerator.Create(), roleName.Trim(), tenantId);
        await _roleRepository.InsertAsync(newRole);
    }

    /// <summary>
    /// Phase 11i (2026-05-04) -- temp password placeholder. The
    /// invitation email carries a password-reset link so the value
    /// itself is throwaway; we still produce something that satisfies
    /// the default ABP password policy. Keeping the helper internal
    /// so it can be substituted in tests if needed.
    /// </summary>
    private static string GenerateTempPassword()
    {
        // Deterministic shape: at least 8 chars, with upper, lower,
        // digit, and symbol. Random suffix so concurrent invitations
        // do not collide. Cite ABP Identity password options:
        // https://abp.io/docs/latest/modules/identity#identity-options
        return "Acc!" + Guid.NewGuid().ToString("N").Substring(0, 12);
    }
}