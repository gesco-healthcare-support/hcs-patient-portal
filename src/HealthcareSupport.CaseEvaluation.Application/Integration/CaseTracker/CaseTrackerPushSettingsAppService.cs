using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Reads and writes the per-office <c>CaseTrackerPushEnabled</c> switch from the host surface.
///
/// <para>Gated on <see cref="CaseEvaluationPermissions.CaseTrackerIntegration"/>, a Host-only
/// permission, and every method also refuses a caller inside an office (2026-09-24). Each acts on the
/// office it names, or on all of them, so they belong to the host alone. Connecting and disconnecting an
/// office need <see cref="CaseEvaluationPermissions.CaseTrackerIntegration.ManageConnection"/> as well
/// (2026-10-26): the host Supervisor sees the offices but cannot change them. They used to share
/// <see cref="CaseEvaluationPermissions.Appointments.PushToCaseTracker"/> with the per-appointment
/// push button, but that permission is Both-sided because the button lives inside an office. The new
/// permission reaches IT Admin and the host Supervisor through the role seed
/// (<c>InternalUserRoleDataSeedContributor</c>), which matters because the IT Admin role cannot be
/// re-permissioned through the UI.</para>
/// </summary>
// Not auto-exposed. The hand-written CaseTrackerOfficesController is this service's only HTTP surface, so the
// guards written on that surface (input validation, binding) cannot be bypassed through a
// second, auto-generated route.
[RemoteService(IsEnabled = false)]
[Authorize]
public class CaseTrackerPushSettingsAppService : CaseEvaluationAppService, ICaseTrackerPushSettingsAppService
{
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly ITenantStore _tenantStore;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISettingManager _settingManager;
    private readonly IIntegrationOutboxRepository _outboxRepository;
    private readonly CaseTrackerFeedManager _feedManager;
    private readonly ILogger<CaseTrackerPushSettingsAppService> _logger;

