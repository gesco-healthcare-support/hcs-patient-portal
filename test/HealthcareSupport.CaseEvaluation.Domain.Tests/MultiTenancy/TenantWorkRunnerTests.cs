using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentDrafts;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// Pins the cross-office iteration seam (Phase C / decision D8). The integration
/// seed creates two offices (TenantA + TenantB) in the tenant registry; these tests
/// prove the runner enumerates BOTH and switches the office context per iteration --
/// the foundation the recurring jobs and the host dashboard rely on under
/// database-per-office.
/// </summary>
public abstract class TenantWorkRunnerTests<TStartupModule> : CaseEvaluationDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ITenantWorkRunner _runner;
    private readonly IRepository<AppointmentDraft, Guid> _draftRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IClock _clock;

    protected TenantWorkRunnerTests()
    {
        _runner = GetRequiredService<ITenantWorkRunner>();
        _draftRepository = GetRequiredService<IRepository<AppointmentDraft, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _clock = GetRequiredService<IClock>();
    }

    [Fact]
    public async Task ForEachOfficeAsync_visits_every_office_in_its_own_context()
    {
        var visited = new List<(Guid Office, Guid? Context)>();

        await WithUnitOfWorkAsync(async () =>
        {
            await _runner.ForEachOfficeAsync(officeId =>
            {
                visited.Add((officeId, _currentTenant.Id));
                return Task.CompletedTask;
            });
        });

        visited.ShouldContain(v => v.Office == TenantsTestData.TenantARef);
        visited.ShouldContain(v => v.Office == TenantsTestData.TenantBRef);
        // Each iteration runs scoped to that office's own context.
        visited.ShouldAllBe(v => v.Context == v.Office);
    }

    [Fact]
    public async Task AggregateAcrossOfficesAsync_returns_one_value_per_office_scoped_to_that_office()
    {
        var label = "twr-" + Guid.NewGuid().ToString("N")[..8];
        var now = _clock.Now;

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(TenantsTestData.TenantARef))
            {
                await _draftRepository.InsertAsync(
                    new AppointmentDraft(Guid.NewGuid(), "{}", 1, now, label, TenantsTestData.TenantARef), autoSave: true);
                await _draftRepository.InsertAsync(
                    new AppointmentDraft(Guid.NewGuid(), "{}", 1, now, label, TenantsTestData.TenantARef), autoSave: true);
            }

            using (_currentTenant.Change(TenantsTestData.TenantBRef))
            {
                await _draftRepository.InsertAsync(
                    new AppointmentDraft(Guid.NewGuid(), "{}", 1, now, label, TenantsTestData.TenantBRef), autoSave: true);
            }
        });

        List<(Guid Office, int Count)> perOffice = null!;
        await WithUnitOfWorkAsync(async () =>
        {
            perOffice = await _runner.AggregateAcrossOfficesAsync(async officeId =>
                (officeId, await _draftRepository.CountAsync(d => d.Label == label)));
        });

        // Each office sees ONLY its own drafts; the host sum equals their total.
        perOffice.Single(o => o.Office == TenantsTestData.TenantARef).Count.ShouldBe(2);
        perOffice.Single(o => o.Office == TenantsTestData.TenantBRef).Count.ShouldBe(1);
        perOffice.Sum(o => o.Count).ShouldBe(3);
    }
}
