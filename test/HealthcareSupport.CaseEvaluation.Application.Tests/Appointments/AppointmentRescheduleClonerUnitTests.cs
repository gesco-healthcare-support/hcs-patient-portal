using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Phase 11c (2026-05-04) -- pure scalar-clone tests for
/// <see cref="AppointmentRescheduleCloner.BuildScalarClone"/>. Verifies
/// that every scalar field is copied correctly, the reschedule-chain
/// linkage works, and the confirmation-number reuse / override branch
/// behaves per OLD parity.
/// </summary>
public class AppointmentRescheduleClonerUnitTests
{
    private static Appointment MakeSource(string confirmationNumber = "A12345")
    {
        var src = new Appointment(
            id: Guid.NewGuid(),
            patientId: Guid.NewGuid(),
            identityUserId: Guid.NewGuid(),
            appointmentTypeId: Guid.NewGuid(),
            locationId: Guid.NewGuid(),
            doctorAvailabilityId: Guid.NewGuid(),
            appointmentDate: new DateTime(2026, 6, 1, 9, 0, 0),
            requestConfirmationNumber: confirmationNumber,
            appointmentStatus: AppointmentStatusType.Approved,
            panelNumber: "PNL-001",
            dueDate: new DateTime(2026, 7, 1));

        src.TenantId = Guid.NewGuid();
        src.IsPatientAlreadyExist = true;
        src.AppointmentApproveDate = new DateTime(2026, 5, 25);
        src.InternalUserComments = "internal note";
        src.PatientEmail = "patient@test.local";
        src.ApplicantAttorneyEmail = "aa@test.local";
        src.DefenseAttorneyEmail = "da@test.local";
        src.ClaimExaminerEmail = "ce@test.local";
        src.PrimaryResponsibleUserId = Guid.NewGuid();
        src.IsBeyondLimit = false;
        return src;
    }

    [Fact]
    public void BuildScalarClone_ReusesConfirmationNumberByDefault()
    {
        var source = MakeSource(confirmationNumber: "A12345");
        var clone = AppointmentRescheduleCloner.BuildScalarClone(
            source,
            newAppointmentId: Guid.NewGuid(),
            newTenantId: source.TenantId,
            newDoctorAvailabilityId: Guid.NewGuid(),
            newAppointmentDate: new DateTime(2026, 7, 15, 10, 0, 0),
            sameConfirmationNumber: true,
            overrideConfirmationNumber: null,
            approveDate: new DateTime(2026, 6, 30));

        clone.RequestConfirmationNumber.ShouldBe("A12345");
    }

    [Fact]
    public void BuildScalarClone_OverrideConfirmationNumber_UsesOverride()
    {
        var source = MakeSource("A00001");
        var clone = AppointmentRescheduleCloner.BuildScalarClone(
            source,
            newAppointmentId: Guid.NewGuid(),
            newTenantId: source.TenantId,
            newDoctorAvailabilityId: Guid.NewGuid(),
            newAppointmentDate: new DateTime(2026, 7, 15),
            sameConfirmationNumber: false,
            overrideConfirmationNumber: "A99999",
            approveDate: new DateTime(2026, 6, 30));

        clone.RequestConfirmationNumber.ShouldBe("A99999");
    }

    [Fact]
    public void BuildScalarClone_OverrideRequiredButMissing_Throws()
    {
        var source = MakeSource();
        Should.Throw<ArgumentException>(() =>
            AppointmentRescheduleCloner.BuildScalarClone(
                source,
                newAppointmentId: Guid.NewGuid(),
                newTenantId: source.TenantId,
                newDoctorAvailabilityId: Guid.NewGuid(),
                newAppointmentDate: new DateTime(2026, 7, 15),
                sameConfirmationNumber: false,
                overrideConfirmationNumber: null,
                approveDate: DateTime.UtcNow));
    }

    [Fact]
    public void BuildScalarClone_NullSource_Throws()
    {
        Should.Throw<ArgumentNullException>(() =>
            AppointmentRescheduleCloner.BuildScalarClone(
                source: null!,
                newAppointmentId: Guid.NewGuid(),
                newTenantId: null,
                newDoctorAvailabilityId: Guid.NewGuid(),
                newAppointmentDate: DateTime.UtcNow,
                sameConfirmationNumber: true,
                overrideConfirmationNumber: null,
                approveDate: DateTime.UtcNow));
    }

    [Fact]
    public void BuildScalarClone_PointsOriginalAppointmentIdAtSource()
    {
        var source = MakeSource();
        var clone = AppointmentRescheduleCloner.BuildScalarClone(
            source,
            newAppointmentId: Guid.NewGuid(),
            newTenantId: source.TenantId,
            newDoctorAvailabilityId: Guid.NewGuid(),
            newAppointmentDate: new DateTime(2026, 7, 15),
            sameConfirmationNumber: true,
            overrideConfirmationNumber: null,
            approveDate: DateTime.UtcNow);

        clone.OriginalAppointmentId.ShouldBe(source.Id);
        clone.Id.ShouldNotBe(source.Id);
    }

    [Fact]
    public void BuildScalarClone_StatusDefaultsToApproved()
    {
        var source = MakeSource();
        var clone = AppointmentRescheduleCloner.BuildScalarClone(
            source,
            newAppointmentId: Guid.NewGuid(),
            newTenantId: source.TenantId,
            newDoctorAvailabilityId: Guid.NewGuid(),
            newAppointmentDate: new DateTime(2026, 7, 15),
            sameConfirmationNumber: true,
            overrideConfirmationNumber: null,
            approveDate: DateTime.UtcNow);

        clone.AppointmentStatus.ShouldBe(AppointmentStatusType.Approved);
    }