    public CaseTrackerPushSettingsAppService(
        ITenantWorkRunner tenantWorkRunner,
        ITenantStore tenantStore,
        ICurrentTenant currentTenant,
        ISettingManager settingManager,
        IIntegrationOutboxRepository outboxRepository,
        CaseTrackerFeedManager feedManager,
        ILogger<CaseTrackerPushSettingsAppService> logger)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _tenantStore = tenantStore;
        _currentTenant = currentTenant;
        _settingManager = settingManager;
        _outboxRepository = outboxRepository;
        _feedManager = feedManager;
        _logger = logger;
    }

    [Authorize(CaseEvaluationPermissions.CaseTrackerIntegration.Default)]
    public virtual async Task<List<CaseTrackerOfficePushStateDto>> GetOfficesAsync()
    {
        EnsureHostCaller();

        var states = await _tenantWorkRunner.AggregateAcrossOfficesAsync(
            async officeId => await ReadStateAsync(officeId));

        return states.OrderBy(s => s.OfficeName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Connects ONE office to the Case Tracker: its feed is started if it is not already on, then its switch
    /// is turned on, in one call. Before 2026-10-26 these were two buttons ("Turn on", then "Start feed"),
    /// and an office left between them sat in push mode, which has nowhere to push on Azure.
    ///
    /// <para>The feed goes first so that the state in between is the safe one, whichever write lands
    /// first: a started feed with the switch still off sends nothing and keeps every change waiting, where
    /// a switch on without the feed would try to push. Both writes run in this call's unit of work, so a
    /// failure before it completes leaves neither. An office whose feed is already on (disconnected
    /// earlier) just has its switch turned back on, and the Case Tracker resumes from the position it last
    /// acknowledged; connecting a connected office changes nothing.</para>
    /// </summary>
    [Authorize(CaseEvaluationPermissions.CaseTrackerIntegration.ManageConnection)]
    public virtual async Task<CaseTrackerOfficePushStateDto> ConnectAsync(Guid officeId)
    {
        EnsureHostCaller();
        await EnsureOfficeExistsAsync(officeId);

        bool feedStarted;
        using (_currentTenant.Change(officeId))
        {
            feedStarted = await _feedManager.StartAsync(officeId);
            await SetSwitchAsync(true);
        }

        // Deliberately audited: from here the Case Tracker reads this office's changes, ePHI included.
        _logger.LogWarning(
            "CaseTrackerPushSettingsAppService: office {OfficeId} CONNECTED to the Case Tracker by user {UserId}; feed {FeedChange}.",
            officeId, CurrentUser.Id, feedStarted ? "started" : "was already on");

        return await ReadStateAsync(officeId);
    }

    /// <summary>
    /// Disconnects ONE office: its switch goes off and its feed is left as it is. The feed then refuses the
    /// office, nothing is pushed, and its changes wait; connecting it again delivers them.
    /// </summary>
    [Authorize(CaseEvaluationPermissions.CaseTrackerIntegration.ManageConnection)]
    public virtual async Task<CaseTrackerOfficePushStateDto> DisconnectAsync(Guid officeId)
    {
        EnsureHostCaller();
        await EnsureOfficeExistsAsync(officeId);

        using (_currentTenant.Change(officeId))
        {
            await SetSwitchAsync(false);
        }

        _logger.LogWarning(
            "CaseTrackerPushSettingsAppService: office {OfficeId} DISCONNECTED from the Case Tracker by user {UserId}.",
            officeId, CurrentUser.Id);

        return await ReadStateAsync(officeId);
    }

    /// <summary>
    /// Writes the office's switch. The caller has entered the office's scope: the setting store follows the
    /// current tenant, so the row lands where the drain and the feed read it when they enter the same scope
    /// (IntegrationOutboxDrainService). Writing with SetForTenantAsync from host scope would risk a row the
    /// readers never see -- the switch would appear to be on and nothing would send.
    /// </summary>
    private Task SetSwitchAsync(bool on)
    {
        return _settingManager.SetForCurrentTenantAsync(
            CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled,
            on ? "true" : "false");
    }

    /// <summary>Refuses an id that names no office, before any office scope is entered.</summary>
    private async Task EnsureOfficeExistsAsync(Guid officeId)
    {
        if (await _tenantStore.FindAsync(officeId) == null)
        {
            throw new EntityNotFoundException(typeof(TenantConfiguration), officeId);
        }
    }

    /// <summary>The office's push switch as the drain reads it; absent or unparseable reads as off.</summary>
    private async Task<bool> IsPushSwitchOnAsync()
    {
        var raw = await _settingManager.GetOrNullForCurrentTenantAsync(
            CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled);
        return bool.TryParse(raw, out var parsed) && parsed;
    }

    /// <summary>
    /// Refuses a caller who is inside an office, before any office is entered or aggregated. The
    /// Host-only permission already stops an office caller at the authorization interceptor; this
    /// check keeps the refusal in place even if that permission's side is ever widened again.
    /// </summary>
    private void EnsureHostCaller()
    {
        if (_currentTenant.IsAvailable)
        {
            throw new AbpAuthorizationException(
                "Case Tracker delivery is managed from the host, not from inside an office.");
        }
    }

    private async Task<CaseTrackerOfficePushStateDto> ReadStateAsync(Guid officeId)
    {
        var tenant = await _tenantStore.FindAsync(officeId);

        using (_currentTenant.Change(officeId))
        {
            // Absent or unparseable means "not enabled". The host default is false and the whole point
            // of the switch is that sending is opt-in, so anything ambiguous must read as off.
            var enabled = await IsPushSwitchOnAsync();

            var queryable = await _outboxRepository.GetQueryableAsync();
            var pending = queryable.Count(x => x.Status == IntegrationOutboxStatus.Pending);
            var feed = await _feedManager.GetStatusAsync(officeId);

            return new CaseTrackerOfficePushStateDto
            {
                OfficeId = officeId,
                OfficeName = tenant?.Name ?? string.Empty,
                PushEnabled = enabled,
                PendingCount = pending,
                FeedActive = feed.Active,
                FeedStartedAt = feed.StartedAt,
                LastRequestAt = feed.LastRequestAt,
                LastAdvancedAt = feed.LastAdvancedAt,
                OutstandingCount = feed.OutstandingCount,
            };
        }
    }
}
