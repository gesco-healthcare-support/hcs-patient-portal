using System;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Pure guard tests for <see cref="Appointment.RecordBookedBy"/> (R2-2 booker
/// identity). No DB: the booker stamp is a domain invariant exercised directly.
/// All ids are synthetic.
/// </summary>
public class AppointmentBookedByTests
{
    private static Appointment NewAppointment() => new(
        id: Guid.NewGuid(),
        patientId: Guid.NewGuid(),
        identityUserId: null,
        appointmentTypeId: Guid.NewGuid(),
        locationId: Guid.NewGuid(),
        doctorAvailabilityId: Guid.NewGuid(),
        appointmentDate: new DateTime(2027, 7, 1, 10, 0, 0, DateTimeKind.Utc),
        requestConfirmationNumber: "BK-0001",
        appointmentStatus: AppointmentStatusType.Pending);

    [Fact]
    public void RecordBookedBy_stamps_the_booker_user_id()
    {
        var appointment = NewAppointment();
        var bookerId = Guid.NewGuid();

        appointment.RecordBookedBy(bookerId);

        appointment.BookedByUserId.ShouldBe(bookerId);
    }

    [Fact]
    public void RecordBookedBy_rejects_an_empty_id()
    {
        var appointment = NewAppointment();

        Should.Throw<ArgumentException>(() => appointment.RecordBookedBy(Guid.Empty));
    }

    [Fact]
    public void BookedByUserId_is_null_until_stamped()
    {
        NewAppointment().BookedByUserId.ShouldBeNull();
    }
}
