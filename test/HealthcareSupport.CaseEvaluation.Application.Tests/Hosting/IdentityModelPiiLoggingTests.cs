using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Hosting;

/// <summary>
/// IdentityModel's PII and whole-token logging is on in Development only, whatever
/// <c>App:DisablePII</c> holds elsewhere. The first row is the one that was true in every deployed
/// environment before this change: Production with the key unset logged tokens.
/// </summary>
public class IdentityModelPiiLoggingTests
{
    [Theory]
    [InlineData("Production", null, false)]
    [InlineData("Staging", null, false)]
    [InlineData("Development", null, true)]
    [InlineData("Development", "true", false)]
    [InlineData("Production", "false", false)]
    [InlineData("Production", "true", false)]
    public void ShouldEnable_OnlyInDevelopment(string environmentName, string? disablePii, bool expected)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        var settings = new Dictionary<string, string?>();
        if (disablePii != null)
        {
            settings[IdentityModelPiiLogging.DisableKey] = disablePii;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        IdentityModelPiiLogging.ShouldEnable(environment, configuration).ShouldBe(expected);
    }
}
