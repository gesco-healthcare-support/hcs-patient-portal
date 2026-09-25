using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Pins that <see cref="AppNotification.MarkRead"/> is idempotent: marking an already-read
/// notification again keeps the FIRST read time, so the bell's "read at" never moves forward when a
/// client re-sends the mark-read call.
/// </summary>
public class AppNotificationMarkReadTests
{
    [Fact]
    public void MarkRead_OnAnAlreadyReadNotification_KeepsTheFirstReadTime()
    {
        var notification = new AppNotification(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AppNotificationType.AppointmentRequested,
            "TEST-title",
            "TEST-body");
        var firstRead = new DateTime(2026, 9, 23, 17, 0, 0, DateTimeKind.Utc);

        notification.MarkRead(firstRead);
        notification.MarkRead(firstRead.AddHours(3));

        notification.IsRead.ShouldBeTrue();
        notification.ReadTime.ShouldBe(firstRead);
    }
}
