using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Outbox;

/// <summary>
/// T8 unit tests for the <see cref="NotificationOutboxItem"/> delivery state
/// machine: lease/claim eligibility (the visibility-timeout race), idempotent
/// mark-sent, and the attempt cap. Pure entity tests -- no DB.
/// </summary>
public class NotificationOutboxItemTests
{
    private static readonly Guid TenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly DateTime Now = new(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    private static NotificationOutboxItem NewPending(int maxAttempts = 5) =>
        new(
            Guid.NewGuid(), TenantId,
            to: "party@example.test",
            cc: null,
            subject: "Your appointment",
            body: "<p>hello</p>",
            isBodyHtml: true,
            context: "Transition/Approved/appt-1",
            idempotencyKey: "abc123",
            maxAttempts: maxAttempts);

    [Fact]
    public void NewItem_IsPending_WithZeroAttempts_AndNoLease()
    {
        var item = NewPending();

        item.Status.ShouldBe(NotificationOutboxStatus.Pending);
        item.AttemptCount.ShouldBe(0);
        item.SentAt.ShouldBeNull();
        item.LockedUntil.ShouldBeNull();
        item.NextAttemptAt.ShouldBeNull();
    }

    [Fact]
    public void TryClaim_FreshPending_LeasesAndReturnsTrue()
    {
        var item = NewPending();

        var claimed = item.TryClaim(Now, Lease);

        claimed.ShouldBeTrue();
        item.LockedUntil.ShouldBe(Now.Add(Lease));
    }

    [Fact]
    public void TryClaim_WhileLeaseActive_ReturnsFalse()
    {
        var item = NewPending();
        item.TryClaim(Now, Lease).ShouldBeTrue();

        // A second drain arrives one minute later; the lease is still held.
        item.TryClaim(Now.AddMinutes(1), Lease).ShouldBeFalse();
    }

    [Fact]
    public void TryClaim_AfterLeaseExpires_ReclaimsStalePending()
    {
        var item = NewPending();
        item.TryClaim(Now, Lease).ShouldBeTrue();

        // The worker died mid-send; six minutes later the lease has expired.
        var later = Now.AddMinutes(6);
        item.TryClaim(later, Lease).ShouldBeTrue();
        item.LockedUntil.ShouldBe(later.Add(Lease));
    }

    [Fact]
    public void TryClaim_BeforeBackoffElapsed_ReturnsFalse_ThenTrueWhenDue()
    {
        var item = NewPending();
        item.TryClaim(Now, Lease);
        item.MarkFailed(Now, "smtp down", TimeSpan.FromMinutes(10));

        item.Status.ShouldBe(NotificationOutboxStatus.Pending);
        // Backoff not yet elapsed -> not due.
        item.TryClaim(Now.AddMinutes(5), Lease).ShouldBeFalse();
        // Past the backoff -> claimable again.
        item.TryClaim(Now.AddMinutes(11), Lease).ShouldBeTrue();
    }

    [Fact]
    public void MarkSent_SetsSent_AndIsIdempotent()
    {
        var item = NewPending();
        item.TryClaim(Now, Lease);

        var sentAt = Now.AddSeconds(2);
        item.MarkSent(sentAt);

        item.Status.ShouldBe(NotificationOutboxStatus.Sent);
        item.SentAt.ShouldBe(sentAt);
        item.LockedUntil.ShouldBeNull();

        // A duplicate drain of an already-Sent row must not re-send / move the time.
        item.MarkSent(Now.AddMinutes(30));
        item.SentAt.ShouldBe(sentAt);
        item.Status.ShouldBe(NotificationOutboxStatus.Sent);
    }

    [Fact]
    public void MarkFailed_BelowCap_ReschedulesPendingWithBackoff()
    {
        var item = NewPending(maxAttempts: 5);

        item.MarkFailed(Now, "transient", TimeSpan.FromMinutes(10));

        item.AttemptCount.ShouldBe(1);
        item.Status.ShouldBe(NotificationOutboxStatus.Pending);
        item.NextAttemptAt.ShouldBe(Now.AddMinutes(10));
        item.LockedUntil.ShouldBeNull();
    }

    [Fact]
    public void MarkFailed_AtCap_IsTerminalFailed_AndUnclaimable()
    {
        var item = NewPending(maxAttempts: 3);

        item.MarkFailed(Now, "e", TimeSpan.FromMinutes(1)); // 1
        item.MarkFailed(Now, "e", TimeSpan.FromMinutes(1)); // 2
        item.Status.ShouldBe(NotificationOutboxStatus.Pending);

        item.MarkFailed(Now, "e", TimeSpan.FromMinutes(1)); // 3 -> terminal

        item.AttemptCount.ShouldBe(3);
        item.Status.ShouldBe(NotificationOutboxStatus.Failed);
        item.NextAttemptAt.ShouldBeNull();
        item.TryClaim(Now.AddYears(1), Lease).ShouldBeFalse();
    }

    [Fact]
    public void MarkFailed_AfterSent_DoesNotResurrect()
    {
        var item = NewPending();
        item.MarkSent(Now);

        item.MarkFailed(Now.AddMinutes(1), "late error", TimeSpan.FromMinutes(5));

        item.Status.ShouldBe(NotificationOutboxStatus.Sent);
        item.AttemptCount.ShouldBe(0);
    }

    [Fact]
    public void GetCcList_RoundTripsAddresses()
    {
        var item = new NotificationOutboxItem(
            Guid.NewGuid(), TenantId,
            to: "to@example.test",
            cc: new[] { "a@example.test", "  ", "b@example.test" },
            subject: "s", body: "b", isBodyHtml: true,
            context: "ctx", idempotencyKey: "k");

        item.GetCcList().ShouldBe(new[] { "a@example.test", "b@example.test" });
    }
}
