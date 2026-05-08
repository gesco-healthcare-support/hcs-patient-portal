using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.States;
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

namespace HealthcareSupport.CaseEvaluation.DefenseAttorneys;

public class EfCoreDefenseAttorneyRepository : EfCoreRepository<CaseEvaluationDbContext, DefenseAttorney, Guid>, IDefenseAttorneyRepository
{
    public EfCoreDefenseAttorneyRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<DefenseAttorneyWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await (await GetDbSetAsync()).Where(b => b.Id == id).Select(defenseAttorney => new DefenseAttorneyWithNavigationProperties { DefenseAttorney = defenseAttorney, State = dbContext.Set<State>().FirstOrDefault(c => c.Id == defenseAttorney.StateId), IdentityUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == defenseAttorney.IdentityUserId) }).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<List<DefenseAttorneyWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, firmName, phoneNumber, city, stateId, identityUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DefenseAttorneyConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<DefenseAttorneyWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from defenseAttorney in (await GetDbSetAsync())
               join state in (await GetDbContextAsync()).Set<State>() on defenseAttorney.StateId equals state.Id into states
               from state in states.DefaultIfEmpty()
               join identityUser in (await GetDbContextAsync()).Set<IdentityUser>() on defenseAttorney.IdentityUserId equals (Guid?)identityUser.Id into identityUsers
               from identityUser in identityUsers.DefaultIfEmpty()
               select new DefenseAttorneyWithNavigationProperties
               {
                   DefenseAttorney = defenseAttorney,
                   State = state,
                   IdentityUser = identityUser
               };
    }

    protected virtual IQueryable<DefenseAttorneyWithNavigationProperties> ApplyFilter(IQueryable<DefenseAttorneyWithNavigationProperties> query, string? filterText, string? firmName = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.DefenseAttorney.FirmName!.Contains(filterText!) || e.DefenseAttorney.PhoneNumber!.Contains(filterText!) || e.DefenseAttorney.City!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(firmName), e => e.DefenseAttorney.FirmName!.Contains(firmName!)).WhereIf(!string.IsNullOrWhiteSpace(phoneNumber), e => e.DefenseAttorney.PhoneNumber!.Contains(phoneNumber!)).WhereIf(!string.IsNullOrWhiteSpace(city), e => e.DefenseAttorney.City!.Contains(city!)).WhereIf(stateId != null && stateId != Guid.Empty, e => e.State != null && e.State.Id == stateId).WhereIf(identityUserId != null && identityUserId != Guid.Empty, e => e.IdentityUser != null && e.IdentityUser.Id == identityUserId);
    }

    protected virtual IQueryable<DefenseAttorney> ApplyFilter(IQueryable<DefenseAttorney> query, string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.FirmName!.Contains(filterText!) || e.PhoneNumber!.Contains(filterText!) || e.City!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(firmName), e => e.FirmName!.Contains(firmName!)).WhereIf(!string.IsNullOrWhiteSpace(phoneNumber), e => e.PhoneNumber!.Contains(phoneNumber!)).WhereIf(!string.IsNullOrWhiteSpace(city), e => e.City!.Contains(city!));
    }

    public virtual async Task<List<DefenseAttorney>> GetListAsync(string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, firmName, phoneNumber, city);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DefenseAttorneyConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, firmName, phoneNumber, city, stateId, identityUserId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }
}
