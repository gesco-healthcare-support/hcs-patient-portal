using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Notifications.Handlers;
using HealthcareSupport.CaseEvaluation.Notifications.Jobs;
using HealthcareSupport.CaseEvaluation.NotificationTemplates;
using HealthcareSupport.CaseEvaluation.Settings;
using HealthcareSupport.CaseEvaluation.TestData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.SettingManagement;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Notifications.Delivery;

/// <summary>
/// The 08:00 / 16:00 staff summaries, end to end on the real rig: <see cref="StaffSummaryJob"/> counts each
/// office's queue and raises the events, and <see cref="OfficeSummaryEmailHandler"/> /
/// <see cref="SupervisorSummaryEmailHandler"/> turn them into email. Only the dispatcher is replaced (it
/// records, including the context tag the outbox dedupes on).
///
/// <para>Office A holds a Pending appointment and office B an Approved one, so a Reschedule-Requested status
/// is set on B where a test needs B outstanding. Every address is a synthetic test.local one. Decoys: a
/// switched-off Intake user, a Staff Supervisor INSIDE an office, a user in another role, and a switched-off
/// host Supervisor.</para>
///
/// <para><b>Rig gap:</b> host and offices share ONE database here, so this proves each office's queue is read
/// in that office's scope and that host recipients are selected from the host scope, but not that production's
/// separate databases are each reached. That is the same shared-rig limit every multi-office test here has.</para>
/// </summary>
public class StaffSummaryTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private const string InboxA = "TEST-summary-inbox-a@test.local";
    private const string InboxB = "TEST-summary-inbox-b@test.local";

    private readonly TagRecorder _recorder = new();

    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<INotificationDispatcher>(_recorder));
    }

    [Fact]
    public async Task OfficeSummary_GoesToTheInboxAndEachActiveIntakeOnce_NotToSupervisorsOrSwitchedOffStaff()
    {
        var staff = await SeedOfficeAAsync();
        await SetInboxAsync(TenantsTestData.TenantARef, InboxA);
        await SeedHostSupervisorAsync(active: true);

        await RunJobAsync();

        var office = _recorder.Sent.Where(s => s.TemplateCode == NotificationTemplateConsts.Codes.OfficeDailySummary)
            .ShouldHaveSingleItem();
        office.Recipients.ShouldBe(new[] { InboxA, staff.Intake }, ignoreOrder: true);
        office.Recipients.ShouldNotContain(staff.SwitchedOffIntake);
        office.Recipients.ShouldNotContain(staff.OfficeSupervisor);
        office.Recipients.ShouldNotContain(staff.Decoy);
        ((string)office.Variables["SummaryContent"]!).ShouldContain("A90001");
    }

    [Fact]
    public async Task SupervisorSummary_IsOneMailPerActiveHostSupervisor_CoveringBothOffices()
    {
        var staff = await SeedOfficeAAsync();
        await SetInboxAsync(TenantsTestData.TenantARef, InboxA);
        await SetInboxAsync(TenantsTestData.TenantBRef, InboxB);
        await SetStatusAsync(TenantsTestData.TenantBRef, AppointmentsTestData.Appointment2Id, AppointmentStatusType.RescheduleRequested);
        var supervisor = await SeedHostSupervisorAsync(active: true);
        var switchedOff = await SeedHostSupervisorAsync(active: false);

        await RunJobAsync();

        var summary = _recorder.Sent.Where(s => s.TemplateCode == NotificationTemplateConsts.Codes.SupervisorDailySummary)
            .ShouldHaveSingleItem();
        summary.Recipients.ShouldBe(new[] { supervisor });
        summary.Recipients.ShouldNotContain(switchedOff);
        summary.Recipients.ShouldNotContain(staff.OfficeSupervisor);
        summary.Recipients.ShouldNotContain(staff.Intake);
        summary.Recipients.ShouldNotContain(InboxA);
        var content = (string)summary.Variables["SummaryContent"]!;
        content.ShouldContain(TenantsTestData.TenantAName);
        content.ShouldContain(TenantsTestData.TenantBName);

        // Each office still gets its own summary, with its own inbox only.
        _recorder.Sent.Where(s => s.TemplateCode == NotificationTemplateConsts.Codes.OfficeDailySummary)
            .SelectMany(s => s.Recipients).ShouldContain(InboxA);
        _recorder.Sent.Where(s => s.TemplateCode == NotificationTemplateConsts.Codes.OfficeDailySummary)
            .SelectMany(s => s.Recipients).ShouldContain(InboxB);
    }

    [Fact]
    public async Task AnOfficeWithNothingOutstanding_IsLeftOutOfEveryMail()
    {
        await SeedOfficeAAsync();
        await SetInboxAsync(TenantsTestData.TenantARef, InboxA);
        await SetInboxAsync(TenantsTestData.TenantBRef, InboxB);
        await SeedHostSupervisorAsync(active: true);

        await RunJobAsync();

        // B holds only an Approved appointment: information, not work.
        _recorder.Sent.SelectMany(s => s.Recipients).ShouldNotContain(InboxB);
        var content = (string)_recorder.Sent.Single(s => s.TemplateCode == NotificationTemplateConsts.Codes.SupervisorDailySummary)
            .Variables["SummaryContent"]!;
        content.ShouldContain(TenantsTestData.TenantAName);
        content.ShouldNotContain(TenantsTestData.TenantBName);
    }

    [Fact]
    public async Task NothingOutstandingAnywhere_SendsNothing()
    {
        await SeedOfficeAAsync();
        await SetInboxAsync(TenantsTestData.TenantARef, InboxA);
        await SeedHostSupervisorAsync(active: true);
        await SetStatusAsync(TenantsTestData.TenantARef, AppointmentsTestData.Appointment1Id, AppointmentStatusType.Approved);

        await RunJobAsync();

        _recorder.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheAfternoonRunIsItsOwnEmail_ARetryOfTheSameRunIsNot()
    {
        await SeedOfficeAAsync();
        await SetInboxAsync(TenantsTestData.TenantARef, InboxA);
        await SeedHostSupervisorAsync(active: true);
        var morning = new DateTime(2026, 10, 23, 15, 0, 0, DateTimeKind.Utc);       // 08:00 Pacific
        var morningRetry = morning.AddMinutes(25);
        var afternoon = new DateTime(2026, 10, 23, 23, 0, 0, DateTimeKind.Utc);     // 16:00 Pacific

        await RaiseAsync(morning);
        await RaiseAsync(morningRetry);
        await RaiseAsync(afternoon);

        foreach (var code in new[] { NotificationTemplateConsts.Codes.OfficeDailySummary, NotificationTemplateConsts.Codes.SupervisorDailySummary })
        {
            var tags = _recorder.Sent.Where(s => s.TemplateCode == code).Select(s => s.ContextTag).ToList();
            tags.Count.ShouldBe(3);
            tags[0].ShouldBe(tags[1], "a retry of the same run must collapse onto the first send");
            tags[2].ShouldNotBe(tags[0], "the 16:00 run must not collapse onto the 08:00 one");
        }
    }

    [Fact]
    public async Task AnOfficeWithNoInboxAndNoActiveIntake_SendsNoOfficeSummary()
    {
        await SeedHostSupervisorAsync(active: true);

        await RunJobAsync();

        _recorder.Sent.ShouldNotContain(s => s.TemplateCode == NotificationTemplateConsts.Codes.OfficeDailySummary);
        _recorder.Sent.ShouldContain(s => s.TemplateCode == NotificationTemplateConsts.Codes.SupervisorDailySummary);
    }

    // ------------------------------------------------------------------------

    private async Task RunJobAsync()
    {
        _recorder.Clear();
        await WithUnitOfWorkAsync(() => GetRequiredService<StaffSummaryJob>().ExecuteAsync());
    }

    /// <summary>Raises the events a run would, at a chosen time, to check the context tags.</summary>
    private async Task RaiseAsync(DateTime runUtc)
    {
        var counts = new StaffSummaryCounts { Pending = 1 };
        await WithUnitOfWorkAsync(async () =>
        {
            await GetRequiredService<OfficeSummaryEmailHandler>().HandleEventAsync(new OfficeSummaryEto
            {
                TenantId = TenantsTestData.TenantARef,
                OfficeName = TenantsTestData.TenantAName,
                OccurredAt = runUtc,
                Counts = counts,
            });
            await GetRequiredService<SupervisorSummaryEmailHandler>().HandleEventAsync(new SupervisorSummaryEto
            {
                OccurredAt = runUtc,
                Offices = { new SupervisorSummaryOffice { OfficeName = TenantsTestData.TenantAName, Counts = counts } },
            });
        });
    }

    private Task SetInboxAsync(Guid officeId, string inbox) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(officeId))
        {
            await GetRequiredService<ISettingManager>().SetForCurrentTenantAsync(
                CaseEvaluationSettings.NotificationsPolicy.OfficeEmail, inbox);
        }
    });

    private Task SetStatusAsync(Guid officeId, Guid appointmentId, AppointmentStatusType status) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(officeId))
        {
            var repository = GetRequiredService<IRepository<Appointment, Guid>>();
            var appointment = await repository.GetAsync(appointmentId);
            appointment.AppointmentStatus = status;
            await repository.UpdateAsync(appointment, autoSave: true);
        }
    });

    /// <summary>A host user in the host "Staff Supervisor" role, optionally switched off.</summary>
    private Task<string> SeedHostSupervisorAsync(bool active) => WithUnitOfWorkAsync(async () =>
    {
        using (GetRequiredService<ICurrentTenant>().Change(null))
        {
            var users = GetRequiredService<IdentityUserManager>();
            var roles = GetRequiredService<IdentityRoleManager>();
            var (email, _) = await StaffSeeder.CreateAsync(users, roles, null, "Staff Supervisor", active ? "host-sup" : "host-sup-off");
            if (!active)
            {
                var user = (await users.FindByEmailAsync(email))!;
                user.SetIsActive(false);
                (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
            }
            return email;
        }
    });

    private async Task<(string Intake, string SwitchedOffIntake, string OfficeSupervisor, string Decoy)> SeedOfficeAAsync()
    {
        (string Email, string UserName) intake = ("", ""), off = ("", ""), supervisor = ("", ""), decoy = ("", "");
        await WithUnitOfWorkAsync(async () =>
        {
            using (GetRequiredService<ICurrentTenant>().Change(TenantsTestData.TenantARef))
            {
                var users = GetRequiredService<IdentityUserManager>();
                var roles = GetRequiredService<IdentityRoleManager>();
                var office = TenantsTestData.TenantARef;
                intake = await StaffSeeder.CreateAsync(users, roles, office, "Intake Staff", "summary-intake");
                off = await StaffSeeder.CreateAsync(users, roles, office, "Intake Staff", "summary-intake-off");
                supervisor = await StaffSeeder.CreateAsync(users, roles, office, "Staff Supervisor", "summary-office-sup");
                decoy = await StaffSeeder.CreateAsync(users, roles, office, "TEST-other-role", "summary-decoy");
                var user = (await users.FindByEmailAsync(off.Email))!;
                user.SetIsActive(false);
                (await users.UpdateAsync(user)).Succeeded.ShouldBeTrue();
            }
        });
        return (intake.Email, off.Email, supervisor.Email, decoy.Email);
    }

    public sealed record Tagged(string TemplateCode, IReadOnlyList<string> Recipients, IReadOnlyDictionary<string, object?> Variables, string ContextTag);

    /// <summary>Records each dispatch with the context tag the outbox would dedupe on.</summary>
    public sealed class TagRecorder : INotificationDispatcher
    {
        private readonly List<Tagged> _sent = new();

        public IReadOnlyList<Tagged> Sent => _sent;

        public void Clear() => _sent.Clear();

        public Task DispatchAsync(
            string templateCode,
            IReadOnlyCollection<NotificationRecipient> recipients,
            IReadOnlyDictionary<string, object?> variables,
            string contextTag,
            PacketAttachmentRef? packetRef = null,
            CancellationToken cancellationToken = default)
        {
            _sent.Add(new Tagged(templateCode, recipients.Select(r => r.Email).ToList(), variables, contextTag));
            return Task.CompletedTask;
        }

        public Task DispatchToWithCcAsync(
            string templateCode,
            NotificationRecipient to,
            IReadOnlyCollection<NotificationRecipient> cc,
            IReadOnlyDictionary<string, object?> variables,
            string contextTag,
            PacketAttachmentRef? packetRef = null,
            CancellationToken cancellationToken = default)
        {
            var all = new List<string> { to.Email };
            all.AddRange(cc.Select(r => r.Email));
            _sent.Add(new Tagged(templateCode, all, variables, contextTag));
            return Task.CompletedTask;
        }
    }
}
