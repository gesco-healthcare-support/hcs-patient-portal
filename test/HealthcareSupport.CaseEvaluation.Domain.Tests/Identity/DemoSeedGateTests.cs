using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Logging;
using Shouldly;
using Volo.Abp.Data;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// #726: the seeders that create accounts with the published default password need Development AND
/// <c>ALLOW_DEMO_SEED=true</c>. Development alone used to be enough, and that variable reaches a
/// process for reasons that have nothing to do with demo accounts.
///
/// <para>The environment is supplied through <see cref="IDemoSeedEnvironment"/>, never by setting
/// process variables: xUnit runs test classes in parallel, and a Development leaking out of one test
/// would change what the others see.</para>
///
/// <para><b>Why the contributor tests cannot pass vacuously.</b> Each one sets Development, so the
/// old Development-only check would have let the seeder through. Every dependency the seeder holds
/// is null, so a seeder that gets past its gate throws on the first one it touches -- the
/// positive-control test for each contributor proves exactly that, by opening the gate and expecting
/// the throw. Remove the flag check from any one contributor and its closed-gate test fails by
/// name.</para>
/// </summary>
public class DemoSeedGateTests
{
    private static readonly DataSeedContext OfficeContext = new(Guid.NewGuid());

    private sealed class FakeDemoSeedEnvironment(string? environmentName, string? allowDemoSeed)
        : IDemoSeedEnvironment
    {
        public string? EnvironmentName { get; } = environmentName;

        public string? AllowDemoSeed { get; } = allowDemoSeed;
    }

    private static readonly IDemoSeedEnvironment DevelopmentWithoutFlag =
        new FakeDemoSeedEnvironment("Development", null);

    private static readonly IDemoSeedEnvironment DevelopmentWithFlag =
        new FakeDemoSeedEnvironment("Development", "true");

    // ---- the gate itself ----

    [Theory]
    [InlineData("Development", "true", true)]
    [InlineData("development", "TRUE", true)]
    [InlineData("Development", null, false)]
    [InlineData("Development", "", false)]
    [InlineData("Development", "false", false)]
    [InlineData("Development", "1", false)]
    [InlineData("Development", "yes", false)]
    [InlineData("Production", "true", false)]
    [InlineData(null, "true", false)]
    [InlineData(null, null, false)]
    public void IsAllowed_requires_development_and_an_explicit_true_flag(
        string? environmentName, string? flag, bool expected)
    {
        DemoSeedGate.IsAllowed(new FakeDemoSeedEnvironment(environmentName, flag)).ShouldBe(expected);
    }

    [Fact]
    public void ClosedReason_names_the_missing_flag_when_the_environment_is_development()
    {
        DemoSeedGate.ClosedReason(DevelopmentWithoutFlag).ShouldContain(DemoSeedGate.FlagVariable);
    }

    [Fact]
    public void FlagVariable_is_the_name_the_compose_files_and_docs_set()
    {
        // The compose files and docs spell the variable out literally; a rename here must fail.
        DemoSeedGate.FlagVariable.ShouldBe("ALLOW_DEMO_SEED");
    }

    // ---- each contributor: Development alone does not seed ----

    [Fact]
    public async Task ExternalUsers_does_not_seed_in_development_without_the_flag()
    {
        var logger = new RecordingLogger<ExternalUsersDataSeedContributor>();
        await ExternalUsers(logger, DevelopmentWithoutFlag).SeedAsync(OfficeContext);
        ShouldHaveSkippedForTheFlag(logger);
    }

    [Fact]
    public async Task ExternalUsers_reaches_its_dependencies_when_the_gate_is_open()
    {
        var logger = new RecordingLogger<ExternalUsersDataSeedContributor>();
        await Should.ThrowAsync<NullReferenceException>(
            () => ExternalUsers(logger, DevelopmentWithFlag).SeedAsync(OfficeContext));
    }

