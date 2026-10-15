using System;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// Phase 14 (2026-05-04) -- pure unit tests for
/// <see cref="JointDeclarationCutoff.IsAtOrPastCutoff"/>. Pins the
/// at-boundary cases the JDF auto-cancel job depends on.
/// </summary>
public class JointDeclarationCutoffUnitTests
{
    [Fact]
    public void IsAtOrPastCutoff_NullDueDate_ReturnsFalse()
    {
        // No due date -> no cutoff to enforce. OLD-bug-fix vs OLD's
        // implicit NRE on the comparison.
        JointDeclarationCutoff
            .IsAtOrPastCutoff(null, cutoffDays: 7, nowUtc: DateTime.UtcNow)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsAtOrPastCutoff_ZeroCutoffDays_ReturnsFalse()
    {
        // Gate disabled.
        var due = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        JointDeclarationCutoff
            .IsAtOrPastCutoff(due, cutoffDays: 0, nowUtc: due.AddDays(-3))
            .ShouldBeFalse();
    }

    [Fact]
    public void IsAtOrPastCutoff_NegativeCutoffDays_ReturnsFalse()
    {
        // Defensive: a negative configured cutoff should not auto-cancel.
        var due = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        JointDeclarationCutoff
            .IsAtOrPastCutoff(due, cutoffDays: -5, nowUtc: due.AddDays(-1))
            .ShouldBeFalse();
    }

    [Fact]
    public void IsAtOrPastCutoff_NowBeforeCutoff_ReturnsFalse()
    {
        // Due date is 2026-07-15; cutoff is 7 days; cutoff boundary is 2026-07-08.
        // Now = 2026-07-07 -> still inside the upload window.
        var due = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 7, 7, 23, 59, 59, DateTimeKind.Utc);
        JointDeclarationCutoff
            .IsAtOrPastCutoff(due, cutoffDays: 7, nowUtc: now)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsAtOrPastCutoff_NowExactlyAtCutoffBoundary_ReturnsTrue()
    {
        // Cutoff boundary inclusive: at 2026-07-08 00:00 UTC the
        // appointment auto-cancels.
        var due = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc);
        JointDeclarationCutoff
            .IsAtOrPastCutoff(due, cutoffDays: 7, nowUtc: now)
            .ShouldBeTrue();
    }

    [Fact]
    public void IsAtOrPastCutoff_NowOneSecondBeforeBoundary_ReturnsFalse()
    {
        var due = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 7, 7, 23, 59, 59, DateTimeKind.Utc);
        JointDeclarationCutoff
            .IsAtOrPastCutoff(due, cutoffDays: 7, nowUtc: now)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsAtOrPastCutoff_NowOneDayPastBoundary_ReturnsTrue()
    {
        var due = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc);
        JointDeclarationCutoff
            .IsAtOrPastCutoff(due, cutoffDays: 7, nowUtc: now)
            .ShouldBeTrue();
    }

    [Fact]
    public void IsAtOrPastCutoff_NowAtDueDate_ReturnsTrue()
    {
        // The day-of: still well past the cutoff window.
        var due = new DateTime(2026, 7, 15, 9, 0, 0, DateTimeKind.Utc);
        var now = due;
        JointDeclarationCutoff
            .IsAtOrPastCutoff(due, cutoffDays: 7, nowUtc: now)
            .ShouldBeTrue();
    }

    [Fact]
    public void IsAtOrPastCutoff_OneDayCutoff_DayBeforeReturnsTrue()
    {
        // A 1-day cutoff means auto-cancel happens 1 day before the
        // appointment.
        var due = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc);
        JointDeclarationCutoff
            .IsAtOrPastCutoff(due, cutoffDays: 1, nowUtc: now)
            .ShouldBeTrue();
    }

    [Fact]
    public void IsAtOrPastCutoff_LargeCutoff_DistantFutureDueDate_ReturnsTrue()
    {
        // A 30-day cutoff against a 28-day-out due date -> we are
        // already past the cutoff (cutoff boundary is 2 days in the
        // past from now).
        var nowUtc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var due = nowUtc.AddDays(28);
        JointDeclarationCutoff
            .IsAtOrPastCutoff(due, cutoffDays: 30, nowUtc: nowUtc)
            .ShouldBeTrue();
    }
}
