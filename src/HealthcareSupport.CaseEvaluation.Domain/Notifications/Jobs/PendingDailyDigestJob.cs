using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Patients;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Phase 7 (Category 7, 2026-05-10) -- daily pending-appointment digest
/// to the per-tenant clinic-staff inbox. Mirrors OLD
/// <c>SchedulerDomain.cs</c>:72 -- a single fan-in email summarising
/// every <c>AppointmentStatusType.Pending</c> request in the tenant.
///
/// <para>Cron: 09:00 PT daily -- after JDF auto-cancel (06:00),
/// appointment-day reminder (07:00), CCR jobs (08:00), due-date
/// reminders (08:15 / 08:45), and package-doc reminder (08:30) so the
/// digest reflects the latest auto-cancel / approval activity.</para>
///
/// <para>One <see cref="PendingDailyDigestEto"/> per tenant; the
/// handler queries the per-tenant <c>NotificationsPolicy.OfficeEmail</c>
/// setting for the recipient inbox and renders the row list into a
/// <c>##DailyNotificationContent##</c> HTML block.</para>
/// </summary>
public class PendingDailyDigestJob : ITransientDependency
{
    public const string RecurringJobId = "appt-pending-daily-digest";
    public const string CronExpression = "0 9 * * *";

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<PendingDailyDigestJob> _logger;

    public PendingDailyDigestJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        ILocalEventBus localEventBus,
        ILogger<PendingDailyDigestJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _dataFilter = dataFilter;
        _currentTenant = currentTenant;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("PendingDailyDigestJob: starting daily run.");
        var nowUtc = DateTime.UtcNow;
        var tenantIds = await GetDistinctTenantIdsAsync();

        foreach (var tenantId in tenantIds)
        {
            if (!tenantId.HasValue)
            {
                // Host-scope pending appointments do not exist by design.
                continue;
            }
            using (_currentTenant.Change(tenantId))
            {
                await ProcessTenantAsync(tenantId, nowUtc);
            }
        }
    }

    private async Task ProcessTenantAsync(Guid? tenantId, DateTime nowUtc)
    {
        var appointmentQueryable = await _appointmentRepository.GetQueryableAsync();
        var pendingAppointments = appointmentQueryable
            .Where(a => a.AppointmentStatus == AppointmentStatusType.Pending)
            .Select(a => new
            {
                a.Id,
                a.RequestConfirmationNumber,
                a.AppointmentDate,
                a.DueDate,
                a.PatientId,
            })
            .ToList();

        if (pendingAppointments.Count == 0)
        {
            _logger.LogInformation(
                "PendingDailyDigestJob: tenant {TenantId} has no pending appointments; skipping digest.",
                tenantId);
            return;
        }

        var patientIds = pendingAppointments.Select(a => a.PatientId).Distinct().ToList();
        var patientQueryable = await _patientRepository.GetQueryableAsync();
        var patientsById = patientQueryable
            .Where(p => patientIds.Contains(p.Id))
            .Select(p => new { p.Id, p.FirstName, p.LastName })
            .ToList()
            .ToDictionary(p => p.Id);

        var rows = pendingAppointments
            .Select(a =>
            {
                patientsById.TryGetValue(a.PatientId, out var patient);
                var patientName = patient == null
                    ? string.Empty
                    : ($"{patient.FirstName} {patient.LastName}").Trim();
                return new PendingDailyDigestRow
                {
                    RequestConfirmationNumber = a.RequestConfirmationNumber,
                    PatientName = string.IsNullOrWhiteSpace(patientName) ? "(unnamed patient)" : patientName,
                    AppointmentDate = a.AppointmentDate,
                    DueDate = a.DueDate,
                };
            })
            .OrderBy(r => r.AppointmentDate)
            .ToList();

        await _localEventBus.PublishAsync(new PendingDailyDigestEto
        {
            TenantId = tenantId,
            Rows = rows,
            OccurredAt = nowUtc,
        });

        _logger.LogInformation(
            "PendingDailyDigestJob: tenant {TenantId} published digest with {Count} pending row(s).",
            tenantId,
            rows.Count);
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
