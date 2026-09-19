using System;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 6 T1 (2026-08-08) -- the decision is recorded as ONE act.
///
/// <para>Status, deciding user and decided-at together ARE the legal record of a decision. Before
/// this, all three had public setters and were written in different places -- the status and actor
/// at four call sites, the timestamp at a fifth -- so nothing but convention stopped a decision
/// being half-recorded. Adrian: "This is logs for proper legal processes, we want them to be
/// exact." These tests pin that the three move together and cannot be set apart.</para>
///
/// <para>The setters being <c>protected</c> is enforced by the COMPILER, not by a test -- a test
/// asserting "you cannot assign this" would not compile either. That guarantee is verified by the
/// build, which is the stronger check.</para>
/// </summary>
public class ChangeRequestMarkDecidedTests
{
    private static readonly Guid AppointmentId = new("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");
    private static readonly Guid DecidedBy = new("b2c3d4e5-f6a7-4b8c-9d0e-1f2a3b4c5d6e");
    private static readonly DateTime NowUtc = new(2027, 3, 4, 9, 30, 0, DateTimeKind.Utc);

    private static AppointmentChangeRequest NewRequest() =>
        new(
            id: new Guid("c3d4e5f6-a7b8-4c9d-8e1f-2a3b4c5d6e7f"),
            tenantId: new Guid("d4e5f6a7-b8c9-4d0e-8f1a-2b3c4d5e6f70"),
            appointmentId: AppointmentId,
            changeRequestType: ChangeRequestType.Cancel,
            cancellationReason: "Patient asked to cancel.",
            reScheduleReason: null,
            newDoctorAvailabilityId: null,
            isBeyondLimit: false);

    [Fact]
    public void ANewRequestStartsPending_WithNoDecisionRecorded()
    {
        var request = NewRequest();

        request.RequestStatus.ShouldBe(RequestStatusType.Pending);
        request.DecidedAt.ShouldBeNull();
        request.ApprovedById.ShouldBeNull();
        request.RejectedById.ShouldBeNull();
    }

    [Fact]
    public void MarkDecided_Accepted_SetsStatusActorAndTimestampTogether()
    {
        var request = NewRequest();

        request.MarkDecided(RequestStatusType.Accepted, DecidedBy, NowUtc);

        request.RequestStatus.ShouldBe(RequestStatusType.Accepted);
        request.ApprovedById.ShouldBe(DecidedBy);
        request.DecidedAt.ShouldBe(NowUtc);
        // The opposite actor must stay clear: an accepted request was not rejected by anyone.
        request.RejectedById.ShouldBeNull();
    }

    [Fact]
    public void MarkDecided_Rejected_SetsStatusActorAndTimestampTogether()
    {
        var request = NewRequest();

        request.MarkDecided(RequestStatusType.Rejected, DecidedBy, NowUtc);

        request.RequestStatus.ShouldBe(RequestStatusType.Rejected);
        request.RejectedById.ShouldBe(DecidedBy);
        request.DecidedAt.ShouldBe(NowUtc);
        request.ApprovedById.ShouldBeNull();
    }

    [Theory]
    [InlineData(RequestStatusType.Pending)]
    public void MarkDecided_WithANonDecisionOutcome_Throws(RequestStatusType outcome)
    {
        // Pending is not a decision. Coercing it would let this method un-decide a request and
        // quietly erase the record of who decided it and when.
        var request = NewRequest();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            request.MarkDecided(outcome, DecidedBy, NowUtc));
    }

    [Fact]
    public void MarkDecided_IsIdempotentOnTheTimestamp_SoAReDecisionCannotRewriteHistory()
    {
        // The original stamp survives: the FIRST decision is the one that happened. Re-deciding
        // must not silently relabel when it occurred -- that is the exact failure the dedicated
        // column exists to prevent (see the DecidedAt remarks).
        var request = NewRequest();
        request.MarkDecided(RequestStatusType.Accepted, DecidedBy, NowUtc);

        var later = NowUtc.AddHours(3);
        request.MarkDecided(RequestStatusType.Rejected, DecidedBy, later);

        request.DecidedAt.ShouldBe(NowUtc);
    }

    [Fact]
    public void MarkDecided_RequiresAUtcTimestamp()
    {
        // A local-kind timestamp on a legal record is ambiguous the moment it crosses a boundary.
        var request = NewRequest();
        var localKind = DateTime.SpecifyKind(NowUtc, DateTimeKind.Local);

        Should.Throw<ArgumentException>(() =>
            request.MarkDecided(RequestStatusType.Accepted, DecidedBy, localKind));
    }
}
