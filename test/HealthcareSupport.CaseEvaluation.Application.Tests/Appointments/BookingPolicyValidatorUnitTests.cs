using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11b (2026-05-04) -- pure unit tests for
/// <see cref="BookingPolicyValidator.EvaluateBookingPolicy"/>. Covers the
/// lead-time + max-time gate orchestration without IO.
///
/// Bypasses the ABP integration harness (gated on the ABP Pro license
/// blocker per docs/handoffs/2026-05-03-test-host-license-blocker.md).
/// Tests focus on the pure helper; the async <c>ValidateAsync</c> wrapper
/// that fetches SystemParameter + AppointmentType is integration-level.
/// </summary>
public class BookingPolicyValidatorUnitTests
{
    private static readonly DateTime Today = new(2026, 6, 1);

    private static SystemParameter MakeSystemParameter(int leadTime = 3, int pqme = 90, int ame = 120, int other = 60)
    {
        var sp = (SystemParameter)System.Runtime.CompilerServices
            .RuntimeHelpers.GetUninitializedObject(typeof(SystemParameter));
        sp.AppointmentLeadTime = leadTime;
        sp.AppointmentMaxTimePQME = pqme;
        sp.AppointmentMaxTimeAME = ame;
        sp.AppointmentMaxTimeOTHER = other;
        return sp;
    }

    [Fact]
    public void EvaluateBookingPolicy_SlotInsideLeadTime_ReturnsInsideLeadTime()
    {
        // lead time = 3, today = 2026-06-01. Slot on 2026-06-03 is one day
        // earlier than the earliest bookable date (2026-06-04).
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 6, 3), Today, "PQME", MakeSystemParameter(leadTime: 3));

        result.Outcome.ShouldBe(BookingPolicyOutcome.InsideLeadTime);
        result.ThresholdDays.ShouldBe(3);
    }

    [Fact]
    public void EvaluateBookingPolicy_SlotExactlyOnLeadTimeBoundary_ReturnsAllowed()
    {
        // lead time = 3, today = 2026-06-01, earliest bookable = 2026-06-04.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 6, 4), Today, "PQME", MakeSystemParameter(leadTime: 3, pqme: 90));

        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
        result.ThresholdDays.ShouldBe(0);
    }

    [Fact]
    public void EvaluateBookingPolicy_PQMESlotExactlyOnMaxHorizon_ReturnsAllowed()
    {
        // PQME max = 90 days, today = 2026-06-01, latest = 2026-08-30.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 8, 30), Today, "PQME", MakeSystemParameter(pqme: 90));

        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
    }

    [Fact]
    public void EvaluateBookingPolicy_PQMESlotPastMaxHorizon_ReturnsPastMaxHorizon()
    {
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 8, 31), Today, "PQME", MakeSystemParameter(pqme: 90));

        result.Outcome.ShouldBe(BookingPolicyOutcome.PastMaxHorizon);
        result.ThresholdDays.ShouldBe(90);
    }

    [Fact]
    public void EvaluateBookingPolicy_AMESlotUsesAMEHorizon()
    {
        // AME = 120 days; PQME boundary at +90 should still be allowed for
        // an AME slot because the resolver routes to AppointmentMaxTimeAME.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 9, 29), Today, "AME", MakeSystemParameter(ame: 120, pqme: 90));

        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
    }

    [Fact]
    public void EvaluateBookingPolicy_AMERevalRoutesToAMEHorizon()
    {
        // AME-REVAL contains both "AME" and the prefix "AME-REVAL"; resolver
        // must route to AppointmentMaxTimeAME, not AppointmentMaxTimePQME.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 9, 29), Today, "AME-REVAL", MakeSystemParameter(ame: 120, pqme: 90));

        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
    }

    [Fact]
    public void EvaluateBookingPolicy_OtherTypeUsesOtherHorizon()
    {
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 7, 30), Today, "Whatever", MakeSystemParameter(other: 60));

        // 2026-06-01 + 60 days = 2026-07-31; 2026-07-30 is allowed.
        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
    }

    [Fact]
    public void EvaluateBookingPolicy_OtherTypePastHorizon_ReturnsPastMaxHorizon()
    {
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 8, 1), Today, "Whatever", MakeSystemParameter(other: 60));

        result.Outcome.ShouldBe(BookingPolicyOutcome.PastMaxHorizon);
        result.ThresholdDays.ShouldBe(60);
    }

    [Fact]
    public void EvaluateBookingPolicy_LeadTimeFailsBeforeMaxTime()
    {
        // Slot is BOTH inside lead time AND past max horizon (impossible
        // with sensible config but testable). Lead-time check fires first
        // because it's the more user-actionable error.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 5, 15), // before today
            Today,
            "PQME",
            MakeSystemParameter(leadTime: 3, pqme: 1));

        result.Outcome.ShouldBe(BookingPolicyOutcome.InsideLeadTime);
    }

    [Fact]
    public void EvaluateBookingPolicy_NullSystemParameter_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            BookingPolicyValidator.EvaluateBookingPolicy(
                new DateTime(2026, 6, 4), Today, "PQME", null!));
    }

    [Fact]
    public void EvaluateBookingPolicy_LeadTimeZero_TodayIsAllowed()
    {
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            Today, Today, "PQME", MakeSystemParameter(leadTime: 0, pqme: 90));

        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
    }
}
