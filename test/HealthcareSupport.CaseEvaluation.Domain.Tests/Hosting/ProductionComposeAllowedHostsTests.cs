using System;
using System.IO;
using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Hosting;

/// <summary>
/// B4 (2026-09-25) -- pins the AllowedHosts values the production compose gives each web process.
///
/// <para>This is the "CI check" of the approved plan, as a test that runs in CI rather than a workflow
/// step. It reads the compose FILE as text; it does not run compose. The startup check in
/// <see cref="HostingConfigValidator"/> guards any other environment (the Azure VM gets its values
/// elsewhere); this guards the file the in-house box and the Azure VM both deploy from.</para>
/// </summary>
public class ProductionComposeAllowedHostsTests
{
    private const string AuthServerHosts = "${BASE_DOMAIN};*.${BASE_DOMAIN};localhost;authserver";
    private const string ApiHosts = "${BASE_DOMAIN};*.${BASE_DOMAIN};localhost";

    [Theory]
    [InlineData("authserver", "{0}.auth.${BASE_DOMAIN}", AuthServerHosts)]
    [InlineData("api", "{0}.api.${BASE_DOMAIN}", ApiHosts)]
    public void The_production_compose_pins_AllowedHosts_for_each_web_process(
        string service, string tenantDomainFormat, string expectedHosts)
    {
        var block = ServiceBlock(service);

        // CONTROL: prove this is the right block before trusting what it lacks or holds. Without it,
        // a parser that picked the wrong service would report a missing line for the wrong reason.
        block.ShouldContain($"App__TenantDomainFormat: \"{tenantDomainFormat}\"");

        var line = block.Split('\n').Select(l => l.Trim())
            .SingleOrDefault(l => l.StartsWith("AllowedHosts:", StringComparison.Ordinal));
        line.ShouldNotBeNull($"service '{service}' has no AllowedHosts line");
        // Exact match: a bare "*" added anywhere, or an entry dropped, fails here by name.
        line.ShouldBe($"AllowedHosts: \"{expectedHosts}\"");
    }

    /// <summary>The lines of one top-level service (two-space indent) in docker-compose.prod.yml.</summary>
    private static string ServiceBlock(string service)
    {
        var lines = File.ReadAllLines(Path.Combine(RepoRoot(), "docker-compose.prod.yml"));
        var start = Array.FindIndex(lines, l => l == $"  {service}:");
        start.ShouldBeGreaterThanOrEqualTo(0, $"no '{service}:' service in docker-compose.prod.yml");

        var end = Array.FindIndex(lines, start + 1,
            l => l.Length > 2 && l.StartsWith("  ", StringComparison.Ordinal) && l[2] != ' ' && l[2] != '#');
        var count = (end < 0 ? lines.Length : end) - start;
        return string.Join('\n', lines.Skip(start).Take(count));
    }

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
