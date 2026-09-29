using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using HealthcareSupport.CaseEvaluation.Snapshots;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// The approved appointment-transition snapshot.
///
/// Renders every transition declared by AppointmentManager.BuildMachine and compares it with
/// a committed file through the shared <see cref="ApprovedSnapshot"/> gate. Any difference fails
/// until a human regenerates the committed file IN THE SAME PULL REQUEST, so every change to the
/// lifecycle appears in a diff and has to be consciously accepted.
///
/// It proves the DECLARATION has not changed silently. It does not prove any transition is
/// reachable or correctly gated; see the <see cref="AppointmentTransitionSurface"/> docstring.
/// </summary>
public sealed class AppointmentTransitionSurfaceSnapshotTests
{
    private const string ApprovedFileName = "appointment-transitions.approved.txt";

    private static readonly ApprovedSnapshotWording Wording = new(
        Headline: "The appointment transitions changed. Update docs/business-domain/APPOINTMENT-LIFECYCLE.md to match.",
        IssueReference: null,
        RemovedMeaning: "a transition, status or trigger was removed, renamed or renumbered");

    [Fact]
    public void Appointment_transitions_match_the_approved_snapshot()
    {
        ApprovedSnapshot.AssertMatches(
            AppointmentTransitionSurface.Render(),
            Path.Combine(ThisDirectory(), ApprovedFileName),
            Wording);
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

    /// <summary>
    /// The directory holding this source file, resolved at compile time, so the path handed to the
    /// gate is the approved file a developer must commit rather than a build artefact.
    /// </summary>
    private static string ThisDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
