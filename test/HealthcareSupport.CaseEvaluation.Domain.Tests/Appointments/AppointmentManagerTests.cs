using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Domain-service tests for <see cref="AppointmentManager"/>. The concrete
/// EfCoreAppointmentManagerTests subclass lives under EntityFrameworkCore.Tests
/// and supplies CaseEvaluationEntityFrameworkCoreTestModule so the SQLite +
/// repository wiring is in place. Mirrors the abstract+concrete split used by
/// Samples/SampleDomainTests.
///
/// Phase B-6 Wave-2 PR-W2A: greenfield manager-level coverage. These tests
/// exercise the Check.* validation guards inside CreateAsync + the
/// thin-manager intent on UpdateAsync (manager does NOT touch
/// AppointmentStatus, InternalUserComments, AppointmentApproveDate, or
/// IsPatientAlreadyExist on the update path -- per src/.../Appointments/CLAUDE.md
/// Business Rule 7).
/// </summary>
public abstract class AppointmentManagerTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly AppointmentManager _appointmentManager;
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly ICurrentTenant _currentTenant;

    protected AppointmentManagerTests()
    {
        _appointmentManager = GetRequiredService<AppointmentManager>();
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Manager_CreateAsync_WhenRequestConfirmationNumberIsWhitespace_ThrowsArgumentException()
    {
        // Check.NotNullOrWhiteSpace guard fires before Check.Length.
        await Should.ThrowAsync<ArgumentException>(() => _appointmentManager.CreateAsync(
            patientId: PatientsTestData.Patient1Id,
            identityUserId: IdentityUsersTestData.Patient1UserId,
            appointmentTypeId: LocationsTestData.AppointmentType1Id,
            locationId: LocationsTestData.Location1Id,
            doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot2Id,
            appointmentDate: new DateTime(2027, 7, 1, 10, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "   ",
            appointmentStatus: AppointmentStatusType.Pending));
    }

    [Fact]
    public async Task Manager_CreateAsync_WhenRequestConfirmationNumberOverMax_ThrowsArgumentException()
    {
        // RequestConfirmationNumberMaxLength = 50; a 51-char string trips the
        // Check.Length guard.
        var oversized = new string('A', 51);

        await Should.ThrowAsync<ArgumentException>(() => _appointmentManager.CreateAsync(
            patientId: PatientsTestData.Patient1Id,
            identityUserId: IdentityUsersTestData.Patient1UserId,
            appointmentTypeId: LocationsTestData.AppointmentType1Id,
            locationId: LocationsTestData.Location1Id,
            doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot2Id,
            appointmentDate: new DateTime(2027, 7, 1, 10, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: oversized,
            appointmentStatus: AppointmentStatusType.Pending));
    }

    [Fact]
    public async Task Manager_CreateAsync_WhenPanelNumberOverMax_ThrowsArgumentException()
    {
        // PanelNumberMaxLength = 50; the manager validates the optional field
        // when supplied.
        var oversizedPanel = new string('P', 51);

        await Should.ThrowAsync<ArgumentException>(() => _appointmentManager.CreateAsync(
            patientId: PatientsTestData.Patient1Id,
            identityUserId: IdentityUsersTestData.Patient1UserId,
            appointmentTypeId: LocationsTestData.AppointmentType1Id,
            locationId: LocationsTestData.Location1Id,
            doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot2Id,
            appointmentDate: new DateTime(2027, 7, 1, 10, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A12345",
            appointmentStatus: AppointmentStatusType.Pending,
            panelNumber: oversizedPanel));
    }

    [Fact]
    public async Task Manager_UpdateAsync_PreservesStatusAndApproveDate()
    {
        // Thin-manager intent (CLAUDE.md Business Rule 7): UpdateAsync does
        // NOT take AppointmentStatus / InternalUserComments / AppointmentApproveDate
        // / IsPatientAlreadyExist parameters. Therefore an Update call cannot
        // mutate those fields; their values must survive the operation.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                // Capture pre-update snapshot.
                var before = await _appointmentRepository.GetAsync(AppointmentsTestData.Appointment1Id);
                var preStatus = before.AppointmentStatus;
                var preApproveDate = before.AppointmentApproveDate;
                var preInternalComments = before.InternalUserComments;
                var preIsPatientExist = before.IsPatientAlreadyExist;

                // Drive UpdateAsync with the same FKs but a different PanelNumber
                // (a field the manager DOES write). This isolates the assertion:
                // status / approve-date / comments / patient-exist must remain
                // exactly what they were because the manager has no parameter
                // to receive new values for them.
                await _appointmentManager.UpdateAsync(
                    id: AppointmentsTestData.Appointment1Id,
                    patientId: before.PatientId,
                    identityUserId: before.IdentityUserId,
                    appointmentTypeId: before.AppointmentTypeId,
                    locationId: before.LocationId,
                    doctorAvailabilityId: before.DoctorAvailabilityId,
                    appointmentDate: before.AppointmentDate,
                    panelNumber: "PNL-WAVE2-MANAGER",
                    dueDate: before.DueDate,
                    concurrencyStamp: before.ConcurrencyStamp);

                var after = await _appointmentRepository.GetAsync(AppointmentsTestData.Appointment1Id);
                after.AppointmentStatus.ShouldBe(preStatus);
                after.AppointmentApproveDate.ShouldBe(preApproveDate);
                after.InternalUserComments.ShouldBe(preInternalComments);
                after.IsPatientAlreadyExist.ShouldBe(preIsPatientExist);
                after.PanelNumber.ShouldBe("PNL-WAVE2-MANAGER");

                // Restore seed value so downstream tests see the original.
                await _appointmentManager.UpdateAsync(
                    id: AppointmentsTestData.Appointment1Id,
                    patientId: before.PatientId,
                    identityUserId: before.IdentityUserId,
                    appointmentTypeId: before.AppointmentTypeId,
                    locationId: before.LocationId,
                    doctorAvailabilityId: before.DoctorAvailabilityId,
                    appointmentDate: before.AppointmentDate,
                    panelNumber: before.PanelNumber,
                    dueDate: before.DueDate);
            }
        });
    }
}
