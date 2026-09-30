using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// The change-request approval queue's CONSENT-ROUND context: for a reschedule whose date staff
/// already confirmed, the queue row carries the current round's number, proposed slot, both
/// sides' consent status and the send-attempt count. The existing queue test uses requests with
/// no round, so the per-request "latest non-superseded round" projection was never reached.
///
/// <para>The queue is office-scoped, so the test carries an OFFICE decoy: the same kind of
/// request, with its own round, in office B, which office A's queue must not list.</para>
/// </summary>
public class ChangeRequestQueueConsentRoundTests : CaseEvaluationTestBase<CaseEvaluationEntityFrameworkCoreTestModule>
{
    private readonly IAppointmentChangeRequestsApprovalAppService _queue;
    private readonly IAppointmentChangeRequestRepository _requests;
    private readonly IChangeRequestConsentRoundRepository _rounds;
    private readonly ICurrentTenant _currentTenant;

    public ChangeRequestQueueConsentRoundTests()
    {
        _queue = GetRequiredService<IAppointmentChangeRequestsApprovalAppService>();
        _requests = GetRequiredService<IAppointmentChangeRequestRepository>();
        _rounds = GetRequiredService<IChangeRequestConsentRoundRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task Queue_ShowsTheLatestLiveConsentRound_AndOnlyThisOfficesRequests()
    {
        var requestA = await InsertRescheduleWithRoundsAsync(TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id,
            supersededRoundSlot: DoctorAvailabilitiesTestData.Slot1Id, liveRoundSlot: DoctorAvailabilitiesTestData.Slot2Id);
        // Office decoy: a reschedule with a live round in office B.
        var requestB = await InsertRescheduleWithRoundsAsync(TenantsTestData.TenantBRef, AppointmentsTestData.Appointment2Id,
            supersededRoundSlot: null, liveRoundSlot: DoctorAvailabilitiesTestData.Slot3Id);

        var page = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await _queue.GetPendingChangeRequestsAsync(new GetChangeRequestsInput { MaxResultCount = 50 });
            }
        });

        page.Items.ShouldNotContain(dto => dto.Id == requestB);
        var row = page.Items.Single(dto => dto.Id == requestA);
        row.CurrentConsentRoundNumber.ShouldBe(2);
        row.CurrentRoundProposedSlotId.ShouldBe(DoctorAvailabilitiesTestData.Slot2Id);
        row.CurrentRoundSideAStatus.ShouldBe(ChangeRequestConsentStatus.NotRequired);
        row.CurrentRoundSideBStatus.ShouldBe(ChangeRequestConsentStatus.NotRequired);
        row.CurrentRoundSendAttempts.ShouldBe(1);
        row.RequestedSlotDate.ShouldBeNull(); // the requestor proposed no slot; only the round has one
    }

    [Fact]
    public async Task Queue_WithNothingMatchingTheFilter_ReturnsAnEmptyPage()
    {
        // Decoy: a pending CANCELLATION exists, so only the type filter can empty the page.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _requests.InsertAsync(new AppointmentChangeRequest(
                    Guid.NewGuid(), TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id, ChangeRequestType.Cancel,
                    cancellationReason: "TEST-cancel reason", reScheduleReason: null, newDoctorAvailabilityId: null), autoSave: true);
            }
        });

        var page = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                return await _queue.GetPendingChangeRequestsAsync(new GetChangeRequestsInput
                {
                    ChangeRequestType = ChangeRequestType.Reschedule,
                    MaxResultCount = 50,
                });
            }
        });

        page.TotalCount.ShouldBe(0);
        page.Items.ShouldBeEmpty();
    }

    /// <summary>
    /// A pending reschedule on <paramref name="appointmentId"/> with a live round 2 on
    /// <paramref name="liveRoundSlot"/>, and (optionally) a superseded round 1 before it.
    /// </summary>
    private Task<Guid> InsertRescheduleWithRoundsAsync(Guid officeId, Guid appointmentId, Guid? supersededRoundSlot, Guid liveRoundSlot) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeId))
            {
                var request = await _requests.InsertAsync(new AppointmentChangeRequest(
                    Guid.NewGuid(), officeId, appointmentId, ChangeRequestType.Reschedule,
                    cancellationReason: null, reScheduleReason: "TEST-reschedule reason", newDoctorAvailabilityId: null), autoSave: true);

                if (supersededRoundSlot.HasValue)
                {
                    var first = new ChangeRequestConsentRound(Guid.NewGuid(), officeId, request.Id, 1, supersededRoundSlot.Value, proposedByUserId: null);
                    first.Supersede(DateTime.UtcNow);
                    await _rounds.InsertAsync(first, autoSave: true);
                }

                await _rounds.InsertAsync(
                    new ChangeRequestConsentRound(Guid.NewGuid(), officeId, request.Id, 2, liveRoundSlot, proposedByUserId: null), autoSave: true);
                return request.Id;
            }
        });
}
