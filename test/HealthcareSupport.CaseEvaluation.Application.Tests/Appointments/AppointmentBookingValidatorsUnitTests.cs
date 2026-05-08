using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11a (2026-05-04) -- pure unit tests for
/// <see cref="AppointmentBookingValidators"/>. Covers the helpers that
/// the Phase 11b <c>AppointmentManager.CreateAsync</c> rewrite will
/// consume. Bypasses the ABP integration harness because of the
/// pre-existing test-host blocker (license-gated; see
/// docs/handoffs/2026-05-03-test-host-license-blocker.md).
///
/// Coverage:
///   1. Confirmation-number formatter -- 5-digit zero pad, single 'A'.
///   2. Lead-time + max-time gates -- boundary at today / today+lead /
///      today+max.
///   3. Per-type max-time resolver -- PQME / PQME-REVAL / AME /
///      AME-REVAL / OTHER routing.
///   4. 3-of-6 dedup field counter -- each of the six fields counted
///      independently; case-insensitive; nulls do not match nulls.
///   5. <c>IsPatientDuplicate</c> threshold check at the OLD default of 3.
/// </summary>
public class AppointmentBookingValidatorsUnitTests
{
    // ------------------------------------------------------------------
    // Confirmation-number formatter
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(1, "A00001")]
    [InlineData(42, "A00042")]
    [InlineData(99999, "A99999")]
    [InlineData(100000, "A100000")] // overflow past 5 digits still works (single A prefix)
    [InlineData(0, "A00000")]
    public void FormatConfirmationNumber_PrependsAAnd5DigitPad(int sequenceNumber, string expected)
    {
        AppointmentBookingValidators.FormatConfirmationNumber(sequenceNumber).ShouldBe(expected);
    }

    // ------------------------------------------------------------------
    // Lead-time gate
    // ------------------------------------------------------------------

    private static readonly DateTime Today = new(2026, 6, 1);

    [Fact]
    public void IsSlotWithinLeadTime_SlotExactlyOnLeadTimeBoundary_True()
    {
        // lead time = 3 days; today + 3 = 2026-06-04. A slot on 2026-06-04
        // is exactly at the earliest bookable date and SHOULD be allowed.
        AppointmentBookingValidators
            .IsSlotWithinLeadTime(new DateTime(2026, 6, 4), Today, leadTimeDays: 3)
            .ShouldBeTrue();
    }

    [Fact]
    public void IsSlotWithinLeadTime_SlotOneDayBeforeBoundary_False()
    {
        AppointmentBookingValidators
            .IsSlotWithinLeadTime(new DateTime(2026, 6, 3), Today, leadTimeDays: 3)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsSlotWithinLeadTime_LeadTimeZero_TodayIsValid()
    {
        AppointmentBookingValidators
            .IsSlotWithinLeadTime(Today, Today, leadTimeDays: 0)
            .ShouldBeTrue();
    }

    // ------------------------------------------------------------------
    // Max-time gate
    // ------------------------------------------------------------------

    [Fact]
    public void IsSlotWithinMaxTime_SlotExactlyOnHorizon_True()
    {
        AppointmentBookingValidators
            .IsSlotWithinMaxTime(new DateTime(2026, 8, 30), Today, maxTimeDays: 90)
            .ShouldBeTrue();
    }

    [Fact]
    public void IsSlotWithinMaxTime_SlotOneDayPast_False()
    {
        AppointmentBookingValidators
            .IsSlotWithinMaxTime(new DateTime(2026, 8, 31), Today, maxTimeDays: 90)
            .ShouldBeFalse();
    }

    // ------------------------------------------------------------------
    // Per-type max-time resolver
    // ------------------------------------------------------------------

    private static SystemParameter MakeSystemParameter()
    {
        // Test-only construction avoiding ABP create-via-manager / ctor
        // patterns. RuntimeHelpers.GetUninitializedObject is the supported
        // alternative to the obsolete FormatterServices API on .NET 10
        // (https://learn.microsoft.com/dotnet/api/system.runtime.compilerservices.runtimehelpers.getuninitializedobject).
        // The relevant fields are public-virtual so direct assignment works
        // on a fresh instance.
        var sp = (SystemParameter)System.Runtime.CompilerServices
            .RuntimeHelpers.GetUninitializedObject(typeof(SystemParameter));
        sp.AppointmentMaxTimePQME = 90;
        sp.AppointmentMaxTimeAME = 120;
        sp.AppointmentMaxTimeOTHER = 60;
        return sp;
    }

    [Theory]
    [InlineData("PQME", 90)]
    [InlineData("pqme", 90)]
    [InlineData("PQME-REVAL", 90)]
    [InlineData("Pqme-Reval", 90)]
    [InlineData("AME", 120)]
    [InlineData("AME-REVAL", 120)]
    [InlineData("Ame-Reval", 120)]
    [InlineData("OTHER", 60)]
    [InlineData("anything-else", 60)]
    [InlineData("", 60)]
    [InlineData(null, 60)]
    public void ResolveMaxTimeDaysForType_RoutesByNamePattern(string? typeName, int expectedDays)
    {
        AppointmentBookingValidators
            .ResolveMaxTimeDaysForType(typeName, MakeSystemParameter())
            .ShouldBe(expectedDays);
    }

    [Fact]
    public void ResolveMaxTimeDaysForType_NullSystemParameter_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            AppointmentBookingValidators.ResolveMaxTimeDaysForType("PQME", null!));
    }

    // ------------------------------------------------------------------
    // 3-of-6 dedup
    // ------------------------------------------------------------------

    private static PatientDeduplicationCandidate Sample(
        string? lastName = "Smith",
        DateTime? dob = null,
        string? phone = "555-0100",
        string? email = "alice@test.local",
        string? ssn = "111-22-3333",
        string? claim = "ADJ001")
    {
        return new PatientDeduplicationCandidate
        {
            LastName = lastName,
            DateOfBirth = dob ?? new DateTime(1990, 1, 1),
            PhoneNumber = phone,
            Email = email,
            SocialSecurityNumber = ssn,
            ClaimNumber = claim,
        };
    }

    [Fact]
    public void CountMatchingDeduplicationFields_AllSix_ReturnsSix()
    {
        AppointmentBookingValidators
            .CountMatchingDeduplicationFields(Sample(), Sample())
            .ShouldBe(6);
    }

    [Fact]
    public void CountMatchingDeduplicationFields_None_ReturnsZero()
    {
        var a = Sample();
        var b = Sample(
            lastName: "Jones",
            dob: new DateTime(1985, 5, 5),
            phone: "555-9999",
            email: "bob@test.local",
            ssn: "999-88-7777",
            claim: "ZZZ999");
        AppointmentBookingValidators.CountMatchingDeduplicationFields(a, b).ShouldBe(0);
    }

    [Fact]
    public void CountMatchingDeduplicationFields_CaseInsensitiveAndTrimmed()
    {
        var a = Sample(lastName: " smith ", email: "ALICE@test.local");
        var b = Sample(lastName: "Smith", email: "alice@test.local");
        AppointmentBookingValidators
            .CountMatchingDeduplicationFields(a, b)
            .ShouldBe(6);
    }

    [Fact]
    public void CountMatchingDeduplicationFields_NullVsNull_DoesNotCountAsMatch()
    {
        var a = Sample(claim: null);
        var b = Sample(claim: null);
        // Five fields still match. Null claim on both sides must NOT count.
        AppointmentBookingValidators
            .CountMatchingDeduplicationFields(a, b)
            .ShouldBe(5);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(6, true)]
    public void IsPatientDuplicate_ThresholdAtOldDefaultOfThree(int matchCount, bool expected)
    {
        // Build (incoming, existing) pairs that share exactly matchCount
        // fields by selectively differing the others.
        var incoming = Sample();
        var existing = matchCount switch
        {
            2 => Sample(
                lastName: "Different",
                dob: new DateTime(2000, 1, 1),
                phone: "999-0000",
                email: "alice@test.local",     // match 1
                ssn: "111-22-3333",             // match 2
                claim: "DIFF"),
            3 => Sample(
                lastName: "Different",
                dob: new DateTime(2000, 1, 1),
                phone: "999-0000",
                email: "alice@test.local",     // match 1
                ssn: "111-22-3333",             // match 2
                claim: "ADJ001"),               // match 3
            6 => Sample(),
            _ => throw new ArgumentOutOfRangeException(nameof(matchCount)),
        };

        AppointmentBookingValidators
            .IsPatientDuplicate(incoming, existing)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsPatientDuplicate_CustomThreshold_Honored()
    {
        var a = Sample();
        var b = Sample(claim: "DIFF"); // 5 matches
        AppointmentBookingValidators.IsPatientDuplicate(a, b, threshold: 6).ShouldBeFalse();
        AppointmentBookingValidators.IsPatientDuplicate(a, b, threshold: 5).ShouldBeTrue();
    }
}
