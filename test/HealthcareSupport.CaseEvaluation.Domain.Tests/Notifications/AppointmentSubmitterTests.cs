using System;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Who counts as the submitter of an appointment request, and who counts as office staff, for email
/// routing (2026-11-03). Pure rules, no database. Synthetic data only.
/// </summary>
public class AppointmentSubmitterTests
{
    private static Appointment NewAppointment(Guid? creatorId, Guid? bookedByUserId)
    {
        var appointment = new Appointment(
            id: Guid.NewGuid(),
            patientId: Guid.NewGuid(),
            identityUserId: Guid.NewGuid(),
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "TEST-SUBMITTER",
            appointmentStatus: AppointmentStatusType.Pending)
        {
            BookedByUserId = bookedByUserId,
        };
        ObjectHelper.TrySetProperty(appointment, a => a.CreatorId, () => creatorId);
        return appointment;
    }

    [Fact]
    public void TheSubmitter_IsTheCreator_NotThePatientLoginInIdentityUserId()
    {
        var creator = Guid.NewGuid();

        AppointmentSubmitter.IdOf(NewAppointment(creator, bookedByUserId: Guid.NewGuid())).ShouldBe(creator);
    }

    [Fact]
    public void WithNoCreator_TheStampedBookedByUserStandsIn()
    {
        var booked = Guid.NewGuid();

        AppointmentSubmitter.IdOf(NewAppointment(creatorId: null, bookedByUserId: booked)).ShouldBe(booked);
    }

    [Fact]
    public void WithNeitherStamp_ThereIsNoSubmitter()
    {
        AppointmentSubmitter.IdOf(NewAppointment(null, null)).ShouldBeNull();
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("Intake Staff")]
    [InlineData("staff supervisor")]
    [InlineData("  IT Admin  ")]
    public void AnOfficeSideRole_MakesAnAccountStaff(string role)
    {
        AppointmentSubmitter.HoldsStaffRole(new[] { "Patient", role }).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Patient")]
    [InlineData("Applicant Attorney")]
    [InlineData("Defense Attorney")]
    [InlineData("Claim Examiner")]
    public void AnExternalRole_DoesNotMakeAnAccountStaff(string role)
    {
        AppointmentSubmitter.HoldsStaffRole(new[] { role }).ShouldBeFalse();
    }

    [Fact]
    public void NoRoles_IsNotStaff()
    {
        AppointmentSubmitter.HoldsStaffRole(null).ShouldBeFalse();
        AppointmentSubmitter.HoldsStaffRole(Array.Empty<string>()).ShouldBeFalse();
        AppointmentSubmitter.HoldsStaffRole(new string?[] { null, " " }).ShouldBeFalse();
    }
}
