using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.AppointmentDrafts.Jobs;

/// <summary>
/// #15: pins the TTL purge of stale booking drafts. A draft last saved beyond the
/// retention window is physically removed; a recent one is kept. Under
/// database-per-office the job iterates every office via ITenantWorkRunner and
/// purges inside each office's context -- proven here by purging expired drafts in
/// BOTH offices while a fresh draft survives.
/// </summary>
public abstract class DraftCleanupJobTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly DraftCleanupJob _job;
    private readonly IRepository<AppointmentDraft, Guid> _repository;
    private readonly IClock _clock;
    private readonly IDataFilter _dataFilter;
    private readonly ICurrentTenant _currentTenant;

    protected DraftCleanupJobTests()
    {
        _job = GetRequiredService<DraftCleanupJob>();
        _repository = GetRequiredService<IRepository<AppointmentDraft, Guid>>();
        _clock = GetRequiredService<IClock>();
        _dataFilter = GetRequiredService<IDataFilter>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task ExecuteAsync_purges_only_drafts_older_than_the_retention_window()
    {
        var purgeId = Guid.NewGuid();
        var keepId = Guid.NewGuid();
        var now = _clock.Now;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _repository.InsertAsync(
                    new AppointmentDraft(
                        purgeId, "{\"stale\":true}", 1,
                        now.AddDays(-(DraftCleanupJob.RetentionDays + 5)),
                        label: null, tenantId: TenantsTestData.TenantARef),
                    autoSave: true);
                await _repository.InsertAsync(
                    new AppointmentDraft(
                        keepId, "{\"fresh\":true}", 1,
                        now.AddDays(-1),
                        label: null, tenantId: TenantsTestData.TenantARef),
                    autoSave: true);
            }
        });

        await _job.ExecuteAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                (await _repository.FindAsync(purgeId)).ShouldBeNull();
                (await _repository.FindAsync(keepId)).ShouldNotBeNull();
            }
        });
    }

    [Fact]
    public async Task ExecuteAsync_purges_expired_drafts_in_every_office()
    {
        var purgeA = Guid.NewGuid();
        var purgeB = Guid.NewGuid();
        var keepB = Guid.NewGuid();
        var now = _clock.Now;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _repository.InsertAsync(
                    new AppointmentDraft(
                        purgeA, "{\"stale\":true}", 1,
                        now.AddDays(-(DraftCleanupJob.RetentionDays + 5)),
                        label: null, tenantId: TenantsTestData.TenantARef),
                    autoSave: true);
            }

            using (_currentTenant.Change(TenantsTestData.TenantBRef))
            {
                await _repository.InsertAsync(
                    new AppointmentDraft(
                        purgeB, "{\"stale\":true}", 1,
                        now.AddDays(-(DraftCleanupJob.RetentionDays + 5)),
                        label: null, tenantId: TenantsTestData.TenantBRef),
                    autoSave: true);
                await _repository.InsertAsync(
                    new AppointmentDraft(
                        keepB, "{\"fresh\":true}", 1,
                        now.AddDays(-1),
                        label: null, tenantId: TenantsTestData.TenantBRef),
                    autoSave: true);
            }
        });

        await _job.ExecuteAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_dataFilter.Disable<IMultiTenant>())
            {
                // The job iterated BOTH offices: each office's expired draft is gone,
                // and the fresh draft survives.
                (await _repository.FindAsync(purgeA)).ShouldBeNull();
                (await _repository.FindAsync(purgeB)).ShouldBeNull();
                (await _repository.FindAsync(keepB)).ShouldNotBeNull();
            }
        });
    }
}
