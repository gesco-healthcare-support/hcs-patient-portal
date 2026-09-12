using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the inbound integration token. This is the ONLY thing standing between an
/// anonymous caller and a full appointment payload, so the fail-closed case matters as much as the
/// happy path: an unconfigured token must reject everything rather than wave everyone through.
///
/// <para>No real token value appears here; the fixtures are arbitrary strings.</para>
/// </summary>
public class IntegrationTokenValidatorTests
{
    private const string ConfiguredToken = "sample-integration-token-value";

    private static IntegrationTokenValidator Build(string? configuredToken)
    {
        var settings = new Dictionary<string, string?>();
        if (configuredToken != null)
        {
            settings[CaseTrackerIntegrationConsts.TokenConfigurationKey] = configuredToken;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new IntegrationTokenValidator(configuration);
    }

    [Fact]
    public void TheConfiguredToken_IsAccepted()
    {
        Build(ConfiguredToken).IsValid(ConfiguredToken).ShouldBeTrue();
    }

    [Theory]
    [InlineData("sample-integration-token-valu")]   // one byte short
    [InlineData("sample-integration-token-values")] // one byte long
    [InlineData("Sample-integration-token-value")]  // case differs
    [InlineData("wrong")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElse_IsRejected(string? presented)
    {
        Build(ConfiguredToken).IsValid(presented).ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithNoTokenConfigured_EverythingIsRejected(string? configured)
    {
        // Fail closed. An unconfigured secret must not degrade into "no check" -- that would make a
        // fresh deploy silently serve PHI to anyone who found the URL.
        var validator = Build(configured);

        validator.IsValid(ConfiguredToken).ShouldBeFalse();
        validator.IsValid("anything").ShouldBeFalse();
        validator.IsValid("").ShouldBeFalse();
        validator.IsValid(null).ShouldBeFalse();
    }

    [Fact]
    public void WithNoTokenConfigured_EvenAnEmptyPresentedTokenIsRejected()
    {
        // Guards the specific trap: comparing "" to "" would otherwise succeed.
        Build(configuredToken: "").IsValid("").ShouldBeFalse();
    }
}
