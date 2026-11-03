using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Guids;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Pins that change-request SUBMITS send a stakeholder email. A RESCHEDULE submit was silent from
/// 2026-08-05 (its date variables rendered empty on the external path, which leaves
/// <c>NewDoctorAvailabilityId</c> null); 2026-11-03 (email routing, row 7) sends it again with a details
/// block that omits the date line when no date was proposed. A CANCELLATION submit always sent one.
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeChangeRequestSubmitEmailTests : ConsentRoundTestBase
{
    private static readonly Guid EmailTypeId = Guid.Parse("c0000001-0000-4000-9000-000000000001");

    private readonly ILocalEventBus _localEventBus;
    private readonly IRepository<NotificationOutboxItem, Guid> _outboxRepository;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly INotificationTemplateTypeRepository _templateTypeRepository;
    private readonly IGuidGenerator _guidGenerator;

    public MultiOfficeChangeRequestSubmitEmailTests()
    {
        _localEventBus = GetRequiredService<ILocalEventBus>();
        _outboxRepository = GetRequiredService<IRepository<NotificationOutboxItem, Guid>>();
        _templateRepository = GetRequiredService<INotificationTemplateRepository>();
        _templateTypeRepository = GetRequiredService<INotificationTemplateTypeRepository>();
        _guidGenerator = GetRequiredService<IGuidGenerator>();
    }

    [Fact]
    public async Task A_reschedule_submit_queues_the_reschedule_request_email()
    {
        var (office, _) = await GetSeededOfficesAsync();
        var changeRequestId = Guid.NewGuid();

        await InOfficeAsync(office, async () =>
        {
            await SeedTemplatesAsync(office.OfficeId);
            await SeedRescheduleRequestAsync(office, changeRequestId);

            // Nobody told the patient asked, so the patient is left off: the notice needs another party
            // (an attorney named on the appointment) to have anyone to go to.
            var appointments = GetRequiredService<IRepository<Appointment, Guid>>();
            var appointment = await appointments.GetAsync(office.AppointmentId);
            appointment.ApplicantAttorneyEmail = "TEST-reschedule-aa@test.local";
            await appointments.UpdateAsync(appointment, autoSave: true);
        });

        await InOfficeAsync(office, () => PublishSubmittedAsync(
            office, changeRequestId, ChangeRequestType.Reschedule));

        await InOfficeAsync(office, async () =>
            (await CountSubmitRowsAsync(changeRequestId)).ShouldBeGreaterThan(0));
    }

    [Fact]
    public async Task A_cancellation_submit_still_queues_its_stakeholder_email()
    {
        var (office, _) = await GetSeededOfficesAsync();
        var changeRequestId = Guid.NewGuid();

        await InOfficeAsync(office, async () =>
        {
            await SeedTemplatesAsync(office.OfficeId);
            await ChangeRequestRepository.InsertAsync(
                new AppointmentChangeRequest(
                    id: changeRequestId,
                    tenantId: office.OfficeId,
                    appointmentId: office.AppointmentId,
                    changeRequestType: ChangeRequestType.Cancel,
                    cancellationReason: "TEST-cancellation-reason",
                    reScheduleReason: null,
                    newDoctorAvailabilityId: null),
                autoSave: true);
        });

        await InOfficeAsync(office, () => PublishSubmittedAsync(
            office, changeRequestId, ChangeRequestType.Cancel));

        await InOfficeAsync(office, async () =>
            (await CountSubmitRowsAsync(changeRequestId)).ShouldBeGreaterThan(0));
    }

    private Task PublishSubmittedAsync(
        SeededOffice office, Guid changeRequestId, ChangeRequestType type) =>
        _localEventBus.PublishAsync(new AppointmentChangeRequestSubmittedEto
        {
            AppointmentId = office.AppointmentId,
            ChangeRequestId = changeRequestId,
            TenantId = office.OfficeId,
            ChangeRequestType = type,
            OccurredAt = DateTime.UtcNow,
        });

    private async Task<int> CountSubmitRowsAsync(Guid changeRequestId)
    {
        var rows = await _outboxRepository.GetListAsync(
            x => x.Context.Contains(changeRequestId.ToString()));
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
        foreach (var code in NotificationTemplateConsts.Codes.All)
        {
            if (existing.Contains(code))
            {
                continue;
            }
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
