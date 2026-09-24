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
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Reads and writes the per-office <c>CaseTrackerPushEnabled</c> switch from the host surface.
///
/// <para>Gated on <see cref="CaseEvaluationPermissions.CaseTrackerIntegration"/>, a Host-only
/// permission, and every method also refuses a caller inside an office (2026-09-24). Each acts on the
/// office it names, or on all of them, so they belong to the host alone -- including starting the #927
/// feed and returning an office to push. They used to share
/// <see cref="CaseEvaluationPermissions.Appointments.PushToCaseTracker"/> with the per-appointment
/// push button, but that permission is Both-sided because the button lives inside an office. The new
/// permission reaches IT Admin and the host Supervisor through the role seed
/// (<c>InternalUserRoleDataSeedContributor</c>), which matters because the IT Admin role cannot be
/// re-permissioned through the UI.</para>
/// </summary>
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

    [Authorize(CaseEvaluationPermissions.CaseTrackerIntegration.Default)]
    public virtual async Task<CaseTrackerOfficePushStateDto> SetPushEnabledAsync(Guid officeId, bool enabled)
    {
        EnsureHostCaller();

        // Entering the office scope before writing is what makes this correct under
        // database-per-office: the setting store follows the current tenant's connection, so writing
        // here puts the row in the SAME database the drain reads from when it enters the same scope
        // (IntegrationOutboxDrainService). Using SetForTenantAsync from host scope would risk landing
        // the row in the host database, where the drain would never see it -- the switch would appear
        // to be on and nothing would send.
        using (_currentTenant.Change(officeId))
        {
            await _settingManager.SetForCurrentTenantAsync(
                CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled,
                enabled ? "true" : "false");
        }

        // Deliberately audited: this is the action that starts ePHI moving to another system.
        _logger.LogWarning(
            "CaseTrackerPushSettingsAppService: Case Tracker push set to {Enabled} for office {OfficeId} by user {UserId}.",
            enabled, officeId, CurrentUser.Id);

        return await ReadStateAsync(officeId);
    }

    /// <summary>
    /// Cutover (#927). Refused while the office's push switch is off: that switch stays the gate for everything
    /// the portal sends the Case Tracker, and the feed refuses an office that has it off.
    /// </summary>
    [Authorize(CaseEvaluationPermissions.CaseTrackerIntegration.Default)]
    public virtual async Task<CaseTrackerOfficePushStateDto> StartFeedAsync(Guid officeId)
    {
        EnsureHostCaller();

        using (_currentTenant.Change(officeId))
        {
            if (!await IsPushSwitchOnAsync())
            {
                throw new UserFriendlyException(
                    "Turn the Case Tracker push on for this office before starting the feed.");
            }

            if (!await _feedManager.StartAsync(officeId))
            {
                throw new UserFriendlyException("The Case Tracker feed is already on for this office.");
            }
        }

        // Audited like the push switch: from here the Case Tracker reads this office's changes itself.
        _logger.LogWarning(
            "CaseTrackerPushSettingsAppService: Case Tracker FEED started for office {OfficeId} by user {UserId}.",
            officeId, CurrentUser.Id);

        return await ReadStateAsync(officeId);
    }

    /// <summary>Rollback (#927). Refused when the office is not on the feed.</summary>
    [Authorize(CaseEvaluationPermissions.CaseTrackerIntegration.Default)]
    public virtual async Task<CaseTrackerOfficePushStateDto> ReturnToPushAsync(Guid officeId)
    {
        EnsureHostCaller();

        using (_currentTenant.Change(officeId))
        {
            if (!await _feedManager.ReturnToPushAsync())
            {
                throw new UserFriendlyException("The Case Tracker feed is not on for this office.");
            }
        }

        _logger.LogWarning(
            "CaseTrackerPushSettingsAppService: Case Tracker office {OfficeId} returned from the feed to PUSH by user {UserId}.",
            officeId, CurrentUser.Id);

        return await ReadStateAsync(officeId);
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
