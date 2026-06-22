using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
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
    private readonly IDataFilter _dataFilter;
    private readonly IClock _clock;
    private readonly ILogger<DraftCleanupJob> _logger;

    public DraftCleanupJob(
        IRepository<AppointmentDraft, Guid> draftRepository,
        IDataFilter dataFilter,
        IClock clock,
        ILogger<DraftCleanupJob> logger)
    {
        _draftRepository = draftRepository;
        _dataFilter = dataFilter;
        _clock = clock;
        _logger = logger;
    }

    [UnitOfWork]
    public virtual async Task ExecuteAsync()
    {
        var cutoff = _clock.Now.AddDays(-RetentionDays);

        // Drafts are creator+tenant scoped, but the purge must span every tenant,
        // so the multi-tenant filter is disabled for the sweep. Only the count is
        // logged -- never the PHI payload.
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var expired = await _draftRepository.GetListAsync(x => x.LastSavedTime < cutoff);
            if (expired.Count > 0)
            {
                await _draftRepository.DeleteManyAsync(expired, autoSave: true);
            }

            _logger.LogInformation(
                "DraftCleanupJob: purged {Count} booking draft(s) last saved before {Cutoff:o}.",
                expired.Count,
                cutoff);
        }
    }
}
