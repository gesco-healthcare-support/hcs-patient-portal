using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// The two <see cref="GenerateAppointmentPacketJob"/> branches <c>GenerateAppointmentPacketJobTests</c>
/// does not reach: publishing inside an active unit of work, and a packet kind with no template.
/// </summary>
/// <remarks>
/// The existing fixture pins <c>IUnitOfWorkManager.Current</c> to null, so every publish there is
/// immediate. Here a unit of work is present and its completion handlers are captured, which is
/// what shows the event is held back until the work commits. Values are synthetic.
/// </remarks>
public class GenerateAppointmentPacketJobPathTests
{
    private static readonly Guid TenantId = new("7e57d000-0000-4000-9000-000000000001");
    private static readonly Guid AppointmentId = new("7e57d000-0000-4000-9000-000000000002");

    private sealed class World
    {
        public AppointmentPacketManager PacketManager { get; }
        public IHtmlPacketRenderer Renderer { get; } = Substitute.For<IHtmlPacketRenderer>();
        public ILocalEventBus EventBus { get; } = Substitute.For<ILocalEventBus>();
        public IUnitOfWorkManager UnitOfWorkManager { get; } = Substitute.For<IUnitOfWorkManager>();
        public GenerateAppointmentPacketJob Job { get; }

        public World()
        {
            PacketManager = Substitute.For<AppointmentPacketManager>(Substitute.For<IRepository<AppointmentPacket, Guid>>());
            PacketManager
                .EnsureGeneratingAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<PacketKind>(), Arg.Any<string>())
                .Returns(ci => new AppointmentPacket(
                    Guid.NewGuid(), TenantId, AppointmentId, ci.ArgAt<PacketKind>(2), ci.ArgAt<string>(3),
                    PacketGenerationStatus.Generating));

            var tokenResolver = Substitute.For<IPacketTokenResolver>();
            tokenResolver.ResolveAsync(Arg.Any<Guid>()).Returns(new PacketTokenContext { AppointmentType = "TEST AME" });
            Renderer.RenderAsync(Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
                .Returns(new byte[] { 0x25 });

            var container = Substitute.For<IBlobContainer<AppointmentPacketsContainer>>();
            container.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var currentTenant = Substitute.For<ICurrentTenant>();
            currentTenant.Id.Returns(TenantId);
            currentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>()).Returns(Substitute.For<IDisposable>());

            Job = new GenerateAppointmentPacketJob(
                Substitute.For<IRepository<Appointment, Guid>>(),
                PacketManager,
                container,
                tokenResolver,
                Renderer,
                currentTenant,
                EventBus,
                UnitOfWorkManager,
                NullLogger<GenerateAppointmentPacketJob>.Instance);
        }
    }

    [Fact]
    public async Task Inside_a_unit_of_work_each_generated_event_waits_for_the_commit()
    {
        var world = new World();
        var onCompleted = new List<Func<Task>>();
        var uow = Substitute.For<IUnitOfWork>();
        uow.When(u => u.OnCompleted(Arg.Any<Func<Task>>())).Do(ci => onCompleted.Add(ci.Arg<Func<Task>>()));
        world.UnitOfWorkManager.Current.Returns(uow);

        await world.Job.ExecuteAsync(new GenerateAppointmentPacketArgs { AppointmentId = AppointmentId, TenantId = TenantId });

        onCompleted.Count.ShouldBe(3);
        await world.EventBus.DidNotReceive().PublishAsync(Arg.Any<PacketGeneratedEto>());

        foreach (var handler in onCompleted)
        {
            await handler();
        }

        var published = world.EventBus.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(ILocalEventBus.PublishAsync))
            .Select(c => c.GetArguments()[0])
            .OfType<PacketGeneratedEto>()
            .ToList();
        published.Select(e => e.Kind).ShouldBe(
            new[] { PacketKind.Patient, PacketKind.Doctor, PacketKind.AttorneyClaimExaminer },
            ignoreOrder: true);
        published.ShouldAllBe(e => e.AppointmentId == AppointmentId && e.TenantId == TenantId);
    }

    [Fact]
    public async Task A_kind_with_no_template_is_marked_failed_and_the_job_reports_it_incomplete()
    {
        var world = new World();
        var unknownKind = (PacketKind)99;

        await Should.ThrowAsync<PacketGenerationIncompleteException>(() => world.Job.ExecuteAsync(
            new GenerateAppointmentPacketArgs { AppointmentId = AppointmentId, TenantId = TenantId, Kind = unknownKind }));

        await world.PacketManager.Received(1).MarkFailedAsync(
            Arg.Any<Guid>(), Arg.Is<string>(m => m.Contains("No HTML template for this PacketKind.")));
        await world.PacketManager.DidNotReceive().MarkGeneratedAsync(Arg.Any<Guid>(), Arg.Any<string?>());
        await world.Renderer.DidNotReceiveWithAnyArgs().RenderAsync(default!, default!, default);
    }
}
