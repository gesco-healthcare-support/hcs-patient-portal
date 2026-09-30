using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using NSubstitute;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// How <see cref="CaseTrackerDeadLetterRequeuer"/> rebuilds a dead letter (#961). An intake is rebuilt
/// as an intake; a document update is rebuilt as a document update carrying each listed document's
/// CURRENT state, because retrying it as a full intake could never deliver a deletion (the contract
/// removes a document only via <c>deleted: true</c> on the document endpoint).
///
/// <para>The queues and the resolver are substitutes: what is under test is which messages the
/// requeuer asks for. All fixture data is synthetic.</para>
/// </summary>
public class CaseTrackerDeadLetterRequeuerTests
{
    private static readonly Guid OfficeId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private static readonly Guid AppointmentId = new("5b8e1c34-7a2d-4f96-b0e3-8d4c1a6f2e57");
    private static readonly Guid DocumentId = new("e3a7c1d5-2b49-4f86-9c0e-1d5b7a3f8c24");
    private static readonly Guid OtherDocumentId = new("7f2c9e41-d3a8-4b65-8e1f-0a6d4c2b9e73");
    private static readonly Guid PacketId = new("c6d1f8a2-4e37-4b90-a5c2-3f8e1b7d6a49");
    private static readonly DateTime Now = new(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);

    private sealed class Harness
    {
        public CaseTrackerDeadLetterRequeuer Requeuer { get; init; } = null!;
        public ICaseTrackerIntakeQueue IntakeQueue { get; init; } = null!;
        public ICaseTrackerDocumentQueue DocumentQueue { get; init; } = null!;
        public IDocumentListResolver Resolver { get; init; } = null!;
    }

