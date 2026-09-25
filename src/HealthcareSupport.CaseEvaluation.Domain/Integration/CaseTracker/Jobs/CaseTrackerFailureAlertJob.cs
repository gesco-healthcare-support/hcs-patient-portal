using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Notifications.Events;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker.Jobs;

/// <summary>
/// Tells internal staff when Case Tracker pushes have dead-lettered, because a permanently failed push
/// means a case silently never reached the Case Tracker and nothing in either system's UI shows it.
///
/// <para>BATCHED, not one email per failure. The contract originally said per-failure, but the usual
/// cause of a dead letter is systemic -- a wrong token, or their service down -- which fails every
/// queued row at once. Fifty emails would be muted or filtered; one email saying fifty failed is
/// strictly more useful. The <c>AlertedAt</c> stamp on each row is what makes a batch send exactly
/// once.</para>
///
/// <para>Emits an event per staff member rather than sending directly, because the email dispatcher
/// lives in the Application layer. Mirrors <c>InternalStaffQueueDigestJob</c>.</para>
///
/// <para>EARLY WARNING (#917, 2026-09-23). A retryable failure now keeps retrying for 24 hours before it
/// dead-letters, so the dead-letter email alone would tell staff about an outage a day late. A second pass
/// therefore emails about pushes that have failed at least
/// <see cref="IntegrationOutboxConsts.EarlyWarningAfterAttempts"/> times and are still retrying -- once per
/// push, throttled by the <c>EarlyWarnedAt</c> stamp exactly as <c>AlertedAt</c> throttles the dead-letter
/// email. The stamp is written with a set-based update, not a tracked save, because the drain may be
/// recording an attempt on the same row at the same moment.</para>
/// </summary>
public class CaseTrackerFailureAlertJob : ITransientDependency
{
    /// <summary>
    /// Roles that count as internal staff. Same pair <c>InternalStaffQueueDigestJob</c> uses -- these are
    /// the roles that already receive operational appointment mail, so alerting them needs no new
    /// recipient concept.
    /// </summary>
    private static readonly string[] InternalStaffRoles = { "Staff Supervisor", "Intake Staff" };

    /// <summary>
    /// Failures listed in the body. Beyond this the count still reports the true total and the body says
    /// it was truncated, so a systemic failure never produces an unreadable email OR a misleading count.
    /// </summary>
    public const int MaxListedFailures = 20;

    public const string RecurringJobId = "case-tracker-failure-alert";

    /// <summary>
    /// Every 15 minutes, matching the drain sweep. Section I2 requires a human to learn of a failure
    /// within minutes, and a faster cadence would only split one systemic outage across more emails.
    /// </summary>
    public const string CronExpression = "*/15 * * * *";

    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IIntegrationOutboxRepository _outboxRepository;
    private readonly IntegrationOutboxManager _outboxManager;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly ILocalEventBus _localEventBus;
    private readonly ITenantStore _tenantStore;
    private readonly IClock _clock;
    private readonly ILogger<CaseTrackerFailureAlertJob> _logger;

    public CaseTrackerFailureAlertJob(
        ITenantWorkRunner tenantWorkRunner,
        IIntegrationOutboxRepository outboxRepository,
        IntegrationOutboxManager outboxManager,
        IRepository<Appointment, Guid> appointmentRepository,
        IIdentityUserRepository identityUserRepository,
        ILocalEventBus localEventBus,
        ITenantStore tenantStore,
        IClock clock,
        ILogger<CaseTrackerFailureAlertJob> logger)
    {
        _tenantWorkRunner = tenantWorkRunner;
        _outboxRepository = outboxRepository;
        _outboxManager = outboxManager;
        _appointmentRepository = appointmentRepository;
        _identityUserRepository = identityUserRepository;
        _localEventBus = localEventBus;
        _tenantStore = tenantStore;
        _clock = clock;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var offices = 0;
        var alerted = 0;
        var warned = 0;

        await _tenantWorkRunner.ForEachOfficeAsync(async officeId =>
        {
            offices++;
            try
            {
                var (deadLetters, retrying) = await AlertOfficeAsync(officeId);
                alerted += deadLetters;
                warned += retrying;
            }
            catch (Exception ex)
            {
                // Per-office isolation: ForEachOfficeAsync aborts the whole run if a delegate throws, so
                // one office with no staff or a broken database must not silence every other office.
                _logger.LogError(
                    ex,
                    "CaseTrackerFailureAlertJob: office {OfficeId} failed; continuing with the next office.",
                    officeId);
            }
        });

        _logger.LogInformation(
            "CaseTrackerFailureAlertJob: swept {OfficeCount} offices, alerted on {Alerted} dead letter(s), warned on {Warned} retrying push(es).",
            offices, alerted, warned);
    }

