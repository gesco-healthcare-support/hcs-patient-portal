using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The feed's own token (#927). Beyond the fail-closed cases the integration token already has, the point
/// here is SEPARATION: the integration token must never open the feed, even when both are configured.
/// No real token value appears here; the fixtures are arbitrary strings.
/// </summary>
public class FeedTokenValidatorTests
{
    private const string FeedToken = "sample-feed-token-value";
    private const string IntegrationToken = "sample-integration-token-value";

    private static FeedTokenValidator Build(string? feedToken, string? integrationToken = IntegrationToken)
    {
        var settings = new Dictionary<string, string?>
        {
            [CaseTrackerIntegrationConsts.TokenConfigurationKey] = integrationToken,
        };
        if (feedToken != null)
        {
            settings[CaseTrackerFeedConsts.FeedTokenConfigurationKey] = feedToken;
        }

        return new FeedTokenValidator(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
    }

    [Fact]
    public void TheConfiguredFeedToken_IsAccepted()
    {
        Build(FeedToken).IsValid(FeedToken).ShouldBeTrue();
    }

    [Fact]
    public void TheIntegrationToken_DoesNotOpenTheFeed()
    {
        Build(FeedToken).IsValid(IntegrationToken).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithNoFeedTokenConfigured_EveryTokenIsRejected_EvenTheIntegrationOne(string? configured)
    {
        // Fail closed: a deploy that forgot the feed secret must not fall back to anything.
        var validator = Build(configured);

        validator.IsValid(IntegrationToken).ShouldBeFalse();
        validator.IsValid(string.Empty).ShouldBeFalse();
        validator.IsValid(null).ShouldBeFalse();
    }

    [Theory]
    [InlineData("sample-feed-token-valu")]
    [InlineData("Sample-feed-token-value")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElse_IsRejected(string? presented)
    {
        Build(FeedToken).IsValid(presented).ShouldBeFalse();
    }
}