    [Fact]
    public void BuildScalarClone_RecomputesApproveDate()
    {
        var source = MakeSource();
        var newApproveDate = new DateTime(2026, 6, 30, 14, 30, 0);

        var clone = AppointmentRescheduleCloner.BuildScalarClone(
            source,
            newAppointmentId: Guid.NewGuid(),
            newTenantId: source.TenantId,
            newDoctorAvailabilityId: Guid.NewGuid(),
            newAppointmentDate: new DateTime(2026, 7, 15),
            sameConfirmationNumber: true,
            overrideConfirmationNumber: null,
            approveDate: newApproveDate);

        clone.AppointmentApproveDate.ShouldBe(newApproveDate);
        clone.AppointmentApproveDate.ShouldNotBe(source.AppointmentApproveDate);
    }

    [Fact]
    public void BuildScalarClone_UsesNewSlotAndDate()
    {
        var source = MakeSource();
        var newSlot = Guid.NewGuid();
        var newDate = new DateTime(2026, 7, 15, 11, 0, 0);

        var clone = AppointmentRescheduleCloner.BuildScalarClone(
            source,
            newAppointmentId: Guid.NewGuid(),
            newTenantId: source.TenantId,
            newDoctorAvailabilityId: newSlot,
            newAppointmentDate: newDate,
            sameConfirmationNumber: true,
            overrideConfirmationNumber: null,
            approveDate: DateTime.UtcNow);

        clone.DoctorAvailabilityId.ShouldBe(newSlot);
        clone.AppointmentDate.ShouldBe(newDate);
        clone.DoctorAvailabilityId.ShouldNotBe(source.DoctorAvailabilityId);
    }

    [Fact]
    public void BuildScalarClone_CarriesPartyEmails()
    {
        var source = MakeSource();
        var clone = AppointmentRescheduleCloner.BuildScalarClone(
            source,
            newAppointmentId: Guid.NewGuid(),
            newTenantId: source.TenantId,
            newDoctorAvailabilityId: Guid.NewGuid(),
            newAppointmentDate: new DateTime(2026, 7, 15),
            sameConfirmationNumber: true,
            overrideConfirmationNumber: null,
            approveDate: DateTime.UtcNow);

        clone.PatientEmail.ShouldBe(source.PatientEmail);
        clone.ApplicantAttorneyEmail.ShouldBe(source.ApplicantAttorneyEmail);
        clone.DefenseAttorneyEmail.ShouldBe(source.DefenseAttorneyEmail);
        clone.ClaimExaminerEmail.ShouldBe(source.ClaimExaminerEmail);
    }

    [Fact]
    public void BuildScalarClone_CarriesScalarLifecycleFields()
    {
        var source = MakeSource();
        var clone = AppointmentRescheduleCloner.BuildScalarClone(
            source,
            newAppointmentId: Guid.NewGuid(),
            newTenantId: source.TenantId,
            newDoctorAvailabilityId: Guid.NewGuid(),
            newAppointmentDate: new DateTime(2026, 7, 15),
            sameConfirmationNumber: true,
            overrideConfirmationNumber: null,
            approveDate: DateTime.UtcNow);

        clone.PatientId.ShouldBe(source.PatientId);
        clone.IdentityUserId.ShouldBe(source.IdentityUserId);
        clone.AppointmentTypeId.ShouldBe(source.AppointmentTypeId);
        clone.LocationId.ShouldBe(source.LocationId);
        clone.PanelNumber.ShouldBe(source.PanelNumber);
        clone.DueDate.ShouldBe(source.DueDate);
        clone.IsPatientAlreadyExist.ShouldBe(source.IsPatientAlreadyExist);
        clone.InternalUserComments.ShouldBe(source.InternalUserComments);
        clone.PrimaryResponsibleUserId.ShouldBe(source.PrimaryResponsibleUserId);
        clone.TenantId.ShouldBe(source.TenantId);
    }

    [Fact]
    public void BuildScalarClone_BeyondLimitOverride_TrueOverridesFalseSource()
    {
        var source = MakeSource();
        source.IsBeyondLimit = false;

        var clone = AppointmentRescheduleCloner.BuildScalarClone(
            source,
            newAppointmentId: Guid.NewGuid(),
            newTenantId: source.TenantId,
            newDoctorAvailabilityId: Guid.NewGuid(),
            newAppointmentDate: new DateTime(2026, 7, 15),
            sameConfirmationNumber: true,
            overrideConfirmationNumber: null,
            approveDate: DateTime.UtcNow,
            isBeyondLimit: true);

        clone.IsBeyondLimit.ShouldBeTrue();
    }

    [Fact]
    public void BuildScalarClone_DoesNotCarryRescheduleReasonOrChain()
    {
        var source = MakeSource();
        source.ReScheduleReason = "supervisor changed slot";
        source.ReScheduledById = Guid.NewGuid();

        var clone = AppointmentRescheduleCloner.BuildScalarClone(
            source,
            newAppointmentId: Guid.NewGuid(),
            newTenantId: source.TenantId,
            newDoctorAvailabilityId: Guid.NewGuid(),
            newAppointmentDate: new DateTime(2026, 7, 15),
            sameConfirmationNumber: true,
            overrideConfirmationNumber: null,
            approveDate: DateTime.UtcNow);

        // Reschedule fields describe the change request, not the result -- they
        // belong on the source row, not the clone.
        clone.ReScheduleReason.ShouldBeNull();
        clone.ReScheduledById.ShouldBeNull();
    }
}
