using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
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

                // Drive UpdateAsync with the same FKs but a different DueDate
                // (a field the manager DOES write). This isolates the assertion:
                // status / approve-date / comments / patient-exist must remain
                // exactly what they were because the manager has no parameter
                // to receive new values for them. DueDate (not PanelNumber) is
                // the mutated field so this test stays orthogonal to the AF3/AF4
                // panel-number/type rule (Appointment1 is a non-PQME type, which
                // forbids a panel number); panel write-through is covered by
                // Manager_UpdateAsync_WhenPqmeAndPanelNumberProvided_Succeeds.
                var newDueDate = new DateTime(2027, 9, 1, 0, 0, 0, DateTimeKind.Utc);
                await _appointmentManager.UpdateAsync(
                    id: AppointmentsTestData.Appointment1Id,
                    patientId: before.PatientId,
                    identityUserId: before.IdentityUserId,
                    appointmentTypeId: before.AppointmentTypeId,
                    locationId: before.LocationId,
                    doctorAvailabilityId: before.DoctorAvailabilityId,
                    appointmentDate: before.AppointmentDate,
                    panelNumber: before.PanelNumber,
                    dueDate: newDueDate,
                    concurrencyStamp: before.ConcurrencyStamp);

                var after = await _appointmentRepository.GetAsync(AppointmentsTestData.Appointment1Id);
                after.AppointmentStatus.ShouldBe(preStatus);
                after.AppointmentApproveDate.ShouldBe(preApproveDate);
                after.InternalUserComments.ShouldBe(preInternalComments);
                after.IsPatientAlreadyExist.ShouldBe(preIsPatientExist);
                after.DueDate.ShouldBe(newDueDate);

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

    // --- AF3 + AF4 (2026-06-04): Panel Number / appointment-type coupling. ---
    // AppointmentManager.EnsurePanelNumberMatchesType runs in BOTH CreateAsync
    // and UpdateAsync. The two throw directions are verified on each path (they
    // reject before any DB write, so they need no valid FK set / UoW); the two
    // "ok" directions reuse the proven update harness against the seeded
    // Appointment1. PQME is keyed off the seeded PanelQme identity
    // (AppointmentTypesTestData.PqmeAppointmentTypeId); AppointmentType1Id is a
    // non-PQME (AME/IME-equivalent) type that forbids a panel number.

    [Fact]
    public async Task Manager_CreateAsync_WhenPqmeAndPanelNumberBlank_ThrowsRequiredForPqme()
    {
        // PQME requires a panel number; a blank one is rejected before insert.
        var ex = await Should.ThrowAsync<BusinessException>(() => _appointmentManager.CreateAsync(
            patientId: PatientsTestData.Patient1Id,
            identityUserId: IdentityUsersTestData.Patient1UserId,
            appointmentTypeId: AppointmentTypesTestData.PqmeAppointmentTypeId,
            locationId: LocationsTestData.Location1Id,
            doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot2Id,
            appointmentDate: new DateTime(2027, 7, 1, 10, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "PNLREQ1",
            appointmentStatus: AppointmentStatusType.Pending,
            panelNumber: null));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentPanelNumberRequiredForPqme);
    }

    [Fact]
    public async Task Manager_CreateAsync_WhenNonPqmeAndPanelNumberProvided_ThrowsNotAllowed()
    {
        // A non-PQME (AME / IME) type may not carry a panel number; rejected before insert.
        var ex = await Should.ThrowAsync<BusinessException>(() => _appointmentManager.CreateAsync(
            patientId: PatientsTestData.Patient1Id,
            identityUserId: IdentityUsersTestData.Patient1UserId,
            appointmentTypeId: AppointmentTypesTestData.AppointmentType1Id,
            locationId: LocationsTestData.Location1Id,
            doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot2Id,
            appointmentDate: new DateTime(2027, 7, 1, 10, 0, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "PNLNOT1",
            appointmentStatus: AppointmentStatusType.Pending,
            panelNumber: "PNL-12345"));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentPanelNumberNotAllowedForType);
    }

    [Fact]
    public async Task Manager_UpdateAsync_WhenPqmeAndPanelNumberBlank_ThrowsRequiredForPqme()
    {
        // The check runs before the entity is loaded, so the id need not resolve;
        // Appointment1Id keeps the call shape realistic. Whitespace counts as blank.
        var ex = await Should.ThrowAsync<BusinessException>(() => _appointmentManager.UpdateAsync(
            id: AppointmentsTestData.Appointment1Id,
            patientId: PatientsTestData.Patient1Id,
            identityUserId: IdentityUsersTestData.Patient1UserId,
            appointmentTypeId: AppointmentTypesTestData.PqmeAppointmentTypeId,
            locationId: LocationsTestData.Location1Id,
            doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot2Id,
            appointmentDate: new DateTime(2027, 7, 1, 10, 0, 0, DateTimeKind.Utc),
            panelNumber: "   "));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentPanelNumberRequiredForPqme);
    }

    [Fact]
    public async Task Manager_UpdateAsync_WhenNonPqmeAndPanelNumberProvided_ThrowsNotAllowed()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => _appointmentManager.UpdateAsync(
            id: AppointmentsTestData.Appointment1Id,
            patientId: PatientsTestData.Patient1Id,
            identityUserId: IdentityUsersTestData.Patient1UserId,
            appointmentTypeId: AppointmentTypesTestData.AppointmentType1Id,
            locationId: LocationsTestData.Location1Id,
            doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot2Id,
            appointmentDate: new DateTime(2027, 7, 1, 10, 0, 0, DateTimeKind.Utc),
            panelNumber: "PNL-12345"));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.AppointmentPanelNumberNotAllowedForType);
    }

    [Fact(Skip = "Phase F harness (F1): relies on the production AppointmentType seeder's host-scope PQME row, which now seeds per-office (IMultiTenant) under db-per-office; the shared-SQLite test rig can't seed per-tenant catalogs.")]
    public async Task Manager_UpdateAsync_WhenPqmeAndPanelNumberProvided_Succeeds()
    {
        // AF4 happy path: a PQME type WITH a panel number persists. Mutates the
        // seeded Appointment1 to PQME + a panel value, then restores it.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var before = await _appointmentRepository.GetAsync(AppointmentsTestData.Appointment1Id);

                await _appointmentManager.UpdateAsync(
                    id: AppointmentsTestData.Appointment1Id,
                    patientId: before.PatientId,
                    identityUserId: before.IdentityUserId,
                    appointmentTypeId: AppointmentTypesTestData.PqmeAppointmentTypeId,
                    locationId: before.LocationId,
                    doctorAvailabilityId: before.DoctorAvailabilityId,
                    appointmentDate: before.AppointmentDate,
                    panelNumber: "PNL-PQME-OK",
                    dueDate: before.DueDate,
                    concurrencyStamp: before.ConcurrencyStamp);

                var after = await _appointmentRepository.GetAsync(AppointmentsTestData.Appointment1Id);
                after.AppointmentTypeId.ShouldBe(AppointmentTypesTestData.PqmeAppointmentTypeId);
                after.PanelNumber.ShouldBe("PNL-PQME-OK");

                // Restore seed values (type + panel) so downstream tests are unaffected.
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

    [Fact]
    public async Task Manager_UpdateAsync_WhenNonPqmeAndPanelNumberBlank_Succeeds()
    {
        // AF3 "ok" direction: a non-PQME type with no panel number persists.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                var before = await _appointmentRepository.GetAsync(AppointmentsTestData.Appointment1Id);

                var updated = await _appointmentManager.UpdateAsync(
                    id: AppointmentsTestData.Appointment1Id,
                    patientId: before.PatientId,
                    identityUserId: before.IdentityUserId,
                    appointmentTypeId: AppointmentTypesTestData.AppointmentType1Id,
                    locationId: before.LocationId,
                    doctorAvailabilityId: before.DoctorAvailabilityId,
                    appointmentDate: before.AppointmentDate,
                    panelNumber: null,
                    dueDate: before.DueDate,
                    concurrencyStamp: before.ConcurrencyStamp);

                updated.AppointmentTypeId.ShouldBe(AppointmentTypesTestData.AppointmentType1Id);
                updated.PanelNumber.ShouldBeNull();
            }
        });
    }
}
