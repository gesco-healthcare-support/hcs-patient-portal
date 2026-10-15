using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.BlobContainers;
using HealthcareSupport.CaseEvaluation.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Volo.Abp.BlobStoring;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDocuments;

/// <summary>
/// <see cref="PacketAttachmentProvider"/>: which packet row it picks, when it declines to attach,
/// the file name it builds, and that the post-send callback no longer prunes anything.
/// </summary>
/// <remarks>
/// <c>GetAllBytesOrNullAsync</c> is an ABP extension over <c>IBlobContainer.GetOrNullAsync</c>, so
/// the container substitute answers the interface member. Every refusal fact seeds the row it
/// refuses, and the selection fact seeds a same-appointment and a same-kind decoy whose bytes would
/// show if the wrong row were picked. Values are synthetic.
/// </remarks>
public class PacketAttachmentProviderTests
{
    private static readonly Guid TenantId = new("7e57b000-0000-4000-9000-000000000001");
    private static readonly Guid AppointmentId = new("7e57b000-0000-4000-9000-000000000002");
    private static readonly Guid OtherAppointmentId = new("7e57b000-0000-4000-9000-000000000003");

    // 18:00 UTC on 6 May 2031 is 11:00 Pacific (PDT); the name uses a 12-hour clock.
    private static readonly DateTime GeneratedAtUtc = new(2031, 5, 6, 18, 0, 0, DateTimeKind.Utc);

    private sealed class World
    {
        public IRepository<AppointmentPacket, Guid> Packets { get; } = Substitute.For<IRepository<AppointmentPacket, Guid>>();
        public IRepository<Appointment, Guid> Appointments { get; } = Substitute.For<IRepository<Appointment, Guid>>();
        public IBlobContainer<AppointmentPacketsContainer> Container { get; } = Substitute.For<IBlobContainer<AppointmentPacketsContainer>>();
        public PacketAttachmentProvider Provider { get; }

        public World(params AppointmentPacket[] rows)
        {
            var list = rows.ToList();
            Packets.GetQueryableAsync().Returns(_ => list.AsQueryable());
            Provider = new PacketAttachmentProvider(
                Packets, Appointments, Container, NullLogger<PacketAttachmentProvider>.Instance);
        }

        public void Blob(string name, byte[]? bytes) =>
            Container.GetOrNullAsync(name, Arg.Any<CancellationToken>())
                .Returns(_ => bytes == null ? null : new MemoryStream(bytes));

        public void Appointment(Guid id, string confirmation) =>
            Appointments.FindAsync(id, Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(new Appointment(
                    id: id,
                    patientId: Guid.NewGuid(),
                    identityUserId: Guid.NewGuid(),
                    appointmentTypeId: Guid.NewGuid(),
                    locationId: Guid.NewGuid(),
                    doctorAvailabilityId: Guid.NewGuid(),
                    appointmentDate: new DateTime(2031, 6, 2, 0, 0, 0, DateTimeKind.Utc),
                    requestConfirmationNumber: confirmation,
                    appointmentStatus: AppointmentStatusType.Approved));
    }

    private static AppointmentPacket Packet(Guid appointmentId, PacketKind kind, string blob, PacketGenerationStatus status) =>
        new(Guid.NewGuid(), TenantId, appointmentId, kind, blob, status) { GeneratedAt = GeneratedAtUtc };

