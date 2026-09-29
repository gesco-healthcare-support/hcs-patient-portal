using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The approved appointment-transition snapshot.
///
/// Renders every transition declared by AppointmentManager.BuildMachine and compares it with
/// a committed file. Any difference fails until a human regenerates the committed file IN THE
/// SAME PULL REQUEST, so every change to the lifecycle appears in a diff and has to be
/// consciously accepted. Mirrors AuthorizationSurfaceSnapshotTests in Application.Tests.
///
/// It proves the DECLARATION has not changed silently. It does not prove any transition is
/// reachable or correctly gated; see the <see cref="AppointmentTransitionSurface"/> docstring.
/// </summary>
public sealed class AppointmentTransitionSurfaceSnapshotTests
{
    private const string ApprovedFileName = "appointment-transitions.approved.txt";
    private const string ReceivedFileName = "appointment-transitions.received.txt";

    [Fact]
    public void Appointment_transitions_match_the_approved_snapshot()
    {
        var actual = AppointmentTransitionSurface.Render();

        var approvedPath = Path.Combine(ThisDirectory(), ApprovedFileName);
        if (!File.Exists(approvedPath))
        {
            WriteReceived(actual);
            throw new ShouldAssertException(
                $"The approved snapshot is missing at {approvedPath}. It is the gate; without it " +
                "there is nothing to compare against. The current rendering was written to " +
                ReceivedFileName + " beside it for review.");
        }

        // Read without newline translation, so a file committed with CRLF fails loudly here
        // rather than comparing equal on Windows and unequal in CI.
        var approved = File.ReadAllText(approvedPath, Encoding.UTF8);

        if (!string.Equals(approved, actual, StringComparison.Ordinal))
        {
            var receivedPath = WriteReceived(actual);
            throw new ShouldAssertException(BuildFailureMessage(approved, actual, approvedPath, receivedPath));
        }
    }

    /// <summary>
    /// The snapshot has to contain something. A reflection or rendering bug that produced no
    /// transitions would otherwise match an emptied approved file and leave a green gate that
    /// checks nothing.
    /// </summary>
    [Fact]
    public void Snapshot_covers_a_plausible_number_of_transitions()
    {
        var rendered = AppointmentTransitionSurface.Render();

        rendered.Split('\n').Count(l => l.StartsWith("transition ", StringComparison.Ordinal))
            .ShouldBeGreaterThanOrEqualTo(15,
                "BuildMachine is known to declare well over a dozen transitions; far fewer means " +
                "the rendering stopped reading them.");

        rendered.ShouldContain("transition Pending(1) --Approve(1)--> Approved(2)\n",
            customMessage: "the most basic transition is missing from the rendering.");
    }

    /// <summary>
    /// Rendering twice in one process must produce identical text, so the gate fails only when
    /// something changed and never because of enumeration order.
    /// </summary>
    [Fact]
    public void Render_is_deterministic()
    {
        AppointmentTransitionSurface.Render()
            .ShouldBe(AppointmentTransitionSurface.Render());
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

    private static string WriteReceived(string actual)
    {
        var receivedPath = Path.Combine(ThisDirectory(), ReceivedFileName);
        File.WriteAllText(receivedPath, actual, new UTF8Encoding(false));
        return receivedPath;
    }

    private static string BuildFailureMessage(string approved, string actual, string approvedPath, string receivedPath)
    {
        var approvedLines = approved.Split('\n');
        var actualLines = actual.Split('\n');
        var approvedSet = new HashSet<string>(approvedLines, StringComparer.Ordinal);
        var actualSet = new HashSet<string>(actualLines, StringComparer.Ordinal);

        var removed = new StringBuilder();
        foreach (var line in approvedLines.Where(l => l.Length > 0 && !actualSet.Contains(l)))
        {
            removed.Append("  - ").Append(line).Append('\n');
        }

        var added = new StringBuilder();
        foreach (var line in actualLines.Where(l => l.Length > 0 && !approvedSet.Contains(l)))
        {
            added.Append("  + ").Append(line).Append('\n');
        }

        return
            "The appointment transitions changed.\n\n" +
            "This is not necessarily a bug -- it is the gate asking you to confirm the change was\n" +
            "intended. Read the lines below. If every one is a change you meant to make, copy the\n" +
            "received file over the approved file and commit it IN THIS SAME PULL REQUEST, and\n" +
            "update docs/business-domain/APPOINTMENT-LIFECYCLE.md to match:\n\n" +
            $"  cp \"{receivedPath}\" \"{approvedPath}\"\n\n" +
            "Gone from the approved surface:\n" +
            (removed.Length == 0 ? "  (none)\n" : removed.ToString()) +
            "\nNew in the actual surface:\n" +
            (added.Length == 0 ? "  (none)\n" : added.ToString());
    }

    /// <summary>
    /// The directory holding this source file, resolved at compile time, so a developer
    /// updating the snapshot is pointed at the file they must commit rather than at a build
    /// artefact.
    /// </summary>
    private static string ThisDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
