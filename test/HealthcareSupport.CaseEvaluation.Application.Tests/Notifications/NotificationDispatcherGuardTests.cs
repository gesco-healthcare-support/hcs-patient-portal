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
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Unit coverage for the two guards in <see cref="NotificationDispatcher.DispatchAsync"/> that
/// <c>NotificationDispatcherCcUnitTests</c> does not reach: an empty recipient list stops before
/// anything is rendered, and a recipient with no email address is skipped without blocking the rest.
///
/// <para><b>The result asserted is the outbox rows written</b>, the dispatcher's only output. Same
/// fixture as the CC tests: a real <see cref="NotificationOutboxManager"/> over an in-memory row list,
/// and a substituted renderer. No mail is sent -- rows are only written, never drained.</para>
///
/// <para>Synthetic data only (HIPAA).</para>
/// </summary>
public class NotificationDispatcherGuardTests
{
    private sealed class Fixture
    {
        public required NotificationDispatcher Dispatcher { get; init; }
        public required List<NotificationOutboxItem> Rows { get; init; }
        public required INotificationTemplateRenderer Renderer { get; init; }
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

        var dispatcher = new NotificationDispatcher(
            renderer,
            Substitute.For<IBackgroundJobManager>(),
            tenant,
            new NotificationOutboxManager(repo, SimpleGuidGenerator.Instance),
            uowManager,
            NullLogger<NotificationDispatcher>.Instance);
        return new Fixture { Dispatcher = dispatcher, Rows = rows, Renderer = renderer };
    }

    /// <summary>
    /// <b>POSITIVE CONTROL.</b> One addressed recipient produces one outbox row. The two guard Facts
    /// below depend on this fixture being able to write rows at all.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_OneRecipient_WritesOneRow_PositiveControl()
    {
        var f = Build();

        await f.Dispatcher.DispatchAsync(
            "TEST-template",
            new[] { new NotificationRecipient("TEST-one@test.local", role: RecipientRole.Patient) },
            new Dictionary<string, object?>(),
            "TEST-ctx/1");

        f.Rows.ShouldHaveSingleItem().To.ShouldBe("TEST-one@test.local");
    }

    /// <summary>
    /// An empty recipient list short-circuits BEFORE the template is rendered -- no render cost and no
    /// row -- rather than rendering an email nobody receives.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_NoRecipients_RendersNothingAndWritesNoRow()
    {
        var f = Build();

        await f.Dispatcher.DispatchAsync(
            "TEST-template", Array.Empty<NotificationRecipient>(), new Dictionary<string, object?>(), "TEST-ctx/2");

        f.Rows.ShouldBeEmpty();
        await f.Renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
    }

    /// <summary>
    /// A recipient whose email is blank is skipped -- and does not stop the addressed recipients in the
    /// same dispatch from getting their rows.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_ARecipientWithNoEmail_IsSkippedWithoutBlockingTheRest()
    {
        var f = Build();

        await f.Dispatcher.DispatchAsync(
            "TEST-template",
            new[]
            {
                new NotificationRecipient(string.Empty, role: RecipientRole.Patient),
                new NotificationRecipient("TEST-two@test.local", role: RecipientRole.ApplicantAttorney),
                new NotificationRecipient("   ", role: RecipientRole.DefenseAttorney),
            },
            new Dictionary<string, object?>(),
            "TEST-ctx/3");

        f.Rows.Select(r => r.To).ShouldBe(new[] { "TEST-two@test.local" });
    }
}
