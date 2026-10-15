using System;
using System.Collections.Generic;
using System.Linq;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Pins the retry classification the Case Tracker branches on.
///
/// <para>The exhaustive test below is the POINT of this file, not a formality. We told them a
/// lifecycle change here would update their behaviour without renegotiating the contract; the only
/// thing that can make that true is a test that fails when a status is added and left unclassified.
/// Without it the promise decays silently, which is exactly how the original
/// <see cref="AppointmentStatusType.RescheduleRequested"/> error reached them.</para>
/// </summary>
public class AttendanceConflictPolicyTests
{
    /// <summary>
    /// The ONLY statuses from which an appointment can still reach <c>Approved</c>, and therefore
    /// still accept an attendance outcome. Verified against <c>AppointmentManager.BuildMachine</c>
    /// PLUS the one direct write to <c>Approved</c> outside it (<c>RejectRescheduleAsync</c>).
    /// </summary>
    private static readonly AppointmentStatusType[] Retryable =
    [
        AppointmentStatusType.Pending,
        AppointmentStatusType.InfoRequested,
        AppointmentStatusType.RescheduleRequested,
    ];

    [Theory]
    [InlineData(AppointmentStatusType.Pending)]
    [InlineData(AppointmentStatusType.InfoRequested)]
    [InlineData(AppointmentStatusType.RescheduleRequested)]
    public void TheThreeRecoverableStatusesAreRetryable(AppointmentStatusType status)
    {
        AttendanceConflictPolicy.IsRetryable(status).ShouldBeTrue();
    }

    /// <summary>
    /// Called out separately from the table below because it is the one that was WRONG in the
    /// contract, and the one a mechanical reachability check over the state machine still gets
    /// wrong: the machine has no RescheduleRequested -> Approved edge, because the reject path
    /// assigns the status directly. If this test ever starts failing, do not "fix" it by trusting
    /// the graph.
    /// </summary>
    [Fact]
    public void RescheduleRequestedIsRetryableEvenThoughTheStateMachineHasNoEdgeBackToApproved()
    {
        AttendanceConflictPolicy.IsRetryable(AppointmentStatusType.RescheduleRequested).ShouldBeTrue();
    }

    [Theory]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.NoShow)]
    [InlineData(AppointmentStatusType.NotSeen)]
    [InlineData(AppointmentStatusType.CancelledNoBill)]
    [InlineData(AppointmentStatusType.CancelledLate)]
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    [InlineData(AppointmentStatusType.RescheduledLate)]
    [InlineData(AppointmentStatusType.CheckedIn)]
    [InlineData(AppointmentStatusType.CheckedOut)]
    [InlineData(AppointmentStatusType.Billed)]
    [InlineData(AppointmentStatusType.CancellationRequested)]
    [InlineData(AppointmentStatusType.Approved)]
    public void EveryOtherStatusIsPermanent(AppointmentStatusType status)
    {
        AttendanceConflictPolicy.IsRetryable(status).ShouldBeFalse();
    }

    /// <summary>
    /// The guard. Enumerates the enum itself, so a new lifecycle status fails here until someone
    /// decides which list it belongs in and adds it to one of the two tables above.
    /// </summary>
    [Fact]
    public void EveryStatusIsClassifiedByOneOfTheTwoTablesAbove()
    {
        var covered = new HashSet<AppointmentStatusType>(
            Retryable.Concat(
            [
                AppointmentStatusType.Rejected,
                AppointmentStatusType.NoShow,
                AppointmentStatusType.NotSeen,
                AppointmentStatusType.CancelledNoBill,
                AppointmentStatusType.CancelledLate,
                AppointmentStatusType.RescheduledNoBill,
                AppointmentStatusType.RescheduledLate,
                AppointmentStatusType.CheckedIn,
                AppointmentStatusType.CheckedOut,
                AppointmentStatusType.Billed,
                AppointmentStatusType.CancellationRequested,
                AppointmentStatusType.Approved,
            ]));

        var unclassified = Enum.GetValues<AppointmentStatusType>()
            .Where(status => !covered.Contains(status))
            .ToArray();

        unclassified.ShouldBeEmpty(
            "A new AppointmentStatusType was added without deciding whether an attendance 409 in "
            + "that status is worth retrying. Classify it in AttendanceConflictPolicy, add it to "
            + "the matching table in this file, and update the contract's Conflicts (409) section "
            + "-- the Case Tracker branches on this and cannot see the change otherwise. "
            + "Unclassified: " + string.Join(", ", unclassified));
    }
}
