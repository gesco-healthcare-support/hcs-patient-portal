using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Outbox;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// T11: host recurring reconciliation sweep. The backstop for the two remaining
/// loss windows Phase 1 could not close: a packet job lost between approval-commit
/// and enqueue, and an outbox row whose prompt drain enqueue was lost to a crash.
///
/// <para>Iterates every office via <see cref="ITenantWorkRunner.ForEachOfficeAsync"/>
/// with a per-office try/catch (that runner aborts the whole run if a delegate
/// throws, so one bad office must be contained here). Per office it (i) re-enqueues
/// per-kind packet generation for Approved appointments with missing / Failed /
/// stale-Generating packets, and (ii) kicks the outbox drain. Re-enqueuing is safe
/// against a still-running job because T1 idempotency (skip-if-Generated +
/// concurrency-claim skip) prevents a double render / double PHI email.</para>
/// </summary>
public class ApprovalReconciliationJob : ITransientDependency
{
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<ApprovalReconciliationJob> _logger;

    public ApprovalReconciliationJob(
        ITenantWorkRunner tenantWorkRunner,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentPacket, Guid> packetRepository,
        IBackgroundJobManager backgroundJobManager,
        ILogger<ApprovalReconciliationJob> logger)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _appointmentRepository = appointmentRepository;
        _packetRepository = packetRepository;
        _backgroundJobManager = backgroundJobManager;
        _logger = logger;
    }

    public const string RecurringJobId = "approval-reconciliation";

    // Every 15 minutes -- a backstop cadence, not a hot path.
    public const string CronExpression = "*/15 * * * *";

    // A Generating row is only re-driven once its last attempt is older than this.
    // Chosen well beyond the 120s render timeout AND Hangfire's 5-attempt retry
    // window so the sweep does not race a job still legitimately retrying; T1
    // idempotency is the actual double-send guard, this only avoids wasted work.
    private static readonly TimeSpan PacketStaleAfter = TimeSpan.FromMinutes(30);

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var offices = 0;
        var failures = 0;
        await _tenantWorkRunner.ForEachOfficeAsync(async officeId =>
        {
            offices++;
            try
            {
                await ReconcileOfficeAsync(officeId);
            }
            catch (Exception ex)
            {
                // Per-office isolation: the runner aborts the whole sweep if a
                // delegate throws, so one bad office must not stop the rest.
                failures++;
                _logger.LogError(
                    ex,
                    "ApprovalReconciliationJob: office {OfficeId} reconciliation failed; continuing with the next office.",
                    officeId);
            }
        });

        _logger.LogInformation(
            "ApprovalReconciliationJob: swept {OfficeCount} offices ({Failures} failed).",
            offices, failures);
    }

    /// <summary>Reconciles one office (already scoped to its DB by the runner).</summary>
    public virtual async Task ReconcileOfficeAsync(Guid officeId)
    {
        await ReEnqueueIncompletePacketsAsync(officeId);

        // Kick the outbox drain out-of-band (do not block the sweep on SMTP).
        await _backgroundJobManager.EnqueueAsync(new OutboxDrainArgs { TenantId = officeId });
    }

    private async Task ReEnqueueIncompletePacketsAsync(Guid officeId)
    {
        var appointmentQueryable = await _appointmentRepository.GetQueryableAsync();
        var approved = appointmentQueryable
            .Where(a => a.AppointmentStatus == AppointmentStatusType.Approved)
            .ToList();
        if (approved.Count == 0)
        {
            return;
        }

        var packetQueryable = await _packetRepository.GetQueryableAsync();
        var allPackets = packetQueryable.ToList(); // office-scoped by the ambient tenant filter

        var now = DateTime.UtcNow;
        var reEnqueued = 0;
        foreach (var appointment in approved)
        {
            var forAppointment = allPackets.Where(p => p.AppointmentId == appointment.Id);
            foreach (var kind in PacketReconciliation.IncompleteKinds(forAppointment, now, PacketStaleAfter))
            {
                await _backgroundJobManager.EnqueueAsync(new GenerateAppointmentPacketArgs
                {
                    AppointmentId = appointment.Id,
                    TenantId = officeId,
                    Kind = kind,
                });
                reEnqueued++;
            }
        }

        if (reEnqueued > 0)
        {
            _logger.LogInformation(
                "ApprovalReconciliationJob: office {OfficeId} re-enqueued {Count} incomplete packet kinds.",
                officeId, reEnqueued);
        }
    }
}