    /// <summary>Returns how many dead letters were alerted on, and how many retrying rows warned on.</summary>
    private async Task<(int DeadLetters, int Retrying)> AlertOfficeAsync(Guid officeId)
    {
        var deadLetters = await FindUnalertedFailuresAsync();
        var retrying = await _outboxRepository.GetUnwarnedRetryingAsync(IntegrationOutboxConsts.EarlyWarningAfterAttempts);
        if (deadLetters.Count == 0 && retrying.Count == 0)
        {
            return (0, 0);
        }

        var staff = await ResolveInternalStaffAsync();
        if (staff.Count == 0)
        {
            // Do NOT stamp the rows: nobody was told, so a later run (once staff exist) still should.
            _logger.LogWarning(
                "CaseTrackerFailureAlertJob: office {OfficeId} has {DeadLetters} un-alerted dead letter(s) and {Retrying} un-warned retrying push(es) but no internal staff to notify.",
                officeId, deadLetters.Count, retrying.Count);
            return (0, 0);
        }

        // From the tenant STORE, not ICurrentTenant.Name. ForEachOfficeAsync enters each office via
        // ICurrentTenant.Change(id), which sets the id but leaves Name null -- reading Name here would
        // have put a blank office into the alert email. Same bug found live on the admin screen.
        var tenant = await _tenantStore.FindAsync(officeId);
        var office = new OfficeAlert(officeId, tenant?.Name ?? string.Empty, staff, _clock.Now);

        // Each stamp only AFTER its events are published, so a crash mid-publish re-alerts rather than
        // silently swallowing the batch.
        if (deadLetters.Count > 0)
        {
            await PublishToStaffAsync(office, CaseTrackerPushAlertKind.DeadLettered, deadLetters);
            foreach (var row in deadLetters)
            {
                row.MarkAlerted(office.Now);
                await _outboxManager.SaveAsync(row);
            }
        }

        if (retrying.Count > 0)
        {
            await PublishToStaffAsync(office, CaseTrackerPushAlertKind.StillRetrying, retrying);
            await _outboxRepository.StampEarlyWarnedAsync(retrying.Select(r => r.Id).ToList(), office.Now);
        }

        return (deadLetters.Count, retrying.Count);
    }

    /// <summary>One event per staff member, each carrying the same batch.</summary>
    private async Task PublishToStaffAsync(
        OfficeAlert office,
        CaseTrackerPushAlertKind kind,
        List<IntegrationOutboxItem> rows)
    {
        var summaries = await BuildSummariesAsync(rows);

        foreach (var user in office.Staff)
        {
            await _localEventBus.PublishAsync(new CaseTrackerPushFailedEto
            {
                Kind = kind,
                TenantId = office.OfficeId,
                OfficeName = office.OfficeName,
                StaffUserId = user.Id,
                StaffEmail = user.Email,
                StaffFirstName = string.IsNullOrWhiteSpace(user.Name) ? user.UserName : user.Name,
                FailureCount = rows.Count,
                Failures = summaries,
                OccurredAt = office.Now,
            });
        }
    }

    /// <summary>What every alert for one office in one run shares.</summary>
    private sealed record OfficeAlert(Guid OfficeId, string OfficeName, List<IdentityUser> Staff, DateTime Now);

    private async Task<List<IntegrationOutboxItem>> FindUnalertedFailuresAsync()
    {
        var queryable = await _outboxRepository.GetQueryableAsync();

        return queryable
            .Where(x => x.Status == IntegrationOutboxStatus.Failed && x.AlertedAt == null)
            .OrderBy(x => x.CreationTime)
            .ToList();
    }

    /// <summary>
    /// Confirmation numbers resolved in ONE query rather than per row, so a systemic failure does not
    /// turn one alert into fifty appointment reads.
    /// </summary>
    private async Task<List<CaseTrackerPushFailureSummary>> BuildSummariesAsync(
        List<IntegrationOutboxItem> rows)
    {
        var listed = rows.Take(MaxListedFailures).ToList();
        var appointmentIds = listed.Select(r => r.AppointmentId).Distinct().ToList();

        var appointments = await _appointmentRepository.GetListAsync(
            a => appointmentIds.Contains(a.Id));
        var confirmationByAppointment = appointments.ToDictionary(
            a => a.Id, a => a.RequestConfirmationNumber);

        return listed
            .Select(r => new CaseTrackerPushFailureSummary
            {
                AppointmentId = r.AppointmentId,
                ConfirmationNumber = confirmationByAppointment.TryGetValue(r.AppointmentId, out var c)
                    ? c
                    : string.Empty,
                MessageType = r.MessageType.ToString(),
                AttemptCount = r.AttemptCount,
                LastError = r.LastError,
            })
            .ToList();
    }

    private async Task<List<IdentityUser>> ResolveInternalStaffAsync()
    {
        var byId = new Dictionary<Guid, IdentityUser>();

        foreach (var roleName in InternalStaffRoles)
        {
            var users = await _identityUserRepository.GetListByNormalizedRoleNameAsync(roleName.ToUpperInvariant());
            foreach (var user in users.Where(u => !string.IsNullOrWhiteSpace(u.Email)))
            {
                byId[user.Id] = user; // a user holding both roles is alerted once
            }
        }

        return byId.Values.ToList();
    }
}
