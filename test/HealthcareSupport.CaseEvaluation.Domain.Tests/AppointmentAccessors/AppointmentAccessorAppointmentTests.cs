using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

/// <summary>
/// Pure unit tests for the <see cref="AppointmentAccessorAppointment"/> join entity: the
/// constructor stores both ids and the composite key is (accessor, appointment) in that order.
/// </summary>
public class AppointmentAccessorAppointmentTests
{
    [Fact]
    public void Constructor_StoresBothIds()
    {
        var accessorId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        var link = new AppointmentAccessorAppointment(accessorId, appointmentId);

        link.AppointmentAccessorId.ShouldBe(accessorId);
        link.AppointmentId.ShouldBe(appointmentId);
    }

    [Fact]
    public void GetKeys_ReturnsAccessorIdThenAppointmentId()
    {
        var accessorId = Guid.NewGuid();
        var appointmentId = Guid.NewGuid();

        var keys = new AppointmentAccessorAppointment(accessorId, appointmentId).GetKeys();

        keys.ShouldBe(new object[] { accessorId, appointmentId });
    }
}
