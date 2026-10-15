using System;
using System.Globalization;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The refusal path of the #931 ordering lock. <c>sp_getapplock</c> only runs on SQL Server, which
/// this suite does not use, so its return value is interpreted by a pure method tested here directly.
///
/// <para>A plain class with no database base: nothing here touches a context, so it does not pay
/// for one.</para>
/// </summary>
public class AppointmentLockStatusTests
{
    private static readonly Guid AppointmentId = new("6d2f8a41-9c3e-4b75-8e10-3f7a2c5d9b64");

    [Theory]
    [InlineData(0)] // granted at once
    [InlineData(1)] // granted after waiting
    public void EnsureLockGranted_WhenGranted_DoesNotThrow(int status)
    {
        Should.NotThrow(() => EfCoreIntegrationOutboxRepository.EnsureLockGranted(status, AppointmentId));
    }

    [Theory]
    [InlineData(-1)]   // timed out
    [InlineData(-2)]   // cancelled
    [InlineData(-3)]   // chosen as deadlock victim
    [InlineData(-999)] // call error, INCLUDING "no active transaction" -- SQL Server does not raise it
    public void EnsureLockGranted_WhenNotGranted_ThrowsNamingTheStatusAndTheAppointment(int status)
    {
        // Carrying on unlocked would silently reopen the race the lock exists to close.
        var ex = Should.Throw<InvalidOperationException>(
            () => EfCoreIntegrationOutboxRepository.EnsureLockGranted(status, AppointmentId));

        ex.Message.ShouldContain("returned " + status.ToString(CultureInfo.InvariantCulture));
        ex.Message.ShouldContain(AppointmentId.ToString("D"));
    }
}
