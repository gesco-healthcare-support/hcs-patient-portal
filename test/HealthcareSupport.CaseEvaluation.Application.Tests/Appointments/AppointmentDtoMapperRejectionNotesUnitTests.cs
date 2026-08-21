using System;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// F4-02 (2026-05-26) -- regression test for the Mapperly auto-map of
/// the newly-surfaced <see cref="AppointmentDto.RejectionNotes"/> and
/// <see cref="AppointmentDto.RejectedById"/> fields. Plan:
/// docs/plans/2026-05-26-rejection-notes-readback.md.
///
/// Riok.Mapperly maps source properties to target properties by name
/// when both exist. Without this test, a future refactor that drops
/// either field from the DTO would silently regress the
/// rejection-reason readback to the patient-facing UI.
/// </summary>
public class AppointmentDtoMapperRejectionNotesUnitTests
{
    [Fact]
    public void Map_AppointmentWithRejectionNotes_FlowsToDto()
    {
        var rejectedBy = Guid.NewGuid();
        var appointment = new Appointment(
            id: Guid.NewGuid(),
            patientId: Guid.NewGuid(),
            identityUserId: Guid.NewGuid(),
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A99999",
            appointmentStatus: AppointmentStatusType.Rejected)
        {
            RejectionNotes = "synthetic rejection reason for parity test",
            RejectedById = rejectedBy,
        };

        var mapper = new AppointmentToAppointmentDtoMappers();
        var dto = mapper.Map(appointment);

        dto.RejectionNotes.ShouldBe("synthetic rejection reason for parity test");
        dto.RejectedById.ShouldBe(rejectedBy);
    }

    [Fact]
    public void Map_AppointmentWithoutRejectionNotes_LeavesDtoNullable()
    {
        var appointment = new Appointment(
            id: Guid.NewGuid(),
            patientId: Guid.NewGuid(),
            identityUserId: Guid.NewGuid(),
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: new DateTime(2026, 6, 3, 10, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A99998",
            appointmentStatus: AppointmentStatusType.Pending);

        var mapper = new AppointmentToAppointmentDtoMappers();
        var dto = mapper.Map(appointment);

        dto.RejectionNotes.ShouldBeNull();
        dto.RejectedById.ShouldBeNull();
    }
}
