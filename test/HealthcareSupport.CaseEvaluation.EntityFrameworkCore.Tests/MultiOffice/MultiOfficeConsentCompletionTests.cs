using System;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using System.Linq;
using Volo.Abp.Guids;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Cover for completing a change request ON ITS OWN once every solicited side has agreed
/// (2026-10-09), through the real <see cref="ChangeRequestConsentCompletion"/> and the anonymous
/// public consent service. The decision table: a reschedule completes only when the CURRENT round
/// is granted AND carries the outcome staff chose; a staff-filed cancellation likewise; a
/// party-requested cancellation (no proposed outcome) never does.
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeConsentCompletionTests : ConsentRoundTestBase
{
    private static int _counter = 200;

    private readonly ChangeRequestConsentCompletion _completion;
    private readonly ChangeRequestConsentManager _consentManager;
    private readonly IPublicChangeRequestConsentAppService _publicConsent;
    private readonly IRepository<Appointment, Guid> _appointments;
    private readonly IRepository<DoctorAvailability, Guid> _slots;
    private readonly IAppointmentChangeRequestsApprovalAppService _approval;

    public MultiOfficeConsentCompletionTests()
    {
        _completion = GetRequiredService<ChangeRequestConsentCompletion>();
        _consentManager = GetRequiredService<ChangeRequestConsentManager>();
        _publicConsent = GetRequiredService<IPublicChangeRequestConsentAppService>();
        _appointments = GetRequiredService<IRepository<Appointment, Guid>>();
        _slots = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _approval = GetRequiredService<IAppointmentChangeRequestsApprovalAppService>();
    }

    private static readonly Guid EmailTypeId = Guid.Parse("c0000001-0000-4000-9000-000000000001");

    /// <summary>Completion publishes the approved event, whose email handler needs the templates.</summary>
    private async Task SeedTemplatesAsync(Guid officeId)
    {
        var types = GetRequiredService<INotificationTemplateTypeRepository>();
        var templates = GetRequiredService<INotificationTemplateRepository>();
        var guids = GetRequiredService<IGuidGenerator>();
        if (await types.FindAsync(EmailTypeId) == null)
        {
            await types.InsertAsync(new NotificationTemplateType(EmailTypeId, "Email"), autoSave: true);
        }

        var existing = (await templates.GetQueryableAsync()).Select(x => x.TemplateCode).ToList();
        foreach (var code in NotificationTemplateConsts.Codes.All.Where(c => !existing.Contains(c)))
        {
            await templates.InsertAsync(new NotificationTemplate(
                id: guids.Create(), tenantId: officeId, templateCode: code, templateTypeId: EmailTypeId,
                subject: $"[{code}] -- TEST", bodyEmail: $"<p>Stub for {code}</p>", bodySms: $"Stub for {code}",
                description: null, isActive: true), autoSave: true);
        }
    }

    private sealed record Rig(
        SeededOffice Office, Guid AppointmentId, Guid ChangeRequestId, Guid NewSlotId, Guid ProposerId);

    private async Task SeedSlotAsync(SeededOffice office, Guid slotId, DateTime date, TimeOnly from)
    {
        var slot = new DoctorAvailability(slotId, office.LocationId, date, from, from.AddHours(1), BookingStatus.Available);
        slot.TenantId = office.OfficeId;
        slot.AddAppointmentType(office.AppointmentTypeId);
        await _slots.InsertAsync(slot, autoSave: true);
    }

    private async Task<Guid> SeedAppointmentAsync(
        SeededOffice office, Guid slotId, DateTime date, AppointmentStatusType status)
    {
        var id = Guid.NewGuid();
        await _appointments.InsertAsync(
            new Appointment(
                id: id,
                patientId: office.PatientId,
                identityUserId: null,
                appointmentTypeId: office.AppointmentTypeId,
                locationId: office.LocationId,
                doctorAvailabilityId: slotId,
                appointmentDate: date.Date.AddHours(8),
                requestConfirmationNumber: $"RCN-CC-{id:N}"[..20],
                appointmentStatus: status),
            autoSave: true);
        return id;
    }

    /// <summary>
    /// A reschedule request plus a consent round carrying <paramref name="outcome"/>. The raw
    /// tokens of the sides left Pending come back through <paramref name="tokens"/>.
    /// </summary>
    private async Task<Rig> SeedRescheduleAsync(
        AppointmentStatusType? outcome,
        bool sideAPending,
        bool sideBPending,
        string?[] tokens,
        AppointmentStatusType appointmentStatus = AppointmentStatusType.RescheduleRequested)
    {
        var (office, _) = await GetSeededOfficesAsync();
        var n = Interlocked.Increment(ref _counter);
        var originSlot = Guid.NewGuid();
        var newSlot = Guid.NewGuid();
        var changeRequestId = Guid.NewGuid();
        var proposer = Guid.NewGuid();
        Guid appointmentId = Guid.Empty;

        await InOfficeAsync(office, async () =>
        {
            await SeedTemplatesAsync(office.OfficeId);
            await SeedSlotAsync(office, originSlot, TestToday.AddDays(21), new TimeOnly(7, 0).AddMinutes(n));
            await SeedSlotAsync(office, newSlot, TestToday.AddDays(31), new TimeOnly(11, 0).AddMinutes(n));
            appointmentId = await SeedAppointmentAsync(office, originSlot, TestToday.AddDays(21), appointmentStatus);
            await SeedRescheduleRequestAsync(office, changeRequestId, appointmentId);

            var round = new ChangeRequestConsentRound(
                Guid.NewGuid(), office.OfficeId, changeRequestId, 1, newSlot, proposer,
                "TEST-offer", outcome);
            if (sideAPending) { tokens[0] = _consentManager.IssueSideConsent(round, ChangeRequestSide.SideA); }
            if (sideBPending) { tokens[1] = _consentManager.IssueSideConsent(round, ChangeRequestSide.SideB); }
            await RoundRepository.InsertAsync(round, autoSave: true);
        });

        return new Rig(office, appointmentId, changeRequestId, newSlot, proposer);
    }

    private async Task GrantAllAsync(Rig rig)
    {
        await InOfficeAsync(rig.Office, async () =>
        {
            var round = (await RoundRepository.GetCurrentAsync(rig.ChangeRequestId))!;
            foreach (var side in new[] { ChangeRequestSide.SideA, ChangeRequestSide.SideB })
            {
                if (round.SideConsentStatus(side) == ChangeRequestConsentStatus.Pending)
                {
                    round.RecordSideDecision(side, true, "rep@example.test", DateTime.UtcNow);
                }
            }
            await RoundRepository.UpdateAsync(round, autoSave: true);
        });
    }

    private async Task<AppointmentChangeRequest> LoadAsync(Rig rig)
    {
        AppointmentChangeRequest row = null!;
        await InOfficeAsync(rig.Office, async () => row = await ChangeRequestRepository.GetAsync(rig.ChangeRequestId));
        return row;
    }

    private async Task<ChangeRequestCompletionResult> CompleteAsync(Rig rig)
    {
        var result = ChangeRequestCompletionResult.Failed;
        await InOfficeAsync(rig.Office, async () => result = await _completion.TryCompleteAsync(rig.ChangeRequestId));
        return result;
    }

    [Fact]
    public async Task A_granted_reschedule_with_a_proposed_outcome_completes_as_that_outcome()
    {
        var rig = await SeedRescheduleAsync(AppointmentStatusType.RescheduledLate, false, false, new string?[2]);

        (await CompleteAsync(rig)).ShouldBe(ChangeRequestCompletionResult.Completed);

        var row = await LoadAsync(rig);
        row.RequestStatus.ShouldBe(RequestStatusType.Accepted);
        row.CancellationOutcome.ShouldBe(AppointmentStatusType.RescheduledLate);
        row.ApprovedById.ShouldBe(rig.ProposerId, "the acting user is the staff member who set the date");
        row.AdminOverrideSlotId.ShouldBe(rig.NewSlotId);
        await InOfficeAsync(rig.Office, async () =>
        {
            (await _appointments.GetAsync(rig.AppointmentId)).AppointmentStatus
                .ShouldBe(AppointmentStatusType.RescheduledLate);
            (await _appointments.FirstOrDefaultAsync(a => a.RescheduledFromAppointmentId == rig.AppointmentId))
                .ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task A_granted_reschedule_WITHOUT_a_proposed_outcome_is_left_for_staff()
    {
        var rig = await SeedRescheduleAsync(null, false, false, new string?[2]);

        (await CompleteAsync(rig)).ShouldBe(ChangeRequestCompletionResult.NotApplicable);

        (await LoadAsync(rig)).RequestStatus.ShouldBe(RequestStatusType.Pending);
    }

    [Fact]
    public async Task A_reschedule_with_a_side_still_pending_is_not_completed()
    {
        var rig = await SeedRescheduleAsync(AppointmentStatusType.RescheduledNoBill, true, false, new string?[2]);

        (await CompleteAsync(rig)).ShouldBe(ChangeRequestCompletionResult.NotApplicable);

        (await LoadAsync(rig)).RequestStatus.ShouldBe(RequestStatusType.Pending);
    }

    [Fact]
    public async Task An_already_decided_request_is_not_completed_twice()
    {
        var rig = await SeedRescheduleAsync(AppointmentStatusType.RescheduledNoBill, false, false, new string?[2]);
        (await CompleteAsync(rig)).ShouldBe(ChangeRequestCompletionResult.Completed);

        (await CompleteAsync(rig)).ShouldBe(ChangeRequestCompletionResult.NotApplicable);
    }

    [Fact]
    public async Task An_unknown_request_is_not_applicable()
    {
        var (office, _) = await GetSeededOfficesAsync();
        var result = ChangeRequestCompletionResult.Failed;
        await InOfficeAsync(office, async () => result = await _completion.TryCompleteAsync(Guid.NewGuid()));
        result.ShouldBe(ChangeRequestCompletionResult.NotApplicable);
    }

    private async Task<Rig> SeedCancellationAsync(AppointmentStatusType? proposed, Guid submitter)
    {
        var (office, _) = await GetSeededOfficesAsync();
        var n = Interlocked.Increment(ref _counter);
        var slot = Guid.NewGuid();
        var changeRequestId = Guid.NewGuid();
        Guid appointmentId = Guid.Empty;
        await InOfficeAsync(office, async () =>
        {
            await SeedTemplatesAsync(office.OfficeId);
            await SeedSlotAsync(office, slot, TestToday.AddDays(22), new TimeOnly(6, 0).AddMinutes(n));
            appointmentId = await SeedAppointmentAsync(office, slot, TestToday.AddDays(22), AppointmentStatusType.Approved);
            var request = new AppointmentChangeRequest(
                changeRequestId, office.OfficeId, appointmentId, ChangeRequestType.Cancel,
                "TEST-cancel", null, null);
            request.InitiateConsent(null, submitter);
            if (proposed.HasValue) { request.ProposeOutcome(proposed.Value); }
            await ChangeRequestRepository.InsertAsync(request, autoSave: true);
        });
        return new Rig(office, appointmentId, changeRequestId, Guid.Empty, submitter);
    }

    [Fact]
    public async Task A_staff_filed_cancellation_with_a_proposed_outcome_completes_as_that_outcome()
    {
        var staff = Guid.NewGuid();
        var rig = await SeedCancellationAsync(AppointmentStatusType.CancelledLate, staff);

        (await CompleteAsync(rig)).ShouldBe(ChangeRequestCompletionResult.Completed);

        var row = await LoadAsync(rig);
        row.RequestStatus.ShouldBe(RequestStatusType.Accepted);
        row.CancellationOutcome.ShouldBe(AppointmentStatusType.CancelledLate);
        row.ApprovedById.ShouldBe(staff);
        await InOfficeAsync(rig.Office, async () =>
            (await _appointments.GetAsync(rig.AppointmentId)).AppointmentStatus
                .ShouldBe(AppointmentStatusType.CancelledLate));
    }

    [Fact]
    public async Task A_party_requested_cancellation_never_completes_on_its_own()
    {
        var rig = await SeedCancellationAsync(null, Guid.NewGuid());

        (await CompleteAsync(rig)).ShouldBe(ChangeRequestCompletionResult.NotApplicable);

        (await LoadAsync(rig)).RequestStatus.ShouldBe(RequestStatusType.Pending);
    }

    [Fact]
    public async Task The_last_approval_on_the_public_page_completes_the_reschedule_and_the_first_does_not()
    {
        var tokens = new string?[2];
        var rig = await SeedRescheduleAsync(AppointmentStatusType.RescheduledNoBill, true, true, tokens);

        await InOfficeAsync(rig.Office, () =>
            _publicConsent.SubmitDecisionAsync(tokens[0]!, new SubmitChangeRequestConsentDto { Approved = true }));
        (await LoadAsync(rig)).RequestStatus.ShouldBe(RequestStatusType.Pending);

        await InOfficeAsync(rig.Office, () =>
            _publicConsent.SubmitDecisionAsync(tokens[1]!, new SubmitChangeRequestConsentDto { Approved = true }));
        (await LoadAsync(rig)).RequestStatus.ShouldBe(RequestStatusType.Accepted);
    }

    [Fact]
    public async Task A_decline_on_the_public_page_never_completes()
    {
        var tokens = new string?[2];
        var rig = await SeedRescheduleAsync(AppointmentStatusType.RescheduledNoBill, true, false, tokens);

        await InOfficeAsync(rig.Office, () =>
            _publicConsent.SubmitDecisionAsync(tokens[0]!, new SubmitChangeRequestConsentDto { Approved = false }));

        (await LoadAsync(rig)).RequestStatus.ShouldBe(RequestStatusType.Pending);
    }

    [Fact]
    public async Task A_failing_completion_does_not_undo_the_consent_the_party_just_recorded()
    {
        // The source is Approved, which has no reschedule-confirm transition, so the finalize
        // throws once it runs. The recorded Yes must survive and the request must stay open.
        var tokens = new string?[2];
        var rig = await SeedRescheduleAsync(
            AppointmentStatusType.RescheduledNoBill, true, false, tokens,
            appointmentStatus: AppointmentStatusType.Approved);

        await InOfficeAsync(rig.Office, () =>
            _publicConsent.SubmitDecisionAsync(tokens[0]!, new SubmitChangeRequestConsentDto { Approved = true }));

        (await LoadAsync(rig)).RequestStatus.ShouldBe(RequestStatusType.Pending);
        await InOfficeAsync(rig.Office, async () =>
            (await RoundRepository.GetCurrentAsync(rig.ChangeRequestId))!
                .SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Approved));
    }

    [Fact]
    public async Task The_manual_approve_still_finishes_a_round_that_has_no_proposed_outcome()
    {
        var rig = await SeedRescheduleAsync(null, true, false, new string?[2]);
        await GrantAllAsync(rig);

        await InOfficeAsync(rig.Office, () => _approval.ApproveRescheduleAsync(
            rig.ChangeRequestId,
            new ApproveRescheduleInput { RescheduleOutcome = AppointmentStatusType.RescheduledNoBill }));

        (await LoadAsync(rig)).RequestStatus.ShouldBe(RequestStatusType.Accepted);
    }

    [Fact]
    public async Task Confirming_a_date_refuses_a_missing_or_wrong_outcome_and_stores_a_valid_one()
    {
        var rig = await SeedRescheduleAsync(null, false, false, new string?[2]);
        // A fresh request with NO round, so confirm has something to open.
        var (office, _) = await GetSeededOfficesAsync();
        var requestId = Guid.NewGuid();
        await InOfficeAsync(office, () => SeedRescheduleRequestAsync(office, requestId, rig.AppointmentId));

        foreach (var bad in new[] { default, AppointmentStatusType.Approved, AppointmentStatusType.CancelledNoBill })
        {
            await InOfficeAsync(office, async () =>
            {
                var ex = await Should.ThrowAsync<BusinessException>(() => _approval.ConfirmRescheduleDateAsync(
                    requestId,
                    new ConfirmRescheduleDateInput { DoctorAvailabilityId = rig.NewSlotId, RescheduleOutcome = bad }));
                ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestInvalidRescheduleOutcome);
            });
        }

        await InOfficeAsync(office, () => _approval.ConfirmRescheduleDateAsync(
            requestId,
            new ConfirmRescheduleDateInput
            {
                DoctorAvailabilityId = rig.NewSlotId,
                RescheduleOutcome = AppointmentStatusType.RescheduledLate,
            }));

        await InOfficeAsync(office, async () =>
            (await RoundRepository.GetCurrentAsync(requestId))!.ProposedOutcome
                .ShouldBe(AppointmentStatusType.RescheduledLate));
    }
}
