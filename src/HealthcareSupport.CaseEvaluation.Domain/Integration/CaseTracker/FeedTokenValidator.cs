using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Checks the <c>X-Feed-Token</c> the Case Tracker presents to the changes feed (#927) against its OWN secret,
/// <see cref="CaseTrackerFeedConsts.FeedTokenConfigurationKey"/>.
///
/// <para>A separate secret from the integration token, decided 2026-09-24: the feed is read-only and always
/// on, while the integration token also authorises attendance, which closes appointments. The comparison is
/// <see cref="IntegrationTokenValidator.Matches"/>, so both fail closed on a blank secret and compare in
/// constant time.</para>
/// </summary>
public class FeedTokenValidator : ITransientDependency
{
    private readonly IConfiguration _configuration;

    public FeedTokenValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>True only when a feed token IS configured and <paramref name="presented"/> matches it exactly.</summary>
    public virtual bool IsValid(string? presented) =>
        IntegrationTokenValidator.Matches(_configuration[CaseTrackerFeedConsts.FeedTokenConfigurationKey], presented);
}
