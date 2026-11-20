using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>2026-11-20 -- the arithmetic and the real wait of <see cref="LookupResponseFloor"/>.</summary>
public class LookupResponseFloorTests
{
    [Theory]
    [InlineData(0, 750)]
    [InlineData(100, 650)]
    [InlineData(749, 1)]
    [InlineData(750, 0)]
    [InlineData(5000, 0)]
    public void Remaining_IsTheFloorMinusTheTimeTaken_AndNeverNegative(int elapsedMs, int expectedMs) =>
        LookupResponseFloor.Remaining(TimeSpan.FromMilliseconds(elapsedMs))
            .ShouldBe(TimeSpan.FromMilliseconds(expectedMs));

    [Fact]
    public async Task PadAsync_RightAfterStart_WaitsOutTheFloor()
    {
        var floor = new LookupResponseFloor();
        var watch = Stopwatch.StartNew();

        await floor.PadAsync(floor.Start());

        // Timer resolution can end a delay a little early; anything near the floor proves the wait happened.
        watch.Elapsed.ShouldBeGreaterThanOrEqualTo(LookupResponseFloor.Floor - TimeSpan.FromMilliseconds(50));
    }
}
