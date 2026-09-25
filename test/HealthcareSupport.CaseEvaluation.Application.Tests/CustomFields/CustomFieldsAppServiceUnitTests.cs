using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.CustomFields;

/// <summary>
/// Phase 6 (2026-05-03) -- pure unit tests for the static helpers
/// extracted from <see cref="CustomFieldsAppService"/>. Covers:
///
///   1. <see cref="CustomFieldsAppService.IsAtOrOverCap"/> -- per-
///      AppointmentTypeId active-row cap. Mirrors OLD spec line 543
///      ("Up to 10 fields per appointment type") and corrects two OLD
///      bugs (global count + exact-equals comparison) per the audit
///      doc's OLD-bug-fix exception.
///
///   2. <see cref="CustomFieldsAppService.ComputeNextDisplayOrder"/> --
///      auto-assigned DisplayOrder. Mirrors OLD CustomFieldDomain.cs:55-61.
///
/// These bypass the ABP integration harness because of the pre-existing
/// test-host crash (gated on the ABP Pro license code per
/// docs/handoffs/2026-05-03-test-host-license-blocker.md). Same pattern
/// as Phase 3 SystemParameters and Phase 5 PackageDetails unit tests.
/// </summary>
public class CustomFieldsAppServiceUnitTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    [InlineData(int.MaxValue, true)]
    public void IsAtOrOverCap_ReturnsTrueWhenCountIs10OrMore(int activeCount, bool expected)
    {
        CustomFieldsAppService.IsAtOrOverCap(activeCount).ShouldBe(expected);
    }

    [Fact]
    public void IsAtOrOverCap_BoundaryAt10_BlocksInsert()
    {
        // OLD CustomFieldDomain.cs:40 used `== 10` -- if the bucket ever
        // hit 11 by other paths the check became a no-op. NEW uses `>= 10`
        // so 10, 11, 12, ... all block; only 0..9 admit a new row.
        CustomFieldsAppService.IsAtOrOverCap(activeCount: 9).ShouldBeFalse();
        CustomFieldsAppService.IsAtOrOverCap(activeCount: 10).ShouldBeTrue();
        CustomFieldsAppService.IsAtOrOverCap(activeCount: 11).ShouldBeTrue();
    }

    [Fact]
    public void ComputeNextDisplayOrder_EmptyCatalog_ReturnsOne()
    {
        CustomFieldsAppService.ComputeNextDisplayOrder(currentMax: null).ShouldBe(1);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(9, 10)]
    [InlineData(99, 100)]
    public void ComputeNextDisplayOrder_NonEmptyCatalog_ReturnsMaxPlusOne(int currentMax, int expected)
    {
        CustomFieldsAppService.ComputeNextDisplayOrder(currentMax).ShouldBe(expected);
    }

    [Fact]
    public void ComputeNextDisplayOrder_ZeroMax_StillIncrements()
    {
        // Ensures the `?? 0` fallback path and the `+ 1` increment are
        // both exercised on an exactly-zero current-max input -- guards
        // against an edge where the catalog is non-empty but every row
        // somehow has DisplayOrder = 0 (a NEW data-cleanup scenario, not
        // an OLD path -- OLD always assigns >= 1).
        CustomFieldsAppService.ComputeNextDisplayOrder(currentMax: 0).ShouldBe(1);
    }
}
