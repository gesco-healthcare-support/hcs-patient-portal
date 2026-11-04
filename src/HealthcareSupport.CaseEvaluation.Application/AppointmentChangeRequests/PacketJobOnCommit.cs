using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments.Jobs;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// 2026-11-03: enqueues a packet job only once the ambient unit of work has COMPLETED. Hangfire's manager
/// enqueues at once, so a job enqueued mid-transaction can start before the appointment it renders has
/// committed (seen live when a reschedule completed on consent: "no such entity Appointment" in the packet
/// job, then a retry). Same shape as <c>PacketGenerationOnApprovedHandler</c>. Split out of
/// <see cref="RescheduleFinalizer"/> so each branch can be tested on its own.
/// </summary>
internal static class PacketJobOnCommit
{
    public static async Task EnqueueAsync(
        IUnitOfWorkManager unitOfWorkManager,
        IBackgroundJobManager backgroundJobManager,
        ICurrentTenant currentTenant,
        ILogger logger,
        GenerateAppointmentPacketArgs args)
    {
        var current = unitOfWorkManager.Current;
        if (current == null)
        {
            await backgroundJobManager.EnqueueAsync(args);
            return;
        }

        // The callback runs after the commit, possibly outside this tenant scope.
        var tenantId = currentTenant.Id;
        current.OnCompleted(async () =>
        {
            using (currentTenant.Change(tenantId))
            {
                try
                {
                    await backgroundJobManager.EnqueueAsync(args);
                }
                catch (ObjectDisposedException ex)
                {
                    // Test / shutdown only: the DI scope the in-memory job store captured is gone. Losing one
                    // enqueue there is recoverable (staff can regenerate the packet), and the reconciliation
                    // job releases stalled sets.
                    logger.LogWarning(
                        ex,
                        "RescheduleFinalizer: packet job enqueue skipped for appointment {AppointmentId} -- DI scope disposed before the unit of work completed.",
                        args.AppointmentId);
                }
            }
        });
    }
}
