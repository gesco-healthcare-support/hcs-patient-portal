using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 -- only EXTERNAL people can have their emails stopped. Refuses (400) an address that belongs
/// to the portal itself: the office notification inbox, the technical alert list, any host user (every host
/// account is staff), or an office account holding a staff role (a host operator's shadow login, the office
/// admin). Also validates the address.
/// </summary>
public class EmailStopAddressGuard : ITransientDependency
{
    private readonly ISettingProvider _settingProvider;
    private readonly IConfiguration _configuration;
    private readonly IIdentityUserRepository _userRepository;
    private readonly ICurrentTenant _currentTenant;

    public EmailStopAddressGuard(
        ISettingProvider settingProvider,
        IConfiguration configuration,
        IIdentityUserRepository userRepository,
        ICurrentTenant currentTenant)
    {
        _settingProvider = settingProvider;
        _configuration = configuration;
        _userRepository = userRepository;
        _currentTenant = currentTenant;
    }

    /// <summary>The trimmed address when it is a valid email, else a 400.</summary>
    public static string RequireValidEmail(string? email)
    {
        var trimmed = email?.Trim();
        if (string.IsNullOrEmpty(trimmed)
            || trimmed.Length > EmailStopConsts.EmailMaxLength
            || !MailAddress.TryCreate(trimmed, out var parsed)
            || !string.Equals(parsed.Address, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.EmailStopInvalidEmail);
        }
        return trimmed;
    }

    /// <summary>Throws a 400 when the address belongs to the portal itself.</summary>
    public virtual async Task EnsureExternalAsync(string email)
    {
        if (await IsInternalAsync(email))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.EmailStopInternalAddress);
        }
    }

    /// <summary>True when the address is the office inbox, a technical alert address, or staff.</summary>
    public virtual async Task<bool> IsInternalAsync(string? email)
    {
        var normalized = EmailStopAddress.Normalize(email);
        if (normalized == null)
        {
            return false;
        }

        var inbox = await _settingProvider.GetOrNullAsync(CaseEvaluationSettings.NotificationsPolicy.OfficeEmail);
        if (EmailStopAddress.Normalize(inbox) == normalized)
        {
            return true;
        }
        if (TechnicalAlertAddresses().Contains(normalized))
        {
            return true;
        }

        using (_currentTenant.Change(null))
        {
            if (await _userRepository.FindByNormalizedEmailAsync(normalized, includeDetails: false) != null)
            {
                return true;
            }
        }

        if (_currentTenant.Id == null)
        {
            return false;
        }
        var officeUser = await _userRepository.FindByNormalizedEmailAsync(normalized, includeDetails: false);
        return officeUser != null
            && AppointmentSubmitter.HoldsStaffRole(await _userRepository.GetRoleNamesAsync(officeUser.Id));
    }

    private HashSet<string> TechnicalAlertAddresses()
    {
        var raw = _configuration[CaseTrackerFeedConsts.AlertRecipientsConfigurationKey];
        return (raw ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(EmailStopAddress.Normalize)
            .Where(a => a != null)
            .Select(a => a!)
            .ToHashSet(StringComparer.Ordinal);
    }
}
