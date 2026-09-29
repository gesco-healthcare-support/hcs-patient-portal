using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using HealthcareSupport.CaseEvaluation.Snapshots;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// The approved tenancy surface: which Domain entities implement IMultiTenant, and which of the
/// two EF Core models maps each one (see <see cref="TenancySurface"/>).
///
/// Any difference fails until a human regenerates the committed file IN THE SAME PULL REQUEST,
/// so moving an entity between the host and the office databases, or adding or dropping its
/// office scope, always appears in a diff. It is the source docs/architecture/MULTI-TENANCY.md
/// is meant to take its entity classification from, instead of a hand-kept list.
///
/// It does NOT prove that office isolation works; see the docstring on TenancySurface.
/// </summary>
public sealed class TenancySurfaceSnapshotTests
{
    private const string ApprovedFileName = "tenancy-surface.approved.txt";

    private static readonly ApprovedSnapshotWording Wording = new(
        Headline: "The tenancy surface changed.",
        IssueReference: null,
        RemovedMeaning: "an entity was removed or renamed, or changed its office scope or its database");

    [Fact]
    public void Tenancy_surface_matches_the_approved_snapshot()
    {
        ApprovedSnapshot.AssertMatches(
            TenancySurface.Render(), Path.Combine(ThisDirectory(), ApprovedFileName), Wording);
    }

    /// <summary>
    /// The surface has to contain something on every axis. If model building silently returned
    /// nothing, every row would read "absent" for both databases, match a regenerated approved
    /// file, and leave a green gate that checks nothing.
    /// </summary>
    [Fact]
    public void Snapshot_covers_a_plausible_number_of_entities()
    {
        var host = TenancySurface.HostModelTypes();
        var office = TenancySurface.OfficeModelTypes();

        TenancySurface.Entities(TenancySurface.DomainAssembly, host, office).Count.ShouldBeGreaterThan(40,
            "the Domain project is known to declare dozens of entities; a much smaller number means " +
            "the reflection filter stopped matching them.");
        host.Count.ShouldBeGreaterThan(40, "the host model mapped almost no Domain entities.");
        office.Count.ShouldBeGreaterThan(40, "the office model mapped almost no Domain entities.");
    }

    /// <summary>
    /// The database columns must come from the models, never from the interface.
    ///
    /// Both fixtures lack IMultiTenant, and they live in different places: DocumentPackage, a join
    /// entity, is in both databases, while OfficeBranding is in the host database only. A
    /// generator that read "host-only" off the missing interface would render DocumentPackage as
    /// absent from the office database. That is the stale page's error, the one this surface
    /// exists to prevent.
    /// </summary>
    [Fact]
    public void Model_membership_is_observed_not_derived_from_IMultiTenant()
    {
        var lines = TenancySurface.Render().Split('\n');

        LineFor(lines, "PackageDetails.DocumentPackage").ShouldEndWith(
            " -> IMultiTenant=no CaseEvaluationDbContext=mapped CaseEvaluationTenantDbContext=mapped");
        LineFor(lines, "Branding.OfficeBranding").ShouldEndWith(
            " -> IMultiTenant=no CaseEvaluationDbContext=mapped CaseEvaluationTenantDbContext=absent");
    }

    /// <summary>
    /// Rendering twice in one process must produce identical text, so the gate fails when
    /// something changed rather than at random.
    /// </summary>
    [Fact]
    public void Render_is_deterministic()
    {
        TenancySurface.Render().ShouldBe(TenancySurface.Render());
    }

    /// <summary>
    /// The approved file must be stored with LF endings, or the comparison fails on Windows for a
    /// reason that looks nothing like its cause.
    /// </summary>
    [Fact]
    public void Approved_snapshot_is_stored_with_lf_endings()
    {
        var bytes = File.ReadAllBytes(Path.Combine(ThisDirectory(), ApprovedFileName));

        bytes.Count(b => b == (byte)'\r').ShouldBe(0,
            $"{ApprovedFileName} contains CR bytes. It must be LF-only; check that " +
            ".gitattributes is not normalising it on checkout.");
    }

    private static string LineFor(string[] lines, string namespaceAndType)
    {
        var line = lines.SingleOrDefault(l => l.StartsWith(
            "HealthcareSupport.CaseEvaluation." + namespaceAndType + " ", StringComparison.Ordinal));

        line.ShouldNotBeNull(
            $"the fixture entity {namespaceAndType} is gone; point this test at another entity " +
            "with the same shape.");

        return line;
    }

    /// <summary>
    /// The directory holding this source file, so the path is the file a developer must COMMIT,
    /// not a build artefact the next build overwrites.
    /// </summary>
    private static string ThisDirectory([CallerFilePath] string path = "")
    {
        return Path.GetDirectoryName(path)!;
    }
}
