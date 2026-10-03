using System;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// What the demo-account seeders read from the process to decide whether they may run.
/// A seam so a test can supply the values without setting process-wide environment
/// variables, which xUnit's parallel test classes would all see.
/// </summary>
public interface IDemoSeedEnvironment
{
    /// <summary><c>ASPNETCORE_ENVIRONMENT</c>, else <c>DOTNET_ENVIRONMENT</c>.</summary>
    string? EnvironmentName { get; }

    /// <summary>The raw value of <see cref="DemoSeedGate.FlagVariable"/>.</summary>
    string? AllowDemoSeed { get; }
}

public class ProcessDemoSeedEnvironment : IDemoSeedEnvironment, ISingletonDependency
{
    public string? EnvironmentName =>
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

    public string? AllowDemoSeed => Environment.GetEnvironmentVariable(DemoSeedGate.FlagVariable);
}

/// <summary>
/// The gate every seeder that creates accounts with the published default password must pass
/// (#726, decided 2026-10-02): the environment is Development AND <see cref="FlagVariable"/> is
/// explicitly <c>true</c>.
///
/// <para>Development alone used to be enough. But that variable is generic: it reaches a process
/// from a shell profile, an inherited environment, a CI default or a copied compose fragment, and
/// every one of those paths also meant "create accounts anyone can sign in to". The flag exists
/// only to say this one thing, so it does not arrive by accident.</para>
///
/// <para>Both checks fail closed. An unset or empty value is not Development and is not
/// <c>true</c>.</para>
/// </summary>
public static class DemoSeedGate
{
    public const string FlagVariable = "ALLOW_DEMO_SEED";

    public static bool IsDevelopment(IDemoSeedEnvironment environment) =>
        string.Equals(environment.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);

    public static bool IsAllowed(IDemoSeedEnvironment environment) =>
        IsDevelopment(environment)
        && string.Equals(environment.AllowDemoSeed, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Why a closed gate is closed, for the seeder's skip log line.</summary>
    public static string ClosedReason(IDemoSeedEnvironment environment) =>
        IsDevelopment(environment)
            ? $"Development, but {FlagVariable} is not 'true'"
            : "not the Development environment";
}
