using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Phase 14b (2026-05-04) -- package-document reminder. Daily run
/// queries each tenant for AppointmentDocument rows in
/// <see cref="DocumentStatus.Pending"/> or
/// <see cref="DocumentStatus.Rejected"/> AND for which the parent
/// appointment's <c>DueDate</c> is at or past the configured
/// <c>Documents.PackageDocumentReminderDays</c> cutoff. Publishes one
/// <see cref="AppointmentDocumentUploadedEto"/>-shaped reminder event
/// per outstanding row -- subscribers fire the
/// <c>PackageDocumentsReminder</c> template through the dispatcher.
///
/// <para>Mirrors OLD spec lines 569-593 ("Reminder for incomplete
/// package documents (multiple reminders)"). OLD shipped multiple
/// reminder cadences (T-7, T-3, T-1) tuned per clinic; NEW Phase 14b
/// ships ONE reminder at the configured cutoff. Multi-cadence is a
/// post-parity enhancement once a stakeholder demo confirms the
/// volume.</para>
///
/// <para>Cron: 08:30 PT daily -- after the 06:00 JDF auto-cancel +
/// 07:00 appointment-day reminder + 08:00 CCR jobs so the daily
/// notifications fan out in a deterministic order.</para>
/// </summary>
public class PackageDocumentReminderJob : ITransientDependency
{
    public const string RecurringJobId = "appt-package-doc-reminder";
    public const string CronExpression = "30 8 * * *";

    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ISettingProvider _settingProvider;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<PackageDocumentReminderJob> _logger;

    public PackageDocumentReminderJob(
        IRepository<AppointmentDocument, Guid> documentRepository,
        IRepository<Appointment, Guid> appointmentRepository,
        ISettingProvider settingProvider,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        ILocalEventBus localEventBus,
        ILogger<PackageDocumentReminderJob> logger)
    {
        _documentRepository = documentRepository;
        _appointmentRepository = appointmentRepository;
        _settingProvider = settingProvider;
        _dataFilter = dataFilter;
        _currentTenant = currentTenant;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("PackageDocumentReminderJob: starting daily run.");
        var nowUtc = DateTime.UtcNow;
        var tenantIds = await GetDistinctTenantIdsAsync();

        foreach (var tenantId in tenantIds)
        {
            using (_currentTenant.Change(tenantId))
            {
                await ProcessTenantAsync(tenantId, nowUtc);
            }
        }
    }

    private async Task ProcessTenantAsync(Guid? tenantId, DateTime nowUtc)
    {
        var cutoffDays = await _settingProvider.GetAsync<int>(
            CaseEvaluationSettings.DocumentsPolicy.PackageDocumentReminderDays);
        if (cutoffDays <= 0)
        {
            _logger.LogInformation(
                "PackageDocumentReminderJob: tenant {TenantId} cutoff is {CutoffDays}; gate disabled, skipping.",
                tenantId,
                cutoffDays);
            return;
        }

        // Resolve approved appointments inside the cutoff window.
        var appointmentQueryable = await _appointmentRepository.GetQueryableAsync();
        var inWindow = appointmentQueryable
            .Where(a => a.AppointmentStatus == AppointmentStatusType.Approved &&
                        a.DueDate.HasValue)
            .Select(a => new { a.Id, a.DueDate, a.TenantId })
            .ToList()
            .Where(a => JointDeclarationCutoff.IsAtOrPastCutoff(a.DueDate, cutoffDays, nowUtc))
            .ToList();

        if (inWindow.Count == 0)
        {
            return;
        }

        var inWindowIds = inWindow.Select(a => a.Id).ToHashSet();
        var documentQueryable = await _documentRepository.GetQueryableAsync();
        var outstanding = documentQueryable
            .Where(d => inWindowIds.Contains(d.AppointmentId) &&
                        !d.IsAdHoc &&
                        (d.Status == DocumentStatus.Pending || d.Status == DocumentStatus.Rejected))
            .Select(d => new
            {
                d.Id,
                d.AppointmentId,
                d.IsJointDeclaration,
                d.UploadedByUserId,
                d.TenantId,
            })
            .ToList();

        if (outstanding.Count == 0)
        {
            return;
        }

        foreach (var row in outstanding)
        {
            // Reuse the AppointmentDocumentUploadedEto shape for the
            // reminder so the existing DocumentUploadedEmailHandler is
            // re-triggered. The template-code branch (PackageDocumentsReminder
            // vs PatientDocumentUploaded) is decided by a sibling
            // reminder handler -- but to keep Phase 14b atomic and
            // avoid coupling the upload handler to reminder semantics,
            // we publish a dedicated reminder Eto.
            await _localEventBus.PublishAsync(new PackageDocumentReminderEto
            {
                AppointmentId = row.AppointmentId,
                AppointmentDocumentId = row.Id,
                TenantId = row.TenantId,
                IsJointDeclaration = row.IsJointDeclaration,
                OccurredAt = nowUtc,
            });
        }

        _logger.LogInformation(
            "PackageDocumentReminderJob: tenant {TenantId} fired reminders for {Count} outstanding document(s).",
            tenantId,
            outstanding.Count);
    }

    private async Task<Guid?[]> GetDistinctTenantIdsAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var queryable = await _appointmentRepository.GetQueryableAsync();
            return queryable
                .Select(a => a.TenantId)
                .Distinct()
                .ToArray();
        }
    }
}
