using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Logging;
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

    /// <summary>
    /// The same table again, through <c>Apply</c>, reading the switches it actually writes.
    /// <c>ShouldEnable</c> returning false is not the guarantee; the guarantee is that the two
    /// process-wide switches end up off, which is what a host leaks through when it does not.
    /// </summary>
    [Theory]
    [InlineData("Production", null, false)]
    [InlineData("Staging", null, false)]
    [InlineData("Development", null, true)]
    [InlineData("Development", "true", false)]
    [InlineData("Production", "true", false)]
    public void Apply_WritesBothSwitches(string environmentName, string? disablePii, bool expected)
    {
        var showPii = IdentityModelEventSource.ShowPII;
        var logArtifact = IdentityModelEventSource.LogCompleteSecurityArtifact;
        try
        {
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(environmentName);
            var settings = new Dictionary<string, string?>();
            if (disablePii != null)
            {
                settings[IdentityModelPiiLogging.DisableKey] = disablePii;
            }

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            IdentityModelPiiLogging.Apply(environment, configuration);

            IdentityModelEventSource.ShowPII.ShouldBe(expected);
            IdentityModelEventSource.LogCompleteSecurityArtifact.ShouldBe(expected);
        }
        finally
        {
            IdentityModelEventSource.ShowPII = showPii;
            IdentityModelEventSource.LogCompleteSecurityArtifact = logArtifact;
        }
    }

    /// <summary>
    /// A host that already had the switches on does not keep them on. The old shape only ever wrote
    /// <c>true</c>, so this is the case it could not have covered.
    /// </summary>
    [Fact]
    public void Apply_TurnsThemOffAgain_WhenAlreadyOn()
    {
        var showPii = IdentityModelEventSource.ShowPII;
        var logArtifact = IdentityModelEventSource.LogCompleteSecurityArtifact;
        try
        {
            IdentityModelPiiLogging.Apply(true);
            IdentityModelEventSource.ShowPII.ShouldBeTrue();

            IdentityModelPiiLogging.Apply(false);

            IdentityModelEventSource.ShowPII.ShouldBeFalse();
            IdentityModelEventSource.LogCompleteSecurityArtifact.ShouldBeFalse();
        }
        finally
        {
            IdentityModelEventSource.ShowPII = showPii;
            IdentityModelEventSource.LogCompleteSecurityArtifact = logArtifact;
        }
    }
}
