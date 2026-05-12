using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Phase 7 (Category 7, 2026-05-10) -- date-driven incomplete-document
/// reminder. Mirrors OLD <c>SchedulerDomain.cs</c>:176.
///
/// <para>Distinct from <c>PackageDocumentReminderJob</c> (Reminder #3):
/// that job is status-driven (Pending/Rejected docs past the
/// <c>Documents.PackageDocumentReminderDays</c> cutoff, uses the
/// <c>UploadPendingDocuments</c> template). This job is date-driven
/// (7 days before <c>DueDate</c> AND docs outstanding, uses the
/// <c>AppointmentDocumentIncomplete</c> template). Different template +
/// different trigger gives staff and stakeholders a distinguishable
/// inbox signal per OLD's two-stored-proc model.</para>
///
/// <para>Cron: 08:45 PT daily -- after package-doc reminder (08:30) so
/// the two are sequential rather than concurrent.</para>
/// </summary>
public class DueDateDocumentIncompleteJob : ITransientDependency
{
    public const string RecurringJobId = "appt-duedate-document-incomplete";
    public const string CronExpression = "45 8 * * *";

    private const int ReminderDaysBeforeDueDate = 7;

    private static readonly AppointmentStatusType[] EligibleStatuses =
    {
        AppointmentStatusType.Pending,
        AppointmentStatusType.Approved,
        AppointmentStatusType.RescheduleRequested,
    };

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<DueDateDocumentIncompleteJob> _logger;

    public DueDateDocumentIncompleteJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentDocument, Guid> documentRepository,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        ILocalEventBus localEventBus,
        ILogger<DueDateDocumentIncompleteJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _documentRepository = documentRepository;
        _dataFilter = dataFilter;
        _currentTenant = currentTenant;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("DueDateDocumentIncompleteJob: starting daily run.");
        var nowUtc = DateTime.UtcNow;
        var todayDate = nowUtc.Date;
        var tenantIds = await GetDistinctTenantIdsAsync();

        foreach (var tenantId in tenantIds)
        {
            if (!tenantId.HasValue)
            {
                continue;
            }
            using (_currentTenant.Change(tenantId))
            {
                await ProcessTenantAsync(tenantId, todayDate, nowUtc);
            }
        }
    }

    private async Task ProcessTenantAsync(Guid? tenantId, DateTime todayDate, DateTime nowUtc)
    {
        var apptQueryable = await _appointmentRepository.GetQueryableAsync();
        var targetDate = todayDate.AddDays(ReminderDaysBeforeDueDate);
        var candidates = apptQueryable
            .Where(a => a.DueDate.HasValue && EligibleStatuses.Contains(a.AppointmentStatus))
            .Select(a => new { a.Id, a.DueDate })
            .ToList()
            .Where(a => a.DueDate!.Value.Date == targetDate)
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        var candidateIds = candidates.Select(a => a.Id).ToHashSet();
        var docQueryable = await _documentRepository.GetQueryableAsync();
        var outstanding = docQueryable
            .Where(d => candidateIds.Contains(d.AppointmentId) &&
                        (d.Status == DocumentStatus.Pending || d.Status == DocumentStatus.Rejected))
            .Select(d => new { d.AppointmentId, d.DocumentName })
            .ToList()
            .GroupBy(d => d.AppointmentId)
            .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.DocumentName)));

        var published = 0;
        foreach (var candidate in candidates)
        {
            if (!outstanding.TryGetValue(candidate.Id, out var docList) || string.IsNullOrWhiteSpace(docList))
            {
                continue;
            }

            await _localEventBus.PublishAsync(new DueDateDocumentIncompleteEto
            {
                AppointmentId = candidate.Id,
                TenantId = tenantId,
                DaysUntilDue = ReminderDaysBeforeDueDate,
                PendingDocList = docList,
                OccurredAt = nowUtc,
            });
            published++;
        }

        _logger.LogInformation(
            "DueDateDocumentIncompleteJob: tenant {TenantId} fired {Count} incomplete-document event(s).",
            tenantId,
            published);
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
