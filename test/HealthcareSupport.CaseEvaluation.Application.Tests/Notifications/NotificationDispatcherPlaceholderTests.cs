using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments.Notifications;
using HealthcareSupport.CaseEvaluation.EmailStops;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// 2026-11-13 -- the dispatcher never writes a mail to a placeholder address (unknown@..., na@...) that a
/// case already holds, in any of its three dispatch methods, and the one warning it logs names the context
/// tag and the role only: never the address, never the template code. Synthetic data only.
/// </summary>
public class NotificationDispatcherPlaceholderTests
{
    private const string TemplateCode = "TEST-template-code-ABC";

    private sealed class CapturingLogger : ILogger<NotificationDispatcher>
    {
        public List<string> Warnings { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warnings.Add(formatter(state, exception));
            }
        }
    }

    private sealed class Fixture
    {
        public required NotificationDispatcher Dispatcher { get; init; }
        public required List<NotificationOutboxItem> Rows { get; init; }
        public required CapturingLogger Log { get; init; }
    }

    private static Fixture Build()
    {
        var renderer = Substitute.For<INotificationTemplateRenderer>();
        renderer
            .RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RenderedNotification("TEST-subject", "TEST-body", null)));
        var tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns((Guid?)null);
        var rows = new List<NotificationOutboxItem>();
        var repo = Substitute.For<INotificationOutboxRepository>();
        repo.GetQueryableAsync().Returns(_ => rows.AsQueryable());
        repo.InsertAsync(Arg.Any<NotificationOutboxItem>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var item = ci.Arg<NotificationOutboxItem>();
                rows.Add(item);
                return Task.FromResult(item);
            });
        var uowManager = Substitute.For<IUnitOfWorkManager>();
        uowManager.Current.Returns((IUnitOfWork?)null);
        var log = new CapturingLogger();
        var dispatcher = new NotificationDispatcher(
            renderer,
            Substitute.For<IBackgroundJobManager>(),
            tenant,
            new NotificationOutboxManager(repo, SimpleGuidGenerator.Instance),
            uowManager,
            log,
            Substitute.For<IEmailStopPolicy>());
        return new Fixture { Dispatcher = dispatcher, Rows = rows, Log = log };
    }

    private static NotificationRecipient R(string email, RecipientRole role = RecipientRole.ClaimExaminer) =>
        new(email, role: role);

    [Fact]
    public async Task DispatchAsync_LeavesOffThePlaceholder_AndMailsTheRest_PositiveControlIncluded()
    {
        var f = Build();

        await f.Dispatcher.DispatchAsync(
            NotificationCase.None, TemplateCode,
            new[] { R("TEST-real@firm.test", RecipientRole.Patient), R("unknown@gmail.com"), R("na@firm.test") },
            new Dictionary<string, object?>(), "TEST-ctx/1");

        f.Rows.ShouldHaveSingleItem().To.ShouldBe("TEST-real@firm.test");
        f.Log.Warnings.Count.ShouldBe(2);
        foreach (var line in f.Log.Warnings)
        {
            line.ShouldContain("TEST-ctx/1");
            line.ShouldContain("ClaimExaminer");
            line.ShouldNotContain("unknown");
            line.ShouldNotContain("@");
            line.ShouldNotContain(TemplateCode);
        }
    }

    [Fact]
    public async Task DispatchAsync_OnlyPlaceholders_WritesNothingAndRendersNothing()
    {
        var f = Build();

        await f.Dispatcher.DispatchAsync(
            NotificationCase.None, TemplateCode, new[] { R("unknown@gmail.com") },
            new Dictionary<string, object?>(), "TEST-ctx/2");

        f.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchWithPackets_LeavesOffThePlaceholder()
    {
        var f = Build();
        var packet = new HealthcareSupport.CaseEvaluation.Appointments.PacketAttachmentRef
        {
            AppointmentId = Guid.NewGuid(),
            PacketId = Guid.NewGuid(),
            Kind = HealthcareSupport.CaseEvaluation.AppointmentDocuments.PacketKind.AttorneyClaimExaminer,
        };

        await f.Dispatcher.DispatchWithPacketsAsync(
            NotificationCase.None, TemplateCode,
            new[] { R("TEST-real@firm.test", RecipientRole.DefenseAttorney), R("xx@firm.test") },
            new Dictionary<string, object?>(), "TEST-ctx/3", new[] { packet });

        f.Rows.ShouldHaveSingleItem().To.ShouldBe("TEST-real@firm.test");
    }

    [Fact]
    public async Task DispatchToWithCc_APlaceholderCc_IsLeftOff()
    {
        var f = Build();

        await f.Dispatcher.DispatchToWithCcAsync(
            NotificationCase.None, TemplateCode, R("TEST-to@firm.test", RecipientRole.Patient),
            new[] { R("unknown@gmail.com"), R("TEST-cc@firm.test", RecipientRole.DefenseAttorney) },
            new Dictionary<string, object?>(), "TEST-ctx/4");

        var row = f.Rows.ShouldHaveSingleItem();
        row.To.ShouldBe("TEST-to@firm.test");
        row.GetCcList().ShouldBe(new[] { "TEST-cc@firm.test" });
    }

    [Fact]
    public async Task DispatchToWithCc_APlaceholderTo_HandsTheMailToTheFirstRealCc()
    {
        var f = Build();

        await f.Dispatcher.DispatchToWithCcAsync(
            NotificationCase.None, TemplateCode, R("unknown@gmail.com"),
            new[] { R("TEST-first@firm.test", RecipientRole.Patient), R("TEST-second@firm.test", RecipientRole.DefenseAttorney) },
            new Dictionary<string, object?>(), "TEST-ctx/5");

        var row = f.Rows.ShouldHaveSingleItem();
        row.To.ShouldBe("TEST-first@firm.test");
        row.GetCcList().ShouldBe(new[] { "TEST-second@firm.test" });
        f.Log.Warnings.ShouldHaveSingleItem().ShouldNotContain("unknown@");
    }

    [Fact]
    public async Task DispatchToWithCc_APlaceholderToWithNoRealCc_WritesNothing()
    {
        var f = Build();

        await f.Dispatcher.DispatchToWithCcAsync(
            NotificationCase.None, TemplateCode, R("unknown@gmail.com"),
            new[] { R("na@firm.test") },
            new Dictionary<string, object?>(), "TEST-ctx/6");

        f.Rows.ShouldBeEmpty();
    }
}