    private static Harness Build(bool documentQueueWrites = true)
    {
        var intakeQueue = Substitute.For<ICaseTrackerIntakeQueue>();
        intakeQueue.EnqueueIntakeAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Row(IntegrationMessageType.Intake)));

        // MUST be explicit either way: an unconfigured Task<IntegrationOutboxItem?> would come back
        // as a completed task of null, which reads as "the gate suppressed it" -- a real outcome.
        var documentQueue = Substitute.For<ICaseTrackerDocumentQueue>();
        documentQueue.EnqueueDocumentEntriesAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<IReadOnlyList<IntakeDocumentEntry>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IntegrationOutboxItem?>(documentQueueWrites ? Row(IntegrationMessageType.DocumentUpdate) : null));
        documentQueue.EnqueueDeletionsAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<IReadOnlyList<DocumentDeletionEntry>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<IntegrationOutboxItem?>(documentQueueWrites ? Row(IntegrationMessageType.DocumentUpdate) : null));

        var resolver = Substitute.For<IDocumentListResolver>();
        resolver.ResolveDocumentAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IntakeDocumentEntry?>(null));
        resolver.ResolvePacketsAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<IntakeDocumentEntry>()));

        var appointments = Substitute.For<IRepository<Appointment, Guid>>();
        appointments.FindAsync(AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Appointment?>(NewAppointment()));

        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        return new Harness
        {
            Requeuer = new CaseTrackerDeadLetterRequeuer(intakeQueue, documentQueue, resolver, appointments, clock),
            IntakeQueue = intakeQueue,
            DocumentQueue = documentQueue,
            Resolver = resolver,
        };
    }

    private static Appointment NewAppointment() =>
        new(
            AppointmentId,
            patientId: new Guid("e5f6a7b8-c9d0-4e1f-a2b3-c4d5e6f7a8bc"),
            identityUserId: null,
            appointmentTypeId: new Guid("a1c2e3f4-5566-4778-9900-aabbccddeeff"),
            locationId: new Guid("c0ffee0a-bcde-4f01-9abc-de0123456f7a"),
            doctorAvailabilityId: new Guid("d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5fa"),
            appointmentDate: new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00456",
            appointmentStatus: AppointmentStatusType.Approved,
            panelNumber: "PN-SAMPLE")
        {
            TenantId = OfficeId,
        };

    private static IntegrationOutboxItem Row(IntegrationMessageType type, string payload = "[]") =>
        new(
            Guid.NewGuid(),
            OfficeId,
            type,
            type == IntegrationMessageType.Intake ? CaseTrackerEndpoints.Intake : CaseTrackerEndpoints.DocumentUpdate(AppointmentId),
            AppointmentId,
            payload,
            "key-" + Guid.NewGuid().ToString("N"));

    private static IntegrationOutboxItem DocumentDeadLetter(string payload) =>
        Row(IntegrationMessageType.DocumentUpdate, payload);

    private static IntakeDocumentEntry Entry(Guid id, string status, string source = DocumentEntryMapper.DocumentSource) => new()
    {
        Id = id,
        Source = source,
        DocumentName = "Medical Records",
        FileName = "records.pdf",
        ContentType = "application/pdf",
        Status = status,
        ObjectKey = "tenants/a1b2c3d4-e5f6-7890-abcd-ef1234567890/records",
        CreatedAtUtc = "2026-09-20T10:00:00.0000000Z",
        UpdatedAt = "2026-09-22T10:00:00.0000000Z",
    };

    private static string EntriesPayload(params IntakeDocumentEntry[] entries) =>
        IntakePayloadSerializer.SerializeDocumentEntries(entries);

    [Fact]
    public async Task RequeueAsync_AnIntakeDeadLetter_IsRebuiltAsAnIntake()
    {
        var h = Build();

        var rows = await h.Requeuer.RequeueAsync(Row(IntegrationMessageType.Intake), OfficeId);

        rows.Count.ShouldBe(1);
        await h.IntakeQueue.Received(1).EnqueueIntakeAsync(AppointmentId, OfficeId, Arg.Any<CancellationToken>());
        await h.DocumentQueue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(default, default, default!, default);
    }

    [Fact]
    public async Task RequeueAsync_ADocumentStillAccepted_IsSentAsItsCurrentEntry()
    {
        var h = Build();
        var current = Entry(DocumentId, "Accepted");
        h.Resolver.ResolveDocumentAsync(DocumentId, OfficeId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IntakeDocumentEntry?>(current));

        await h.Requeuer.RequeueAsync(DocumentDeadLetter(EntriesPayload(Entry(DocumentId, "Accepted"))), OfficeId);

        await h.DocumentQueue.Received(1).EnqueueDocumentEntriesAsync(
            AppointmentId, OfficeId,
            Arg.Is<IReadOnlyList<IntakeDocumentEntry>>(l => l.Count == 1 && ReferenceEquals(l[0], current)),
            Arg.Any<CancellationToken>());
        await h.DocumentQueue.DidNotReceiveWithAnyArgs().EnqueueDeletionsAsync(default, default, default!, default);
        await h.IntakeQueue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task RequeueAsync_AFailedDeletion_IsSentAsADeletion_NotAsAnIntake()
    {
        // The case that motivated folding documents into #961: a retried deletion used to be re-sent as
        // a full intake, which removes nothing on the Case Tracker side.
        var h = Build();
        var deadLetter = DocumentDeadLetter(IntakePayloadSerializer.SerializeDeletionEntries(new[]
        {
            new DocumentDeletionEntry { Id = DocumentId, UpdatedAt = "2026-09-22T10:00:00.0000000Z" },
        }));

        await h.Requeuer.RequeueAsync(deadLetter, OfficeId);

        await h.DocumentQueue.Received(1).EnqueueDeletionsAsync(
            AppointmentId, OfficeId,
            Arg.Is<IReadOnlyList<DocumentDeletionEntry>>(l => l.Count == 1 && l[0].Id == DocumentId && l[0].Deleted),
            Arg.Any<CancellationToken>());
        await h.IntakeQueue.DidNotReceiveWithAnyArgs().EnqueueIntakeAsync(default, default, default);
    }

    [Fact]
    public async Task RequeueAsync_ADocumentRejectedSinceTheFailure_IsSentAsADeletion()
    {
        // A rejected document still resolves (it has bytes), with status "Rejected". The live flow sends
        // reject-after-accept as a deletion, so the rebuild must too -- never an upsert of a rejection.
        var h = Build();
        h.Resolver.ResolveDocumentAsync(DocumentId, OfficeId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IntakeDocumentEntry?>(Entry(DocumentId, "Rejected")));

        await h.Requeuer.RequeueAsync(DocumentDeadLetter(EntriesPayload(Entry(DocumentId, "Accepted"))), OfficeId);

        await h.DocumentQueue.Received(1).EnqueueDeletionsAsync(
            AppointmentId, OfficeId,
            Arg.Is<IReadOnlyList<DocumentDeletionEntry>>(l => l.Single().Id == DocumentId),
            Arg.Any<CancellationToken>());
        await h.DocumentQueue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(default, default, default!, default);
    }

    [Fact]
    public async Task RequeueAsync_ADeletedDocumentReacceptedSince_IsSentAsItsCurrentEntry()
    {
        // Current state wins over the stored snapshot: replaying the old deletion would remove a
        // document the portal now shows.
        var h = Build();
        var current = Entry(DocumentId, "Accepted");
        h.Resolver.ResolveDocumentAsync(DocumentId, OfficeId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IntakeDocumentEntry?>(current));
        var deadLetter = DocumentDeadLetter(IntakePayloadSerializer.SerializeDeletionEntries(new[]
        {
            new DocumentDeletionEntry { Id = DocumentId, UpdatedAt = "2026-09-22T10:00:00.0000000Z" },
        }));

        await h.Requeuer.RequeueAsync(deadLetter, OfficeId);

        await h.DocumentQueue.Received(1).EnqueueDocumentEntriesAsync(
            AppointmentId, OfficeId,
            Arg.Is<IReadOnlyList<IntakeDocumentEntry>>(l => l.Single().Id == DocumentId),
            Arg.Any<CancellationToken>());
        await h.DocumentQueue.DidNotReceiveWithAnyArgs().EnqueueDeletionsAsync(default, default, default!, default);
    }

    [Fact]
    public async Task RequeueAsync_MixedCurrentStates_QueueAnUpsertAndADeletion()
    {
        var h = Build();
        h.Resolver.ResolveDocumentAsync(DocumentId, OfficeId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IntakeDocumentEntry?>(Entry(DocumentId, "Accepted")));
        // OtherDocumentId resolves to null (deleted), the harness default.

        var rows = await h.Requeuer.RequeueAsync(
            DocumentDeadLetter(EntriesPayload(Entry(DocumentId, "Accepted"), Entry(OtherDocumentId, "Accepted"))),
            OfficeId);

        rows.Count.ShouldBe(2);
        await h.DocumentQueue.Received(1).EnqueueDeletionsAsync(
            AppointmentId, OfficeId,
            Arg.Is<IReadOnlyList<DocumentDeletionEntry>>(l => l.Single().Id == OtherDocumentId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequeueAsync_APacketThatStillResolves_IsSent_AndOneThatDoesNot_IsLeftOut()
    {
        // The portal never deletes a packet on the Case Tracker side, so a packet that no longer
        // resolves is omitted rather than turned into a deletion.
        var h = Build();
        var currentPacket = Entry(PacketId, "Generated", DocumentEntryMapper.PacketSource);
        h.Resolver.ResolvePacketsAsync(Arg.Any<Appointment>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<IntakeDocumentEntry> { currentPacket }));
        var gonePacketId = new Guid("b2e8d4f1-6c39-4a57-9d1e-7f3a5c8b2d06");

        await h.Requeuer.RequeueAsync(
            DocumentDeadLetter(EntriesPayload(
                Entry(PacketId, "Generated", DocumentEntryMapper.PacketSource),
                Entry(gonePacketId, "Generated", DocumentEntryMapper.PacketSource))),
            OfficeId);

        await h.DocumentQueue.Received(1).EnqueueDocumentEntriesAsync(
            AppointmentId, OfficeId,
            Arg.Is<IReadOnlyList<IntakeDocumentEntry>>(l => l.Count == 1 && l[0].Id == PacketId),
            Arg.Any<CancellationToken>());
        await h.DocumentQueue.DidNotReceiveWithAnyArgs().EnqueueDeletionsAsync(default, default, default!, default);
        await h.Resolver.DidNotReceive().ResolveDocumentAsync(gonePacketId, Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequeueAsync_WhenTheDocumentQueueWritesNothing_SendsTheIntakeInstead()
    {
        // The #931 gate writes nothing for an appointment with no intake row; the documents then
        // belong inside the intake, which is built from the current list.
        var h = Build(documentQueueWrites: false);
        h.Resolver.ResolveDocumentAsync(DocumentId, OfficeId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IntakeDocumentEntry?>(Entry(DocumentId, "Accepted")));

        var rows = await h.Requeuer.RequeueAsync(DocumentDeadLetter(EntriesPayload(Entry(DocumentId, "Accepted"))), OfficeId);

        rows.Count.ShouldBe(1);
        rows[0].MessageType.ShouldBe(IntegrationMessageType.Intake);
        await h.IntakeQueue.Received(1).EnqueueIntakeAsync(AppointmentId, OfficeId, Arg.Any<CancellationToken>());
    }
}
