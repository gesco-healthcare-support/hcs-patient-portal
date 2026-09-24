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
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

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
    /// <summary>
    /// Dead letters one "Retry all" press handles. Each retry rebuilds its payload from current data, so
    /// an unbounded pass after a long outage could outrun the request timeout and fail with nothing
    /// reported; the remainder is counted and handled by pressing again.
    /// </summary>
    public const int MaxRetryAllPerCall = 100;

    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IIntegrationOutboxRepository _outboxRepository;
    private readonly IntegrationOutboxManager _outboxManager;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly CaseTrackerDeadLetterRequeuer _deadLetterRequeuer;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantStore _tenantStore;
    private readonly IClock _clock;

    public CaseTrackerDeadLetterAppService(
        ITenantWorkRunner tenantWorkRunner,
        IIntegrationOutboxRepository outboxRepository,
        IntegrationOutboxManager outboxManager,
        IRepository<Appointment, Guid> appointmentRepository,
        CaseTrackerDeadLetterRequeuer deadLetterRequeuer,
        ICurrentTenant currentTenant,
        ITenantStore tenantStore,
        IClock clock)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _outboxRepository = outboxRepository;
        _outboxManager = outboxManager;
        _appointmentRepository = appointmentRepository;
        _deadLetterRequeuer = deadLetterRequeuer;
        _currentTenant = currentTenant;
        _tenantStore = tenantStore;
        _clock = clock;
    }

    [Authorize(CaseEvaluationPermissions.Appointments.ViewIntegrationDeadLetters)]
    public virtual async Task<List<CaseTrackerDeadLetterDto>> GetListAsync()
    {
        EnsureHostCaller();

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

        // From the tenant STORE, not ICurrentTenant.Name. ITenantWorkRunner enters each office via
        // ICurrentTenant.Change(id), which sets the id but leaves Name null -- so reading Name here
        // produced a blank Clinic column. Caught in live testing; the unit tests substituted
        // ICurrentTenant and returned a name, so they could not see it.
        var tenant = await _tenantStore.FindAsync(officeId);
        var officeName = tenant?.Name ?? string.Empty;

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

    [Authorize(CaseEvaluationPermissions.CaseTrackerIntegration.Default)]
    public virtual async Task<CaseTrackerDeadLetterRetryResultDto> RetryAsync(Guid officeId, Guid outboxItemId)
    {
        EnsureHostCaller();

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

            // On a refusal the exception rolls back anything the requeue wrote, and the dead letter stays
            // listed.
            var outcome = await RetryRowAsync(row, officeId);
            if (!outcome.CanResolve)
            {
                throw new UserFriendlyException(outcome.RefusalReason!);
            }

            return new CaseTrackerDeadLetterRetryResultDto
            {
                QueuedOutboxItemId = outcome.QueuedOutboxItemId!.Value,
                ResolvedOutboxItemId = row.Id,
                AlreadyDelivered = outcome.AlreadyDelivered,
            };
        }
    }

    [Authorize(CaseEvaluationPermissions.Appointments.PushToCaseTracker)]
    public virtual async Task<CaseTrackerDeadLetterRetryAllResultDto> RetryAllAsync(Guid officeId)
    {
        if (officeId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "OfficeId"]);
        }

        var result = new CaseTrackerDeadLetterRetryAllResultDto();

        using (_currentTenant.Change(officeId))
        {
            var ids = await GetFailedIdsOldestFirstAsync();
            result.Remaining = Math.Max(0, ids.Count - MaxRetryAllPerCall);

            foreach (var id in ids.Take(MaxRetryAllPerCall))
            {
                var outcome = await RetryInOwnTransactionAsync(id, officeId);
                if (outcome == null || !outcome.CanResolve)
                {
                    result.NotRetried++;
                }
                else if (outcome.AlreadyDelivered)
                {
                    result.AlreadyDelivered++;
                }
                else
                {
                    result.Requeued++;
                }
            }
        }

        Logger.LogInformation(
            "CaseTrackerDeadLetterAppService: retry-all in office {OfficeId}: requeued {Requeued}, already delivered {AlreadyDelivered}, not retried {NotRetried}, remaining {Remaining}.",
            officeId, result.Requeued, result.AlreadyDelivered, result.NotRetried, result.Remaining);

        return result;
    }

    /// <summary>Oldest first, so repeated presses work through a backlog in the order it failed.</summary>
    private async Task<List<Guid>> GetFailedIdsOldestFirstAsync()
    {
        var queryable = await _outboxRepository.GetQueryableAsync();
        return queryable
            .Where(x => x.Status == IntegrationOutboxStatus.Failed)
            .OrderBy(x => x.LastModificationTime ?? x.CreationTime)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();
    }

    /// <summary>
    /// One row of a retry-all, in its OWN transaction: committed when the retry is judged sound, rolled
    /// back when it is refused or throws, so a bad row never undoes the rows before it. Returns null when
    /// the row is no longer a dead letter (resolved or retried by someone else since the list was read),
    /// or when its retry threw -- logged here, and the row stays listed for a person to look at.
    /// </summary>
    private async Task<DeadLetterRetryOutcome?> RetryInOwnTransactionAsync(Guid rowId, Guid officeId)
    {
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        try
        {
            var row = await _outboxRepository.FindAsync(rowId);
            if (row == null || row.Status != IntegrationOutboxStatus.Failed)
            {
                await uow.RollbackAsync();
                return null;
            }

            var outcome = await RetryRowAsync(row, officeId);
            if (outcome.CanResolve)
            {
                await uow.CompleteAsync();
            }
            else
            {
                await uow.RollbackAsync();
            }

            return outcome;
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "CaseTrackerDeadLetterAppService: retry-all could not retry dead letter {RowId} in office {OfficeId}; it stays listed.",
                rowId, officeId);
            await uow.RollbackAsync();
            return null;
        }
    }

    /// <summary>
    /// The retry itself, shared by the single and bulk paths. Requeues from CURRENT data and judges what
    /// came back; resolves the dead letter only when the judgement allows. Never throws on a refusal:
    /// each caller decides what a refusal means for its transaction.
    /// </summary>
    private async Task<DeadLetterRetryOutcome> RetryRowAsync(IntegrationOutboxItem row, Guid officeId)
    {
        // Fresh payload from CURRENT data, not the stored snapshot -- an intake as an intake, a
        // document update as a document update (see CaseTrackerDeadLetterRequeuer).
        var queued = await _deadLetterRequeuer.RequeueAsync(row, officeId);

        // Judge what came back rather than trust it (#961): this is the only recovery a human has
        // for a stuck message, and it once reported "queued" while sending nothing.
        var outcome = DeadLetterRetryOutcome.Evaluate(row, queued);
        if (!outcome.CanResolve)
        {
            Logger.LogWarning(
                "CaseTrackerDeadLetterAppService: retry of dead letter {RowId} for appointment {AppointmentId} in office {OfficeId} refused; {QueuedCount} row(s) came back.",
                row.Id, row.AppointmentId, officeId, queued.Count);
            return outcome;
        }

        row.MarkResolved(_clock.Now);
        await _outboxManager.SaveAsync(row);

        Logger.LogInformation(
            "CaseTrackerDeadLetterAppService: retried dead letter {RowId} for appointment {AppointmentId} in office {OfficeId}; queued {QueuedId}, already delivered {AlreadyDelivered}.",
            row.Id, row.AppointmentId, officeId, outcome.QueuedOutboxItemId, outcome.AlreadyDelivered);

        return outcome;
    }

    /// <summary>
    /// Refuses a caller who is inside an office, before any office is entered or aggregated. Every action
    /// here reaches whichever office it names, or all of them, so they belong to the host alone. The
    /// Host-only permissions on each method already stop an office caller at the authorization interceptor;
    /// this check keeps the refusal in place even if a permission's side is ever widened again.
    /// </summary>
    private void EnsureHostCaller()
    {
        if (_currentTenant.IsAvailable)
        {
            throw new AbpAuthorizationException(
                "Case Tracker delivery is managed from the host, not from inside an office.");
        }
    }
}
