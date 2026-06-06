using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Volo.Abp.EventBus.Local;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Group L wiring tests for <see cref="DueDateDocumentIncompleteJob"/>: the
/// RemindersEnabled gate mutes the job, and the T-minus anchor setting drives
/// which due-soon appointments with outstanding documents fire.
/// </summary>
public class DueDateDocumentIncompleteJobTests
{
    private static readonly Guid OnAnchorId = Guid.Parse("eeee0001-0000-0000-0000-000000000001");
    private static readonly Guid OffAnchorId = Guid.Parse("eeee0002-0000-0000-0000-000000000002");

    private static DueDateDocumentIncompleteJob Build(
        bool enabled, ILocalEventBus bus, Appointment[] appointments, AppointmentDocument[] documents)
    {
        return new DueDateDocumentIncompleteJob(
            ReminderJobTestHarness.AppointmentRepo(appointments),
            ReminderJobTestHarness.DocumentRepo(documents),
            ReminderJobTestHarness.NoopDataFilter(),
            ReminderJobTestHarness.NoopCurrentTenant(),
            bus,
            ReminderJobTestHarness.Settings(
                enabled,
                CaseEvaluationSettings.RemindersPolicy.DueDateDocumentIncompleteAnchors,
                "7"),
            NullLogger<DueDateDocumentIncompleteJob>.Instance);
    }

    private static Appointment WithDueDate(Guid id, DateTime dueDate)
    {
        var appt = ReminderJobTestHarness.Appt(id, AppointmentStatusType.Approved, dueDate);
        appt.DueDate = dueDate;
        return appt;
    }

    private static AppointmentDocument Outstanding(Guid appointmentId)
    {
        return new AppointmentDocument(
            id: Guid.NewGuid(),
            tenantId: ReminderJobTestHarness.TenantId,
            appointmentId: appointmentId,
            documentName: "Medical records",
            fileName: "scan.pdf",
            blobName: "blob-key",
            contentType: "application/pdf",
            fileSize: 1,
            uploadedByUserId: Guid.NewGuid())
        {
            Status = DocumentStatus.Pending,
        };
    }

    [Fact]
    public async Task Muted_when_reminders_disabled()
    {
        var today = DateTime.UtcNow.Date;
        var bus = Substitute.For<ILocalEventBus>();
        var job = Build(
            false,
            bus,
            new[] { WithDueDate(OnAnchorId, today.AddDays(7)) },
            new[] { Outstanding(OnAnchorId) });

        await job.ExecuteAsync();

        await bus.DidNotReceive().PublishAsync(Arg.Any<DueDateDocumentIncompleteEto>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Fires_only_for_anchor_day_with_outstanding_docs()
    {
        var today = DateTime.UtcNow.Date;
        var bus = Substitute.For<ILocalEventBus>();
        var job = Build(
            true,
            bus,
            new[]
            {
                WithDueDate(OnAnchorId, today.AddDays(7)),  // anchor day
                WithDueDate(OffAnchorId, today.AddDays(3)),  // not an anchor day
            },
            new[] { Outstanding(OnAnchorId), Outstanding(OffAnchorId) });

        await job.ExecuteAsync();

        await bus.Received(1).PublishAsync(
            Arg.Is<DueDateDocumentIncompleteEto>(e => e.AppointmentId == OnAnchorId), Arg.Any<bool>());
        await bus.DidNotReceive().PublishAsync(
            Arg.Is<DueDateDocumentIncompleteEto>(e => e.AppointmentId == OffAnchorId), Arg.Any<bool>());
    }
}
