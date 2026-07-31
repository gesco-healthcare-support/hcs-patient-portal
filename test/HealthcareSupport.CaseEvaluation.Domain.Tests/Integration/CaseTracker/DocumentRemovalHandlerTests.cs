using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Handlers;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the two removal triggers -- reject-after-accept and IT-Admin delete. Both publish
/// the SAME tombstone, because from the receiver's side a repudiated document and a deleted one are
/// indistinguishable: their staff should stop seeing it either way.
///
/// <para>Covered together because they are one behaviour reached by two routes; splitting them would
/// duplicate the whole harness to assert the same tombstone twice.</para>
/// </summary>
public class DocumentRemovalHandlerTests
{
    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");
    private static readonly Guid DocumentId = new("f97796c9-365b-4ad3-a164-08f72981cae3");
    private static readonly DateTime Now = new(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

    private static Appointment NewAppointment(AppointmentStatusType status) =>
        new(
            AppointmentId,
            patientId: new Guid("e5f6a7b8-c9d0-4e1f-a2b3-c4d5e6f7a8bc"),
            identityUserId: null,
            appointmentTypeId: new Guid("a1c2e3f4-5566-4778-9900-aabbccddeeff"),
            locationId: new Guid("c0ffee0a-bcde-4f01-9abc-de0123456f7a"),
            doctorAvailabilityId: new Guid("d1e2f3a4-b5c6-4d7e-8f90-a1b2c3d4e5fa"),
            appointmentDate: new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc),
            requestConfirmationNumber: "A00065",
            appointmentStatus: status,
            panelNumber: "PN-SAMPLE")
        {
            TenantId = TenantId,
        };

    private static (IRepository<Appointment, Guid> Repo, ICaseTrackerDocumentQueue Queue, IClock Clock) Build(
        AppointmentStatusType status)
    {
        var repo = Substitute.For<IRepository<Appointment, Guid>>();
        repo.FindAsync(AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Appointment?>(NewAppointment(status)));

        var clock = Substitute.For<IClock>();
        clock.Now.Returns(Now);

        return (repo, Substitute.For<ICaseTrackerDocumentQueue>(), clock);
    }

    private static AppointmentDocumentRejectedEto RejectedEvent() => new()
    {
        AppointmentId = AppointmentId,
        AppointmentDocumentId = DocumentId,
        TenantId = TenantId,
        RejectionNotes = "Illegible scan.",
        RejectedByUserId = new Guid("aabbccdd-eeff-4011-a223-344556677889"),
        OccurredAt = Now,
    };

    private static AppointmentDocumentDeletedEto DeletedEvent() => new()
    {
        AppointmentId = AppointmentId,
        AppointmentDocumentId = DocumentId,
        TenantId = TenantId,
        DeletedByUserId = new Guid("aabbccdd-eeff-4011-a223-344556677889"),
        OccurredAt = Now,
    };

    [Fact]
    public async Task WhenStaffRejectADocument_ATombstoneIsQueued()
    {
        var (repo, queue, clock) = Build(AppointmentStatusType.Approved);
        var handler = new DocumentRejectedHandler(
            repo, queue, clock, NullLogger<DocumentRejectedHandler>.Instance);

        await handler.HandleEventAsync(RejectedEvent());

        await queue.Received(1).EnqueueDeletionsAsync(
            AppointmentId,
            TenantId,
            Arg.Is<IReadOnlyList<DocumentDeletionEntry>>(list =>
                list.Count == 1 && list[0].Id == DocumentId && list[0].Deleted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenADocumentIsDeleted_ATombstoneIsQueued()
    {
        var (repo, queue, clock) = Build(AppointmentStatusType.Approved);
        var handler = new DocumentDeletedHandler(
            repo, queue, clock, NullLogger<DocumentDeletedHandler>.Instance);

        await handler.HandleEventAsync(DeletedEvent());

        await queue.Received(1).EnqueueDeletionsAsync(
            AppointmentId,
            TenantId,
            Arg.Is<IReadOnlyList<DocumentDeletionEntry>>(list => list.Count == 1 && list[0].Deleted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TheTombstoneCarriesTheCurrentInstant()
    {
        // The receiver uses updatedAt as a monotonic guard, so a tombstone must not look older than
        // the entry it supersedes.
        var (repo, queue, clock) = Build(AppointmentStatusType.Approved);
        var handler = new DocumentRejectedHandler(
            repo, queue, clock, NullLogger<DocumentRejectedHandler>.Instance);

        await handler.HandleEventAsync(RejectedEvent());

        await queue.Received(1).EnqueueDeletionsAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid?>(),
            Arg.Is<IReadOnlyList<DocumentDeletionEntry>>(list =>
                list[0].UpdatedAt == IntegrationTimestamp.ToIsoUtc(Now)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectionIsNotSentAsAStatusUpdate()
    {
        // Decision: reject-after-accept is a removal, not a Rejected status, so their staff stop
        // seeing a document the portal has repudiated.
        var (repo, queue, clock) = Build(AppointmentStatusType.Approved);
        var handler = new DocumentRejectedHandler(
            repo, queue, clock, NullLogger<DocumentRejectedHandler>.Instance);

        await handler.HandleEventAsync(RejectedEvent());

        await queue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(
            default, default, default!, default);
    }

    [Fact]
    public async Task WhenTheAppointmentWasNeverApproved_NoTombstoneIsQueued()
    {
        var (repo, queue, clock) = Build(AppointmentStatusType.Pending);
        var handler = new DocumentRejectedHandler(
            repo, queue, clock, NullLogger<DocumentRejectedHandler>.Instance);

        await handler.HandleEventAsync(RejectedEvent());

        await queue.DidNotReceiveWithAnyArgs().EnqueueDeletionsAsync(
            default, default, default!, default);
    }

    [Fact]
    public async Task PreviousPublicationIsNotChecked()
    {
        // Decision: deleting an id the receiver does not hold is a harmless no-op, so tracking
        // published-state would duplicate the outbox ledger for no benefit.
        var (repo, queue, clock) = Build(AppointmentStatusType.Approved);
        var handler = new DocumentDeletedHandler(
            repo, queue, clock, NullLogger<DocumentDeletedHandler>.Instance);

        await handler.HandleEventAsync(DeletedEvent());

        // The document row is already soft-deleted by now, so any lookup of it would have failed;
        // the handler must not need one.
        await queue.ReceivedWithAnyArgs(1).EnqueueDeletionsAsync(default, default, default!, default);
    }

    [Fact]
    public async Task WhenQueueingFails_TheStaffActionStillSucceeds()
    {
        var (repo, queue, clock) = Build(AppointmentStatusType.Approved);
        queue.EnqueueDeletionsAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<IReadOnlyList<DocumentDeletionEntry>>(),
                Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("db down"));
        var handler = new DocumentDeletedHandler(
            repo, queue, clock, NullLogger<DocumentDeletedHandler>.Instance);

        await Should.NotThrowAsync(() => handler.HandleEventAsync(DeletedEvent()));
    }
}
