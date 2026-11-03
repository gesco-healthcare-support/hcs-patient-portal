using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
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
/// 2026-11-03 -- <see cref="NotificationDispatcher.DispatchWithPacketsAsync"/>, the one-email-several-packets
/// send behind the office inbox's packet copy: ONE outbox row per recipient carrying the first packet in the
/// existing columns and the rest beside it, in order. Synthetic ids and addresses only.
/// </summary>
public class NotificationDispatcherPacketCopyUnitTests
{
    private static readonly Guid AppointmentId = new("7e57c000-0000-4000-9000-0000000000a1");

    private static (NotificationDispatcher Dispatcher, List<NotificationOutboxItem> Rows, IBackgroundJobManager Jobs) Build()
    {
        var renderer = Substitute.For<INotificationTemplateRenderer>();
        renderer
            .RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, object?>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new RenderedNotification("Subj", "Body", null)));
        var jobs = Substitute.For<IBackgroundJobManager>();
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
            renderer, jobs, tenant, new NotificationOutboxManager(repo, SimpleGuidGenerator.Instance),
            uowManager, NullLogger<NotificationDispatcher>.Instance);
        return (dispatcher, rows, jobs);
    }

    private static PacketAttachmentRef Packet(PacketKind kind) =>
        new() { AppointmentId = AppointmentId, PacketId = Guid.NewGuid(), Kind = kind };

    [Fact]
    public async Task WritesOneRowPerRecipient_WithTheFirstPacketInTheColumnsAndTheRestAlongside()
    {
        var (dispatcher, rows, jobs) = Build();
        var attyCe = Packet(PacketKind.AttorneyClaimExaminer);
        var patient = Packet(PacketKind.Patient);

        await dispatcher.DispatchWithPacketsAsync(
            "OfficePacketCopy",
            new[] { new NotificationRecipient("TEST-inbox@test.local", role: RecipientRole.OfficeAdmin) },
            new Dictionary<string, object?>(),
            "OfficePacketCopy/x/v0",
            new[] { attyCe, patient });

        var row = rows.ShouldHaveSingleItem();
        row.To.ShouldBe("TEST-inbox@test.local");
        row.PacketId.ShouldBe(attyCe.PacketId);
        row.PacketKind.ShouldBe(PacketKind.AttorneyClaimExaminer);
        row.PacketAppointmentId.ShouldBe(AppointmentId);
        var extra = row.GetExtraPacketRefs().ShouldHaveSingleItem();
        extra.PacketId.ShouldBe(patient.PacketId);
        extra.Kind.ShouldBe(PacketKind.Patient);
        await jobs.Received().EnqueueAsync(Arg.Any<OutboxDrainArgs>(), Arg.Any<BackgroundJobPriority>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task ARepeatOfTheSameSend_CollapsesOntoTheFirstRow()
    {
        var (dispatcher, rows, _) = Build();
        var refs = new[] { Packet(PacketKind.AttorneyClaimExaminer), Packet(PacketKind.Patient) };
        var to = new[] { new NotificationRecipient("TEST-inbox@test.local", role: RecipientRole.OfficeAdmin) };

        await dispatcher.DispatchWithPacketsAsync("OfficePacketCopy", to, new Dictionary<string, object?>(), "OfficePacketCopy/x/v0", refs);
        await dispatcher.DispatchWithPacketsAsync("OfficePacketCopy", to, new Dictionary<string, object?>(), "OfficePacketCopy/x/v0", refs);
        await dispatcher.DispatchWithPacketsAsync("OfficePacketCopy", to, new Dictionary<string, object?>(), "OfficePacketCopy/x/v1", refs);

        rows.Count.ShouldBe(2, "the repeat of v0 collapses; the re-rendered v1 is a new copy");
    }

    [Fact]
    public async Task NoRecipientsOrNoPackets_WritesNothing()
    {
        var (dispatcher, rows, _) = Build();

        await dispatcher.DispatchWithPacketsAsync(
            "OfficePacketCopy", Array.Empty<NotificationRecipient>(), new Dictionary<string, object?>(),
            "OfficePacketCopy/x/v0", new[] { Packet(PacketKind.Patient) });
        await dispatcher.DispatchWithPacketsAsync(
            "OfficePacketCopy",
            new[] { new NotificationRecipient("TEST-inbox@test.local", role: RecipientRole.OfficeAdmin) },
            new Dictionary<string, object?>(), "OfficePacketCopy/x/v0", Array.Empty<PacketAttachmentRef>());

        rows.ShouldBeEmpty();
    }
}
