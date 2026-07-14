using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Appointments;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// F1 self-validation: PROVES the multi-office harness gives real (not false) isolation
/// confidence before any cross-office matrix is built on it (risk RF1).
///
/// The decisive assertion is the physical one: an Appointment seeded in office A is
/// invisible to office B EVEN WITH ABP's IMultiTenant query filter disabled. In the old
/// single-connection harness, disabling that filter would surface office A's row (all
/// tenants share one database); here it does not, because office B's connection string
/// resolves to a genuinely separate in-memory database. If routing silently fell back to
/// a shared/host database (the F-8 failure mode), the filter-disabled assertion would
/// fail -- so this test is the guard on the harness itself.
/// </summary>
[Collection(MultiOfficeCollection.Name)]
public class MultiOfficeHarnessSelfValidationTests : CaseEvaluationMultiOfficeTestBase
{
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly IRepository<Appointment, Guid> _appointmentRepository;
    private readonly IDbContextProvider<CaseEvaluationDbContext> _dbContextProvider;

    public MultiOfficeHarnessSelfValidationTests()
    {
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _dataFilter = GetRequiredService<IDataFilter>();
        _appointmentRepository = GetRequiredService<IRepository<Appointment, Guid>>();
        _dbContextProvider = GetRequiredService<IDbContextProvider<CaseEvaluationDbContext>>();
    }

    [Fact]
    public async Task Office_A_appointment_is_physically_absent_from_Office_B()
    {
        var (officeA, officeB) = await GetSeededOfficesAsync();

        // Control: office A genuinely persisted its appointment.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            {
                (await _appointmentRepository.FindAsync(officeA.AppointmentId)).ShouldNotBeNull();
            }
        }, requiresNew: true);

        // Normal (filtered) path: office B sees nothing of office A's appointment.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeB.OfficeId))
            {
                (await _appointmentRepository.FindAsync(officeA.AppointmentId)).ShouldBeNull();
            }
        }, requiresNew: true);

        // Physical proof (F-2): even with the IMultiTenant filter OFF, office B's
        // DbContext cannot see office A's appointment -- it lives in a different database.
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeB.OfficeId))
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var dbContext = await _dbContextProvider.GetDbContextAsync();
                var visibleInOfficeB = await dbContext.Set<Appointment>()
                    .AnyAsync(a => a.Id == officeA.AppointmentId);
                visibleInOfficeB.ShouldBeFalse();
            }
        }, requiresNew: true);

        // And the row IS physically present in office A's database (filter off).
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(officeA.OfficeId))
            using (_dataFilter.Disable<IMultiTenant>())
            {
                var dbContext = await _dbContextProvider.GetDbContextAsync();
                var visibleInOfficeA = await dbContext.Set<Appointment>()
                    .AnyAsync(a => a.Id == officeA.AppointmentId);
                visibleInOfficeA.ShouldBeTrue();
            }
        }, requiresNew: true);
    }
}
