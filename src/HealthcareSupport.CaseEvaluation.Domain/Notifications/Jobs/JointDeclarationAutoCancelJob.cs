using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Data;
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

// AppointmentStatusChangedEto lives under HealthcareSupport.CaseEvaluation.Appointments.

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Phase 14 (2026-05-04) -- JDF auto-cancel recurring job. Daily run
/// queries every tenant's Approved AME appointments, finds ones with
/// no uploaded JDF document AND a due-date inside the
/// <c>SystemParameter.JointDeclarationUploadCutoffDays</c> window,
/// transitions each to <c>CancelledNoBill</c>, and publishes
/// <see cref="AppointmentAutoCancelledEto"/> for the per-feature
/// stakeholder-notification handler (Phase 14b).
///
/// <para>Mirrors OLD spec line 419: "In case if the document is
/// pending as of specified number of days before the appointment due
/// date, the appointment will be auto-cancelled and a notification
/// email will be sent to all the stakeholders related to the
/// appointment."</para>
///
/// <para>Cron: 06:00 PT daily -- earlier than the existing 07:00 PT
/// AppointmentDayReminderJob so the auto-cancel happens before the
/// reminder fans out (a cancelled appointment should not fire a
/// reminder for an appointment-day visit that won't happen). Cutoff
/// predicate is <see cref="JointDeclarationCutoff.IsAtOrPastCutoff"/>
/// for unit-test coverage.</para>
/// </summary>
public class JointDeclarationAutoCancelJob : ITransientDependency
{
    public const string RecurringJobId = "appt-jdf-auto-cancel";
    public const string CronExpression = "0 6 * * *";

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly ISettingProvider _settingProvider;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<JointDeclarationAutoCancelJob> _logger;

    public JointDeclarationAutoCancelJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentDocument, Guid> documentRepository,
        ISettingProvider settingProvider,
        IDataFilter dataFilter,
        ICurrentTenant currentTenant,
        ILocalEventBus localEventBus,
        ILogger<JointDeclarationAutoCancelJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _documentRepository = documentRepository;
        _settingProvider = settingProvider;
        _dataFilter = dataFilter;
        _currentTenant = currentTenant;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("JointDeclarationAutoCancelJob: starting daily run.");
        var nowUtc = DateTime.UtcNow;

        // Iterate every tenant by switching ICurrentTenant. Disabling
        // the IMultiTenant filter for the discovery query lets us see
        // every tenant's appointments in one pass. Per-tenant scope is
        // re-applied for the manager.TransitionAsync call.
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
            CaseEvaluationSettings.DocumentsPolicy.JointDeclarationUploadCutoffDays);
        if (cutoffDays <= 0)
        {
            _logger.LogInformation(
                "JointDeclarationAutoCancelJob: tenant {TenantId} cutoff is {CutoffDays}; gate disabled, skipping.",
                tenantId,
                cutoffDays);
            return;
        }

        // Discover Approved AME appointments WITHOUT an uploaded JDF
        // doc AND due-date inside the cutoff window. The cutoff
        // predicate is reused for unit testability.
        var appointmentQueryable = await _appointmentRepository.GetQueryableAsync();
        var documentQueryable = await _documentRepository.GetQueryableAsync();

        var ameId = CaseEvaluationSeedIds.AppointmentTypes.Ame;
        var candidates = appointmentQueryable
            .Where(a => a.AppointmentStatus == AppointmentStatusType.Approved &&
                        a.AppointmentTypeId == ameId &&
                        a.DueDate.HasValue)
            .Select(a => new { a.Id, a.DueDate, a.DoctorAvailabilityId })
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        // For each, check (a) no JDF document with a non-Rejected
        // status, AND (b) cutoff predicate.
        foreach (var candidate in candidates)
        {
            if (!JointDeclarationCutoff.IsAtOrPastCutoff(candidate.DueDate, cutoffDays, nowUtc))
            {
                continue;
            }
            var hasJdf = documentQueryable
                .Where(d => d.AppointmentId == candidate.Id &&
                            d.IsJointDeclaration &&
                            d.Status != DocumentStatus.Rejected)
                .Any();
            if (hasJdf)
            {
                continue;
            }

            try
            {
                // Strict-parity exception: Phase 14 sets the status
                // directly rather than going through Session A's
                // private TransitionAsync. Reasons:
                //   - Session B's directive: do NOT modify
                //     AppointmentManager.cs.
                //   - The state machine has no Approved -> Cancelled*
                //     direct edge; an auto-cancel is conceptually
                //     distinct from a user-initiated change request,
                //     so threading through CancellationRequested ->
                //     Confirm would be ceremony.
                //   - Publishing AppointmentStatusChangedEto manually
                //     drives Session A's existing SlotCascadeHandler
                //     so the slot transitions Booked -> Available
                //     identically to the supervisor-cancel path.
                // Phase 14b refines if Session A grows a public
                // auto-cancel trigger.
                var entity = await _appointmentRepository.GetAsync(candidate.Id);
                var fromStatus = entity.AppointmentStatus;
                entity.AppointmentStatus = AppointmentStatusType.CancelledNoBill;
                await _appointmentRepository.UpdateAsync(entity, autoSave: true);

                await _localEventBus.PublishAsync(new AppointmentStatusChangedEto(
                    appointmentId: entity.Id,
                    tenantId: entity.TenantId,
                    fromStatus: fromStatus,
                    toStatus: entity.AppointmentStatus,
                    actingUserId: null,
                    reason: "JDF-not-uploaded",
                    occurredAt: DateTime.UtcNow,
                    doctorAvailabilityId: entity.DoctorAvailabilityId));

                await _localEventBus.PublishAsync(new AppointmentAutoCancelledEto
                {
                    AppointmentId = candidate.Id,
                    TenantId = tenantId,
                    Reason = "JDF-not-uploaded",
                    OccurredAt = DateTime.UtcNow,
                });

                _logger.LogInformation(
                    "JointDeclarationAutoCancelJob: tenant {TenantId} auto-cancelled appointment {AppointmentId} (DueDate={DueDate}, cutoff={CutoffDays} days).",
                    tenantId,
                    candidate.Id,
                    candidate.DueDate,
                    cutoffDays);
            }
            catch (Exception ex)
            {
                // Per-row failure should not block the rest of the
                // tenant's auto-cancel pass; log and continue.
                _logger.LogWarning(
                    ex,
                    "JointDeclarationAutoCancelJob: tenant {TenantId} failed to auto-cancel appointment {AppointmentId}; continuing.",
                    tenantId,
                    candidate.Id);
            }
        }
    }

    private async Task<Guid?[]> GetDistinctTenantIdsAsync()
    {
        // Disable the IMultiTenant filter for tenant discovery so we
        // see every tenant's rows. The host tenant (null) is included
        // for the pathological case of host-scoped appointments.
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
