using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.DefenseAttorneys;
using HealthcareSupport.CaseEvaluation.Appointments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.AppointmentDefenseAttorneys;

public class EfCoreAppointmentDefenseAttorneyRepository : EfCoreRepository<CaseEvaluationDbContext, AppointmentDefenseAttorney, Guid>, IAppointmentDefenseAttorneyRepository
{
    public EfCoreAppointmentDefenseAttorneyRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<AppointmentDefenseAttorneyWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await (await GetDbSetAsync()).Where(b => b.Id == id).Select(appointmentDefenseAttorney => new AppointmentDefenseAttorneyWithNavigationProperties { AppointmentDefenseAttorney = appointmentDefenseAttorney, Appointment = dbContext.Set<Appointment>().FirstOrDefault(c => c.Id == appointmentDefenseAttorney.AppointmentId), DefenseAttorney = dbContext.Set<DefenseAttorney>().FirstOrDefault(c => c.Id == appointmentDefenseAttorney.DefenseAttorneyId), IdentityUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == appointmentDefenseAttorney.IdentityUserId) }).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<List<AppointmentDefenseAttorneyWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, Guid? appointmentId = null, Guid? defenseAttorneyId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, appointmentId, defenseAttorneyId, identityUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentDefenseAttorneyConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<AppointmentDefenseAttorneyWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from appointmentDefenseAttorney in (await GetDbSetAsync())
               join appointment in (await GetDbContextAsync()).Set<Appointment>() on appointmentDefenseAttorney.AppointmentId equals appointment.Id into appointments
               from appointment in appointments.DefaultIfEmpty()
               join defenseAttorney in (await GetDbContextAsync()).Set<DefenseAttorney>() on appointmentDefenseAttorney.DefenseAttorneyId equals defenseAttorney.Id into defenseAttorneys
               from defenseAttorney in defenseAttorneys.DefaultIfEmpty()
               join identityUser in (await GetDbContextAsync()).Set<IdentityUser>() on appointmentDefenseAttorney.IdentityUserId equals identityUser.Id into identityUsers
               from identityUser in identityUsers.DefaultIfEmpty()
               select new AppointmentDefenseAttorneyWithNavigationProperties
               {
                   AppointmentDefenseAttorney = appointmentDefenseAttorney,
                   Appointment = appointment,
                   DefenseAttorney = defenseAttorney,
                   IdentityUser = identityUser
               };
    }

    protected virtual IQueryable<AppointmentDefenseAttorneyWithNavigationProperties> ApplyFilter(IQueryable<AppointmentDefenseAttorneyWithNavigationProperties> query, string? filterText, Guid? appointmentId = null, Guid? defenseAttorneyId = null, Guid? identityUserId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true).WhereIf(appointmentId != null && appointmentId != Guid.Empty, e => e.Appointment != null && e.Appointment.Id == appointmentId).WhereIf(defenseAttorneyId != null && defenseAttorneyId != Guid.Empty, e => e.DefenseAttorney != null && e.DefenseAttorney.Id == defenseAttorneyId).WhereIf(identityUserId != null && identityUserId != Guid.Empty, e => e.IdentityUser != null && e.IdentityUser.Id == identityUserId);
    }

    protected virtual IQueryable<AppointmentDefenseAttorney> ApplyFilter(IQueryable<AppointmentDefenseAttorney> query, string? filterText = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true);
    }

    public virtual async Task<List<AppointmentDefenseAttorney>> GetListAsync(string? filterText = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentDefenseAttorneyConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, Guid? appointmentId = null, Guid? defenseAttorneyId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, appointmentId, defenseAttorneyId, identityUserId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }
}
