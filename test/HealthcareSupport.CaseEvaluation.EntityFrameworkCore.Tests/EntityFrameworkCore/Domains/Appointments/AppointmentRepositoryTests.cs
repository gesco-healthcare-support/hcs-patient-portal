using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Appointments;

[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class AppointmentRepositoryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly IAppointmentRepository _appointmentRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepoGeneric;
    private readonly IDoctorAvailabilityRepository _slotRepository;
    private readonly ICurrentTenant _currentTenant;

    public AppointmentRepositoryTests()
    {
        _appointmentRepository = GetRequiredService<IAppointmentRepository>();
        _appointmentRepoGeneric = GetRequiredService<IRepository<Appointment, Guid>>();
        _slotRepository = GetRequiredService<IDoctorAvailabilityRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task GetActiveCountForSlotAsync_ExcludesFiveFreedStatuses()
    {
        // 2026-05-15 -- pin the active-count predicate for the capacity gate.
        // Seed a scratch slot in TenantA, insert one appointment per terminal
        // status; the five "freed" statuses must NOT count, the Approved one
        // must count -- so the active-count is exactly 1.

        var scratchSlotId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _slotRepository.InsertAsync(new DoctorAvailability(
                    id: scratchSlotId,
                    locationId: LocationsTestData.Location1Id,
                    availableDate: new DateTime(2031, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    fromTime: new TimeOnly(9, 0),
                    toTime: new TimeOnly(10, 0),
                    bookingStatusId: BookingStatus.Available), autoSave: true);

                var statuses = new[]
                {
                    AppointmentStatusType.Approved,        // active -- counts
                    AppointmentStatusType.Rejected,        // freed
                    AppointmentStatusType.CancelledNoBill, // freed
                    AppointmentStatusType.CancelledLate,   // freed
                    AppointmentStatusType.RescheduledNoBill, // freed
                    AppointmentStatusType.RescheduledLate, // freed
                };

                var seq = 99000;
                foreach (var status in statuses)
                {
                    seq++;
                    await _appointmentRepoGeneric.InsertAsync(new Appointment(
                        id: Guid.NewGuid(),
                        patientId: PatientsTestData.Patient1Id,
                        identityUserId: IdentityUsersTestData.Patient1UserId,
                        appointmentTypeId: LocationsTestData.AppointmentType1Id,
                        locationId: LocationsTestData.Location1Id,
                        doctorAvailabilityId: scratchSlotId,
                        appointmentDate: new DateTime(2031, 1, 1, 9, 15, 0, DateTimeKind.Utc),
                        requestConfirmationNumber: $"A{seq}",
                        appointmentStatus: status), autoSave: true);
                }
            }
        });

        long count = -1;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                count = await _appointmentRepository.GetActiveCountForSlotAsync(scratchSlotId);
            }
        });

        count.ShouldBe(1);
    }
}
