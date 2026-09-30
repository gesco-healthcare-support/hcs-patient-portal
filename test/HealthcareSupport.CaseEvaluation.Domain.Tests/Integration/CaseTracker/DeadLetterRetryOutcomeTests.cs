using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Every branch of <see cref="DeadLetterRetryOutcome.Evaluate"/>: when a dead-letter retry may resolve
/// the dead letter, and when it must refuse and leave it listed (#961). All fixture data is synthetic.
/// </summary>
public class DeadLetterRetryOutcomeTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("9d4e2a17-6b3c-4f58-a1e0-5c7b3d9f2e84");
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    private static IntegrationOutboxItem Row(IntegrationOutboxStatus status)
    {
        var row = new IntegrationOutboxItem(
            Guid.NewGuid(),
            TenantId,
            IntegrationMessageType.Intake,
            CaseTrackerEndpoints.Intake,
            AppointmentId,
            "{\"data\":{}}",
            "key-" + Guid.NewGuid().ToString("N"));
        switch (status)
        {
            case IntegrationOutboxStatus.Sent:
                row.MarkSent(Now);
                break;
            case IntegrationOutboxStatus.Failed:
                row.MarkFatal(Now, "401 invalid token");
                break;
            case IntegrationOutboxStatus.Resolved:
                row.MarkFatal(Now, "401 invalid token");
                row.MarkResolved(Now);
                break;
        }

        row.Status.ShouldBe(status); // guards the fixture: MarkResolved only acts on a Failed row
        return row;
    }

    [Fact]
    public void Evaluate_WhenANewPendingRowWasQueued_ResolvesAndReportsIt()
    {
        var deadLetter = Row(IntegrationOutboxStatus.Failed);
        var queued = Row(IntegrationOutboxStatus.Pending);

        var outcome = DeadLetterRetryOutcome.Evaluate(deadLetter, new[] { queued });

        outcome.CanResolve.ShouldBeTrue();
        outcome.AlreadyDelivered.ShouldBeFalse();
        outcome.QueuedOutboxItemId.ShouldBe(queued.Id);
    }

    [Fact]
    public void Evaluate_WhenANewerRowAlreadyDeliveredTheContent_ResolvesAsAlreadyDelivered()
    {
        // Decided 2026-09-23: the Case Tracker already holds the current state, so the dead letter
        // has nothing left to deliver. Refusing would leave it listed with no way to clear it.
        var deadLetter = Row(IntegrationOutboxStatus.Failed);
        var delivered = Row(IntegrationOutboxStatus.Sent);

        var outcome = DeadLetterRetryOutcome.Evaluate(deadLetter, new[] { delivered });

        outcome.CanResolve.ShouldBeTrue();
        outcome.AlreadyDelivered.ShouldBeTrue();
        outcome.QueuedOutboxItemId.ShouldBe(delivered.Id);
    }

    [Fact]
    public void Evaluate_WithADeliveredAndAPendingRow_ReportsThePendingOne()
    {
        // A document retry can queue an upsert and a deletion; if either is on its way, the retry
        // queued something and must not claim "already delivered".
        var deadLetter = Row(IntegrationOutboxStatus.Failed);
        var delivered = Row(IntegrationOutboxStatus.Sent);
        var pending = Row(IntegrationOutboxStatus.Pending);

        var outcome = DeadLetterRetryOutcome.Evaluate(deadLetter, new[] { delivered, pending });

        outcome.CanResolve.ShouldBeTrue();
        outcome.AlreadyDelivered.ShouldBeFalse();
        outcome.QueuedOutboxItemId.ShouldBe(pending.Id);
    }

    [Fact]
    public void Evaluate_WhenTheDeadLetterItselfCameBack_Refuses()
    {
        // The #961 defect exactly: the enqueue returned the dead letter, and resolving it would have
        // reported "queued" while nothing was ever sent. Refused by the STATUS rule -- the dead letter
        // is Failed -- which is why there is no separate identity check to test.
        var deadLetter = Row(IntegrationOutboxStatus.Failed);

        var outcome = DeadLetterRetryOutcome.Evaluate(deadLetter, new[] { deadLetter });

        outcome.CanResolve.ShouldBeFalse();
        outcome.RefusalReason.ShouldNotBeNullOrWhiteSpace();
        outcome.QueuedOutboxItemId.ShouldBeNull();
    }

    [Fact]
    public void Evaluate_WhenNothingWasQueued_Refuses()
    {
        var outcome = DeadLetterRetryOutcome.Evaluate(Row(IntegrationOutboxStatus.Failed), Array.Empty<IntegrationOutboxItem>());

        outcome.CanResolve.ShouldBeFalse();
        outcome.RefusalReason.ShouldNotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(IntegrationOutboxStatus.Failed)]
    [InlineData(IntegrationOutboxStatus.Resolved)]
    public void Evaluate_WhenTheQueuedRowCannotBeDeliveredEither_Refuses(IntegrationOutboxStatus status)
    {
        // A different row that is itself failed or set aside is no more on its way than the dead letter.
        var outcome = DeadLetterRetryOutcome.Evaluate(Row(IntegrationOutboxStatus.Failed), new[] { Row(status) });

        outcome.CanResolve.ShouldBeFalse();
        outcome.AlreadyDelivered.ShouldBeFalse();
    }
}
