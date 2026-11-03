using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// The twice-daily staff summaries (2026-11-03), replacing the 09:00 pending digest and the 09:15
/// per-staff queue-counts email. Runs at 08:00 and 16:00 Pacific. Each run:
///
/// <list type="bullet">
///   <item>counts every office's queue and raises one <see cref="OfficeSummaryEto"/> per office that has
///         something outstanding (handled in that office: inbox + Intake Staff);</item>
///   <item>raises ONE <see cref="SupervisorSummaryEto"/> covering all offices that have something
///         outstanding (handled in host scope: each host Staff Supervisor gets one email).</item>
/// </list>
///
/// <para>A quiet office is left out and a run with nothing outstanding anywhere sends nothing, the same
/// as the old pending digest did. Approved appointments alone are information, not work.</para>
///
/// <para>This lives in the Application layer because the overdue rule
/// (<see cref="DecisionSlaPolicy"/>) does, and the dashboard tile and these emails must agree to the day.</para>
/// </summary>
public class StaffSummaryJob : ITransientDependency
{
    public const string RecurringJobId = "appt-staff-summary";

    /// <summary>08:00 and 16:00; registered with the Pacific time zone like every other job.</summary>
    public const string CronExpression = "0 8,16 * * *";

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly ISystemParameterRepository _systemParameterRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<StaffSummaryJob> _logger;

    public StaffSummaryJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<Patient, Guid> patientRepository,
        ISystemParameterRepository systemParameterRepository,
        ITenantRepository tenantRepository,
        ITenantWorkRunner tenantWorkRunner,
        ICurrentTenant currentTenant,
        ILocalEventBus localEventBus,
        ILogger<StaffSummaryJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _patientRepository = patientRepository;
        _systemParameterRepository = systemParameterRepository;
        _tenantRepository = tenantRepository;
        _tenantWorkRunner = tenantWorkRunner;
        _currentTenant = currentTenant;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("StaffSummaryJob: starting run.");
        var nowUtc = DateTime.UtcNow;

        var names = await OfficeNamesAsync();
        var snapshots = await _tenantWorkRunner.AggregateAcrossOfficesAsync(
            officeId => SnapshotAsync(officeId, names, nowUtc));

        var outstanding = snapshots
            .Where(s => s != null && s.Counts.HasOutstanding)
            .Select(s => s!)
            .ToList();

        foreach (var office in outstanding)
        {
            await _localEventBus.PublishAsync(office);
        }

        if (outstanding.Count > 0)
        {
            await _localEventBus.PublishAsync(new SupervisorSummaryEto
            {
                OccurredAt = nowUtc,
                Offices = outstanding
                    .Select(o => new SupervisorSummaryOffice { OfficeName = o.OfficeName, Counts = o.Counts })
                    .OrderBy(o => o.OfficeName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            });
        }

        _logger.LogInformation(
            "StaffSummaryJob: {Outstanding} of {Total} office(s) had something outstanding.",
            outstanding.Count,
            snapshots.Count);
    }

    private async Task<Dictionary<Guid, string>> OfficeNamesAsync()
    {
        using (_currentTenant.Change(null))
        {
            var tenants = await _tenantRepository.GetListAsync();
            return tenants.ToDictionary(t => t.Id, t => t.Name);
        }
    }

    /// <summary>
    /// One office's queue, read in that office's scope. A failure is logged and skips THAT office, so one
    /// unreadable database does not stop every other office's summary.
    /// </summary>
    private async Task<OfficeSummaryEto?> SnapshotAsync(Guid officeId, Dictionary<Guid, string> names, DateTime nowUtc)
    {
        try
        {
            var systemParameter = await _systemParameterRepository.GetCurrentTenantAsync();
            var decisionDueDays = systemParameter?.PendingAppointmentOverDueNotificationDays
                ?? SystemParameterConsts.DefaultPendingAppointmentOverDueNotificationDays;

            var appointments = await _appointmentRepository.GetQueryableAsync();
            var byStatus = appointments
                .GroupBy(a => a.AppointmentStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(g => g.Status, g => g.Count);

            var rows = await PendingRowsAsync(appointments, decisionDueDays, nowUtc);

            return new OfficeSummaryEto
            {
                TenantId = officeId,
                OfficeName = names.TryGetValue(officeId, out var name) ? name : officeId.ToString(),
                OccurredAt = nowUtc,
                DecisionDueDays = decisionDueDays,
                Rows = rows,
                Counts = new StaffSummaryCounts
                {
                    Pending = CountOf(byStatus, AppointmentStatusType.Pending),
                    PendingOverdue = rows.Count(r => r.IsOverdue),
                    Approved = CountOf(byStatus, AppointmentStatusType.Approved),
                    RescheduleRequested = CountOf(byStatus, AppointmentStatusType.RescheduleRequested),
                    CancellationRequested = CountOf(byStatus, AppointmentStatusType.CancellationRequested),
                    InfoRequested = CountOf(byStatus, AppointmentStatusType.InfoRequested),
                },
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StaffSummaryJob: office {OfficeId} could not be read; its summary is skipped this run.", officeId);
            return null;
        }
    }

    private static int CountOf(Dictionary<AppointmentStatusType, int> byStatus, AppointmentStatusType status) =>
        byStatus.TryGetValue(status, out var count) ? count : 0;

    private async Task<List<OfficeSummaryRow>> PendingRowsAsync(
        IQueryable<Appointment> appointments, int decisionDueDays, DateTime nowUtc)
    {
        var pending = appointments
            .Where(a => a.AppointmentStatus == AppointmentStatusType.Pending)
            .Select(a => new
            {
                a.RequestConfirmationNumber,
                a.AppointmentDate,
                a.DueDate,
                a.PatientId,
                a.CreationTime,
            })
            .ToList();
        if (pending.Count == 0)
        {
            return new List<OfficeSummaryRow>();
        }

        var patientIds = pending.Select(a => a.PatientId).Distinct().ToList();
        var patientQueryable = await _patientRepository.GetQueryableAsync();
        var patientsById = patientQueryable
            .Where(p => patientIds.Contains(p.Id))
            .Select(p => new { p.Id, p.FirstName, p.LastName })
            .ToList()
            .ToDictionary(p => p.Id);

        return pending
            .Select(a =>
            {
                patientsById.TryGetValue(a.PatientId, out var patient);
                var patientName = patient == null ? string.Empty : ($"{patient.FirstName} {patient.LastName}").Trim();
                return new OfficeSummaryRow
                {
                    RequestConfirmationNumber = a.RequestConfirmationNumber,
                    PatientName = string.IsNullOrWhiteSpace(patientName) ? "(unnamed patient)" : patientName,
                    AppointmentDate = a.AppointmentDate,
                    DueDate = a.DueDate,
                    DecisionDue = DecisionSlaPolicy.DecisionDueDate(a.CreationTime, decisionDueDays),
                    IsOverdue = DecisionSlaPolicy.IsDecisionOverdue(a.CreationTime, nowUtc, decisionDueDays),
                };
            })
            .OrderBy(r => r.AppointmentDate)
            .ToList();
    }
}
