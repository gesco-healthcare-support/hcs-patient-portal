using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

/// <summary>
/// The two zero-returning guards of <see cref="DoctorAvailabilitiesAppService.EstimateSlotCount"/>,
/// the estimate the 5,000-slot cap is checked against. Generation validation refuses both inputs
/// before the estimate runs, so only a direct call reaches them; they still decide what the cap
/// sees if validation ever changes order.
/// </summary>
public class DoctorAvailabilitySlotEstimateGuardTests
{
    [Fact]
    public void EstimateSlotCount_WithNoTimeRanges_IsZero()
    {
        var input = Input();
        input.TimeRanges = new List<TimeRangeDto>();

        DoctorAvailabilitiesAppService.EstimateSlotCount(input).ShouldBe(0);
    }

    [Fact]
    public void EstimateSlotCount_CountsNothingForARangeWithANonPositiveDuration_ButCountsTheOthers()
    {
        var input = Input();
        input.TimeRanges.Add(new TimeRangeDto { FromTime = new TimeOnly(13, 0), ToTime = new TimeOnly(15, 0), AppointmentDurationMinutes = 0 });

        // 2 picked days x (3 one-hour morning slots + 0 from the zero-duration range) = 6.
        DoctorAvailabilitiesAppService.EstimateSlotCount(input).ShouldBe(6);
    }

    private static DoctorAvailabilityGenerateInputDto Input() => new()
    {
        SelectedDates = new List<DateOnly> { new(2031, 3, 3), new(2031, 3, 4) },
        TimeRanges = new List<TimeRangeDto> { new() { FromTime = new TimeOnly(9, 0), ToTime = new TimeOnly(12, 0) } },
        AppointmentDurationMinutes = 60,
    };
}
