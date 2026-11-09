using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Delivery;

/// <summary>
/// The packet copy and the polished summary subjects through the REAL dispatcher, renderer, template
/// repository and outbox on the real database: nothing is substituted, so what is asserted is the row that
/// would be sent (recipient, rendered subject and body, and BOTH packet references surviving the round trip
/// through the database). The rig seeds only the host-scoped template codes, so each office's copy of a
/// template is inserted from its shipped seed content, as the sibling renderer tests do.
/// </summary>
public class OfficePacketCopyOutboxTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string Inbox = "TEST-copy-outbox-inbox@test.local";

    [Fact]
    public async Task TheCopyIsOneOutboxRowToTheInbox_WithBothPacketsPersisted_AndASubjectNamingTheCase()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                await EnsureTemplateAsync(NotificationTemplateConsts.Codes.OfficePacketCopy, TenantsTestData.TenantARef);
                await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                    CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, Inbox);
            }
        });
        var attyCeId = Guid.NewGuid();
        var patientId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var repository = GetRequiredService<IRepository<AppointmentPacket, Guid>>();
                foreach (var (id, kind) in new[] { (attyCeId, PacketKind.AttorneyClaimExaminer), (patientId, PacketKind.Patient), (Guid.NewGuid(), PacketKind.Doctor) })
                {
                    await repository.InsertAsync(
                        new AppointmentPacket(id, TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id,
                            kind, $"TEST-outbox-{kind}-{id:N}.pdf", PacketGenerationStatus.Generated),
                        autoSave: true);
                }
            }
        });

        await WithUnitOfWorkAsync(() => GetRequiredService<OfficePacketCopyEmailHandler>().HandleEventAsync(new PacketGeneratedEto
        {
            AppointmentId = AppointmentsTestData.Appointment1Id,
            TenantId = TenantsTestData.TenantARef,
            PacketId = patientId,
            Kind = PacketKind.Patient,
            OccurredAt = new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc),
        }));

        NotificationOutboxItem row = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var rows = await GetRequiredService<IRepository<NotificationOutboxItem, Guid>>()
                    .GetListAsync(r => r.Context.StartsWith("OfficePacketCopy/"));
                row = rows.ShouldHaveSingleItem();
            }
        });
        row.To.ShouldBe(Inbox);
        row.GetCcList().ShouldBeEmpty();
        row.Subject.ShouldStartWith("Appointment Portal - ");
        row.Subject.ShouldEndWith(" - Packet copies for the office");
        row.Subject.ShouldNotContain("##");
        row.Body.ShouldContain("Copies are attached for the office");
        row.Body.ShouldNotContain("##");
        row.PacketKind.ShouldBe(PacketKind.AttorneyClaimExaminer);
        row.PacketId.ShouldBe(attyCeId);
        var extra = row.GetExtraPacketRefs().ShouldHaveSingleItem();
        extra.Kind.ShouldBe(PacketKind.Patient);
        extra.PacketId.ShouldBe(patientId);
        extra.AppointmentId.ShouldBe(AppointmentsTestData.Appointment1Id);
    }

    [Fact]
    public async Task TheOfficeSummarySubject_NamesTheOfficeItIsRenderedFor_AndTheSupervisorSubjectNamesNoOffice()
    {
        var subjects = new Dictionary<string, string>();
        foreach (var (name, id) in new[] { (TenantsTestData.TenantAName, TenantsTestData.TenantARef), (TenantsTestData.TenantBName, TenantsTestData.TenantBRef) })
        {
            await WithUnitOfWorkAsync(async () =>
            {
                using (GetRequiredService<ICurrentTenant>().Change(id))
                {
                    await EnsureTemplateAsync(NotificationTemplateConsts.Codes.OfficeDailySummary, id);
                    var rendered = await GetRequiredService<INotificationTemplateRenderer>().RenderAsync(
                        NotificationTemplateConsts.Codes.OfficeDailySummary,
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["SummaryLabel"] = "Morning",
                            ["SummaryContent"] = "<p>TEST</p>",
                            ["PortalUrl"] = "https://portal.test.local",
                        });
                    subjects[name] = rendered.Subject;
                }
            });
        }

        string supervisorSubject = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(null))
            {
                await EnsureTemplateAsync(NotificationTemplateConsts.Codes.SupervisorDailySummary, null);
                supervisorSubject = (await GetRequiredService<INotificationTemplateRenderer>().RenderAsync(
                    NotificationTemplateConsts.Codes.SupervisorDailySummary,
                    new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["SummaryLabel"] = "Afternoon",
                        ["SummaryContent"] = "<p>TEST</p>",
                        ["PortalUrl"] = "https://portal.test.local",
                    })).Subject;
            }
        });

        // 2026-11-09: the office's practice name, not its web-address slug. The rig stores no
        // letterhead, so each office reads as its own seeded doctor.
        subjects[TenantsTestData.TenantAName].ShouldBe(
            $"Appointment Portal - Dr. {DoctorsTestData.Doctor1FirstName} {DoctorsTestData.Doctor1LastName} - Morning summary");
        subjects[TenantsTestData.TenantBName].ShouldBe(
            $"Appointment Portal - Dr. {DoctorsTestData.Doctor2FirstName} {DoctorsTestData.Doctor2LastName} - Morning summary");
        supervisorSubject.ShouldBe("Appointment Portal - Afternoon summary, all offices");
    }

    private async Task EnsureTemplateAsync(string code, Guid? tenantId)
    {
        var templates = GetRequiredService<INotificationTemplateRepository>();
        if (await templates.FindByCodeAsync(code) != null)
        {
            return;
        }

        var types = GetRequiredService<INotificationTemplateTypeRepository>();
        var type = new NotificationTemplateType(Guid.NewGuid(), "TEST-Email-" + Guid.NewGuid().ToString("N")[..6]);
        await types.InsertAsync(type, autoSave: true);

        var defaults = NotificationTemplateSeedDefaults.GetSeedDefaults(code);
        await templates.InsertAsync(
            new NotificationTemplate(
                id: Guid.NewGuid(),
                tenantId: tenantId,
                templateCode: code,
                templateTypeId: type.Id,
                subject: defaults.Subject,
                bodyEmail: defaults.BodyEmail,
                bodySms: defaults.BodySms),
            autoSave: true);
    }
}
