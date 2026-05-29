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
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// BUG-036 sub-bug 3 (defense-in-depth): the per-kind catch filter inside
/// <c>GenerateAppointmentPacketJob.GenerateKindAsync</c> must catch
/// <see cref="AbpDbConcurrencyException"/> alongside the existing
/// IOException / InvalidOperationException / ArgumentException family.
/// </summary>
public class GenerateAppointmentPacketJobTests
{
    private static readonly Guid TenantId =
        new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId =
        new("b2c3d4e5-f6a7-8901-bcde-f12345678901");
    private static readonly Guid AppointmentTypeId =
        new("c3d4e5f6-a7b8-9012-cdef-123456789012");
    private static readonly Guid PatientId =
        new("d4e5f6a7-b8c9-0123-def0-1234567890ab");
    private static readonly Guid IdentityUserId =
        new("e5f6a7b8-c9d0-1234-ef01-234567890abc");
    private static readonly Guid LocationId =
        new("f6a7b8c9-d0e1-2345-f012-34567890abcd");
    private static readonly Guid DoctorAvailabilityId =
        new("a7b8c9d0-e1f2-3456-0123-4567890abcde");

    [Fact]
    public async Task GenerateKindAsync_WhenMarkGeneratedThrowsAbpDbConcurrency_CatchesAndMarksFailed()
    {
        var fixture = new JobFixture();
        fixture.PacketManager
            .MarkGeneratedAsync(Arg.Any<Guid>(), Arg.Any<string?>())
            .Returns(_ => throw new AbpDbConcurrencyException("simulated concurrency"));

        var ex = await Record.ExceptionAsync(() => fixture.Job.ExecuteAsync(fixture.Args));
        ex.ShouldBeNull("AbpDbConcurrencyException must be caught by the widened filter, not propagated to Hangfire.");

        await fixture.PacketManager.Received(3).MarkFailedAsync(
            Arg.Any<Guid>(),
            Arg.Is<string>(msg => msg.Contains("simulated concurrency")));
    }

    [Fact]
    public async Task GenerateKindAsync_WhenRendererThrowsInvalidOperation_StillCaught()
    {
        var fixture = new JobFixture();
        fixture.Renderer
            .Render(Arg.Any<byte[]>(), Arg.Any<PacketTokenContext>())
            .Returns(_ => throw new InvalidOperationException("template render failure"));

        var ex = await Record.ExceptionAsync(() => fixture.Job.ExecuteAsync(fixture.Args));
        ex.ShouldBeNull();

        await fixture.PacketManager.Received(3).MarkFailedAsync(
            Arg.Any<Guid>(),
            Arg.Is<string>(msg => msg.Contains("template render failure")));
    }

    [Fact]
    public async Task GenerateKindAsync_WhenAllSucceed_NoFailedMarks()
    {
        var fixture = new JobFixture();

        var ex = await Record.ExceptionAsync(() => fixture.Job.ExecuteAsync(fixture.Args));
        ex.ShouldBeNull();

        await fixture.PacketManager.Received(3).MarkGeneratedAsync(
            Arg.Any<Guid>(), Arg.Any<string?>());
        await fixture.PacketManager.DidNotReceive().MarkFailedAsync(
            Arg.Any<Guid>(), Arg.Any<string>());
    }

    private sealed class JobFixture
    {
        public IRepository<Appointment, Guid> AppointmentRepository { get; }
        public AppointmentPacketManager PacketManager { get; }
        public IBlobContainer<AppointmentPacketsContainer> Container { get; }
        public IPacketTokenResolver TokenResolver { get; }
        public IDocxTemplateRenderer Renderer { get; }
        public IDocxToPdfConverter Converter { get; }
        public ICurrentTenant CurrentTenant { get; }
        public ILocalEventBus EventBus { get; }
        public IUnitOfWorkManager UnitOfWorkManager { get; }
        public GenerateAppointmentPacketJob Job { get; }
        public GenerateAppointmentPacketArgs Args { get; }

        public JobFixture()
        {
            AppointmentRepository = Substitute.For<IRepository<Appointment, Guid>>();

            var packetRepository = Substitute.For<IRepository<AppointmentPacket, Guid>>();
            PacketManager = Substitute.For<AppointmentPacketManager>(packetRepository);
            PacketManager
                .EnsureGeneratingAsync(Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<PacketKind>(), Arg.Any<string>())
                .Returns(callInfo =>
                {
                    var kind = callInfo.ArgAt<PacketKind>(2);
                    var blobName = callInfo.ArgAt<string>(3);
                    return new AppointmentPacket(
                        Guid.NewGuid(), TenantId, AppointmentId, kind, blobName,
                        PacketGenerationStatus.Generating);
                });

            Container = Substitute.For<IBlobContainer<AppointmentPacketsContainer>>();
            Container
                .SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            TokenResolver = Substitute.For<IPacketTokenResolver>();
            TokenResolver.ResolveAsync(Arg.Any<Guid>()).Returns(new PacketTokenContext());

            Renderer = Substitute.For<IDocxTemplateRenderer>();
            Renderer.Render(Arg.Any<byte[]>(), Arg.Any<PacketTokenContext>())
                .Returns(new byte[] { 0x00 });

            Converter = Substitute.For<IDocxToPdfConverter>();
            Converter.ConvertAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
                .Returns(new byte[] { 0x00 });

            CurrentTenant = Substitute.For<ICurrentTenant>();
            CurrentTenant.Id.Returns(TenantId);
            CurrentTenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
                .Returns(Substitute.For<IDisposable>());

            EventBus = Substitute.For<ILocalEventBus>();
            UnitOfWorkManager = Substitute.For<IUnitOfWorkManager>();
            UnitOfWorkManager.Current.Returns((IUnitOfWork?)null);

            AppointmentRepository.GetAsync(AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(BuildAppointmentStub());

            Job = new GenerateAppointmentPacketJob(
                AppointmentRepository,
                PacketManager,
                Container,
                TokenResolver,
                Renderer,
                Converter,
                CurrentTenant,
                EventBus,
                UnitOfWorkManager,
                NullLogger<GenerateAppointmentPacketJob>.Instance);

            Args = new GenerateAppointmentPacketArgs
            {
                AppointmentId = AppointmentId,
                TenantId = TenantId,
            };
        }

        // F5 (2026-05-29): the job now generates all three packet kinds for
        // every appointment type, so each scenario below exercises 3 kinds
        // (Patient, Doctor, AttorneyClaimExaminer) -- hence Received(3).

        private static Appointment BuildAppointmentStub()
        {
            return new Appointment(
                id: AppointmentId,
                patientId: PatientId,
                identityUserId: IdentityUserId,
                appointmentTypeId: AppointmentTypeId,
                locationId: LocationId,
                doctorAvailabilityId: DoctorAvailabilityId,
                appointmentDate: new DateTime(2026, 5, 23, 0, 0, 0, DateTimeKind.Utc),
                requestConfirmationNumber: "synthetic-confirmation-token",
                appointmentStatus: AppointmentStatusType.Approved);
        }
    }
}
