using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// QA item 7: raises in-app notifications for a whole office's internal staff.
/// Per-user fan-out -- one <see cref="AppNotification"/> row per Staff Supervisor
/// + Intake Staff user in the current office -- so each recipient has independent
/// read state. Called from the parallel in-app notification event handlers
/// (alongside, not inside, the email handlers).
///
/// <para>Runs inside the office's tenant scope (the caller wraps
/// <c>ICurrentTenant.Change(tenantId)</c>); the entity ctor stamps TenantId
/// explicitly so ABP's insert-time auto-stamp is suppressed, matching the
/// AppointmentDraft precedent.</para>
/// </summary>
public class AppNotificationManager : DomainService
{
    /// <summary>Internal staff roles that receive in-app notifications (mirrors the
    /// StatusChangeEmailHandler no-show allow-list -- supervisor + intake tiers).</summary>
    public static readonly string[] InternalStaffRoles =
    {
        "Staff Supervisor",
        "Intake Staff",
    };

    private readonly IRepository<AppNotification, Guid> _notificationRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentTenant _currentTenant;

    public AppNotificationManager(
        IRepository<AppNotification, Guid> notificationRepository,
        IdentityUserManager userManager,
        ICurrentTenant currentTenant)
    {
        _notificationRepository = notificationRepository;
        _userManager = userManager;
        _currentTenant = currentTenant;
    }

    /// <summary>
    /// Fans out one notification per internal staff user in the current office.
    /// No-op (logged) when the office has no staff users -- the notification is a
    /// side effect and must never block the upstream operation.
    /// </summary>
    public virtual async Task RaiseForOfficeStaffAsync(
        AppNotificationType type, string title, string body, string? url = null)
    {
        var recipientIds = await ResolveOfficeStaffUserIdsAsync();
        if (recipientIds.Count == 0)
        {
            Logger.LogInformation(
                "AppNotificationManager: office {TenantId} has no internal staff; skipping {Type} notification.",
                _currentTenant.Id, type);
            return;
        }

        var notifications = BuildForRecipients(recipientIds, type, title, body, url, _currentTenant.Id);
        await _notificationRepository.InsertManyAsync(notifications);
    }

    /// <summary>
    /// Pure fan-out: one <see cref="AppNotification"/> per recipient id with the
    /// same content. Internal so unit tests can assert the fan-out (count, fields,
    /// per-recipient rows) without standing up the IdentityUserManager harness.
    /// </summary>
    internal List<AppNotification> BuildForRecipients(
        IReadOnlyCollection<Guid> recipientIds,
        AppNotificationType type,
        string title,
        string body,
        string? url,
        Guid? tenantId)
    {
        return recipientIds
            .Select(uid => new AppNotification(
                GuidGenerator.Create(), uid, type, title, body, url, tenantId))
            .ToList();
    }

    private async Task<List<Guid>> ResolveOfficeStaffUserIdsAsync()
    {
        var ids = new HashSet<Guid>();
        foreach (var roleName in InternalStaffRoles)
        {
            var users = await _userManager.GetUsersInRoleAsync(roleName);
            foreach (var user in users)
            {
                ids.Add(user.Id);
            }
        }
        return ids.ToList();
    }
}
