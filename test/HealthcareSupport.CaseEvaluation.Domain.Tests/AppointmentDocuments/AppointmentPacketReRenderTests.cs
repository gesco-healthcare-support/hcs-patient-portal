using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Pdf;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Templates;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// The packet re-render that follows a party change on a live appointment (2026-10-21): the manager
/// keeps the replaced file as an <see cref="AppointmentPacketVersion"/>, and the job renders an
/// already Generated kind again only when asked, under a new key, announcing it after the commit.
/// Values are synthetic.
/// </summary>
public class AppointmentPacketReRenderTests
{
    private static readonly Guid TenantId = new("7e57b000-0000-4000-9000-000000000001");
    private static readonly Guid AppointmentId = new("7e57b000-0000-4000-9000-000000000002");
    private static readonly Guid StaffUserId = new("7e57b000-0000-4000-9000-000000000003");
    private const string OldBlob = "tenant/appointment/packet/attorneyclaimexaminer/old.pdf";

    // ----------------------------------------------------------------- the manager

    private sealed class ManagerWorld
    {
        public AppointmentPacket Packet { get; }
        public IRepository<AppointmentPacket, Guid> Packets { get; } = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        public IRepository<AppointmentPacketVersion, Guid> Versions { get; } = Substitute.For<IRepository<AppointmentPacketVersion, Guid>>();
        public List<AppointmentPacketVersion> Inserted { get; } = new();
        public AppointmentPacketManager Manager { get; }

        public ManagerWorld(PacketGenerationStatus status = PacketGenerationStatus.Generated)
        {
            Packet = new AppointmentPacket(Guid.NewGuid(), TenantId, AppointmentId, PacketKind.AttorneyClaimExaminer, OldBlob, status);
            Packets.GetAsync(Packet.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Packet);
            Versions.InsertAsync(Arg.Do<AppointmentPacketVersion>(Inserted.Add), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(ci => ci.Arg<AppointmentPacketVersion>());

            Manager = new AppointmentPacketManager(Packets, Versions);
            var lazy = Substitute.For<IAbpLazyServiceProvider>();
            lazy.LazyGetService<IGuidGenerator>(Arg.Any<IGuidGenerator>()).Returns(SimpleGuidGenerator.Instance);
            Manager.LazyServiceProvider = lazy;
        }
    }

