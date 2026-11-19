using System;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using HealthcareSupport.CaseEvaluation.RateLimiting;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Controllers.Integration;

/// <summary>The blank forms' and office list's own hourly allowances.</summary>
public class CaseTrackerFormsAllowanceTests
{
    private static readonly Guid OfficeA = new("0ff1ce00-0000-4000-8000-00000000000a");
    private static readonly Guid OfficeB = new("0ff1ce00-0000-4000-8000-00000000000b");

    [Fact]
    public void TheFormAllowance_IsPerOffice_AndRunsOutAtThePermit()
    {
        using var allowance = new CaseTrackerFormsAllowance();
        for (var i = 0; i < CaseTrackerFormsConsts.FormRequestsPerHourPerOffice; i++)
        {
            allowance.TryAcquireForm(OfficeA).ShouldBeTrue();
        }

        allowance.TryAcquireForm(OfficeA).ShouldBeFalse();
        allowance.TryAcquireForm(OfficeB).ShouldBeTrue();
    }

    [Fact]
    public void TheOfficeListAllowance_IsOneBucketForThePortal_AndRunsOutAtThePermit()
    {
        using var allowance = new CaseTrackerFormsAllowance();
        for (var i = 0; i < CaseTrackerFormsConsts.OfficeListRequestsPerHour; i++)
        {
            allowance.TryAcquireOfficeList().ShouldBeTrue();
        }

        allowance.TryAcquireOfficeList().ShouldBeFalse();
    }

    [Fact]
    public void TheTwoAllowances_AreIndependent()
    {
        using var allowance = new CaseTrackerFormsAllowance();
        for (var i = 0; i < CaseTrackerFormsConsts.OfficeListRequestsPerHour; i++)
        {
            allowance.TryAcquireOfficeList();
        }

        allowance.TryAcquireForm(OfficeA).ShouldBeTrue();
    }

    [Fact]
    public void Dispose_CanBeCalledMoreThanOnce()
    {
        var allowance = new CaseTrackerFormsAllowance();

        allowance.Dispose();

        Should.NotThrow(allowance.Dispose);
    }
}
