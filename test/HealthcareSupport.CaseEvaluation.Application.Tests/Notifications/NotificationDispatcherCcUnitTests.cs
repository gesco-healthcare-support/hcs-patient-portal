using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// E1 (2026-06-03) -- unit tests for the CC plumbing in
/// <see cref="NotificationDispatcher.DispatchToWithCcAsync"/>: render once, write
/// exactly ONE outbox row addressed To the primary with the rest CC'd (the To
/// address + duplicates dropped). T10 (2026-07-15): dispatch now writes a Pending
/// outbox row instead of enqueueing SendAppointmentEmailJob directly, so the
/// assertions target the row.
/// </summary>
public class NotificationDispatcherCcUnitTests
{
    private sealed class Fixture
    {
        public required NotificationDispatcher Dispatcher { get; init; }
        public required List<NotificationOutboxItem> Rows { get; init; }
    }

    private static Fixture Build()
    {
        var renderer = Substitute.For<INotificationTemplateRenderer>();
        renderer
            .RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RenderedNotification("Subj", "Body", null)));

        var jobs = Substitute.For<IBackgroundJobManager>();

        var tenant = Substitute.For<ICurrentTenant>();
        tenant.Name.Returns("Falkinstein");
        tenant.Id.Returns((Guid?)null);

        var rows = new List<NotificationOutboxItem>();
        var repo = Substitute.For<IRepository<NotificationOutboxItem, Guid>>();
        repo.GetQueryableAsync().Returns(_ => rows.AsQueryable());
        repo.InsertAsync(Arg.Any<NotificationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var item = ci.Arg<NotificationOutboxItem>();
                rows.Add(item);
                return Task.FromResult(item);
            });
        var outboxManager = new NotificationOutboxManager(repo, SimpleGuidGenerator.Instance);

        // No ambient UoW in this unit test -> ScheduleDrainAsync enqueues the drain
        // immediately (harmless here); the assertions are on the written rows.
        var uowManager = Substitute.For<IUnitOfWorkManager>();
        uowManager.Current.Returns((IUnitOfWork?)null);

        var dispatcher = new NotificationDispatcher(
            renderer, jobs, tenant, outboxManager, uowManager, NullLogger<NotificationDispatcher>.Instance);
        return new Fixture { Dispatcher = dispatcher, Rows = rows };
    }

    [Fact]
    public async Task DispatchToWithCc_WritesOneRow_ToPrimary_CcRest_ToExcludedAndDeduped()
    {
        var f = Build();
        var to = new NotificationRecipient("booker@gesco.com", role: RecipientRole.Patient, isRegistered: true);
        var cc = new[]
        {
            new NotificationRecipient("aa@gesco.com", role: RecipientRole.ApplicantAttorney),
            new NotificationRecipient("da@gesco.com", role: RecipientRole.DefenseAttorney),
            new NotificationRecipient("booker@gesco.com", role: RecipientRole.Patient), // == To -> dropped
            new NotificationRecipient("AA@gesco.com", role: RecipientRole.ApplicantAttorney), // case-dup -> dropped
        };

        await f.Dispatcher.DispatchToWithCcAsync(
            "AppointmentRequested", to, cc, new Dictionary<string, object?>(), "ctx/1");

        var row = f.Rows.ShouldHaveSingleItem();
        row.To.ShouldBe("booker@gesco.com");
        row.Subject.ShouldBe("Subj");
        row.Body.ShouldBe("Body");
        var ccList = row.GetCcList();
        ccList.Count.ShouldBe(2);
        ccList.ShouldContain("aa@gesco.com");
        ccList.ShouldContain("da@gesco.com");
        ccList.ShouldNotContain("booker@gesco.com");
    }

    [Fact]
    public async Task DispatchToWithCc_EmptyTo_WritesNoRow()
    {
        var f = Build();
        var to = new NotificationRecipient(string.Empty, role: RecipientRole.Patient);

        await f.Dispatcher.DispatchToWithCcAsync(
            "AppointmentRequested", to, Array.Empty<NotificationRecipient>(),
            new Dictionary<string, object?>(), "ctx/1");

        f.Rows.ShouldBeEmpty();
    }
}