    [Fact]
    public async Task A_generated_packet_is_attached_as_a_pdf_named_by_confirmation_kind_and_pacific_time()
    {
        var wanted = Packet(AppointmentId, PacketKind.Patient, "blob/patient.pdf", PacketGenerationStatus.Generated);
        var sameAppointmentOtherKind = Packet(AppointmentId, PacketKind.Doctor, "blob/doctor.pdf", PacketGenerationStatus.Generated);
        var sameKindOtherAppointment = Packet(OtherAppointmentId, PacketKind.Patient, "blob/other.pdf", PacketGenerationStatus.Generated);
        var world = new World(sameAppointmentOtherKind, sameKindOtherAppointment, wanted);
        world.Blob("blob/patient.pdf", new byte[] { 1, 2, 3 });
        world.Blob("blob/doctor.pdf", new byte[] { 9 });
        world.Blob("blob/other.pdf", new byte[] { 8 });
        world.Appointment(AppointmentId, "TEST-00042");

        var attachment = await world.Provider.GetAttachmentAsync(AppointmentId, PacketKind.Patient);

        attachment.ShouldNotBeNull();
        attachment.Bytes.ShouldBe(new byte[] { 1, 2, 3 });
        attachment.FileName.ShouldBe("TEST-00042_Patient Packet_06052031_110000.pdf");
        attachment.ContentType.ShouldBe(PacketAttachmentProvider.PdfContentType);
    }

    [Fact]
    public async Task A_missing_appointment_falls_back_to_the_appointment_id_in_the_file_name()
    {
        var world = new World(Packet(AppointmentId, PacketKind.Doctor, "blob/doctor.pdf", PacketGenerationStatus.Generated));
        world.Blob("blob/doctor.pdf", new byte[] { 4 });

        var attachment = await world.Provider.GetAttachmentAsync(AppointmentId, PacketKind.Doctor);

        attachment.ShouldNotBeNull();
        attachment.FileName.ShouldBe($"{AppointmentId:N}_Doctor Packet_06052031_110000.pdf");
    }

    [Fact]
    public async Task No_attachment_is_offered_for_a_packet_that_is_not_generated_or_absent()
    {
        var generating = Packet(AppointmentId, PacketKind.Patient, "blob/generating.pdf", PacketGenerationStatus.Generating);
        var failed = Packet(AppointmentId, PacketKind.Doctor, "blob/failed.pdf", PacketGenerationStatus.Failed);
        var world = new World(generating, failed);
        world.Blob("blob/generating.pdf", new byte[] { 1 });
        world.Blob("blob/failed.pdf", new byte[] { 2 });

        (await world.Provider.GetAttachmentAsync(AppointmentId, PacketKind.Patient)).ShouldBeNull();
        (await world.Provider.GetAttachmentAsync(AppointmentId, PacketKind.Doctor)).ShouldBeNull();
        (await world.Provider.GetAttachmentAsync(AppointmentId, PacketKind.AttorneyClaimExaminer)).ShouldBeNull();

        await world.Container.DidNotReceiveWithAnyArgs().GetOrNullAsync(default!, default);
    }

    [Fact]
    public async Task A_generated_row_whose_blob_is_gone_offers_no_attachment()
    {
        var world = new World(Packet(AppointmentId, PacketKind.Patient, "blob/gone.pdf", PacketGenerationStatus.Generated));
        world.Blob("blob/gone.pdf", null);
        world.Appointment(AppointmentId, "TEST-00043");

        var attachment = await world.Provider.GetAttachmentAsync(AppointmentId, PacketKind.Patient);

        attachment.ShouldBeNull();
        await world.Appointments.DidNotReceiveWithAnyArgs().FindAsync(default(Guid), default, default);
    }

    [Fact]
    public async Task The_send_completed_callback_keeps_every_packet_kind_after_a_successful_send()
    {
        var attorneyPacket = Packet(AppointmentId, PacketKind.AttorneyClaimExaminer, "blob/atty.pdf", PacketGenerationStatus.Generated);
        var world = new World(attorneyPacket);

        await world.Provider.NotifySendCompletedAsync(attorneyPacket.Id, success: true);

        await world.Packets.DidNotReceiveWithAnyArgs().DeleteAsync(default(AppointmentPacket)!, default, default);
        await world.Packets.DidNotReceiveWithAnyArgs().DeleteAsync(default(Guid), default, default);
        await world.Container.DidNotReceiveWithAnyArgs().DeleteAsync(default!, default);
    }
}
