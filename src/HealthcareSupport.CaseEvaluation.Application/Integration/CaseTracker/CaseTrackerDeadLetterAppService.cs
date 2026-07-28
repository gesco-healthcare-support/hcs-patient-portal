using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Backs the admin dead-letter screen. Host-scoped: it aggregates across every office database, because
/// internal staff work on the host surface and a failure someone must chase is the last thing that
/// should have to be hunted for office by office.
///
/// <para>Cost accepted: the list costs one query per office. Acceptable for an operations screen loaded
/// occasionally by a handful of staff, and the alternative -- a screen per office -- puts the work on the
/// human instead.</para>
/// </summary>
[Authorize]
public class CaseTrackerDeadLetterAppService : CaseEvaluationAppService, ICaseTrackerDeadLetterAppService
{
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IIntegrationOutboxRepository _outboxRepository;
    private readonly IntegrationOutboxManager _outboxManager;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly ICaseTrackerIntakeQueue _intakeQueue;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    public CaseTrackerDeadLetterAppService(
        ITenantWorkRunner tenantWorkRunner,
        IIntegrationOutboxRepository outboxRepository,
        IntegrationOutboxManager outboxManager,
        IRepository<Appointment, Guid> appointmentRepository,
        ICaseTrackerIntakeQueue intakeQueue,
        ICurrentTenant currentTenant,
        IClock clock)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _outboxRepository = outboxRepository;
        _outboxManager = outboxManager;
        _appointmentRepository = appointmentRepository;
        _intakeQueue = intakeQueue;
        _currentTenant = currentTenant;
        _clock = clock;
    }

    [Authorize(CaseEvaluationPermissions.Appointments.ViewIntegrationDeadLetters)]
    public virtual async Task<List<CaseTrackerDeadLetterDto>> GetListAsync()
    {
        var perOffice = await _tenantWorkRunner.AggregateAcrossOfficesAsync(
            async officeId => await CollectOfficeFailuresAsync(officeId));

        return perOffice
            .SelectMany(rows => rows)
            .OrderByDescending(r => r.FailedAt)
            .ToList();
    }

    /// <summary>
    /// Only <see cref="IntegrationOutboxStatus.Failed"/> rows. A row a human has already dealt with is
    /// <c>Resolved</c> and deliberately absent, so the list only ever shows outstanding work.
    /// </summary>
    private async Task<List<CaseTrackerDeadLetterDto>> CollectOfficeFailuresAsync(Guid officeId)
    {
        var queryable = await _outboxRepository.GetQueryableAsync();
        var failures = queryable
            .Where(x => x.Status == IntegrationOutboxStatus.Failed)
            .OrderByDescending(x => x.LastModificationTime ?? x.CreationTime)
            .ToList();

        if (failures.Count == 0)
        {
            return new List<CaseTrackerDeadLetterDto>();
        }

        // Confirmation numbers in one query, not one per row.
        var appointmentIds = failures.Select(f => f.AppointmentId).Distinct().ToList();
        var appointments = await _appointmentRepository.GetListAsync(a => appointmentIds.Contains(a.Id));
        var confirmationByAppointment = appointments.ToDictionary(a => a.Id, a => a.RequestConfirmationNumber);

        var officeName = _currentTenant.Name ?? string.Empty;

        return failures
            .Select(f => new CaseTrackerDeadLetterDto
            {
                Id = f.Id,
                OfficeId = officeId,
                OfficeName = officeName,
                AppointmentId = f.AppointmentId,
                ConfirmationNumber = confirmationByAppointment.TryGetValue(f.AppointmentId, out var c)
                    ? c
                    : string.Empty,
                MessageType = f.MessageType.ToString(),
                TargetPath = f.TargetPath,
                AttemptCount = f.AttemptCount,
                LastError = f.LastError,
                FailedAt = f.LastModificationTime ?? f.CreationTime,
                AlertedAt = f.AlertedAt,
            })
            .ToList();
    }

    [Authorize(CaseEvaluationPermissions.Appointments.PushToCaseTracker)]
    public virtual async Task<CaseTrackerDeadLetterRetryResultDto> RetryAsync(Guid officeId, Guid outboxItemId)
    {
        if (officeId == Guid.Empty || outboxItemId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "OfficeId"]);
        }

        // The row lives in that office's database, and this call arrives on the host surface with no
        // office context of its own, so the scope must be entered explicitly.
        using (_currentTenant.Change(officeId))
        {
            var row = await _outboxRepository.FindAsync(outboxItemId);
            if (row == null)
            {
                throw new EntityNotFoundException(typeof(IntegrationOutboxItem), outboxItemId);
            }

            if (row.Status != IntegrationOutboxStatus.Failed)
            {
                // Retrying a Pending row would duplicate a push that is still due; retrying a Sent one
                // would re-send something already delivered.
                throw new UserFriendlyException(
                    "Only a permanently failed push can be retried. This one is no longer in that state.");
            }

            // Fresh payload from CURRENT data, not the stored snapshot.
            var queued = await _intakeQueue.EnqueueIntakeAsync(row.AppointmentId, officeId);

            row.MarkResolved(_clock.Now);
            await _outboxManager.SaveAsync(row);

            Logger.LogInformation(
                "CaseTrackerDeadLetterAppService: retried dead letter {RowId} for appointment {AppointmentId} in office {OfficeId}; queued {QueuedId}.",
                row.Id, row.AppointmentId, officeId, queued.Id);

            return new CaseTrackerDeadLetterRetryResultDto
            {
                QueuedOutboxItemId = queued.Id,
                ResolvedOutboxItemId = row.Id,
            };
        }
    }
}
