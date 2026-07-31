using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Host-side control of the per-office Case Tracker push switch.
///
/// <para>Exists because the switch is an ABP setting with no admin surface: without this the only way
/// to enable an office is a hand-written row in <c>AbpSettings</c> in that office's database, against
/// a value the app reads through a distributed cache. That is not a procedure to improvise during a
/// live first-contact test with the receiving team.</para>
/// </summary>
public interface ICaseTrackerPushSettingsAppService : IApplicationService
{
    /// <summary>Every office with its current push state and pending backlog.</summary>
    Task<List<CaseTrackerOfficePushStateDto>> GetOfficesAsync();

    /// <summary>Turns the push on or off for ONE office. Returns that office's refreshed state.</summary>
    Task<CaseTrackerOfficePushStateDto> SetPushEnabledAsync(Guid officeId, bool enabled);
}
