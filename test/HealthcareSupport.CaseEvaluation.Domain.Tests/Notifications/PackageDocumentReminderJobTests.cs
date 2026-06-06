using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Settings;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Group L gate test for <see cref="PackageDocumentReminderJob"/>. Unlike the
/// exact-day reminder jobs this job keeps its at-or-past cutoff model; Group L
/// only adds the RemindersEnabled mute gate, so these tests assert the gate
/// without disturbing the existing cutoff-driven selection.
/// </summary>
public class PackageDocumentReminderJobTests
{
    private static readonly Guid ApptId = Guid.Parse("ffff0001-0000-0000-0000-000000000001");

    private static (PackageDocumentReminderJob Job, ILocalEventBus Bus) Build(bool enabled)
    {
        var today = DateTime.UtcNow.Date;
        var appt = ReminderJobTestHarness.Appt(ApptId, AppointmentStatusType.Approved, today);
        appt.DueDate = today.AddDays(5); // within the 7-day cutoff (at-or-past)

        var doc = new AppointmentDocument(
            id: Guid.NewGuid(),
            tenantId: ReminderJobTestHarness.TenantId,
            appointmentId: ApptId,
            documentName: "Medical records",
            fileName: "scan.pdf",
            blobName: "blob-key",
            contentType: "application/pdf",
            fileSize: 1,
            uploadedByUserId: Guid.NewGuid())
        {
            Status = DocumentStatus.Pending,
        };

        var settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(CaseEvaluationSettings.RemindersPolicy.RemindersEnabled)
            .Returns(enabled ? "true" : "false");
        settings.GetOrNullAsync(CaseEvaluationSettings.DocumentsPolicy.PackageDocumentReminderDays)
            .Returns("7");

        var bus = Substitute.For<ILocalEventBus>();
        var job = new PackageDocumentReminderJob(
            ReminderJobTestHarness.DocumentRepo(doc),
            ReminderJobTestHarness.AppointmentRepo(appt),
            settings,
            ReminderJobTestHarness.NoopDataFilter(),
            ReminderJobTestHarness.NoopCurrentTenant(),
            bus,
            NullLogger<PackageDocumentReminderJob>.Instance);

        return (job, bus);
    }

    [Fact]
    public async Task Muted_when_reminders_disabled()
    {
        var (job, bus) = Build(enabled: false);

        await job.ExecuteAsync();

        await bus.DidNotReceive().PublishAsync(Arg.Any<PackageDocumentReminderEto>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Publishes_for_outstanding_docs_when_enabled()
    {
        var (job, bus) = Build(enabled: true);

        await job.ExecuteAsync();

        await bus.Received(1).PublishAsync(
            Arg.Is<PackageDocumentReminderEto>(e => e.AppointmentId == ApptId), Arg.Any<bool>());
    }
}
