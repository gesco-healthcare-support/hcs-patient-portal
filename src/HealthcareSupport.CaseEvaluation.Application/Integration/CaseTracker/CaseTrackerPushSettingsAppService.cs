using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Reads and writes the per-office <c>CaseTrackerPushEnabled</c> switch from the host surface.
///
/// <para>Gated on <see cref="CaseEvaluationPermissions.Appointments.PushToCaseTracker"/> rather than a
/// new permission. That is the permission the dead-letter retry already uses, and both actions do the
/// same thing in kind -- cause the portal to send ePHI to the Case Tracker. A new permission would
/// have to be granted before the control appeared, and the IT Admin role cannot be re-permissioned
/// through the UI, so the realistic outcome of being stricter here is an invisible button during a
/// live test. The screen is host-only and internal-staff-only.</para>
/// </summary>
[Authorize]
public class CaseTrackerPushSettingsAppService : CaseEvaluationAppService, ICaseTrackerPushSettingsAppService
{
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly ITenantStore _tenantStore;
    private readonly ICurrentTenant _currentTenant;
    private readonly ISettingManager _settingManager;
    private readonly IIntegrationOutboxRepository _outboxRepository;
    private readonly ILogger<CaseTrackerPushSettingsAppService> _logger;

    public CaseTrackerPushSettingsAppService(
        ITenantWorkRunner tenantWorkRunner,
        ITenantStore tenantStore,
        ICurrentTenant currentTenant,
        ISettingManager settingManager,
        IIntegrationOutboxRepository outboxRepository,
        ILogger<CaseTrackerPushSettingsAppService> logger)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _tenantStore = tenantStore;
        _currentTenant = currentTenant;
        _settingManager = settingManager;
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    [Authorize(CaseEvaluationPermissions.Appointments.PushToCaseTracker)]
    public virtual async Task<List<CaseTrackerOfficePushStateDto>> GetOfficesAsync()
    {
        var states = await _tenantWorkRunner.AggregateAcrossOfficesAsync(
            async officeId => await ReadStateAsync(officeId));

        return states.OrderBy(s => s.OfficeName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    [Authorize(CaseEvaluationPermissions.Appointments.PushToCaseTracker)]
    public virtual async Task<CaseTrackerOfficePushStateDto> SetPushEnabledAsync(Guid officeId, bool enabled)
    {
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

    private async Task<CaseTrackerOfficePushStateDto> ReadStateAsync(Guid officeId)
    {
        var tenant = await _tenantStore.FindAsync(officeId);

        using (_currentTenant.Change(officeId))
        {
            var raw = await _settingManager.GetOrNullForCurrentTenantAsync(
                CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled);

            // Absent or unparseable means "not enabled". The host default is false and the whole point
            // of the switch is that sending is opt-in, so anything ambiguous must read as off.
            var enabled = bool.TryParse(raw, out var parsed) && parsed;

            var queryable = await _outboxRepository.GetQueryableAsync();
            var pending = queryable.Count(x => x.Status == IntegrationOutboxStatus.Pending);

            return new CaseTrackerOfficePushStateDto
            {
                OfficeId = officeId,
                OfficeName = tenant?.Name ?? string.Empty,
                PushEnabled = enabled,
                PendingCount = pending,
            };
        }
    }
}
