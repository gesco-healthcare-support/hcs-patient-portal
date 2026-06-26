using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Doctors;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// F1 self-validation: PROVES the multi-office harness gives real (not false)
/// isolation confidence before any cross-office matrix is built on it (risk RF1).
///
/// The decisive assertion is the physical one: a Doctor written under office A is
/// invisible to office B EVEN WITH ABP's IMultiTenant query filter disabled. In the
/// old single-connection harness, disabling that filter would surface office A's row
/// (all tenants share one database); here it does not, because office B's connection
/// string resolves to a genuinely separate in-memory database. If routing silently
/// fell back to a shared/host database (the F-8 failure mode), the filter-disabled
/// assertion would fail -- so this test is the guard on the harness itself.
/// </summary>
public class MultiOfficeHarnessSelfValidationTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly ITenantManager _tenantManager;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly IRepository<Doctor, Guid> _doctorRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly IDbContextProvider<CaseEvaluationDbContext> _dbContextProvider;

    public MultiOfficeHarnessSelfValidationTests()
    {
        _tenantManager = GetRequiredService<ITenantManager>();
        _tenantRepository = GetRequiredService<IRepository<Tenant, Guid>>();
        _doctorRepository = GetRequiredService<IRepository<Doctor, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
        _dbContextProvider = GetRequiredService<IDbContextProvider<CaseEvaluationDbContext>>();
    }

    [Fact]
    public async Task Office_A_data_is_physically_absent_from_Office_B()
    {
        var (officeA, officeB) = await CreateTwoOfficesAsync();
        var doctorId = Guid.NewGuid();

        // Write a Doctor into office A's database.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA))
            {
                await _doctorRepository.InsertAsync(
                    new Doctor(doctorId, "Ada", "Alpha", "ada.alpha@example.test", default),
                    autoSave: true);
            }
        }, requiresNew: true);

        // Control: office A genuinely persisted the row (so a "B sees nothing" pass
        // cannot be a false positive from a failed insert).
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA))
            {
                (await _doctorRepository.FindAsync(doctorId)).ShouldNotBeNull();
            }
        }, requiresNew: true);

        // Normal (filtered) path: office B sees nothing.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeB))
            {
                (await _doctorRepository.FindAsync(doctorId)).ShouldBeNull();
            }
        }, requiresNew: true);

        // Physical proof (F-2): even with the IMultiTenant filter OFF, office B's
        // DbContext cannot see office A's row -- it lives in a different database.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeB))
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var dbContext = await _dbContextProvider.GetDbContextAsync();
                var visibleInOfficeB = await dbContext.Set<Doctor>()
                    .AnyAsync(d => d.Id == doctorId);
                visibleInOfficeB.ShouldBeFalse();
            }
        }, requiresNew: true);

        // And the row IS physically present in office A's database (filter off).
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA))
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var dbContext = await _dbContextProvider.GetDbContextAsync();
                var visibleInOfficeA = await dbContext.Set<Doctor>()
                    .AnyAsync(d => d.Id == doctorId);
                visibleInOfficeA.ShouldBeTrue();
            }
        }, requiresNew: true);
    }

    /// <summary>
    /// Creates two tenants and stores a distinct office connection string on each,
    /// exactly as production provisioning does (tenant.SetDefaultConnectionString).
    /// ABP's stock resolver then routes each office to its own database.
    /// </summary>
    private async Task<(Guid OfficeA, Guid OfficeB)> CreateTwoOfficesAsync()
    {
        return await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var a = await _tenantManager.CreateAsync("F1-office-a");
                a.SetDefaultConnectionString(MultiOfficeTestDatabase.OfficeAConnectionString);
                await _tenantRepository.InsertAsync(a, autoSave: true);

                var b = await _tenantManager.CreateAsync("F1-office-b");
                b.SetDefaultConnectionString(MultiOfficeTestDatabase.OfficeBConnectionString);
                await _tenantRepository.InsertAsync(b, autoSave: true);

                return (a.Id, b.Id);
            }
        }, requiresNew: true);
    }
}
