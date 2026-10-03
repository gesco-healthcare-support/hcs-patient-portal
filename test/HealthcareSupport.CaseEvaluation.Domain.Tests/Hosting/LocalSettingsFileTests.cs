using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Hosting;

/// <summary>
/// #595: an empty <c>appsettings.Local.json</c> must load like an absent one. Measured before the fix with
/// <c>AddJsonFile(..., optional: true)</c>: absent and <c>{}</c> loaded; 0 bytes and whitespace-only threw
/// <c>InvalidDataException</c> and stopped the host at configuration loading.
/// </summary>
public sealed class LocalSettingsFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "local-settings-" + Guid.NewGuid().ToString("N"));

    public LocalSettingsFileTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void An_absent_file_loads()
    {
        Build().ShouldNotBeNull();
    }

    [Fact]
    public void An_empty_object_loads()
    {
        Write("{}");

        Build().ShouldNotBeNull();
    }

    [Fact]
    public void A_zero_byte_file_loads_as_if_absent()
    {
        Write("");

        Build().ShouldNotBeNull();
    }

    [Fact]
    public void A_whitespace_only_file_loads_as_if_absent()
    {
        Write("  \r\n\t\n");

        Build().ShouldNotBeNull();
    }

    /// <summary>
    /// CONTROL for the four above: the file IS read when it has content. Without it, a helper that never loaded
    /// the file at all would pass every one of them.
    /// </summary>
    [Fact]
    public void A_file_with_content_is_still_loaded()
    {
        Write("{ \"Probe\": { \"Value\": \"from-local\" } }");

        Build()["Probe:Value"].ShouldBe("from-local");
    }

    /// <summary>Only a BLANK file is forgiven: malformed content must still stop the host, loudly.</summary>
    [Fact]
    public void A_malformed_file_still_fails()
    {
        Write("{ \"Probe\": ");

        Should.Throw<InvalidDataException>(() => Build());
    }

    /// <summary>
    /// The helper protects nothing unless every loader uses it. Each process that reads the file is named here,
    /// and no other code in <c>src</c> may load it directly.
    /// </summary>
    [Theory]
    [InlineData("src/HealthcareSupport.CaseEvaluation.HttpApi/Hosting/CaseEvaluationHost.cs")]
    [InlineData("src/HealthcareSupport.CaseEvaluation.DbMigrator/Program.cs")]
    public void Each_loader_goes_through_the_helper(string relativePath)
    {
        File.ReadAllText(Path.Combine(RepoRoot(), relativePath)).ShouldContain(".AddLocalSettingsJson()");
    }

    [Fact]
    public void Nothing_in_src_loads_the_file_directly()
    {
        var helper = Path.Combine("Hosting", nameof(LocalSettingsFile) + ".cs");
        var direct = Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(helper, StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("\"" + LocalSettingsFile.FileName + "\"", StringComparison.Ordinal))
            .ToList();

        direct.ShouldBeEmpty();
    }

    private IConfigurationRoot Build() =>
        new ConfigurationBuilder().SetBasePath(_directory).AddLocalSettingsJson().Build();

    private void Write(string content) => File.WriteAllText(Path.Combine(_directory, LocalSettingsFile.FileName), content);

    /// <summary>Walks up from the test assembly to the directory holding the solution file.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HealthcareSupport.CaseEvaluation.slnx")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull("repository root (HealthcareSupport.CaseEvaluation.slnx) not found");
        return dir!.FullName;
    }
}