    [Fact]
    public async Task DemoExternalUsers_does_not_seed_in_development_without_the_flag()
    {
        var logger = new RecordingLogger<DemoExternalUsersDataSeedContributor>();
        await DemoExternalUsers(logger, DevelopmentWithoutFlag).SeedAsync(OfficeContext);
        ShouldHaveSkippedForTheFlag(logger);
    }

    [Fact]
    public async Task DemoExternalUsers_reaches_its_dependencies_when_the_gate_is_open()
    {
        var logger = new RecordingLogger<DemoExternalUsersDataSeedContributor>();
        await Should.ThrowAsync<NullReferenceException>(
            () => DemoExternalUsers(logger, DevelopmentWithFlag).SeedAsync(OfficeContext));
    }

    [Fact]
    public async Task DemoPatient_does_not_seed_in_development_without_the_flag()
    {
        var logger = new RecordingLogger<DemoPatientDataSeedContributor>();
        await DemoPatient(logger, DevelopmentWithoutFlag).SeedAsync(OfficeContext);
        ShouldHaveSkippedForTheFlag(logger);
    }

    [Fact]
    public async Task DemoPatient_reaches_its_dependencies_when_the_gate_is_open()
    {
        var logger = new RecordingLogger<DemoPatientDataSeedContributor>();
        await Should.ThrowAsync<NullReferenceException>(
            () => DemoPatient(logger, DevelopmentWithFlag).SeedAsync(OfficeContext));
    }

    [Fact]
    public async Task InternalUsers_does_not_seed_in_development_without_the_flag()
    {
        var logger = new RecordingLogger<InternalUsersDataSeedContributor>();
        await InternalUsers(logger, DevelopmentWithoutFlag).SeedAsync(OfficeContext);
        ShouldHaveSkippedForTheFlag(logger);
    }

    [Fact]
    public async Task InternalUsers_does_not_seed_the_host_in_development_without_the_flag()
    {
        // The host branch seeds the IT Admin and host operator logins; it shares the one gate.
        var logger = new RecordingLogger<InternalUsersDataSeedContributor>();
        await InternalUsers(logger, DevelopmentWithoutFlag).SeedAsync(new DataSeedContext());
        ShouldHaveSkippedForTheFlag(logger);
    }

    [Fact]
    public async Task InternalUsers_reaches_its_dependencies_when_the_gate_is_open()
    {
        var logger = new RecordingLogger<InternalUsersDataSeedContributor>();
        await Should.ThrowAsync<NullReferenceException>(
            () => InternalUsers(logger, DevelopmentWithFlag).SeedAsync(OfficeContext));
    }

    private static void ShouldHaveSkippedForTheFlag<T>(RecordingLogger<T> logger)
    {
        var entry = logger.Entries.ShouldHaveSingleItem();
        entry.Message.ShouldContain("skipping");
        entry.Message.ShouldContain(DemoSeedGate.FlagVariable);
        logger.Entries.ShouldNotContain(e => e.Message.Contains("DEMO SEED created"));
    }

    // Every dependency but the logger and the environment is null on purpose: see the class summary.
    private static ExternalUsersDataSeedContributor ExternalUsers(
        RecordingLogger<ExternalUsersDataSeedContributor> logger, IDemoSeedEnvironment environment) =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, logger, environment);

    private static DemoExternalUsersDataSeedContributor DemoExternalUsers(
        RecordingLogger<DemoExternalUsersDataSeedContributor> logger, IDemoSeedEnvironment environment) =>
        new(null!, null!, null!, null!, logger, environment);

    private static DemoPatientDataSeedContributor DemoPatient(
        RecordingLogger<DemoPatientDataSeedContributor> logger, IDemoSeedEnvironment environment) =>
        new(null!, null!, null!, null!, null!, null!, logger, environment);

    private static InternalUsersDataSeedContributor InternalUsers(
        RecordingLogger<InternalUsersDataSeedContributor> logger, IDemoSeedEnvironment environment) =>
        new(null!, null!, null!, null!, logger, environment);
}
