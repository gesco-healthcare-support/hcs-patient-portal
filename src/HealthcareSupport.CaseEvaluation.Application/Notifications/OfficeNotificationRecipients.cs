using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// The office's own copies of an appointment email (2026-11-02): the office notification inbox
/// (<c>NotificationsPolicy.OfficeEmail</c>, read in the current office's scope) plus every switched-on
/// Intake Staff user of the office. Staff Supervisors are not recipients of per-event email, and the
/// per-office <c>SystemParameter.CcEmailIds</c> list is no longer appended anywhere, so nobody outside
/// the party list, the inbox and Intake Staff is mailed. Every method returns each address once
/// (case-insensitive), so a person who is both the inbox and an Intake user, or a party, gets one email.
/// </summary>
public class OfficeNotificationRecipients : ITransientDependency
{
    private const string IntakeStaffRoleName = "Intake Staff";

    private readonly ISettingProvider _settingProvider;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<OfficeNotificationRecipients> _logger;

    public OfficeNotificationRecipients(
        ISettingProvider settingProvider,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant,
        ILogger<OfficeNotificationRecipients> logger)
    {
        _settingProvider = settingProvider;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _logger = logger;
    }

    /// <summary>
    /// The office inbox as a single-item list; empty (with a warning) when the office has not set one.
    /// </summary>
    public virtual async Task<List<NotificationRecipient>> GetOfficeInboxAsync(string contextTag)
    {
        var inbox = (await _settingProvider.GetOrNullAsync(
            CaseEvaluationSettings.NotificationsPolicy.OfficeEmail))?.Trim();
        if (string.IsNullOrWhiteSpace(inbox))
        {
            _logger.LogWarning(
                "OfficeNotificationRecipients: office notification inbox is unset for tenant {TenantId}; "
                + "the office copy of {Context} will not be sent.",
                _currentTenant.Id, contextTag);
            return new List<NotificationRecipient>();
        }

        return new List<NotificationRecipient>
        {
            new(email: inbox, role: RecipientRole.OfficeAdmin, isRegistered: false),
        };
    }

    /// <summary>The switched-on Intake Staff users of the current office, one row per address.</summary>
    public virtual async Task<List<NotificationRecipient>> GetIntakeAsync()
    {
        var users = await _userManager.GetActiveUsersInRoleAsync(IntakeStaffRoleName);
        var result = new List<NotificationRecipient>();
        foreach (var user in users)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogDebug(
                    "OfficeNotificationRecipients: skipping Intake Staff user {UserId} -- empty email.",
                    user.Id);
                continue;
            }
            result.Add(new NotificationRecipient(
                email: user.Email, role: RecipientRole.OfficeAdmin, isRegistered: true));
        }
        return Distinct(result);
    }

    /// <summary>The office inbox followed by the Intake Staff, each address once.</summary>
    public virtual async Task<List<NotificationRecipient>> GetOfficeInboxAndIntakeAsync(string contextTag)
    {
        var all = await GetOfficeInboxAsync(contextTag);
        all.AddRange(await GetIntakeAsync());
        return Distinct(all);
    }

    /// <summary>
    /// Adds the Intake Staff to <paramref name="recipients"/> (mutated in place), skipping any address
    /// already present.
    /// </summary>
    public virtual async Task AppendIntakeAsync(List<NotificationRecipient> recipients)
    {
        var existing = new HashSet<string>(
            recipients.Select(r => r.Email), StringComparer.OrdinalIgnoreCase);
        foreach (var intake in await GetIntakeAsync())
        {
            if (existing.Add(intake.Email))
            {
                recipients.Add(intake);
            }
        }
    }

    /// <summary>First spelling of each address wins; the comparison ignores case.</summary>
    public static List<NotificationRecipient> Distinct(IEnumerable<NotificationRecipient> recipients)
    {
        return recipients
            .Where(r => !string.IsNullOrWhiteSpace(r.Email))
            .GroupBy(r => r.Email.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }
}
