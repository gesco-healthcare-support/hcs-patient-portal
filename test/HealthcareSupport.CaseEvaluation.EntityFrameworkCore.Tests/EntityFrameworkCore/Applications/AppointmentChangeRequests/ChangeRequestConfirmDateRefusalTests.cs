using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Staff confirming a reschedule date on a slot that is already taken. Only a free slot, or the
/// slot this same request already holds, may be proposed to the parties; anything else is refused
/// before a consent round is opened.
/// </summary>
public class ChangeRequestConfirmDateRefusalTests : CaseEvaluationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IAppointmentChangeRequestsApprovalAppService _decisions;
    private readonly IAppointmentChangeRequestRepository _requests;
    private readonly IChangeRequestConsentRoundRepository _rounds;
    private readonly IRepository<DoctorAvailability, Guid> _slots;
    private readonly IRepository<AppointmentType, Guid> _types;
    private readonly IRepository<Appointment, Guid> _appointments;
    private readonly ICurrentTenant _currentTenant;

    public ChangeRequestConfirmDateRefusalTests()
    {
        _types = GetRequiredService<IRepository<AppointmentType, Guid>>();
        _appointments = GetRequiredService<IRepository<Appointment, Guid>>();
        _decisions = GetRequiredService<IAppointmentChangeRequestsApprovalAppService>();
        _requests = GetRequiredService<IAppointmentChangeRequestRepository>();
        _rounds = GetRequiredService<IChangeRequestConsentRoundRepository>();
        _slots = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task ConfirmDate_OnASlotBookedByOthers_IsRefused_AndOpensNoRound()
    {
        var day = DateTime.UtcNow.Date.AddDays(30); // inside the internal 90-day horizon, past any lead time
        // Decoy: the request DOES hold a slot of its own (held slots are exempt), just not this one.
        var heldByRequest = await InsertSlotAsync(day, 9, BookingStatus.Reserved);
        var bookedByOthers = await InsertSlotAsync(day, 11, BookingStatus.Booked);
        var appointmentId = await InsertAppointmentWithOfficeTypeAsync();
        var requestId = await InOfficeAAsync(async () => (await _requests.InsertAsync(new AppointmentChangeRequest(
            Guid.NewGuid(), TenantsTestData.TenantARef, appointmentId, ChangeRequestType.Reschedule,
            cancellationReason: null, reScheduleReason: "TEST-reschedule reason", newDoctorAvailabilityId: heldByRequest), autoSave: true)).Id);

        var ex = await Should.ThrowAsync<BusinessException>(() => InOfficeAAsync(() => _decisions.ConfirmRescheduleDateAsync(
            requestId, new ConfirmRescheduleDateInput { DoctorAvailabilityId = bookedByOthers })));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotNotAvailable);
        (await InOfficeAAsync(() => _rounds.CountAsync(r => r.AppointmentChangeRequestId == requestId))).ShouldBe(0);
        (await InOfficeAAsync(() => _requests.GetAsync(requestId))).RequestStatus.ShouldBe(RequestStatusType.Pending);
    }

    /// <summary>
    /// An Approved appointment in office A whose appointment type office A owns. The booking-policy
    /// check reads the type, and the seeded types are host rows that an office cannot see.
    /// </summary>
    private Task<Guid> InsertAppointmentWithOfficeTypeAsync() =>
        InOfficeAAsync(async () =>
        {
            var type = await _types.InsertAsync(new AppointmentType(Guid.NewGuid(), "TEST-Type-" + Guid.NewGuid().ToString("N")[..6]), autoSave: true);
            var appointment = await _appointments.InsertAsync(new Appointment(
                id: Guid.NewGuid(),
                patientId: PatientsTestData.Patient1Id,
                identityUserId: null,
                appointmentTypeId: type.Id,
                locationId: LocationsTestData.Location1Id,
                doctorAvailabilityId: DoctorAvailabilitiesTestData.Slot1Id,
                appointmentDate: DateTime.UtcNow.Date.AddDays(20),
                requestConfirmationNumber: "TEST-" + Guid.NewGuid().ToString("N")[..8],
                appointmentStatus: AppointmentStatusType.Approved)
            {
                TenantId = TenantsTestData.TenantARef,
            }, autoSave: true);
            return appointment.Id;
        });

    private Task<Guid> InsertSlotAsync(DateTime day, int fromHour, BookingStatus status) =>
        InOfficeAAsync(async () =>
        {
            var slot = new DoctorAvailability(Guid.NewGuid(), LocationsTestData.Location1Id, day,
                new TimeOnly(fromHour, 0), new TimeOnly(fromHour + 1, 0), status);
            slot.TenantId = TenantsTestData.TenantARef;
            slot.AddAppointmentType(LocationsTestData.AppointmentType1Id);
            return (await _slots.InsertAsync(slot, autoSave: true)).Id;
        });

    private Task<T> InOfficeAAsync<T>(Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await action();
            }
        });
}
