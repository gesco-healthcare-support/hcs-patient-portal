using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HealthcareSupport.CaseEvaluation.Repository;

/// <summary>
/// Renders the repository counts that README.md states -- the .NET projects under src/ and
/// test/, and the services in docker-compose.yml and docker-compose.prod.yml -- into a
/// deterministic, sorted table read from the checked-out tree.
///
/// WHY THIS EXISTS. README.md kept these counts by hand, and the copies drifted apart: the
/// repository tree said "4 test projects" when there were five, and "6-service local stack"
/// on the same page that said the local stack runs nine services. A hand-kept number cannot
/// notice that the repository moved. This type reads the repository itself, so the committed
/// snapshot can only differ from the tree when someone has added, removed or renamed a
/// project or a service.
///
/// WHAT IT RECORDS: raw names, then a total per group. A renamed or swapped project changes
/// the snapshot even when the total does not, and a counting bug cannot hide behind a total
/// that happens to match.
///
/// WHAT IT DOES NOT PROVE:
/// - that a project builds, or that it is listed in HealthcareSupport.CaseEvaluation.slnx;
/// - that a Compose service starts, or anything about its configuration;
/// - the numbers README.md takes from GitHub rather than from the repository (branch
///   protection, security advisories);
/// - the appointment statuses and the roles, which the appointment-transition and
///   authorization snapshots own.
/// </summary>
public static partial class RepositoryCountsSurface
{
    private const string SolutionFileName = "HealthcareSupport.CaseEvaluation.slnx";

    /// <summary>The Compose files README.md describes, in the order they are rendered.</summary>
    private static readonly string[] ComposeFiles = ["docker-compose.yml", "docker-compose.prod.yml"];

    /// <summary>A service key: exactly two spaces of indent, a name, then a colon.</summary>
    [GeneratedRegex(@"^  ([A-Za-z0-9_.-]+):", RegexOptions.CultureInvariant)]
    private static partial Regex ServiceKey();

    /// <summary>A top-level key, which ends the services block. Comments start with '#'.</summary>
    [GeneratedRegex(@"^[A-Za-z0-9_.-]", RegexOptions.CultureInvariant)]
    private static partial Regex TopLevelKey();

    /// <summary>
    /// The repository root: the nearest directory above this source file that holds the
    /// solution file. Resolved from the source path rather than the test's working directory,
    /// so the test reads the checked-out tree and not a copy under bin/.
    /// </summary>
    public static string RepositoryRoot([CallerFilePath] string sourcePath = "")
    {
        var directory = Path.GetDirectoryName(sourcePath);
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, SolutionFileName)))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException(
            $"No directory above '{sourcePath}' contains {SolutionFileName}, so the repository root " +
            "cannot be found and the repository counts cannot be rendered.");
    }

    /// <summary>
    /// Every .csproj under <paramref name="directory"/>, recursively, named by file stem and
    /// sorted ordinal. Anything under a bin or obj directory is build output and is skipped.
    /// </summary>
    public static IReadOnlyList<string> Projects(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new InvalidOperationException($"'{directory}' does not exist, so its projects cannot be counted.");
        }

        return Directory.EnumerateFiles(directory, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(Path.GetRelativePath(directory, path)))
            .Select(Path.GetFileNameWithoutExtension)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The service names in a Compose file: the keys at exactly two-space indent inside the
    /// top-level services block, up to the next top-level key, sorted ordinal. A line scan
    /// rather than a YAML parser, because the solution references no YAML library. Throws when
    /// the file has no services block, so a moved or rewritten file cannot render as zero.
    /// </summary>
    public static IReadOnlyList<string> ComposeServices(string composeFile)
    {
        if (!File.Exists(composeFile))
        {
            throw new InvalidOperationException($"'{composeFile}' does not exist, so its services cannot be counted.");
        }

        var services = new List<string>();
        var inServices = false;
        var sawServicesBlock = false;
        foreach (var rawLine in File.ReadLines(composeFile))
        {
            var line = rawLine.TrimEnd('\r');
            if (line == "services:")
            {
                inServices = true;
                sawServicesBlock = true;
                continue;
            }

            if (!inServices)
            {
                continue;
            }

            if (TopLevelKey().IsMatch(line))
            {
                break;
            }

            var match = ServiceKey().Match(line);
            if (match.Success)
            {
                services.Add(match.Groups[1].Value);
            }
        }

        if (!sawServicesBlock)
        {
            throw new InvalidOperationException(
                $"'{composeFile}' has no top-level 'services:' block, so its services cannot be counted.");
        }

        return services.OrderBy(name => name, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The full rendering: LF lines in a fixed section order (src projects, test projects, then
    /// each Compose file), sorted within each section, each section closed by its total.
    /// </summary>
    public static string Render()
    {
        var root = RepositoryRoot();
        var output = new StringBuilder();

        AppendSection(output, "src project", "src projects", Projects(Path.Combine(root, "src")));
        AppendSection(output, "test project", "test projects", Projects(Path.Combine(root, "test")));
        foreach (var composeFile in ComposeFiles)
        {
            AppendSection(
                output,
                $"compose service ({composeFile})",
                $"compose services ({composeFile})",
                ComposeServices(Path.Combine(root, composeFile)));
        }

        return output.ToString();
    }

    private static void AppendSection(StringBuilder output, string itemLabel, string totalLabel, IReadOnlyList<string> names)
    {
        foreach (var name in names)
        {
            output.Append(itemLabel).Append(": ").Append(name).Append('\n');
        }

        output.Append(totalLabel).Append(": ")
            .Append(names.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
    }

    private static bool IsBuildOutput(string relativePath)
    {
        return relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj");
    }
}
