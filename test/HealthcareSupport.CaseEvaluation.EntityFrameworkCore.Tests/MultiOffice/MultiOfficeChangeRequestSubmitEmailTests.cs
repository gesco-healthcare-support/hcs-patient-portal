using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;
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
/// Pins which change-request SUBMITS send a stakeholder email (Adrian, 2026-08-05).
///
/// <para>A RESCHEDULE submit sends none: the consent email dispatched when staff confirm a date
/// already tells both sides a reschedule was requested AND names the date, which the submit
/// email could not -- since 4b the external path leaves <c>NewDoctorAvailabilityId</c> null, so
/// its date variables rendered empty. A CANCELLATION submit keeps its email, because
/// cancellation consent is issued at submit and there is no later message to fold it into.</para>
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
    public async Task A_reschedule_submit_queues_no_stakeholder_email()
    {
        var (office, _) = await GetSeededOfficesAsync();
        var changeRequestId = Guid.NewGuid();

        await InOfficeAsync(office, async () =>
        {
            await SeedTemplatesAsync(office.OfficeId);
            await SeedRescheduleRequestAsync(office, changeRequestId);
        });

        await InOfficeAsync(office, () => PublishSubmittedAsync(
            office, changeRequestId, ChangeRequestType.Reschedule));

        await InOfficeAsync(office, async () =>
            (await CountSubmitRowsAsync(changeRequestId)).ShouldBe(0));
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
