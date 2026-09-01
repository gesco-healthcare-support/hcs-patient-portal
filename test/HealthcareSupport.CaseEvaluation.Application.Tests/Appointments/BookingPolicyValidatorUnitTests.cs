using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
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
///
/// 2026-06-02: the max-time horizon is now selected by the stored
/// <see cref="AppointmentMaxTimeCategory"/> on the AppointmentType, not by a
/// display-name substring, so these cases pass the category directly.
/// </summary>
public class BookingPolicyValidatorUnitTests
{
    private static readonly DateTime Today = new(2026, 6, 1);

    private static SystemParameter MakeSystemParameter(int leadTime = 3, int pqme = 90, int ame = 120, int other = 60, int internalMax = 90)
    {
        var sp = (SystemParameter)System.Runtime.CompilerServices
            .RuntimeHelpers.GetUninitializedObject(typeof(SystemParameter));
        sp.AppointmentLeadTime = leadTime;
        sp.AppointmentMaxTimePQME = pqme;
        sp.AppointmentMaxTimeAME = ame;
        sp.AppointmentMaxTimeOTHER = other;
        sp.AppointmentMaxTimeInternal = internalMax;
        return sp;
    }

    [Fact]
    public void EvaluateBookingPolicy_SlotInsideLeadTime_ReturnsInsideLeadTime()
    {
        // lead time = 3, today = 2026-06-01. Slot on 2026-06-03 is one day
        // earlier than the earliest bookable date (2026-06-04).
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 6, 3), Today, AppointmentMaxTimeCategory.Pqme, MakeSystemParameter(leadTime: 3));

        result.Outcome.ShouldBe(BookingPolicyOutcome.InsideLeadTime);
        result.ThresholdDays.ShouldBe(3);
    }

    [Fact]
    public void EvaluateBookingPolicy_SlotExactlyOnLeadTimeBoundary_ReturnsAllowed()
    {
        // lead time = 3, today = 2026-06-01, earliest bookable = 2026-06-04.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 6, 4), Today, AppointmentMaxTimeCategory.Pqme, MakeSystemParameter(leadTime: 3, pqme: 90));

        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
        result.ThresholdDays.ShouldBe(0);
    }

    [Fact]
    public void EvaluateBookingPolicy_PqmeSlotExactlyOnMaxHorizon_ReturnsAllowed()
    {
        // PQME max = 90 days, today = 2026-06-01, latest = 2026-08-30.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 8, 30), Today, AppointmentMaxTimeCategory.Pqme, MakeSystemParameter(pqme: 90));

        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
    }

    [Fact]
    public void EvaluateBookingPolicy_PqmeSlotPastMaxHorizon_ReturnsPastMaxHorizon()
    {
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 8, 31), Today, AppointmentMaxTimeCategory.Pqme, MakeSystemParameter(pqme: 90));

        result.Outcome.ShouldBe(BookingPolicyOutcome.PastMaxHorizon);
        result.ThresholdDays.ShouldBe(90);
    }

    [Fact]
    public void EvaluateBookingPolicy_AmeSlotUsesAMEHorizon()
    {
        // AME = 120 days; a slot at +120 is allowed for an Ame-category type
        // even though it is past the PQME (+90) horizon.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 9, 29), Today, AppointmentMaxTimeCategory.Ame, MakeSystemParameter(ame: 120, pqme: 90));

        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
    }

    [Fact]
    public void EvaluateBookingPolicy_OtherCategoryUsesOtherHorizon()
    {
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 7, 30), Today, AppointmentMaxTimeCategory.Other, MakeSystemParameter(other: 60));

        // 2026-06-01 + 60 days = 2026-07-31; 2026-07-30 is allowed.
        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
    }

    [Fact]
    public void EvaluateBookingPolicy_NullCategoryFallsBackToOtherHorizon()
    {
        // A type with no classification (null) uses the OTHER horizon.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 8, 1), Today, null, MakeSystemParameter(other: 60));

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
            AppointmentMaxTimeCategory.Pqme,
            MakeSystemParameter(leadTime: 3, pqme: 1));

        result.Outcome.ShouldBe(BookingPolicyOutcome.InsideLeadTime);
    }

    [Fact]
    public void EvaluateBookingPolicy_NullSystemParameter_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            BookingPolicyValidator.EvaluateBookingPolicy(
                new DateTime(2026, 6, 4), Today, AppointmentMaxTimeCategory.Pqme, null!));
    }

    [Fact]
    public void EvaluateBookingPolicy_LeadTimeZero_TodayIsAllowed()
    {
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            Today, Today, AppointmentMaxTimeCategory.Pqme, MakeSystemParameter(leadTime: 0, pqme: 90));

        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
    }

    // -- Role-aware horizon (2026-06-11): external = per-type (60), internal = 90 -----------

    [Fact]
    public void EvaluateBookingPolicy_InternalCaller_UsesInternalHorizon_AllowedBeyondPerType()
    {
        // OTHER per-type max = 60; internal max = 90. A slot at +90 days
        // (2026-08-30) is past the external OTHER horizon but within the
        // internal horizon, so an internal caller is allowed.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 8, 30), Today, AppointmentMaxTimeCategory.Other,
            MakeSystemParameter(other: 60, internalMax: 90), isInternalCaller: true);

        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
    }

    [Fact]
    public void EvaluateBookingPolicy_InternalCaller_PastInternalHorizon_ReturnsPastMaxHorizon()
    {
        // Internal max = 90; latest bookable = 2026-08-30. A slot at +91
        // (2026-08-31) is past the absolute ceiling even for internal staff.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 8, 31), Today, AppointmentMaxTimeCategory.Other,
            MakeSystemParameter(other: 60, internalMax: 90), isInternalCaller: true);

        result.Outcome.ShouldBe(BookingPolicyOutcome.PastMaxHorizon);
        result.ThresholdDays.ShouldBe(90);
    }

    [Fact]
    public void EvaluateBookingPolicy_ExternalCaller_CappedByPerTypeNotInternal()
    {
        // Same config as the internal-allowed case, but the external caller
        // stays bound by the OTHER per-type horizon (60) even though the
        // internal horizon (90) would permit it. Slot at +61 (2026-08-01).
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 8, 1), Today, AppointmentMaxTimeCategory.Other,
            MakeSystemParameter(other: 60, internalMax: 90), isInternalCaller: false);

        result.Outcome.ShouldBe(BookingPolicyOutcome.PastMaxHorizon);
        result.ThresholdDays.ShouldBe(60);
    }

    [Fact]
    public void EvaluateBookingPolicy_InternalCaller_OnInternalHorizonBoundary_ReturnsAllowed()
    {
        // Internal max = 90; the boundary day (2026-08-30) is inclusive.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 8, 30), Today, AppointmentMaxTimeCategory.Pqme,
            MakeSystemParameter(pqme: 60, internalMax: 90), isInternalCaller: true);

        result.Outcome.ShouldBe(BookingPolicyOutcome.Allowed);
    }

    [Fact]
    public void EvaluateBookingPolicy_InternalCaller_LeadTimeStillApplies()
    {
        // Lead-time gate fires before the horizon gate regardless of role.
        var result = BookingPolicyValidator.EvaluateBookingPolicy(
            new DateTime(2026, 6, 2), Today, AppointmentMaxTimeCategory.Other,
            MakeSystemParameter(leadTime: 3, other: 60, internalMax: 90), isInternalCaller: true);

        result.Outcome.ShouldBe(BookingPolicyOutcome.InsideLeadTime);
        result.ThresholdDays.ShouldBe(3);
    }
}
