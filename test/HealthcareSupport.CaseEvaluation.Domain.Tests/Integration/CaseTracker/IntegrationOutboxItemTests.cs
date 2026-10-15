using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the <see cref="IntegrationOutboxItem"/> delivery state machine. Mirrors
/// <c>NotificationOutboxItemTests</c> (lease eligibility, idempotent mark-sent, attempt cap)
/// and adds the behaviour this ledger has and the email one does not: <c>MarkFatal</c>, the
/// immediate dead-letter for responses a retry can never fix (bad token, malformed payload), and
/// since #917 a 24-hour retry window on growing waits. Pure entity tests -- no DB, no HTTP.
/// </summary>
public class IntegrationOutboxItemTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("8f14e45f-ceea-467a-9f3a-1a2b3c4d5e6f");
    private static readonly DateTime Now = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(IntegrationOutboxConsts.LeaseDurationSeconds);
    private static readonly TimeSpan FirstWait = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    private static IntegrationOutboxItem NewPending(int maxAttempts = IntegrationOutboxConsts.MaxAttempts) =>
        new(
            Guid.NewGuid(),
            TenantId,
            IntegrationMessageType.Intake,
            targetPath: "api/intake/appointments",
            appointmentId: AppointmentId,
            payload: "{\"data\":{}}",
            idempotencyKey: "abc123",
            maxAttempts: maxAttempts);

    [Fact]
    public void NewItem_IsPending_WithZeroAttempts_AndNoLease()
    {
        var item = NewPending();

        item.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        item.MessageType.ShouldBe(IntegrationMessageType.Intake);
        item.AppointmentId.ShouldBe(AppointmentId);
        item.AttemptCount.ShouldBe(0);
        item.SentAt.ShouldBeNull();
        item.LockedUntil.ShouldBeNull();
        item.NextAttemptAt.ShouldBeNull();
    }

    [Fact]
    public void NewItem_DefaultsMaxAttemptsToTheBackstop()
    {
        // #917: the 24-hour window ends retries; this is only a ceiling on attempts inside it.
        NewPending().MaxAttempts.ShouldBe(100);
    }

    [Fact]
    public void TryClaim_FreshPending_LeasesAndReturnsTrue()
    {
        var item = NewPending();

        item.TryClaim(Now, Lease).ShouldBeTrue();
        item.LockedUntil.ShouldBe(Now.Add(Lease));
    }

    [Fact]
    public void TryClaim_WhileLeaseActive_ReturnsFalse()
    {
        var item = NewPending();
        item.TryClaim(Now, Lease).ShouldBeTrue();

        item.TryClaim(Now.AddSeconds(30), Lease).ShouldBeFalse();
    }

    [Fact]
    public void TryClaim_AfterLeaseExpires_ReclaimsStalePending()
    {
        var item = NewPending();
        item.TryClaim(Now, Lease).ShouldBeTrue();

        // The worker died mid-post; the visibility timeout has since elapsed.
        var later = Now.Add(Lease).AddSeconds(1);
        item.TryClaim(later, Lease).ShouldBeTrue();
        item.LockedUntil.ShouldBe(later.Add(Lease));
    }

    [Fact]
    public void TryClaim_BeforeTheRetryWaitElapsed_ReturnsFalse_ThenTrueWhenDue()
    {
        var item = NewPending();
        item.TryClaim(Now, Lease);
        item.MarkFailed(Now, "case tracker 503");

        item.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        item.TryClaim(Now.AddSeconds(60), Lease).ShouldBeFalse();
        item.TryClaim(Now.Add(FirstWait).AddSeconds(1), Lease).ShouldBeTrue();
    }

    [Fact]
    public void MarkSent_SetsSent_AndIsIdempotent()
    {
        var item = NewPending();
        item.TryClaim(Now, Lease);

        var sentAt = Now.AddSeconds(2);
        item.MarkSent(sentAt);

        item.Status.ShouldBe(IntegrationOutboxStatus.Sent);
        item.SentAt.ShouldBe(sentAt);
        item.LockedUntil.ShouldBeNull();

        // A duplicate drain of an already-Sent row must not move the send time.
        item.MarkSent(Now.AddMinutes(30));
        item.SentAt.ShouldBe(sentAt);
        item.Status.ShouldBe(IntegrationOutboxStatus.Sent);
    }

    [Fact]
    public void MarkFailed_FirstFailure_ReschedulesPendingAfterFiveMinutes_AndStartsTheWindow()
    {
        var item = NewPending();

        item.MarkFailed(Now, "timeout");

        item.AttemptCount.ShouldBe(1);
        item.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        item.NextAttemptAt.ShouldBe(Now.Add(FirstWait));
        item.FirstFailedAt.ShouldBe(Now);
        item.LockedUntil.ShouldBeNull();
        item.LastError.ShouldBe("timeout");
    }

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 30)]
    [InlineData(40, 30)]
    public void WaitAfterAttempt_GrowsThenHoldsAtThirtyMinutes(int attemptCount, int expectedMinutes)
    {
        IntegrationOutboxItem.WaitAfterAttempt(attemptCount).ShouldBe(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Fact]
    public void MarkFailed_SchedulesEachRetryFromThatFailure_OnTheGrowingWaits()
    {
        var item = NewPending();
        var at = Now;

        foreach (var minutes in new[] { 5, 10, 20, 30, 30 })
        {
            item.MarkFailed(at, "503");
            item.NextAttemptAt.ShouldBe(at.AddMinutes(minutes));
            at = item.NextAttemptAt!.Value;
        }

        // Later failures never move the start of the window.
        item.FirstFailedAt.ShouldBe(Now);
    }

    [Fact]
    public void MarkFailed_InsideTheWindow_KeepsRetrying()
    {
        var item = NewPending();
        item.MarkFailed(Now, "503");

        item.MarkFailed(Now.Add(Window).AddMinutes(-1), "503");

        item.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        item.NextAttemptAt.ShouldNotBeNull();
    }

    [Fact]
    public void MarkFailed_OnceTheWindowHasPassed_DeadLetters()
    {
        // Measured from the FIRST failure, not the creation time or the last attempt.
        var item = NewPending();
        item.MarkFailed(Now, "503");

        item.MarkFailed(Now.Add(Window), "503");

        item.Status.ShouldBe(IntegrationOutboxStatus.Failed);
        item.NextAttemptAt.ShouldBeNull();
        item.TryClaim(Now.AddYears(1), Lease).ShouldBeFalse();
    }

    [Fact]
    public void MarkFailed_WindowStartsAtTheFirstFailure_NotAtCreation()
    {
        // A row can sit Pending for a day while the push is switched off; that must not count against it.
        var item = NewPending();
        var firstFailure = Now.AddDays(3);

        item.MarkFailed(firstFailure, "503");
        item.MarkFailed(firstFailure.AddHours(1), "503");

        item.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        item.FirstFailedAt.ShouldBe(firstFailure);
    }

    [Fact]
    public void MarkFailed_AtAStoredCapOfThree_IsTerminalFailed_AndUnclaimable()
    {
        // Rows queued before #917 keep the cap stored on them, so they end where they always did.
        var item = NewPending(maxAttempts: 3);

        item.MarkFailed(Now, "e"); // 1
        item.MarkFailed(Now, "e"); // 2
        item.Status.ShouldBe(IntegrationOutboxStatus.Pending);

        item.MarkFailed(Now, "e"); // 3 -> terminal

        item.AttemptCount.ShouldBe(3);
        item.Status.ShouldBe(IntegrationOutboxStatus.Failed);
        item.NextAttemptAt.ShouldBeNull();
        item.TryClaim(Now.AddYears(1), Lease).ShouldBeFalse();
    }

    [Fact]
    public void MarkFatal_OnFirstAttempt_IsImmediatelyTerminal_AndUnclaimable()
    {
        var item = NewPending();
        item.TryClaim(Now, Lease);

        item.MarkFatal(Now, "401 invalid token");

        item.Status.ShouldBe(IntegrationOutboxStatus.Failed);
        item.AttemptCount.ShouldBe(1);
        item.NextAttemptAt.ShouldBeNull();
        item.LockedUntil.ShouldBeNull();
        item.LastError.ShouldBe("401 invalid token");
        item.FirstFailedAt.ShouldBe(Now);
        item.TryClaim(Now.AddYears(1), Lease).ShouldBeFalse();
    }

    [Fact]
    public void MarkFatal_AfterSent_DoesNotResurrect()
    {
        var item = NewPending();
        item.MarkSent(Now);

        item.MarkFatal(Now.AddMinutes(1), "late 400");

        item.Status.ShouldBe(IntegrationOutboxStatus.Sent);
        item.AttemptCount.ShouldBe(0);
    }

    [Fact]
    public void MarkFailed_AfterSent_DoesNotResurrect()
    {
        var item = NewPending();
        item.MarkSent(Now);

        item.MarkFailed(Now.AddMinutes(1), "late error");

        item.Status.ShouldBe(IntegrationOutboxStatus.Sent);
        item.AttemptCount.ShouldBe(0);
    }

    [Fact]
    public void MarkFailed_TruncatesAnOverlongError()
    {
        var item = NewPending();

        item.MarkFailed(Now, new string('x', IntegrationOutboxConsts.LastErrorMaxLength + 50));

        item.LastError!.Length.ShouldBe(IntegrationOutboxConsts.LastErrorMaxLength);
    }

    // -- Part 5: alerting and resolution -----------------------------------

    [Fact]
    public void MarkAlerted_StampsTheRow()
    {
        var item = NewPending();
        item.MarkFatal(Now, "401 invalid token");

        item.MarkAlerted(Now);

        item.AlertedAt.ShouldBe(Now);
    }

    [Fact]
    public void MarkAlerted_IsIdempotent_SoStaffAreNotMailedTwice()
    {
        // The whole throttle rests on this: once a row is stamped, a later run must not re-alert it.
        var item = NewPending();
        item.MarkFatal(Now, "401 invalid token");
        item.MarkAlerted(Now);

        item.MarkAlerted(Now.AddHours(1));

        item.AlertedAt.ShouldBe(Now);
    }

    [Fact]
    public void MarkResolved_MovesAFailedRowOutOfTheDeadLetterList()
    {
        var item = NewPending();
        item.MarkFatal(Now, "401 invalid token");

        item.MarkResolved(Now.AddMinutes(5));

        item.Status.ShouldBe(IntegrationOutboxStatus.Resolved);
    }

    [Theory]
    [InlineData(IntegrationOutboxStatus.Pending)]
    [InlineData(IntegrationOutboxStatus.Sent)]
    public void MarkResolved_OnlyActsOnAFailedRow(IntegrationOutboxStatus startingStatus)
    {
        // Resolving a Pending row would silently cancel a push that is still due; resolving a Sent row
        // would rewrite delivery history.
        var item = NewPending();
        if (startingStatus == IntegrationOutboxStatus.Sent)
        {
            item.MarkSent(Now);
        }

        item.MarkResolved(Now);

        item.Status.ShouldBe(startingStatus);
    }

    [Fact]
    public void MarkResolved_IsIdempotent()
    {
        var item = NewPending();
        item.MarkFatal(Now, "401 invalid token");
        item.MarkResolved(Now);

        item.MarkResolved(Now.AddHours(1));

        item.Status.ShouldBe(IntegrationOutboxStatus.Resolved);
    }

    [Fact]
    public void AResolvedRow_IsNeverLeasableByADrain()
    {
        // Guards the reason Resolved is safe to introduce: TryClaim admits only Pending, so a resolved
        // row can never be picked up and re-sent.
        var item = NewPending();
        item.MarkFatal(Now, "401 invalid token");
        item.MarkResolved(Now);

        item.TryClaim(Now.AddDays(1), Lease).ShouldBeFalse();
    }
}
