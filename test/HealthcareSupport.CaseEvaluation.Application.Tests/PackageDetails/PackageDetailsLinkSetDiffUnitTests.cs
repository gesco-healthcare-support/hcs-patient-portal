using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.PackageDetails;

/// <summary>
/// Phase 5 (2026-05-03) -- pure unit tests for
/// <see cref="PackageDetailsAppService.ComputeLinkSetDiff"/>. The diff
/// between persisted and desired link sets drives Link / Unlink semantics
/// for IT Admin's package-document linking, so it is the highest-value
/// piece of business logic to test in isolation.
///
/// These tests deliberately bypass the ABP integration harness because of
/// the pre-existing test-host crash (CaseEvaluationEntityFrameworkCoreTestModule
/// path; documented in <c>memory/project_two-session-split.md</c>). The
/// approach matches the Phase 3 SystemParameters validator unit tests.
/// </summary>
public class PackageDetailsLinkSetDiffUnitTests
{
    private static readonly Guid PackageId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Doc1 = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid Doc2 = Guid.Parse("a0000001-0000-0000-0000-000000000002");
    private static readonly Guid Doc3 = Guid.Parse("a0000001-0000-0000-0000-000000000003");

    [Fact]
    public void ComputeLinkSetDiff_NoExisting_AddsAllDesired()
    {
        var (toAdd, toRemove) = PackageDetailsAppService.ComputeLinkSetDiff(
            PackageId,
            existing: Array.Empty<DocumentPackage>(),
            desiredDocumentIds: new[] { Doc1, Doc2, Doc3 });

        toRemove.ShouldBeEmpty();
        toAdd.Count.ShouldBe(3);
        toAdd.Select(x => x.DocumentId).ShouldBe(new[] { Doc1, Doc2, Doc3 }, ignoreOrder: true);
        toAdd.ShouldAllBe(x => x.PackageDetailId == PackageId);
        toAdd.ShouldAllBe(x => x.IsActive);
    }

    [Fact]
    public void ComputeLinkSetDiff_DesiredEmpty_RemovesAllExisting()
    {
        var existing = new[]
        {
            new DocumentPackage(PackageId, Doc1),
            new DocumentPackage(PackageId, Doc2),
        };

        var (toAdd, toRemove) = PackageDetailsAppService.ComputeLinkSetDiff(
            PackageId,
            existing,
            desiredDocumentIds: Array.Empty<Guid>());

        toAdd.ShouldBeEmpty();
        toRemove.Count.ShouldBe(2);
        toRemove.Select(x => x.DocumentId).ShouldBe(new[] { Doc1, Doc2 }, ignoreOrder: true);
    }

    [Fact]
    public void ComputeLinkSetDiff_PartialOverlap_AddsOnlyMissingAndRemovesOnlyExtra()
    {
        var existing = new[]
        {
            new DocumentPackage(PackageId, Doc1),
            new DocumentPackage(PackageId, Doc2),
        };

        var (toAdd, toRemove) = PackageDetailsAppService.ComputeLinkSetDiff(
            PackageId,
            existing,
            desiredDocumentIds: new[] { Doc2, Doc3 });

        toAdd.Count.ShouldBe(1);
        toAdd[0].DocumentId.ShouldBe(Doc3);

        toRemove.Count.ShouldBe(1);
        toRemove[0].DocumentId.ShouldBe(Doc1);
    }

    [Fact]
    public void ComputeLinkSetDiff_IdenticalSets_AreIdempotent()
    {
        var existing = new[]
        {
            new DocumentPackage(PackageId, Doc1),
            new DocumentPackage(PackageId, Doc2),
        };

        var (toAdd, toRemove) = PackageDetailsAppService.ComputeLinkSetDiff(
            PackageId,
            existing,
            desiredDocumentIds: new[] { Doc1, Doc2 });

        toAdd.ShouldBeEmpty();
        toRemove.ShouldBeEmpty();
    }

    [Fact]
    public void ComputeLinkSetDiff_DesiredHasDuplicates_DeduplicatesBeforeAdding()
    {
        var (toAdd, toRemove) = PackageDetailsAppService.ComputeLinkSetDiff(
            PackageId,
            existing: Array.Empty<DocumentPackage>(),
            desiredDocumentIds: new[] { Doc1, Doc1, Doc2, Doc1 });

        toRemove.ShouldBeEmpty();
        toAdd.Count.ShouldBe(2);
        toAdd.Select(x => x.DocumentId).ShouldBe(new[] { Doc1, Doc2 }, ignoreOrder: true);
    }

    [Fact]
    public void ComputeLinkSetDiff_NewLinks_HaveTargetPackageIdAndIsActiveTrue()
    {
        var (toAdd, _) = PackageDetailsAppService.ComputeLinkSetDiff(
            PackageId,
            existing: Array.Empty<DocumentPackage>(),
            desiredDocumentIds: new[] { Doc1 });

        toAdd[0].PackageDetailId.ShouldBe(PackageId);
        toAdd[0].DocumentId.ShouldBe(Doc1);
        toAdd[0].IsActive.ShouldBeTrue();
    }
}
