using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.AppointmentDrafts.Jobs;

/// <summary>
/// #15 (2026-06-22): daily TTL purge of stale booking drafts. Drafts hold PHI, so
/// any not touched within <see cref="RetentionDays"/> is physically deleted to
/// minimize data at rest. Follows the existing Hangfire recurring-job pattern
/// (static RecurringJobId + CronExpression, [UnitOfWork] ExecuteAsync); registered
/// in CaseEvaluationHttpApiHostModule.ConfigureHangfireRecurringJobs().
/// </summary>
public class DraftCleanupJob : ITransientDependency
{
    public const string RecurringJobId = "appt-draft-cleanup";
    public const string CronExpression = "0 3 * * *";
    public const int RetentionDays = 30;

    private readonly IRepository<AppointmentDraft, Guid> _draftRepository;
    private readonly ITenantWorkRunner _tenantWorkRunner;
    private readonly IClock _clock;
    private readonly ILogger<DraftCleanupJob> _logger;

    public DraftCleanupJob(
        IRepository<AppointmentDraft, Guid> draftRepository,
        ITenantWorkRunner tenantWorkRunner,
        IClock clock,
        ILogger<DraftCleanupJob> logger)
    {
        _draftRepository = draftRepository;
        _tenantWorkRunner = tenantWorkRunner;
        _clock = clock;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var cutoff = _clock.Now.AddDays(-RetentionDays);

        // Drafts are creator+tenant scoped, and under database-per-office each
        // office's drafts live in its own database. Iterate every office from the
        // tenant registry and purge inside that office's context, where the
        // IMultiTenant filter naturally scopes the sweep to that office. Only the
        // count is logged -- never the PHI payload.
        await _tenantWorkRunner.ForEachOfficeAsync(async officeId =>
        {
            var expired = await _draftRepository.GetListAsync(x => x.LastSavedTime < cutoff);
            if (expired.Count > 0)
            {
                await _draftRepository.DeleteManyAsync(expired, autoSave: true);
            }

            _logger.LogInformation(
                "DraftCleanupJob: office {OfficeId} purged {Count} booking draft(s) last saved before {Cutoff:o}.",
                officeId,
                expired.Count,
                cutoff);
        });
    }
}
