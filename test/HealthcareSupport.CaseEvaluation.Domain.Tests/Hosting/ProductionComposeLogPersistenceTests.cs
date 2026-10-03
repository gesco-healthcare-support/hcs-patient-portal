using System;
using System.IO;
using System.Linq;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Hosting;

/// <summary>
/// #908 -- pins that each web process's log directory survives the container being recreated.
///
/// <para>The Serilog file sink writes to <c>Logs/logs.txt</c> relative to the content root, which is
/// container-local. A deploy recreates the container, so before this the log that would explain
/// whatever prompted the deploy was destroyed by the deploy. <see cref="Logging.LogFileConsts"/>
/// bounds how much is kept; this guards that any of it is kept at all.</para>
///
/// <para>MEASURED, not assumed: after <c>up -d --force-recreate</c> the container id changes and
/// <c>docker logs</c> returns only the new container's output, so the <c>json-file</c> driver does
/// not cover this either -- both destinations live inside the container's lifetime. A named volume
/// kept both runs.</para>
///
/// <para>THREE FILES ARE COUPLED HERE AND NONE OF THEM MENTIONS THE OTHERS: the sink path in
/// <c>CaseEvaluationHost</c>, the <c>mkdir</c>/<c>chown</c> in each Dockerfile, and the mount in
/// <c>docker-compose.prod.yml</c>. Move the directory in one and the others keep working while the
/// logs quietly stop persisting, because Serilog does not throw on a failed sink -- it drops the
/// line. This test is the only thing that will say so.</para>
/// </summary>
public class ProductionComposeLogPersistenceTests
{
    /// <summary>
    /// WORKDIR is /app in both runtime stages and the sink path is relative to it, so this is the
    /// absolute form of <c>CaseEvaluationHost</c>'s <c>Logs/logs.txt</c>. Asserted against each
    /// Dockerfile below rather than trusted, so the three stay in step.
    /// </summary>
    private const string LogDirectory = "/app/Logs";

    [Theory]
    [InlineData("api", "apilogs", "src/HealthcareSupport.CaseEvaluation.HttpApi.Host/Dockerfile")]
    [InlineData("authserver", "authserverlogs", "src/HealthcareSupport.CaseEvaluation.AuthServer/Dockerfile")]
    public void Each_web_process_keeps_its_logs_in_a_named_volume_that_outlives_the_container(
        string service, string volumeName, string dockerfile)
    {
        var block = ServiceBlock(service);

        // CONTROL: prove this is the right block before trusting what it holds. Without it, a
        // parser that picked the wrong service would pass or fail for the wrong reason.
        block.ShouldContain($"dockerfile: {dockerfile}");

        var mount = block.Split('\n').Select(l => l.Trim())
            .SingleOrDefault(l => l.EndsWith($":{LogDirectory}\"", StringComparison.Ordinal));
        mount.ShouldNotBeNull(
            $"service '{service}' mounts nothing at {LogDirectory}, so a deploy destroys its logs");

        // A NAMED volume, not a bind mount. Docker initialises a named volume from the image path
        // WITH its ownership; a bind-mount source Docker creates is root-owned, and these processes
        // run as uid 1654. That failure is silent -- the container runs and writes nothing -- and it
        // has already cost this deploy two host-side prerequisites (#1152, #1127).
        mount.ShouldBe($"- \"{volumeName}:{LogDirectory}\"");

        DeclaredVolumes().ShouldContain(volumeName,
            $"'{volumeName}' is mounted but not declared, so compose would treat it as a bind mount");
    }

    [Theory]
    [InlineData("src/HealthcareSupport.CaseEvaluation.HttpApi.Host/Dockerfile")]
    [InlineData("src/HealthcareSupport.CaseEvaluation.AuthServer/Dockerfile")]
    public void Each_runtime_image_creates_the_log_directory_owned_by_the_process_user(string dockerfile)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), dockerfile));

        // The mount point must exist in the image and be owned by the runtime user, because that is
        // what the named volume is initialised FROM. Lose this and the volume is created root-owned.
        text.ShouldContain($"mkdir -p {LogDirectory} && chown app:app {LogDirectory}");
        text.ShouldContain("USER app");
    }

    /// <summary>The names declared under the top-level <c>volumes:</c> key.</summary>
    private static string[] DeclaredVolumes()
    {
        var lines = File.ReadAllLines(Path.Combine(RepoRoot(), "docker-compose.prod.yml"));
        var start = Array.FindIndex(lines, l => l == "volumes:");
        start.ShouldBeGreaterThanOrEqualTo(0, "no top-level 'volumes:' key in docker-compose.prod.yml");

        return lines.Skip(start + 1)
            .TakeWhile(l => l.Length == 0 || l.StartsWith("  ", StringComparison.Ordinal))
            .Select(l => l.Trim())
            .Where(l => l.EndsWith(':') && !l.StartsWith('#'))
            .Select(l => l.TrimEnd(':'))
            .ToArray();
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
