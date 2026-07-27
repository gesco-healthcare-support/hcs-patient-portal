using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the <see cref="IntegrationOutboxItem"/> delivery state machine. Mirrors
/// <c>NotificationOutboxItemTests</c> (lease eligibility, idempotent mark-sent, attempt cap)
/// and adds the behaviour this ledger has and the email one does not: <c>MarkFatal</c>, the
/// immediate dead-letter for responses a retry can never fix (bad token, malformed payload).
/// Pure entity tests -- no DB, no HTTP.
/// </summary>
public class IntegrationOutboxItemTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("8f14e45f-ceea-467a-9f3a-1a2b3c4d5e6f");
    private static readonly DateTime Now = new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(IntegrationOutboxConsts.LeaseDurationSeconds);
    private static readonly TimeSpan Backoff = TimeSpan.FromSeconds(IntegrationOutboxConsts.RetryBackoffSeconds);

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
    public void NewItem_DefaultsMaxAttemptsToTheFailFastCap()
    {
        NewPending().MaxAttempts.ShouldBe(3);
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
    public void TryClaim_BeforeBackoffElapsed_ReturnsFalse_ThenTrueWhenDue()
    {
        var item = NewPending();
        item.TryClaim(Now, Lease);
        item.MarkFailed(Now, "case tracker 503", Backoff);

        item.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        item.TryClaim(Now.AddSeconds(60), Lease).ShouldBeFalse();
        item.TryClaim(Now.Add(Backoff).AddSeconds(1), Lease).ShouldBeTrue();
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
    public void MarkFailed_BelowCap_ReschedulesPendingWithBackoff()
    {
        var item = NewPending();

        item.MarkFailed(Now, "timeout", Backoff);

        item.AttemptCount.ShouldBe(1);
        item.Status.ShouldBe(IntegrationOutboxStatus.Pending);
        item.NextAttemptAt.ShouldBe(Now.Add(Backoff));
        item.LockedUntil.ShouldBeNull();
        item.LastError.ShouldBe("timeout");
    }

    [Fact]
    public void MarkFailed_AtCap_IsTerminalFailed_AndUnclaimable()
    {
        var item = NewPending(maxAttempts: 3);

        item.MarkFailed(Now, "e", Backoff); // 1
        item.MarkFailed(Now, "e", Backoff); // 2
        item.Status.ShouldBe(IntegrationOutboxStatus.Pending);

        item.MarkFailed(Now, "e", Backoff); // 3 -> terminal

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

        item.MarkFailed(Now.AddMinutes(1), "late error", Backoff);

        item.Status.ShouldBe(IntegrationOutboxStatus.Sent);
        item.AttemptCount.ShouldBe(0);
    }

    [Fact]
    public void MarkFailed_TruncatesAnOverlongError()
    {
        var item = NewPending();

        item.MarkFailed(Now, new string('x', IntegrationOutboxConsts.LastErrorMaxLength + 50), Backoff);

        item.LastError!.Length.ShouldBe(IntegrationOutboxConsts.LastErrorMaxLength);
    }
}
