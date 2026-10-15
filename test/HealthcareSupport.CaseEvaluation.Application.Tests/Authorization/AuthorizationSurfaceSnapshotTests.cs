using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using HealthcareSupport.CaseEvaluation.Snapshots;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Authorization;

/// <summary>
/// #707 layer 1 -- the approved authorization-surface snapshot.
///
/// Reflects every (service, method, required permission) triple into a sorted table and
/// compares it against a committed file. Any difference fails until a human regenerates
/// the committed file IN THE SAME PULL REQUEST, so every authorization change appears in
/// a diff and has to be consciously accepted. That is the mechanism the issue asks for:
/// not "permissions are correct today" but "any change is always intended".
///
/// Catches deletion, a renamed or mistyped permission, accidental widening, and a new
/// endpoint added carrying no permission at all.
///
/// It deliberately does NOT prove that a check is enforced -- both harnesses call
/// AddAlwaysAllowAuthorization(), so nothing here could. That is layer 3's job, and
/// layer 2 (<see cref="AuthorizationSurfaceInvariantTests"/>) is what stops this snapshot
/// being regenerated carelessly to make a build pass.
/// </summary>
public sealed class AuthorizationSurfaceSnapshotTests
{
    private const string ApprovedFileName = "authorization-surface.approved.txt";

    private static readonly ApprovedSnapshotWording Wording = new(
        Headline: "The authorization surface changed.",
        IssueReference: "#707",
        RemovedMeaning: "a permission was removed, renamed, or weakened");

    [Fact]
    public void Authorization_surface_matches_the_approved_snapshot()
    {
        var actual = AuthorizationSurface.Render(
            typeof(CaseEvaluationApplicationModule).Assembly);

        ApprovedSnapshot.AssertMatches(
            actual, Path.Combine(ThisDirectory(), ApprovedFileName), Wording);
    }

    /// <summary>
    /// The snapshot has to actually contain something. Without this, a reflection bug
    /// that returned zero services would render an empty string, match an emptied
    /// approved file, and leave a permanently green gate that checks nothing -- the same
    /// defect shape as the markdownlint job that reported "0 file(s) / 0 error(s)" while
    /// looking green.
    /// </summary>
    [Fact]
    public void Snapshot_covers_a_plausible_number_of_services_and_permissions()
    {
        var assembly = typeof(CaseEvaluationApplicationModule).Assembly;

        var services = AuthorizationSurface.Services(assembly);
        services.Count.ShouldBeGreaterThan(30,
            "the Application assembly is known to carry dozens of app services; a much " +
            "smaller number means the reflection filter stopped matching them.");

        var rendered = AuthorizationSurface.Render(assembly);
        rendered.ShouldContain("CaseEvaluation.",
            customMessage: "no permission names rendered at all.");
    }

    /// <summary>
    /// A class-level permission must still be recorded on a method that carries its own.
    ///
    /// This is a regression test for a bug in this gate's first draft, which treated a
    /// method-level [Authorize] as REPLACING the class-level one. ASP.NET Core and ABP
    /// AND them together, so that draft rendered
    /// AppointmentTypesAppService.CreateAsync as requiring only
    /// AppointmentTypes.Create. Deleting the class-level [Authorize(AppointmentTypes)]
    /// would then have changed nothing in three of that service's five lines, and the
    /// gate would have sat silent through exactly the edit it exists to catch.
    ///
    /// AppointmentTypesAppService is used as the fixture because it has the shape in its
    /// plainest form: a Default permission on the class and Create/Edit/Delete on the
    /// methods.
    /// </summary>
    [Fact]
    public void Class_level_permission_is_recorded_alongside_a_method_level_one()
    {
        var rendered = AuthorizationSurface.Render(
            typeof(CaseEvaluationApplicationModule).Assembly);

        var line = rendered
            .Split('\n')
            .SingleOrDefault(l => l.Contains(
                "AppointmentTypesAppService.CreateAsync(", StringComparison.Ordinal));

        line.ShouldNotBeNull(
            "the fixture method is gone; point this test at another service that carries " +
            "a class-level permission plus method-level ones.");

        line.ShouldContain("class=CaseEvaluation.AppointmentTypes ",
            customMessage: "the class-level permission was dropped from the rendered line.");
        line.ShouldContain("method=CaseEvaluation.AppointmentTypes.Create",
            customMessage: "the method-level permission was dropped from the rendered line.");
    }

    /// <summary>
    /// Rendering twice in one process must produce identical text. Guards the ordering
    /// being accidentally left to reflection order, which is not specified and can differ
    /// between runtimes -- that would make the gate fail at random rather than when
    /// something changed.
    /// </summary>
    [Fact]
    public void Render_is_deterministic()
    {
        var assembly = typeof(CaseEvaluationApplicationModule).Assembly;

        AuthorizationSurface.Render(assembly)
            .ShouldBe(AuthorizationSurface.Render(assembly));
    }

    /// <summary>
    /// The approved file must be stored with LF endings. If it is ever committed with
    /// CRLF, the comparison above fails on every machine for a reason that looks nothing
    /// like its cause.
    /// </summary>
    [Fact]
    public void Approved_snapshot_is_stored_with_lf_endings()
    {
        var approvedPath = Path.Combine(ThisDirectory(), ApprovedFileName);
        var bytes = File.ReadAllBytes(approvedPath);

        var carriageReturns = 0;
        foreach (var b in bytes)
        {
            if (b == (byte)'\r')
            {
                carriageReturns++;
            }
        }

        carriageReturns.ShouldBe(0,
            $"{ApprovedFileName} contains CR bytes. It must be LF-only; check that " +
            ".gitattributes is not normalising it on checkout.");
    }

    /// <summary>
    /// The directory holding this source file, resolved at compile time. Used instead of
    /// copying the approved file to the output directory, because a developer updating
    /// the snapshot needs the path to the file they must COMMIT, not to a build artefact
    /// that the next build overwrites.
    /// </summary>
    private static string ThisDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
