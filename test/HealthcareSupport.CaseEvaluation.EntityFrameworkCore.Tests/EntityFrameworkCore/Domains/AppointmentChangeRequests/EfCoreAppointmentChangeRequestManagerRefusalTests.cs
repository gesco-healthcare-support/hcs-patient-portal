using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using HealthcareSupport.CaseEvaluation.Timing;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// <see cref="AppointmentChangeRequestManager"/>'s refusals for cancellation and reschedule
/// requests, against the real repositories: a missing appointment, the tenant's no-cancel window,
/// a malformed or missing proposed slot, a blank reason, a source that is not approved, and a
/// proposed slot that is already taken.
///
/// <para>Each refusal is asserted by its error code AND by the absence of any change request
/// for the appointment afterwards, so a refusal that still wrote a request would fail. The
/// appointment and slots are created per fact in TenantA with fresh ids.</para>
/// </summary>
public class EfCoreAppointmentChangeRequestManagerRefusalTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly AppointmentChangeRequestManager _manager;
    private readonly IRepository<AppointmentChangeRequest, Guid> _requests;
    private readonly IRepository<Appointment, Guid> _appointments;
    private readonly IDoctorAvailabilityRepository _slots;
    private readonly ICurrentTenant _currentTenant;

    public EfCoreAppointmentChangeRequestManagerRefusalTests()
    {
        _manager = GetRequiredService<AppointmentChangeRequestManager>();
        _requests = GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>();
        _appointments = GetRequiredService<IRepository<Appointment, Guid>>();
        _slots = GetRequiredService<IDoctorAvailabilityRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Get_UnknownId_IsNotFound()
    {
        await Should.ThrowAsync<EntityNotFoundException>(() => InTenantAAsync(() => _manager.GetAsync(Guid.NewGuid())));
    }

    [Fact]
    public async Task Cancellation_OfAnUnknownAppointment_IsNotFound_AndWritesNoRequest()
    {
        var unknown = Guid.NewGuid();

        await Should.ThrowAsync<EntityNotFoundException>(() => InTenantAAsync(() =>
            _manager.SubmitCancellationAsync(unknown, "TEST-reason", allowPendingSource: false, actingUserId: null)));

        (await CountRequestsAsync(unknown)).ShouldBe(0);
    }

    [Fact]
    public async Task Cancellation_InsideTheNoCancelWindow_IsRefused_AndWritesNoRequest()
    {
        // The tenant's seeded cancel time is SystemParameterConsts.DefaultAppointmentCancelTime (2 days),
        // so a slot tomorrow (Pacific) is inside the window.
        var tomorrow = PacificTime.TodayFrom(DateTime.UtcNow).AddDays(1);
        var appointmentId = await InsertAppointmentAsync(AppointmentStatusType.Approved, await InsertSlotAsync(tomorrow, BookingStatus.Booked));

        var ex = await Should.ThrowAsync<BusinessException>(() => InTenantAAsync(() =>
            _manager.SubmitCancellationAsync(appointmentId, "TEST-reason", allowPendingSource: false, actingUserId: null)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestCancelTimeWindow);
        (await CountRequestsAsync(appointmentId)).ShouldBe(0);
    }

    [Fact]
    public async Task Reschedule_WithAnEmptySlotId_IsRefused()
    {
        var appointmentId = await InsertApprovedAppointmentAsync();

        var ex = await Should.ThrowAsync<BusinessException>(() => InTenantAAsync(() =>
            _manager.SubmitRescheduleAsync(appointmentId, Guid.Empty, "TEST-reason", false, false, null)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotRequired);
        (await CountRequestsAsync(appointmentId)).ShouldBe(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reschedule_WithABlankReason_IsRefused(string reason)
    {
        var appointmentId = await InsertApprovedAppointmentAsync();

        var ex = await Should.ThrowAsync<BusinessException>(() => InTenantAAsync(() =>
            _manager.SubmitRescheduleAsync(appointmentId, null, reason, false, false, null)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestRescheduleReasonRequired);
        (await CountRequestsAsync(appointmentId)).ShouldBe(0);
    }

    [Fact]
    public async Task Reschedule_OfAnUnknownAppointment_IsNotFound()
    {
        await Should.ThrowAsync<EntityNotFoundException>(() => InTenantAAsync(() =>
            _manager.SubmitRescheduleAsync(Guid.NewGuid(), null, "TEST-reason", false, false, null)));
    }

    [Fact]
    public async Task Reschedule_OfAPendingAppointment_IsRefused_WhenPendingSourcesAreNotAllowed()
    {
        var appointmentId = await InsertAppointmentAsync(AppointmentStatusType.Pending, await InsertSlotAsync(FarFuture(), BookingStatus.Booked));

        var ex = await Should.ThrowAsync<BusinessException>(() => InTenantAAsync(() =>
            _manager.SubmitRescheduleAsync(appointmentId, null, "TEST-reason", false, allowPendingSource: false, null)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestAppointmentNotApproved);
        (await CountRequestsAsync(appointmentId)).ShouldBe(0);
    }

    [Fact]
    public async Task Reschedule_ToAnUnknownSlot_IsNotFound()
    {
        var appointmentId = await InsertApprovedAppointmentAsync();

        await Should.ThrowAsync<EntityNotFoundException>(() => InTenantAAsync(() =>
            _manager.SubmitRescheduleAsync(appointmentId, Guid.NewGuid(), "TEST-reason", false, false, null)));

        (await CountRequestsAsync(appointmentId)).ShouldBe(0);
    }

    [Fact]
    public async Task Reschedule_ToASlotThatIsAlreadyBooked_IsRefused()
    {
        var appointmentId = await InsertApprovedAppointmentAsync();
        var takenSlot = await InsertSlotAsync(FarFuture().AddDays(3), BookingStatus.Booked);

        var ex = await Should.ThrowAsync<BusinessException>(() => InTenantAAsync(() =>
            _manager.SubmitRescheduleAsync(appointmentId, takenSlot, "TEST-reason", false, false, null)));

        ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotNotAvailable);
        (await CountRequestsAsync(appointmentId)).ShouldBe(0);
    }

    [Fact]
    public async Task Cancellation_OfAnotherOfficesAppointment_IsNotFound_AndWritesNoRequest()
    {
        // Office decoy: an approved, cancellable appointment exists in TenantB. TenantA must not be
        // able to file a cancellation against it; TenantB still sees its own appointment.
        var appointmentB = await InTenantAsync(TenantsTestData.TenantBRef, async () =>
        {
            var slotId = Guid.NewGuid();
            await _slots.InsertAsync(new DoctorAvailability(slotId, LocationsTestData.Location1Id, FarFuture().AddDays(9),
                new TimeOnly(9, 0), new TimeOnly(10, 0), BookingStatus.Booked), autoSave: true);
            var id = Guid.NewGuid();
            await _appointments.InsertAsync(new Appointment(id, PatientsTestData.Patient2Id, IdentityUsersTestData.Patient2UserId,
                LocationsTestData.AppointmentType1Id, LocationsTestData.Location1Id, slotId,
                FarFuture().AddDays(9).AddHours(9), "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                AppointmentStatusType.Approved), autoSave: true);
            return id;
        });

        await Should.ThrowAsync<EntityNotFoundException>(() => InTenantAAsync(() =>
            _manager.SubmitCancellationAsync(appointmentB, "TEST-reason", allowPendingSource: false, actingUserId: null)));

        (await InTenantAsync(TenantsTestData.TenantBRef, () => _requests.CountAsync(r => r.AppointmentId == appointmentB))).ShouldBe(0);
        (await InTenantAsync(TenantsTestData.TenantBRef, () => _appointments.FindAsync(appointmentB))).ShouldNotBeNull();
    }

    private static DateTime FarFuture() => new(2035, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private Task<int> CountRequestsAsync(Guid appointmentId) =>
        InTenantAAsync(() => _requests.CountAsync(r => r.AppointmentId == appointmentId));

    private async Task<Guid> InsertApprovedAppointmentAsync() =>
        await InsertAppointmentAsync(AppointmentStatusType.Approved, await InsertSlotAsync(FarFuture(), BookingStatus.Booked));

    private Task<Guid> InsertSlotAsync(DateTime day, BookingStatus status) =>
        InTenantAAsync(async () =>
        {
            var id = Guid.NewGuid();
            await _slots.InsertAsync(
                new DoctorAvailability(id, LocationsTestData.Location1Id, day, new TimeOnly(9, 0), new TimeOnly(10, 0), status),
                autoSave: true);
            return id;
        });

    private Task<Guid> InsertAppointmentAsync(AppointmentStatusType status, Guid slotId) =>
        InTenantAAsync(async () =>
        {
            var id = Guid.NewGuid();
            await _appointments.InsertAsync(
                new Appointment(id, PatientsTestData.Patient1Id, IdentityUsersTestData.Patient1UserId,
                    LocationsTestData.AppointmentType1Id, LocationsTestData.Location1Id, slotId,
                    new DateTime(2035, 6, 1, 9, 0, 0, DateTimeKind.Utc), "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), status),
                autoSave: true);
            return id;
        });

    private Task<T> InTenantAAsync<T>(Func<Task<T>> action) => InTenantAsync(TenantsTestData.TenantARef, action);

    private Task<T> InTenantAsync<T>(Guid tenantId, Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                return await action();
            }
        });
}
