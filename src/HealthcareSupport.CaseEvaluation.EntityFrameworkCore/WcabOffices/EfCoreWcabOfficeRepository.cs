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

namespace HealthcareSupport.CaseEvaluation.WcabOffices;

public class EfCoreWcabOfficeRepository : EfCoreRepository<CaseEvaluationDbContext, WcabOffice, Guid>, IWcabOfficeRepository
{
    public EfCoreWcabOfficeRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? name = null, string? abbreviation = null, string? address = null, string? city = null, string? zipCode = null, bool? isActive = null, Guid? stateId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, name, abbreviation, address, city, zipCode, isActive, stateId);
        var ids = query.Select(x => x.WcabOffice.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<WcabOfficeWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(wcabOffice => new WcabOfficeWithNavigationProperties { WcabOffice = wcabOffice, State = dbContext.Set<State>().FirstOrDefault(c => c.Id == wcabOffice.StateId) }).FirstOrDefault();
    }

    public virtual async Task<List<WcabOfficeWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? name = null, string? abbreviation = null, string? address = null, string? city = null, string? zipCode = null, bool? isActive = null, Guid? stateId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, name, abbreviation, address, city, zipCode, isActive, stateId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? WcabOfficeConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<WcabOfficeWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from wcabOffice in (await GetDbSetAsync())
               join state in (await GetDbContextAsync()).Set<State>() on wcabOffice.StateId equals state.Id into states
               from state in states.DefaultIfEmpty()
               select new WcabOfficeWithNavigationProperties
               {
                   WcabOffice = wcabOffice,
                   State = state
               };
    }

    protected virtual IQueryable<WcabOfficeWithNavigationProperties> ApplyFilter(IQueryable<WcabOfficeWithNavigationProperties> query, string? filterText, string? name = null, string? abbreviation = null, string? address = null, string? city = null, string? zipCode = null, bool? isActive = null, Guid? stateId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.WcabOffice.Name!.Contains(filterText!) || e.WcabOffice.Abbreviation!.Contains(filterText!) || e.WcabOffice.Address!.Contains(filterText!) || e.WcabOffice.City!.Contains(filterText!) || e.WcabOffice.ZipCode!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.WcabOffice.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(abbreviation), e => e.WcabOffice.Abbreviation.Contains(abbreviation)).WhereIf(!string.IsNullOrWhiteSpace(address), e => e.WcabOffice.Address.Contains(address)).WhereIf(!string.IsNullOrWhiteSpace(city), e => e.WcabOffice.City.Contains(city)).WhereIf(!string.IsNullOrWhiteSpace(zipCode), e => e.WcabOffice.ZipCode.Contains(zipCode)).WhereIf(isActive.HasValue, e => e.WcabOffice.IsActive == isActive).WhereIf(stateId != null && stateId != Guid.Empty, e => e.State != null && e.State.Id == stateId);
    }

    public virtual async Task<List<WcabOffice>> GetListAsync(string? filterText = null, string? name = null, string? abbreviation = null, string? address = null, string? city = null, string? zipCode = null, bool? isActive = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, name, abbreviation, address, city, zipCode, isActive);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? WcabOfficeConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? name = null, string? abbreviation = null, string? address = null, string? city = null, string? zipCode = null, bool? isActive = null, Guid? stateId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, name, abbreviation, address, city, zipCode, isActive, stateId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<WcabOffice> ApplyFilter(IQueryable<WcabOffice> query, string? filterText = null, string? name = null, string? abbreviation = null, string? address = null, string? city = null, string? zipCode = null, bool? isActive = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Name!.Contains(filterText!) || e.Abbreviation!.Contains(filterText!) || e.Address!.Contains(filterText!) || e.City!.Contains(filterText!) || e.ZipCode!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(abbreviation), e => e.Abbreviation.Contains(abbreviation)).WhereIf(!string.IsNullOrWhiteSpace(address), e => e.Address.Contains(address)).WhereIf(!string.IsNullOrWhiteSpace(city), e => e.City.Contains(city)).WhereIf(!string.IsNullOrWhiteSpace(zipCode), e => e.ZipCode.Contains(zipCode)).WhereIf(isActive.HasValue, e => e.IsActive == isActive);
    }
}