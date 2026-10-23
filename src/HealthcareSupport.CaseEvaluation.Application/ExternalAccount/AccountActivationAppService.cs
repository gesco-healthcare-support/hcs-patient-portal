using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Identity;
using Volo.Saas.Host;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.ExternalAccount;

/// <summary>
/// Staff "Resend activation email" buttons. See <see cref="IAccountActivationAppService"/>.
///
/// <para>Each send gets its own context tag (<see cref="NotificationOccurrence.PerRequest"/>), so a
/// second press really sends. That also means nothing else would stop a double-click, or a script
/// holding a staff session, from filling one inbox, so two sends to the same account or office are
/// at least <see cref="Cooldown"/> apart. The anonymous paths (forgot password, resend verification)
/// carry their own per-address limits in <see cref="ExternalAccountAppService"/>.</para>
/// </summary>
[Authorize]
[RemoteService(IsEnabled = false)]
public class AccountActivationAppService : CaseEvaluationAppService, IAccountActivationAppService
{
    internal static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(60);

    private readonly IdentityUserManager _userManager;
    private readonly IAccountActivationSender _activationSender;
    private readonly ITenantRepository _tenantRepository;
    private readonly IDistributedCache _cache;

    public AccountActivationAppService(
        IdentityUserManager userManager,
        IAccountActivationSender activationSender,
        ITenantRepository tenantRepository,
        IDistributedCache cache)
    {
        _userManager = userManager;
        _activationSender = activationSender;
        _tenantRepository = tenantRepository;
        _cache = cache;
    }

    [Authorize(IdentityPermissions.Users.Update)]
    public virtual async Task ResendToUserAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new EntityNotFoundException(typeof(IdentityUser), userId);

        if (!PrivilegedAccounts.CallerIsPrivileged(CurrentUser)
            && PrivilegedAccounts.HoldsPrivilegedRole(await _userManager.GetRolesAsync(user)))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.PrivilegedUserChangeNotAllowed);
        }

        if (user.EmailConfirmed)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ActivationAlreadyActive);
        }

        if (!user.IsActive)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ActivationAccountInactive);
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ActivationNoAddress);
        }

        await EnsureNotTooSoonAsync($"user:{user.Id:N}");
        await _activationSender.SendAsync(user, NotificationOccurrence.PerRequest($"AccountActivation/StaffResend/{user.Id}"));
        await StampAsync($"user:{user.Id:N}");
    }

    [Authorize(SaasHostPermissions.Tenants.Create)]
    public virtual async Task ResendToOfficeAdministratorAsync(Guid officeId)
    {
        if (CurrentTenant.IsAvailable)
        {
            throw new AbpAuthorizationException("Practices are managed from the host, not from inside an office.");
        }

        _ = await _tenantRepository.FindAsync(officeId)
            ?? throw new EntityNotFoundException(typeof(Tenant), officeId);

        await EnsureNotTooSoonAsync($"office:{officeId:N}");
        var outcome = await _activationSender.TrySendToOfficeAdminAsync(officeId, greetingName: null);
        switch (outcome)
        {
            case OfficeAdminActivationOutcome.Sent:
                await StampAsync($"office:{officeId:N}");
                return;
            case OfficeAdminActivationOutcome.AlreadyActive:
                throw new BusinessException(CaseEvaluationDomainErrorCodes.ActivationAlreadyActive);
            case OfficeAdminActivationOutcome.Inactive:
                throw new BusinessException(CaseEvaluationDomainErrorCodes.ActivationAccountInactive);
            case OfficeAdminActivationOutcome.NoAdministrator:
                throw new BusinessException(CaseEvaluationDomainErrorCodes.ActivationNoOfficeAdministrator);
            default:
                throw new BusinessException(CaseEvaluationDomainErrorCodes.ActivationNotQueued);
        }
    }

    private static string CooldownKey(string subject) => $"account-activation-resend:{subject}";

    private async Task EnsureNotTooSoonAsync(string subject)
    {
        if (await _cache.GetStringAsync(CooldownKey(subject)) != null)
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ActivationResendTooSoon)
                .WithData("retryAfterSeconds", (int)Cooldown.TotalSeconds);
        }
    }

    private Task StampAsync(string subject) =>
        _cache.SetStringAsync(
            CooldownKey(subject),
            "1",
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Cooldown });
}
