using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using HealthcareSupport.CaseEvaluation.Snapshots;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Repository;

/// <summary>
/// The approved repository-counts snapshot.
///
/// Renders the projects under src/ and test/ and the services in both Compose files, and
/// compares the result with a committed file through the shared <see cref="ApprovedSnapshot"/>
/// gate. Any difference fails until a human regenerates the committed file IN THE SAME PULL
/// REQUEST, so adding, removing or renaming a project or a service appears in a diff next to
/// the README.md counts it affects.
///
/// It proves the names and totals have not changed silently. It does not prove README.md
/// states them correctly; see the <see cref="RepositoryCountsSurface"/> docstring.
/// </summary>
public sealed class RepositoryCountsSurfaceSnapshotTests
{
    private const string ApprovedFileName = "repository-counts.approved.txt";

    private static readonly ApprovedSnapshotWording Wording = new(
        Headline: "The repository counts changed. Update the project and service counts in README.md to match.",
        IssueReference: null,
        RemovedMeaning: "a project or a Compose service was removed or renamed");

    [Fact]
    public void Repository_counts_match_the_approved_snapshot()
    {
        ApprovedSnapshot.AssertMatches(
            RepositoryCountsSurface.Render(),
            Path.Combine(ThisDirectory(), ApprovedFileName),
            Wording);
    }

    /// <summary>
    /// The snapshot has to contain something. A path or scanning bug that found nothing would
    /// otherwise match an emptied approved file and leave a green gate that checks nothing.
    /// </summary>
    [Fact]
    public void Snapshot_covers_a_plausible_number_of_projects_and_services()
    {
        var lines = RepositoryCountsSurface.Render().Split('\n');

        CountOf(lines, "src project: ").ShouldBeGreaterThanOrEqualTo(8,
            "src/ is known to hold about ten projects; far fewer means the scan stopped finding them.");
        CountOf(lines, "test project: ").ShouldBeGreaterThanOrEqualTo(3,
            "test/ is known to hold several projects; far fewer means the scan stopped finding them.");
        CountOf(lines, "compose service (docker-compose.yml): ").ShouldBeGreaterThanOrEqualTo(5,
            "the local stack is known to run several services; far fewer means the scan stopped reading them.");

        lines.ShouldContain("src project: HealthcareSupport.CaseEvaluation.Domain",
            "the Domain project is missing from the rendering.");
    }

    /// <summary>
    /// Rendering twice in one process must produce identical text, so the gate fails only when
    /// something changed and never because of file-system enumeration order.
    /// </summary>
    [Fact]
    public void Render_is_deterministic()
    {
        RepositoryCountsSurface.Render()
            .ShouldBe(RepositoryCountsSurface.Render());
    }

    /// <summary>
    /// The approved file must be stored with LF endings; see the matching line in
    /// .gitattributes. A CRLF copy would fail the comparison for a reason that looks nothing
    /// like its cause.
    /// </summary>
    [Fact]
    public void Approved_snapshot_is_stored_with_lf_endings()
    {
        var approvedPath = Path.Combine(ThisDirectory(), ApprovedFileName);
        File.Exists(approvedPath).ShouldBeTrue($"{ApprovedFileName} is missing.");

        File.ReadAllBytes(approvedPath).Count(b => b == (byte)'\r').ShouldBe(0,
            $"{ApprovedFileName} contains CR bytes. It must be LF-only; check that .gitattributes " +
            "pins it to eol=lf.");
    }

    private static int CountOf(string[] lines, string prefix)
    {
        return lines.Count(line => line.StartsWith(prefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// The directory holding this source file, resolved at compile time, so the path handed to the
    /// gate is the approved file a developer must commit rather than a build artefact.
    /// </summary>
    private static string ThisDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
