using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Settings;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Settings;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Whether the drain may PUSH for the current office (#927). Two switches decide it, deliberately apart:
/// <c>CaseTrackerPushEnabled</c> says the office talks to the Case Tracker at all -- it also gates reconcile
/// and attendance, which carry on unchanged after cutover -- and an active feed record says the office is
/// delivered by the feed instead. Push needs the first on and the second absent.
///
/// <para>One place for the rule, so the drain's per-pass check and its per-row check cannot disagree. Reads
/// in the current office scope, like every Case Tracker read.</para>
/// </summary>
public class CaseTrackerDeliveryModeReader : ITransientDependency
{
    private readonly ISettingProvider _settingProvider;
    private readonly ICaseTrackerFeedStateRepository _feedStateRepository;

    public CaseTrackerDeliveryModeReader(
        ISettingProvider settingProvider,
        ICaseTrackerFeedStateRepository feedStateRepository)
    {
        _settingProvider = settingProvider;
        _feedStateRepository = feedStateRepository;
    }

    /// <summary>The office's push switch, as reconcile and attendance read it.</summary>
    public virtual Task<bool> IsPushSwitchOnAsync() =>
        _settingProvider.IsTrueAsync(CaseEvaluationSettings.IntegrationPolicy.CaseTrackerPushEnabled);

    /// <summary>True while the office is delivered by the feed.</summary>
    public virtual async Task<bool> IsFeedActiveAsync()
    {
        var state = await _feedStateRepository.FindCurrentAsync();
        return state is { IsActive: true };
    }
}
