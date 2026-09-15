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
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Unit tests for the accept trigger. Two rules carry the design: only ACCEPTED documents are
/// published (unvetted uploads stay inside the portal), and nothing is published for an appointment
/// whose intake was never pushed -- otherwise every pre-approval document review would enqueue a
/// push that 404s and dead-letters.
/// </summary>
public class DocumentAcceptedHandlerTests
{
    private static readonly Guid TenantId = new("b8844bba-414c-e238-4a71-3a22841f21af");
    private static readonly Guid AppointmentId = new("ada5e3c5-0034-ebde-253c-3a2293631dee");
    private static readonly Guid DocumentId = new("f97796c9-365b-4ad3-a164-08f72981cae3");

    private sealed class Harness
    {
        public DocumentAcceptedHandler Handler { get; init; } = null!;
        public ICaseTrackerDocumentQueue Queue { get; init; } = null!;
    }

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

    private static IntakeDocumentEntry AcceptedEntry() => new()
    {
        Id = DocumentId,
        Source = DocumentEntryMapper.DocumentSource,
        DocumentName = "Medical Records",
        FileName = "records.pdf",
        ContentType = "application/pdf",
        Status = nameof(AppointmentDocuments.DocumentStatus.Accepted),
        ObjectKey = "tenants/b8844bba-414c-e238-4a71-3a22841f21af/records",
        CreatedAtUtc = "2026-07-28T10:00:00.0000000Z",
        UpdatedAt = "2026-07-28T11:30:00.0000000Z",
    };

    private static Harness Build(
        AppointmentStatusType status = AppointmentStatusType.Approved,
        bool documentHasBytes = true,
        bool resolverThrows = false)
    {
        var appointmentRepo = Substitute.For<IRepository<Appointment, Guid>>();
        appointmentRepo.FindAsync(AppointmentId, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Appointment?>(NewAppointment(status)));

        var resolver = Substitute.For<IDocumentListResolver>();
        if (resolverThrows)
        {
            resolver.ResolveDocumentAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
                .Throws(new InvalidOperationException("storage down"));
        }
        else
        {
            resolver.ResolveDocumentAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IntakeDocumentEntry?>(documentHasBytes ? AcceptedEntry() : null));
        }

        var queue = Substitute.For<ICaseTrackerDocumentQueue>();

        return new Harness
        {
            Handler = new DocumentAcceptedHandler(
                appointmentRepo, resolver, queue, NullLogger<DocumentAcceptedHandler>.Instance),
            Queue = queue,
        };
    }

    private static AppointmentDocumentAcceptedEto Event() => new()
    {
        AppointmentId = AppointmentId,
        AppointmentDocumentId = DocumentId,
        TenantId = TenantId,
        AcceptedByUserId = new Guid("aabbccdd-eeff-4011-a223-344556677889"),
        OccurredAt = new DateTime(2026, 7, 28, 11, 30, 0, DateTimeKind.Utc),
    };

    [Fact]
    public async Task WhenStaffAcceptADocument_ItIsQueuedAsASingleEntry()
    {
        var h = Build();

        await h.Handler.HandleEventAsync(Event());

        await h.Queue.Received(1).EnqueueDocumentEntriesAsync(
            AppointmentId,
            TenantId,
            Arg.Is<IReadOnlyList<IntakeDocumentEntry>>(list =>
                list.Count == 1 && list[0].Id == DocumentId && list[0].Status == "Accepted"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenTheAppointmentIsStillPending_NothingIsQueued()
    {
        // Its intake has never been pushed, so a document update would reference a case that does
        // not exist. The document is published later, by the intake push that approval triggers.
        var h = Build(status: AppointmentStatusType.Pending);

        await h.Handler.HandleEventAsync(Event());

        await h.Queue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(
            default, default, default!, default);
    }

    [Theory]
    [InlineData(AppointmentStatusType.Rejected)]
    [InlineData(AppointmentStatusType.InfoRequested)]
    public async Task WhenTheAppointmentWasNeverApproved_NothingIsQueued(AppointmentStatusType status)
    {
        // Both states are reachable only from Pending, so no intake was ever pushed.
        var h = Build(status: status);

        await h.Handler.HandleEventAsync(Event());

        await h.Queue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(
            default, default, default!, default);
    }

    [Theory]
    [InlineData(AppointmentStatusType.CancelledLate)]
    [InlineData(AppointmentStatusType.RescheduledNoBill)]
    [InlineData(AppointmentStatusType.CheckedIn)]
    public async Task ForAPostApprovalStatus_TheDocumentIsStillQueued(AppointmentStatusType status)
    {
        // The case exists on their side, so late-arriving documents must still reach it.
        var h = Build(status: status);

        await h.Handler.HandleEventAsync(Event());

        await h.Queue.ReceivedWithAnyArgs(1).EnqueueDocumentEntriesAsync(
            default, default, default!, default);
    }

    [Fact]
    public async Task WhenTheDocumentHasNoBytes_NothingIsQueued()
    {
        // Publishing a non-fetchable row would hand the receiver an object key that 404s.
        var h = Build(documentHasBytes: false);

        await h.Handler.HandleEventAsync(Event());

        await h.Queue.DidNotReceiveWithAnyArgs().EnqueueDocumentEntriesAsync(
            default, default, default!, default);
    }

    [Fact]
    public async Task WhenQueueingFails_TheAcceptanceItselfStillSucceeds()
    {
        // Staff accepting a document is the primary business action; the push is downstream of it.
        var h = Build(resolverThrows: true);

        await Should.NotThrowAsync(() => h.Handler.HandleEventAsync(Event()));
    }
}
