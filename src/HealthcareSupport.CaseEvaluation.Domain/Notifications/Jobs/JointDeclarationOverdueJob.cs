using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using HealthcareSupport.CaseEvaluation.Settings;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Settings;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Notifications.Jobs;

/// <summary>
/// Daily sweep that FLAGS AME appointments whose Joint Declaration Form deadline has passed with
/// no JDF uploaded, so office staff can decide what to do about them.
///
/// <para>Called <c>JointDeclarationAutoCancelJob</c> until 2026-08-08, when it did exactly that:
/// set <c>CancelledNoBill</c>, wrote a cancellation reason and mailed every stakeholder, with no
/// human involved at any point. Adrian: "auto-cancel without staff or anyone's involvement seems
/// like a risky thing and we should not do that." It now stamps
/// <see cref="Appointment.JointDeclarationOverdueAt"/> and raises
/// <see cref="AppointmentJointDeclarationOverdueEto"/>; the appointment keeps its status.</para>
///
/// <para>Two consequences worth knowing. Nothing reaches the Case Tracker from this path any more,
/// because no status changes. And these appointments now accumulate as Approved-but-overdue, which
/// is the point -- the flag makes visible a backlog that used to be silently absorbed.</para>
///
/// <para>Cron: 06:00 PT daily, kept ahead of the 07:00 AppointmentDayReminderJob. That ordering
/// mattered more when this cancelled things; it is retained so staff see the flag before the day's
/// reminders go out. Cutoff predicate is
/// <see cref="JointDeclarationCutoff.IsAtOrPastCutoff"/> for unit-test coverage.</para>
/// </summary>
public class JointDeclarationOverdueJob : ITransientDependency
{
    /// <summary>
    /// PERSISTED Hangfire recurring-job key. Deliberately still reads "auto-cancel" after the
    /// 2026-08-08 rename: Hangfire keys registrations by this string, so changing it without
    /// deleting the old registration leaves TWO jobs running against the same appointments. The
    /// consolidated reminder job takes the same trade-off with "appt-duedate-approaching".
    /// </summary>
    public const string RecurringJobId = "appt-jdf-auto-cancel";

    public const string CronExpression = "0 6 * * *";

    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IRepository<AppointmentDocument, Guid> _documentRepository;
    private readonly ISettingProvider _settingProvider;
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly ILocalEventBus _localEventBus;
    private readonly ILogger<JointDeclarationOverdueJob> _logger;

    public JointDeclarationOverdueJob(
        IRepository<Appointment, Guid> appointmentRepository,
        IRepository<AppointmentDocument, Guid> documentRepository,
        ISettingProvider settingProvider,
        ITenantWorkRunner tenantWorkRunner,
        ILocalEventBus localEventBus,
        ILogger<JointDeclarationOverdueJob> logger)
    {
        _appointmentRepository = appointmentRepository;
        _documentRepository = documentRepository;
        _settingProvider = settingProvider;
        _tenantWorkRunner = tenantWorkRunner;
        _localEventBus = localEventBus;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        _logger.LogInformation("JointDeclarationOverdueJob: starting daily run.");
        var nowUtc = DateTime.UtcNow;

        // Iterate every office from the tenant registry, processing each inside its
        // own database context. Per-office scope re-applies the IMultiTenant filter
        // automatically for the candidate query and the marker write.
        await _tenantWorkRunner.ForEachOfficeAsync(officeId =>
            ProcessTenantAsync(officeId, nowUtc));
    }

    private async Task ProcessTenantAsync(Guid? tenantId, DateTime nowUtc)
    {
        var cutoffDays = await _settingProvider.GetAsync<int>(
            CaseEvaluationSettings.DocumentsPolicy.JointDeclarationUploadCutoffDays);
        if (cutoffDays <= 0)
        {
            _logger.LogInformation(
                "JointDeclarationOverdueJob: tenant {TenantId} cutoff is {CutoffDays}; gate disabled, skipping.",
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
            .Select(a => new { a.Id, a.DueDate, a.DoctorAvailabilityId, a.JointDeclarationOverdueAt })
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

            // Already flagged on an earlier run. Skip so the stamp keeps recording WHEN the
            // deadline passed rather than when the job last looked, and so staff are told once
            // rather than every morning for as long as the document is missing.
            if (candidate.JointDeclarationOverdueAt.HasValue)
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
                // 2026-08-08: this block used to set AppointmentStatus = CancelledNoBill and
                // publish AppointmentStatusChangedEto, cancelling the appointment with no human
                // involved. That behaviour is REMOVED. The appointment keeps its status; we record
                // that the deadline passed and let staff decide.
                //
                // A welcome side effect: the old code bypassed the state machine deliberately
                // (there is no Approved -> Cancelled* edge) and carried a documented strict-parity
                // exception for doing so. Not writing the status at all retires that exception.
                var entity = await _appointmentRepository.GetAsync(candidate.Id);
                entity.JointDeclarationOverdueAt = nowUtc;
                await _appointmentRepository.UpdateAsync(entity, autoSave: true);

                await _localEventBus.PublishAsync(new AppointmentJointDeclarationOverdueEto
                {
                    AppointmentId = candidate.Id,
                    TenantId = tenantId,
                    DueDate = candidate.DueDate,
                    OccurredAt = nowUtc,
                });

                _logger.LogInformation(
                    "JointDeclarationOverdueJob: tenant {TenantId} flagged appointment {AppointmentId} as JDF-overdue (DueDate={DueDate}, cutoff={CutoffDays} days). Status left unchanged.",
                    tenantId,
                    candidate.Id,
                    candidate.DueDate,
                    cutoffDays);
            }
            catch (Exception ex)
            {
                // Per-row failure should not block the rest of the
                // tenant's pass; log and continue.
                _logger.LogWarning(
                    ex,
                    "JointDeclarationOverdueJob: tenant {TenantId} failed to flag appointment {AppointmentId}; continuing.",
                    tenantId,
                    candidate.Id);
            }
        }
    }
}