    [Fact]
    public async Task Replacing_a_generated_file_records_the_previous_one_and_points_the_packet_at_the_new_one()
    {
        var world = new ManagerWorld();
        var renderedAt = world.Packet.GeneratedAt;

        await world.Manager.ReplaceGeneratedFileAsync(world.Packet.Id, "tenant/appointment/packet/attorneyclaimexaminer/new.pdf", StaffUserId);

        var version = world.Inserted.ShouldHaveSingleItem();
        version.BlobName.ShouldBe(OldBlob, "the version keeps the file the parties already mailed hold");
        version.AppointmentPacketId.ShouldBe(world.Packet.Id);
        version.AppointmentId.ShouldBe(AppointmentId);
        version.TenantId.ShouldBe(TenantId);
        version.Kind.ShouldBe(PacketKind.AttorneyClaimExaminer);
        version.FileGeneratedAt.ShouldBe(renderedAt);
        version.ReplacedByUserId.ShouldBe(StaffUserId);

        world.Packet.BlobName.ShouldBe("tenant/appointment/packet/attorneyclaimexaminer/new.pdf");
        world.Packet.Status.ShouldBe(PacketGenerationStatus.Generated);
        world.Packet.RegeneratedAt.ShouldNotBeNull();
        await world.Packets.Received(1).UpdateAsync(world.Packet, Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_second_replacement_records_when_the_file_it_replaces_was_rendered()
    {
        var world = new ManagerWorld();
        await world.Manager.ReplaceGeneratedFileAsync(world.Packet.Id, "key/second.pdf", StaffUserId);
        var secondRenderedAt = world.Packet.RegeneratedAt!.Value;

        await world.Manager.ReplaceGeneratedFileAsync(world.Packet.Id, "key/third.pdf", null);

        world.Inserted.Count.ShouldBe(2);
        world.Inserted[1].BlobName.ShouldBe("key/second.pdf");
        world.Inserted[1].FileGeneratedAt.ShouldBe(secondRenderedAt);
        world.Inserted[1].ReplacedByUserId.ShouldBeNull();
    }

    [Theory]
    [InlineData(PacketGenerationStatus.Generating)]
    [InlineData(PacketGenerationStatus.Failed)]
    public async Task Only_a_generated_packet_can_have_its_file_replaced(PacketGenerationStatus status)
    {
        var world = new ManagerWorld(status);

        await Should.ThrowAsync<InvalidOperationException>(
            () => world.Manager.ReplaceGeneratedFileAsync(world.Packet.Id, "key/new.pdf", StaffUserId));

        world.Inserted.ShouldBeEmpty();
        world.Packet.BlobName.ShouldBe(OldBlob);
    }

    [Fact]
    public async Task A_replacement_under_the_same_key_is_refused_because_it_would_overwrite_the_kept_file()
    {
        var world = new ManagerWorld();

        await Should.ThrowAsync<InvalidOperationException>(
            () => world.Manager.ReplaceGeneratedFileAsync(world.Packet.Id, OldBlob, StaffUserId));

        world.Inserted.ShouldBeEmpty();
    }

    // ----------------------------------------------------------------- the job

    private sealed class JobWorld
    {
        public AppointmentPacket Packet { get; }
        public AppointmentPacketManager PacketManager { get; }
        public IHtmlPacketRenderer Renderer { get; } = Substitute.For<IHtmlPacketRenderer>();
        public IBlobContainer<AppointmentPacketsContainer> Container { get; } = Substitute.For<IBlobContainer<AppointmentPacketsContainer>>();
        public ILocalEventBus EventBus { get; } = Substitute.For<ILocalEventBus>();
        public List<Func<Task>> AfterCommit { get; } = new();
        public GenerateAppointmentPacketJob Job { get; }

        public JobWorld()
        {
            Packet = new AppointmentPacket(Guid.NewGuid(), TenantId, AppointmentId, PacketKind.AttorneyClaimExaminer, OldBlob, PacketGenerationStatus.Generated);
            PacketManager = Substitute.For<AppointmentPacketManager>(
                Substitute.For<IRepository<AppointmentPacket, Guid>>(),
                Substitute.For<IRepository<AppointmentPacketVersion, Guid>>());
            // Every kind is already Generated, as on a live appointment.
            PacketManager
                .EnsureGeneratingAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<PacketKind>(), Arg.Any<string>())
                .Returns(ci => ci.ArgAt<PacketKind>(2) == PacketKind.AttorneyClaimExaminer
                    ? Packet
                    : new AppointmentPacket(Guid.NewGuid(), TenantId, AppointmentId, ci.ArgAt<PacketKind>(2), "other/kind.pdf", PacketGenerationStatus.Generated));

            var tokenResolver = Substitute.For<IPacketTokenResolver>();
            tokenResolver.ResolveAsync(Arg.Any<Guid>()).Returns(new PacketTokenContext { AppointmentType = "TEST AME" });
            Renderer.RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(new byte[] { 0x25, 0x50, 0x44, 0x46 });
            Container.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var currentTenant = Substitute.For<ICurrentTenant>();
            currentTenant.Id.Returns(TenantId);
            currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(Substitute.For<IDisposable>());

            var uow = Substitute.For<IUnitOfWork>();
            uow.When(u => u.OnCompleted(Arg.Any<Func<Task>>())).Do(ci => AfterCommit.Add(ci.Arg<Func<Task>>()));
            var uowManager = Substitute.For<IUnitOfWorkManager>();
            uowManager.Current.Returns(uow);

            Job = new GenerateAppointmentPacketJob(
                Substitute.For<IRepository<Appointment, Guid>>(),
                PacketManager,
                Container,
                tokenResolver,
                Renderer,
                currentTenant,
                EventBus,
                uowManager,
                NullLogger<GenerateAppointmentPacketJob>.Instance);
        }

        public GenerateAppointmentPacketArgs ReRenderArgs() => new()
        {
            AppointmentId = AppointmentId,
            TenantId = TenantId,
            Kind = PacketKind.AttorneyClaimExaminer,
            ReRender = true,
            RequestedByUserId = StaffUserId,
        };

        public async Task<List<PacketGeneratedEto>> RunAfterCommitAsync()
        {
            foreach (var action in AfterCommit)
            {
                await action();
            }
            return EventBus.ReceivedCalls()
                .Where(c => c.GetMethodInfo().Name == nameof(ILocalEventBus.PublishAsync))
                .Select(c => c.GetArguments()[0])
                .OfType<PacketGeneratedEto>()
                .ToList();
        }
    }

    [Fact]
    public async Task A_re_render_of_a_generated_kind_stores_a_new_file_and_announces_it_after_the_commit()
    {
        var world = new JobWorld();
        string? savedKey = null;
        bool? overwrite = null;
        world.Container
            .When(c => c.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()))
            .Do(ci => { savedKey = ci.ArgAt<string>(0); overwrite = ci.ArgAt<bool>(2); });

        await world.Job.ExecuteAsync(world.ReRenderArgs());

        await world.Renderer.Received(1).RenderAsync(
            PacketTemplateNames.AttorneyAme, Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
        savedKey.ShouldNotBeNull();
        savedKey.ShouldNotBe(OldBlob, "the new file goes under its own key");
        savedKey.ShouldContain("/packet/attorneyclaimexaminer/");
        overwrite.ShouldBe(false, "a re-render never overwrites a stored file");
        await world.PacketManager.Received(1).ReplaceGeneratedFileAsync(world.Packet.Id, savedKey!, StaffUserId);
        await world.PacketManager.DidNotReceive().MarkGeneratedAsync(Arg.Any<Guid>(), Arg.Any<string?>());
        await world.PacketManager.DidNotReceive().MarkFailedAsync(Arg.Any<Guid>(), Arg.Any<string>());

        await world.EventBus.DidNotReceive().PublishAsync(Arg.Any<PacketGeneratedEto>());
        var published = await world.RunAfterCommitAsync();
        var eto = published.ShouldHaveSingleItem();
        eto.PacketId.ShouldBe(world.Packet.Id);
        eto.Kind.ShouldBe(PacketKind.AttorneyClaimExaminer);
        eto.AppointmentId.ShouldBe(AppointmentId);
    }

    [Fact]
    public async Task Without_a_kind_the_re_render_flag_is_ignored_and_nothing_is_rendered_again()
    {
        var world = new JobWorld();
        var args = world.ReRenderArgs();
        args.Kind = null;

        await world.Job.ExecuteAsync(args);

        await world.Renderer.DidNotReceive().RenderAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
        await world.PacketManager.DidNotReceive().ReplaceGeneratedFileAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>());
        (await world.RunAfterCommitAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Without_the_flag_a_generated_kind_is_still_skipped()
    {
        var world = new JobWorld();
        var args = world.ReRenderArgs();
        args.ReRender = false;

        await world.Job.ExecuteAsync(args);

        await world.Renderer.DidNotReceive().RenderAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
        (await world.RunAfterCommitAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_re_render_that_cannot_be_produced_keeps_the_previous_file_and_still_announces_it()
    {
        var world = new JobWorld();
        world.Renderer
            .RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("template render failure"));

        var ex = await Record.ExceptionAsync(() => world.Job.ExecuteAsync(world.ReRenderArgs()));

        ex.ShouldBeNull("a repeatable failure is not retried; the job completing is what sends the email");
        await world.PacketManager.DidNotReceive().MarkFailedAsync(Arg.Any<Guid>(), Arg.Any<string>());
        await world.PacketManager.DidNotReceive().ReplaceGeneratedFileAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>());
        world.Packet.Status.ShouldBe(PacketGenerationStatus.Generated);
        world.Packet.BlobName.ShouldBe(OldBlob);
        (await world.RunAfterCommitAsync()).ShouldHaveSingleItem().PacketId.ShouldBe(world.Packet.Id);
    }

    [Fact]
    public async Task A_renderer_outage_during_a_re_render_propagates_for_a_retry_and_announces_nothing()
    {
        var world = new JobWorld();
        world.Renderer
            .RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("packet-renderer unreachable"));

        await Should.ThrowAsync<HttpRequestException>(() => world.Job.ExecuteAsync(world.ReRenderArgs()));

        await world.PacketManager.DidNotReceive().MarkFailedAsync(Arg.Any<Guid>(), Arg.Any<string>());
        world.AfterCommit.ShouldBeEmpty("the email waits for the retry that produces the new file");
    }
}
