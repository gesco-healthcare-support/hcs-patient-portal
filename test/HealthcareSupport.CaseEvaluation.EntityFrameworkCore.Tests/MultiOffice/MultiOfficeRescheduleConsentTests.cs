using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.DoctorAvailabilities;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// End-to-end cover for the phase 4c reschedule consent flow (2026-08-05) through the REAL
/// approval app service: confirm opens a round and sends, confirming the same date resends
/// inside it, confirming a different date supersedes it and opens the next, and finalize is
/// blocked until the round's solicited sides have agreed.
///
/// <para>Consent state is set DIRECTLY on the round in setup -- a tokenised email click cannot
/// be driven from a test, and the raw token is never persisted.</para>
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeRescheduleConsentTests : ConsentRoundTestBase
{
    private static readonly Guid EmailTypeId = Guid.Parse("c0000001-0000-4000-9000-000000000001");

    private readonly IAppointmentChangeRequestsApprovalAppService _approvalAppService;
    private readonly IChangeRequestConsentRoundRepository _roundRepository;
    private readonly IRepository<AppointmentChangeRequest, Guid> _changeRequestRepository;
    private readonly IRepository<DoctorAvailability, Guid> _slotRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<NotificationOutboxItem, Guid> _outboxRepository;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly INotificationTemplateTypeRepository _templateTypeRepository;
    private readonly IGuidGenerator _guidGenerator;

    public MultiOfficeRescheduleConsentTests()
    {
        _approvalAppService = GetRequiredService<IAppointmentChangeRequestsApprovalAppService>();
        _roundRepository = GetRequiredService<IChangeRequestConsentRoundRepository>();
        _changeRequestRepository = GetRequiredService<IRepository<AppointmentChangeRequest, Guid>>();
        _slotRepository = GetRequiredService<IRepository<DoctorAvailability, Guid>>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _outboxRepository = GetRequiredService<IRepository<NotificationOutboxItem, Guid>>();
        _templateRepository = GetRequiredService<INotificationTemplateRepository>();
        _templateTypeRepository = GetRequiredService<INotificationTemplateTypeRepository>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task Confirming_a_date_opens_round_one_on_that_slot()
    {
        var scenario = await NewScenarioAsync();

        await InOfficeAsync(scenario.Office, async () =>
        {
            await _approvalAppService.ConfirmRescheduleDateAsync(
                scenario.ChangeRequestId,
                new ConfirmRescheduleDateInput
                {
                    DoctorAvailabilityId = scenario.FirstSlotId,
                    AdminReScheduleReason = "TEST-first offer",
                });
        });

        await InOfficeAsync(scenario.Office, async () =>
        {
            var round = await _roundRepository.GetCurrentAsync(scenario.ChangeRequestId);

            round.ShouldNotBeNull();
            round!.RoundNumber.ShouldBe(1);
            round.ProposedDoctorAvailabilityId.ShouldBe(scenario.FirstSlotId);
            round.ProposedReason.ShouldBe("TEST-first offer");
            round.SendAttempts.ShouldBe(1);
            round.SupersededAt.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Confirming_a_date_asks_at_least_one_side_and_queues_its_email()
    {
        var scenario = await NewScenarioAsync();

        await InOfficeAsync(scenario.Office, () =>
            _approvalAppService.ConfirmRescheduleDateAsync(
                scenario.ChangeRequestId,
                new ConfirmRescheduleDateInput { DoctorAvailabilityId = scenario.FirstSlotId }));

        await InOfficeAsync(scenario.Office, async () =>
        {
            var round = (await _roundRepository.GetCurrentAsync(scenario.ChangeRequestId))!;

            // The seeded office has a patient, so Side A always has a representative to ask.
            round.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Pending);
            round.SideConsentTokenHash(ChangeRequestSide.SideA).ShouldNotBeNullOrWhiteSpace();

            (await CountConsentOutboxRowsAsync(scenario.ChangeRequestId)).ShouldBe(1);
        });
    }

    [Fact]
    public async Task Confirming_the_same_date_again_resends_inside_the_round_instead_of_opening_a_new_one()
    {
        var scenario = await NewScenarioAsync();
        var input = new ConfirmRescheduleDateInput { DoctorAvailabilityId = scenario.FirstSlotId };

        await InOfficeAsync(scenario.Office, () =>
            _approvalAppService.ConfirmRescheduleDateAsync(scenario.ChangeRequestId, input));
        await InOfficeAsync(scenario.Office, () =>
            _approvalAppService.ConfirmRescheduleDateAsync(scenario.ChangeRequestId, input));

        await InOfficeAsync(scenario.Office, async () =>
        {
            var rounds = await _roundRepository.GetListAsync(
                r => r.AppointmentChangeRequestId == scenario.ChangeRequestId);

            rounds.Count.ShouldBe(1);
            rounds[0].RoundNumber.ShouldBe(1);
            rounds[0].SendAttempts.ShouldBe(2);

            // The attempt discriminator is what stops the outbox collapsing the resend onto the
            // first row: same tenant, same recipient, same template -- only the tag differs.
            (await CountConsentOutboxRowsAsync(scenario.ChangeRequestId)).ShouldBe(2);
        });
    }

    [Fact]
    public async Task Confirming_a_different_date_supersedes_round_one_and_opens_round_two()
    {
        var scenario = await NewScenarioAsync();

        await InOfficeAsync(scenario.Office, () =>
            _approvalAppService.ConfirmRescheduleDateAsync(
                scenario.ChangeRequestId,
                new ConfirmRescheduleDateInput { DoctorAvailabilityId = scenario.FirstSlotId }));
        await InOfficeAsync(scenario.Office, () =>
            _approvalAppService.ConfirmRescheduleDateAsync(
                scenario.ChangeRequestId,
                new ConfirmRescheduleDateInput { DoctorAvailabilityId = scenario.SecondSlotId }));

        await InOfficeAsync(scenario.Office, async () =>
        {
            var rounds = (await _roundRepository.GetListAsync(
                r => r.AppointmentChangeRequestId == scenario.ChangeRequestId))
                .OrderBy(r => r.RoundNumber)
                .ToList();

            rounds.Count.ShouldBe(2);
            rounds[0].SupersededAt.ShouldNotBeNull();
            rounds[0].ProposedDoctorAvailabilityId.ShouldBe(scenario.FirstSlotId);
            rounds[1].RoundNumber.ShouldBe(2);
            rounds[1].SupersededAt.ShouldBeNull();
            rounds[1].ProposedDoctorAvailabilityId.ShouldBe(scenario.SecondSlotId);

            // THE contextTag regression: with a tag that omits the round, round 2's email keys
            // identically to round 1's and NotificationOutboxManager silently returns the
            // existing row -- no throw, no log, no second email. This assertion is the guard.
            (await CountConsentOutboxRowsAsync(scenario.ChangeRequestId)).ShouldBe(2);
        });
    }

    [Fact]
    public async Task Finalize_is_blocked_while_a_solicited_side_has_not_agreed()
    {
        var scenario = await NewScenarioAsync();

        await InOfficeAsync(scenario.Office, () =>
            _approvalAppService.ConfirmRescheduleDateAsync(
                scenario.ChangeRequestId,
                new ConfirmRescheduleDateInput { DoctorAvailabilityId = scenario.FirstSlotId }));

        await InOfficeAsync(scenario.Office, async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _approvalAppService.ApproveRescheduleAsync(
                    scenario.ChangeRequestId,
                    new ApproveRescheduleInput
                    {
                        RescheduleOutcome = AppointmentStatusType.RescheduledNoBill,
                    }));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotGranted);
        });

        await InOfficeAsync(scenario.Office, async () =>
        {
            var appointment = await _appointmentRepository.GetAsync(scenario.AppointmentId);
            appointment.DoctorAvailabilityId.ShouldBe(scenario.OriginSlotId);
        });
    }

    [Fact]
    public async Task Finalize_is_blocked_when_no_date_has_been_confirmed_at_all()
    {
        // The parent's own consent columns are both NotRequired on a reschedule, and the cancel
        // gate reads both-NotRequired as "nothing to consent" -- so without the round-shaped gate
        // this call would succeed with zero consent recorded.
        var scenario = await NewScenarioAsync();

        await InOfficeAsync(scenario.Office, async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _approvalAppService.ApproveRescheduleAsync(
                    scenario.ChangeRequestId,
                    new ApproveRescheduleInput
                    {
                        RescheduleOutcome = AppointmentStatusType.RescheduledNoBill,
                    }));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotGranted);
        });
    }

    [Fact]
    public async Task Finalize_moves_the_appointment_to_the_consented_round_slot()
    {
        var scenario = await NewScenarioAsync();

        await InOfficeAsync(scenario.Office, () =>
            _approvalAppService.ConfirmRescheduleDateAsync(
                scenario.ChangeRequestId,
                new ConfirmRescheduleDateInput
                {
                    DoctorAvailabilityId = scenario.SecondSlotId,
                    AdminReScheduleReason = "TEST-agreed offer",
                }));

        await GrantEverySolicitedSideAsync(scenario.ChangeRequestId, scenario.Office);

        await InOfficeAsync(scenario.Office, () =>
            _approvalAppService.ApproveRescheduleAsync(
                scenario.ChangeRequestId,
                new ApproveRescheduleInput
                {
                    RescheduleOutcome = AppointmentStatusType.RescheduledNoBill,
                }));

        await InOfficeAsync(scenario.Office, async () =>
        {
            var appointment = await _appointmentRepository.GetAsync(scenario.AppointmentId);
            appointment.DoctorAvailabilityId.ShouldBe(scenario.SecondSlotId);
            appointment.AppointmentDate.Date.ShouldBe(scenario.SecondSlotDate.Date);

            var changeRequest = await _changeRequestRepository.GetAsync(scenario.ChangeRequestId);
            changeRequest.RequestStatus.ShouldBe(RequestStatusType.Accepted);
            // The approval email resolves its date from this column, so finalize must write it.
            changeRequest.AdminOverrideSlotId.ShouldBe(scenario.SecondSlotId);
            changeRequest.CancellationOutcome.ShouldBe(AppointmentStatusType.RescheduledNoBill);
        });
    }

    [Fact]
    public async Task Resending_without_a_confirmed_date_is_rejected()
    {
        var scenario = await NewScenarioAsync();

        await InOfficeAsync(scenario.Office, async () =>
        {
            var ex = await Should.ThrowAsync<BusinessException>(() =>
                _approvalAppService.ResendConsentRequestAsync(scenario.ChangeRequestId));

            ex.Code.ShouldBe(CaseEvaluationDomainErrorCodes.ChangeRequestNewSlotRequired);
        });
    }

    [Fact]
    public async Task Resending_mints_a_new_token_for_the_side_that_has_not_answered()
    {
        // Only the token HASH is stored, so the old URL cannot be rebuilt -- the resend therefore
        // replaces the token and the previous email's link stops working (Adrian, 2026-08-05).
        var scenario = await NewScenarioAsync();
        string originalHash = null!;

        await InOfficeAsync(scenario.Office, () =>
            _approvalAppService.ConfirmRescheduleDateAsync(
                scenario.ChangeRequestId,
                new ConfirmRescheduleDateInput { DoctorAvailabilityId = scenario.FirstSlotId }));

        await InOfficeAsync(scenario.Office, async () =>
        {
            var round = (await _roundRepository.GetCurrentAsync(scenario.ChangeRequestId))!;
            originalHash = round.SideConsentTokenHash(ChangeRequestSide.SideA)!;
        });

        await InOfficeAsync(scenario.Office, () =>
            _approvalAppService.ResendConsentRequestAsync(scenario.ChangeRequestId));

        await InOfficeAsync(scenario.Office, async () =>
        {
            var round = (await _roundRepository.GetCurrentAsync(scenario.ChangeRequestId))!;

            round.SendAttempts.ShouldBe(2);
            round.RoundNumber.ShouldBe(1);
            round.SideConsentTokenHash(ChangeRequestSide.SideA).ShouldNotBe(originalHash);
            round.SideConsentStatus(ChangeRequestSide.SideA).ShouldBe(ChangeRequestConsentStatus.Pending);
        });
    }

    // ---- setup helpers ----

    private sealed record Scenario(
        SeededOffice Office,
        Guid AppointmentId,
        Guid ChangeRequestId,
        Guid OriginSlotId,
        Guid FirstSlotId,
        Guid SecondSlotId,
        DateTime SecondSlotDate);

    /// <summary>
    /// A fresh APPOINTMENT, change request and two scratch slots per test, so the shared seeded
    /// office data is never mutated.
    ///
    /// <para>The dedicated appointment is not optional: finalize MOVES the appointment it acts
    /// on, and reusing the office's shared one broke
    /// <c>MultiOfficeAppointmentsAppServiceTests</c> in a full-suite run while every test still
    /// passed in isolation. Slot dates are anchored to TODAY because
    /// <c>BookingPolicyValidator</c> compares against <c>DateTime.Today</c>: the office's own
    /// seeded slot is in the past and would be rejected by the lead-time gate.</para>
    /// </summary>
    private async Task<Scenario> NewScenarioAsync()
    {
        var (office, _) = await GetSeededOfficesAsync();
        var appointmentId = Guid.NewGuid();
        var changeRequestId = Guid.NewGuid();
        var originSlotId = Guid.NewGuid();
        var firstSlotId = Guid.NewGuid();
        var secondSlotId = Guid.NewGuid();
        var originSlotDate = DateTime.Today.AddDays(20);
        var firstSlotDate = DateTime.Today.AddDays(30);
        var secondSlotDate = DateTime.Today.AddDays(40);

        await InOfficeAsync(office, async () =>
        {
            await SeedTemplatesAsync(office.OfficeId);
            await SeedSlotAsync(office, originSlotId, originSlotDate, new TimeOnly(8, 0));
            await SeedSlotAsync(office, firstSlotId, firstSlotDate, new TimeOnly(9, 0));
            await SeedSlotAsync(office, secondSlotId, secondSlotDate, new TimeOnly(10, 30));

            // (TenantId, RequestConfirmationNumber) is uniquely indexed, so the number must be
            // distinct per scenario.
            await _appointmentRepository.InsertAsync(
                new Appointment(
                    id: appointmentId,
                    patientId: office.PatientId,
                    identityUserId: null,
                    appointmentTypeId: office.AppointmentTypeId,
                    locationId: office.LocationId,
                    doctorAvailabilityId: originSlotId,
                    appointmentDate: originSlotDate.Date.AddHours(8),
                    requestConfirmationNumber: $"RCN-4C-{appointmentId:N}"[..20],
                    appointmentStatus: AppointmentStatusType.Approved),
                autoSave: true);

            await _changeRequestRepository.InsertAsync(
                new AppointmentChangeRequest(
                    id: changeRequestId,
                    tenantId: office.OfficeId,
                    appointmentId: appointmentId,
                    changeRequestType: ChangeRequestType.Reschedule,
                    cancellationReason: null,
                    reScheduleReason: "TEST-requestor reason",
                    newDoctorAvailabilityId: null),
                autoSave: true);
        });

        return new Scenario(
            office,
            appointmentId,
            changeRequestId,
            originSlotId,
            firstSlotId,
            secondSlotId,
            secondSlotDate);
    }

    private async Task SeedSlotAsync(SeededOffice office, Guid slotId, DateTime date, TimeOnly fromTime)
    {
        var slot = new DoctorAvailability(
            id: slotId,
            locationId: office.LocationId,
            availableDate: date,
            fromTime: fromTime,
            toTime: fromTime.AddHours(1),
            bookingStatusId: BookingStatus.Available);
        slot.TenantId = office.OfficeId;
        slot.AddAppointmentType(office.AppointmentTypeId);
        await _slotRepository.InsertAsync(slot, autoSave: true);
    }

    /// <summary>
    /// Approves every side the round actually solicited. A tokenised email click cannot be driven
    /// from a test and the raw token is never persisted, so the decision is recorded straight on
    /// the round -- the same shortcut the phase-2 live check used.
    /// </summary>
    private async Task GrantEverySolicitedSideAsync(Guid changeRequestId, SeededOffice office)
    {
        await InOfficeAsync(office, async () =>
        {
            var round = (await _roundRepository.GetCurrentAsync(changeRequestId))!;
            foreach (var side in new[] { ChangeRequestSide.SideA, ChangeRequestSide.SideB })
            {
                if (round.SideConsentStatus(side) == ChangeRequestConsentStatus.Pending)
                {
                    round.RecordSideDecision(side, approved: true, $"rep-{side}@example.test", DateTime.UtcNow);
                }
            }
            await _roundRepository.UpdateAsync(round, autoSave: true);
        });
    }

    private async Task<int> CountConsentOutboxRowsAsync(Guid changeRequestId)
    {
        var prefix = $"ChangeRequestConsent/{changeRequestId}";
        var rows = await _outboxRepository.GetListAsync(x => x.Context.StartsWith(prefix));
        return rows.Count;
    }

    private async Task SeedTemplatesAsync(Guid officeId)
    {
        if (await _templateTypeRepository.FindAsync(EmailTypeId) == null)
        {
            await _templateTypeRepository.InsertAsync(
                new NotificationTemplateType(EmailTypeId, "Email"), autoSave: true);
        }

        var queryable = await _templateRepository.GetQueryableAsync();
        var existing = queryable.Select(x => x.TemplateCode).ToList();
        foreach (var code in NotificationTemplateConsts.Codes.All.Where(c => !existing.Contains(c)))
        {
            await _templateRepository.InsertAsync(new NotificationTemplate(
                id: _guidGenerator.Create(),
                tenantId: officeId,
                templateCode: code,
                templateTypeId: EmailTypeId,
                subject: $"[{code}] -- TEST",
                bodyEmail: $"<p>Stub for {code}</p>",
                bodySms: $"Stub for {code}",
                description: null,
                isActive: true), autoSave: true);
        }
    }

}
