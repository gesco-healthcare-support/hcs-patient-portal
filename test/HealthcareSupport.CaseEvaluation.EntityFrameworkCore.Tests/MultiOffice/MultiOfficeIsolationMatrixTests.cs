using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using HealthcareSupport.CaseEvaluation.Patients;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// F2 cross-office isolation matrix (deny-by-default): a principal in office B must
/// never reach office A's data through ANY pathway. These run on the multi-office
/// harness, so each assertion exercises genuine separate-database routing -- not just
/// ABP's row filter. Authorization is bypassed here (the harness allows all), which makes
/// these strict DATA-isolation proofs: even an all-powerful caller in office B cannot see
/// office A's rows, because they are not in office B's database.
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeIsolationMatrixTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly IRepository<Patient, Guid> _patientRepository;
    private readonly IRepository<AppointmentType, Guid> _appointmentTypeRepository;
    private readonly IDbContextProvider<CaseEvaluationDbContext> _dbContextProvider;
    private readonly IPatientsAppService _patientsAppService;
    private readonly IAppointmentTypesAppService _appointmentTypesAppService;
    private readonly ITenantWorkRunner _tenantWorkRunner;

    public MultiOfficeIsolationMatrixTests()
    {
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
        _patientRepository = GetRequiredService<IRepository<Patient, Guid>>();
        _appointmentTypeRepository = GetRequiredService<IRepository<AppointmentType, Guid>>();
        _dbContextProvider = GetRequiredService<IDbContextProvider<CaseEvaluationDbContext>>();
        _patientsAppService = GetRequiredService<IPatientsAppService>();
        _appointmentTypesAppService = GetRequiredService<IAppointmentTypesAppService>();
        _tenantWorkRunner = GetRequiredService<ITenantWorkRunner>();
    }

    // --- Patient: PHI, and NOT IMultiTenant -> isolation rests on the separate database
    // (and the explicit repo filter), so this is the strongest db-per-office proof. ---
    [Fact]
    public async Task Patient_PHI_fromOfficeA_isPhysicallyInvisibleToOfficeB()
    {
        var (officeA, officeB) = await GetSeededOfficesAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeB.OfficeId))
            using (_dataFilter.Disable<IMultiTenant>())
            {
                // Even with every filter disabled, office B's database has no row for
                // office A's patient.
                var dbContext = await _dbContextProvider.GetDbContextAsync();
                var present = await dbContext.Set<Patient>().AnyAsync(p => p.Id == officeA.PatientId);
                present.ShouldBeFalse();
            }
        }, requiresNew: true);

        // Sanity: office A's database does hold it.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                (await _patientRepository.FindAsync(officeA.PatientId)).ShouldNotBeNull();
            }
        }, requiresNew: true);
    }

    [Fact]
    public async Task PatientsAppService_GetAsync_acrossOffices_isDenied()
    {
        var (officeA, officeB) = await GetSeededOfficesAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeB.OfficeId))
            {
                await Should.ThrowAsync<EntityNotFoundException>(
                    () => _patientsAppService.GetAsync(officeA.PatientId));
            }
        }, requiresNew: true);
    }

    // The full-SSN reveal endpoint is the most sensitive PHI pathway. Across offices the
    // patient cannot even be loaded, so the SSN never crosses an office boundary.
    [Fact]
    public async Task PatientsAppService_GetFullSsn_acrossOffices_isDenied()
    {
        var (officeA, officeB) = await GetSeededOfficesAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeB.OfficeId))
            {
                await Should.ThrowAsync<EntityNotFoundException>(
                    () => _patientsAppService.GetFullSsnAsync(officeA.PatientId));
            }
        }, requiresNew: true);
    }

    // --- Catalogs are IMultiTenant per office since Phase A. An edit made in office A
    // must not appear in office B (here: office A's appointment type is unreachable). ---
    [Fact]
    public async Task AppointmentTypesAppService_GetAsync_acrossOffices_isDenied()
    {
        var (officeA, officeB) = await GetSeededOfficesAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeB.OfficeId))
            {
                await Should.ThrowAsync<EntityNotFoundException>(
                    () => _appointmentTypesAppService.GetAsync(officeA.AppointmentTypeId));
            }
        }, requiresNew: true);
    }

    [Fact]
    public async Task AppointmentTypesAppService_GetList_isScopedToTheCurrentOffice()
    {
        var (officeA, officeB) = await GetSeededOfficesAsync();

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeB.OfficeId))
            {
                var list = await _appointmentTypesAppService.GetListAsync(
                    new GetAppointmentTypesInput { MaxResultCount = 1000 });
                var ids = list.Items.Select(x => x.Id).ToList();
                ids.ShouldContain(officeB.AppointmentTypeId);
                ids.ShouldNotContain(officeA.AppointmentTypeId);
            }
        }, requiresNew: true);
    }

    // --- Cross-office WORK (jobs C2 / dashboard host-aggregation C3): the runner must
    // visit BOTH offices, and each visit must see only that office's data. ---
    [Fact]
    public async Task TenantWorkRunner_visitsEveryOffice_eachScopedToItsOwnData()
    {
        await GetSeededOfficesAsync();

        var perOfficePatientCounts = await _tenantWorkRunner.AggregateAcrossOfficesAsync(
            async _ => await _patientRepository.GetCountAsync());

        // Both seeded offices were visited, and each saw exactly its own single patient
        // -- never the other office's. (No office returns the combined count of 2.)
        perOfficePatientCounts.Count.ShouldBeGreaterThanOrEqualTo(2);
        perOfficePatientCounts.ShouldAllBe(count => count == 1);
    }
}
