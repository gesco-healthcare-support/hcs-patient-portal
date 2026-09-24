using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// The missing-intake report (#944): every published appointment, in every office, that has NO intake outbox
/// row -- with NO date floor.
///
/// <para>Why it exists: A00001 and A00004 were approved and never reached the Case Tracker, and nothing on
/// either side reported it. The hourly <see cref="Jobs.CaseTrackerCompletenessSweepJob"/> recovers such a loss
/// only inside its 7-day window, and that floor must stay: without it, switching an office on would send its
/// whole history as fresh intakes. So anything older is invisible to the sweep, and this report covers it.</para>
///
/// <para>REPORT ONLY (promised to the Case Tracker side, 2026-09-15). This class takes no dependency that can
/// enqueue, push or change an outbox row, so it cannot cause the flood the sweep's floor prevents. A person
/// reads the report and decides; the manual push inside the office is the way to act.</para>
///
/// <para>Old history is told apart from real losses by the office's FIRST intake row, the rule in the query
/// the Case Tracker side was sent on 2026-09-15: an appointment approved before the office's integration wrote
/// anything has no row by design (contract decision 10).</para>
/// </summary>
public class CaseTrackerMissingIntakeReporter : ITransientDependency
{
    /// <summary>
    /// How many before-integration appointments an office lists. An office with long history can hold
    /// thousands; the true count is always reported. Likely-lost appointments are never truncated.
    /// </summary>
    public const int BeforeIntegrationListLimit = 200;

    /// <summary>
    /// The statuses the sweep treats as published (<see cref="CaseTrackerPublishPolicy.ShouldPublish"/>), as a
    /// list the database can match, since the policy itself is a C# switch.
    /// </summary>
    private static readonly List<AppointmentStatusType> PublishedStatuses =
        Enum.GetValues<AppointmentStatusType>().Where(CaseTrackerPublishPolicy.ShouldPublish).ToList();

    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly ITenantStore _tenantStore;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentPacket, Guid> _packetRepository;
    private readonly IIntegrationOutboxRepository _outboxRepository;
    private readonly IClock _clock;
    private readonly ILogger<CaseTrackerMissingIntakeReporter> _logger;

    public CaseTrackerMissingIntakeReporter(
        ITenantWorkRunner tenantWorkRunner,
        ITenantStore tenantStore,
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentPacket, Guid> packetRepository,
        IIntegrationOutboxRepository outboxRepository,
        IClock clock,
        ILogger<CaseTrackerMissingIntakeReporter> logger)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _tenantStore = tenantStore;
        _appointmentRepository = appointmentRepository;
        _packetRepository = packetRepository;
        _outboxRepository = outboxRepository;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>One section per office, in the order the offices are read.</summary>
    public virtual Task<List<CaseTrackerMissingIntakeOffice>> BuildAsync(CancellationToken cancellationToken = default) =>
        _tenantWorkRunner.AggregateAcrossOfficesAsync(officeId => BuildOfficeSafelyAsync(officeId, cancellationToken));

    /// <summary>
    /// The rule, pure. Approved before the office's first intake row, or in an office that never wrote one:
    /// before its integration. At or after it: likely lost once the packet set has settled, and settling until
    /// then, because an appointment with no row while its packets render is normal.
    /// </summary>
    public static CaseTrackerMissingIntakeKind Classify(DateTime approvedAt, DateTime? firstIntakeRowAt, bool settled)
    {
        if (firstIntakeRowAt is null || approvedAt < firstIntakeRowAt.Value)
        {
            return CaseTrackerMissingIntakeKind.BeforeIntegration;
        }

        return settled ? CaseTrackerMissingIntakeKind.LikelyLost : CaseTrackerMissingIntakeKind.Settling;
    }

    /// <summary>One office that cannot be read is reported as failed; it does not stop the others.</summary>
    private async Task<CaseTrackerMissingIntakeOffice> BuildOfficeSafelyAsync(Guid officeId, CancellationToken cancellationToken)
    {
        var officeName = (await _tenantStore.FindAsync(officeId))?.Name ?? string.Empty;
        try
        {
            return await BuildOfficeAsync(officeId, officeName, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "CaseTrackerMissingIntakeReporter: office {OfficeId} could not be read; reported as failed.",
                officeId);
            return new CaseTrackerMissingIntakeOffice { OfficeId = officeId, OfficeName = officeName, Failed = true };
        }
    }

    private async Task<CaseTrackerMissingIntakeOffice> BuildOfficeAsync(
        Guid officeId,
        string officeName,
        CancellationToken cancellationToken)
    {
        // An intake row in ANY state counts as present -- Pending, Sent, Failed or Resolved -- exactly as the
        // sweep reads it: the point is an appointment for which nothing was ever written.
        var outbox = await _outboxRepository.GetQueryableAsync();
        var intakeRows = outbox.Where(x => x.MessageType == IntegrationMessageType.Intake);
        var firstIntakeRowAt = intakeRows.Min(x => (DateTime?)x.CreationTime);
        var withIntake = intakeRows.Select(x => x.AppointmentId).Distinct().ToList();

        var appointments = await _appointmentRepository.GetQueryableAsync();
        var missing = appointments
            .Where(a => PublishedStatuses.Contains(a.AppointmentStatus) && !withIntake.Contains(a.Id))
            .ToList();

        var settleCutoff = PacketSetPolicy.Cutoff(_clock.Now);
        var lost = new List<CaseTrackerMissingIntakeItem>();
        var settling = new List<CaseTrackerMissingIntakeItem>();
        var before = new List<CaseTrackerMissingIntakeItem>();

        foreach (var appointment in missing)
        {
            var approvedAt = appointment.AppointmentApproveDate ?? appointment.CreationTime;
            var item = ToItem(appointment, approvedAt);

            // Packets are read only for appointments after the first intake row: the settle check cannot
            // change the answer for older ones, and an office with long history has many of those.
            if (Classify(approvedAt, firstIntakeRowAt, settled: true) == CaseTrackerMissingIntakeKind.BeforeIntegration)
            {
                before.Add(item);
                continue;
            }

            var packets = await _packetRepository.GetListAsync(
                p => p.AppointmentId == appointment.Id, cancellationToken: cancellationToken);
            var settled = IntakeSettlePolicy.IsSettled(appointment, packets, settleCutoff);
            (Classify(approvedAt, firstIntakeRowAt, settled) == CaseTrackerMissingIntakeKind.LikelyLost ? lost : settling)
                .Add(item);
        }

        return new CaseTrackerMissingIntakeOffice
        {
            OfficeId = officeId,
            OfficeName = officeName,
            FirstIntakeRowAt = firstIntakeRowAt,
            LikelyLost = NewestFirst(lost),
            Settling = NewestFirst(settling),
            BeforeIntegration = NewestFirst(before).Take(BeforeIntegrationListLimit).ToList(),
            BeforeIntegrationCount = before.Count,
        };
    }

    private static CaseTrackerMissingIntakeItem ToItem(Appointment appointment, DateTime approvedAt) => new()
    {
        AppointmentId = appointment.Id,
        ConfirmationNumber = appointment.RequestConfirmationNumber,
        Status = appointment.AppointmentStatus,
        ApprovedAt = approvedAt,
    };

    private static List<CaseTrackerMissingIntakeItem> NewestFirst(IEnumerable<CaseTrackerMissingIntakeItem> items) =>
        items.OrderByDescending(i => i.ApprovedAt).ThenBy(i => i.ConfirmationNumber, StringComparer.Ordinal).ToList();
}
