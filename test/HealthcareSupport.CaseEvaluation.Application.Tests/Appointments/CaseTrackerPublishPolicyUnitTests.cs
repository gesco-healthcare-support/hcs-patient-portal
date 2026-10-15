using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 5 (2026-08-07) -- pure tests for the publish gate.
///
/// <para>The point of these is the SEPARATION of the two questions. <c>IsPublished</c> means "the
/// intake landed, so a follow-up can be delivered"; <c>ShouldPublish</c> adds "and we have something
/// worth saying". Folding the attendance outcomes into the first would be false -- a no-showed
/// appointment WAS published -- and would break the closed-set invariant its remarks rest on.</para>
/// </summary>
public class CaseTrackerPublishPolicyUnitTests
{
    [Theory]
    [InlineData(AppointmentStatusType.Pending, false)]
    [InlineData(AppointmentStatusType.Rejected, false)]
    [InlineData(AppointmentStatusType.InfoRequested, false)]
    [InlineData(AppointmentStatusType.Approved, true)]
    [InlineData(AppointmentStatusType.CancelledNoBill, true)]
    [InlineData(AppointmentStatusType.RescheduledLate, true)]
    [InlineData(AppointmentStatusType.Billed, true)]
    // The load-bearing pair: phase 5 must NOT have changed these two answers.
    [InlineData(AppointmentStatusType.NoShow, true)]
    [InlineData(AppointmentStatusType.NotSeen, true)]
    public void IsPublished_StillAnswersExactlyWhatItAnsweredBefore(
        AppointmentStatusType status,
        bool expected)
    {
        // A no-showed appointment WAS published: its intake landed at approval, which is why the
        // Case Tracker holds a case to report a no-show against in the first place.
        CaseTrackerPublishPolicy.IsPublished(status).ShouldBe(expected);
    }

    [Theory]
    [InlineData(AppointmentStatusType.NoShow)]
    [InlineData(AppointmentStatusType.NotSeen)]
    public void ShouldPublish_SuppressesTheAttendanceOutcomes(AppointmentStatusType status)
    {
        CaseTrackerPublishPolicy.IsAttendanceClosed(status).ShouldBeTrue();
        CaseTrackerPublishPolicy.ShouldPublish(status).ShouldBeFalse();
    }

    [Theory]
    [InlineData(AppointmentStatusType.Approved)]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    [InlineData(AppointmentStatusType.CancelledLate)]
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    [InlineData(AppointmentStatusType.RescheduledLate)]
    [InlineData(AppointmentStatusType.CheckedIn)]
    [InlineData(AppointmentStatusType.CheckedOut)]
    [InlineData(AppointmentStatusType.Billed)]
    public void ShouldPublish_LeavesEveryOtherPublishedStatusAlone(AppointmentStatusType status)
    {
        CaseTrackerPublishPolicy.ShouldPublish(status).ShouldBeTrue();
    }

    [Theory]
    [InlineData(AppointmentStatusType.Pending)]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.InfoRequested)]
    public void ShouldPublish_StillWithholdsTheNeverPublishedStates(AppointmentStatusType status)
    {
        CaseTrackerPublishPolicy.ShouldPublish(status).ShouldBeFalse();
        CaseTrackerPublishPolicy.IsAttendanceClosed(status).ShouldBeFalse();
    }

    [Fact]
    public void IsAttendanceClosed_MatchesTheSingleDefinitionInTheDomain()
    {
        // One definition of the pair, not two. If these ever disagree, a third attendance cause was
        // added in one place only.
        foreach (AppointmentStatusType status in System.Enum.GetValues<AppointmentStatusType>())
        {
            CaseTrackerPublishPolicy.IsAttendanceClosed(status)
                .ShouldBe(AppointmentLifecycleValidators.IsAttendanceOutcome(status));
        }
    }
}
