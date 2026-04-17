using HealthcareSupport.CaseEvaluation.AppointmentTypes;
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

namespace HealthcareSupport.CaseEvaluation.Locations;

public class EfCoreLocationRepository : EfCoreRepository<CaseEvaluationDbContext, Location, Guid>, ILocationRepository
{
    public EfCoreLocationRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? name = null, string? city = null, string? zipCode = null, decimal? parkingFeeMin = null, decimal? parkingFeeMax = null, bool? isActive = null, Guid? stateId = null, Guid? appointmentTypeId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, name, city, zipCode, parkingFeeMin, parkingFeeMax, isActive, stateId, appointmentTypeId);
        var ids = query.Select(x => x.Location.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<LocationWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(location => new LocationWithNavigationProperties { Location = location, State = dbContext.Set<State>().FirstOrDefault(c => c.Id == location.StateId), AppointmentType = dbContext.Set<AppointmentType>().FirstOrDefault(c => c.Id == location.AppointmentTypeId) }).FirstOrDefault();
    }

    public virtual async Task<List<LocationWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? name = null, string? city = null, string? zipCode = null, decimal? parkingFeeMin = null, decimal? parkingFeeMax = null, bool? isActive = null, Guid? stateId = null, Guid? appointmentTypeId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, name, city, zipCode, parkingFeeMin, parkingFeeMax, isActive, stateId, appointmentTypeId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? LocationConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<LocationWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from location in (await GetDbSetAsync())
               join state in (await GetDbContextAsync()).Set<State>() on location.StateId equals state.Id into states
               from state in states.DefaultIfEmpty()
               join appointmentType in (await GetDbContextAsync()).Set<AppointmentType>() on location.AppointmentTypeId equals appointmentType.Id into appointmentTypes
               from appointmentType in appointmentTypes.DefaultIfEmpty()
               select new LocationWithNavigationProperties
               {
                   Location = location,
                   State = state,
                   AppointmentType = appointmentType
               };
    }

    protected virtual IQueryable<LocationWithNavigationProperties> ApplyFilter(IQueryable<LocationWithNavigationProperties> query, string? filterText, string? name = null, string? city = null, string? zipCode = null, decimal? parkingFeeMin = null, decimal? parkingFeeMax = null, bool? isActive = null, Guid? stateId = null, Guid? appointmentTypeId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Location.Name!.Contains(filterText!) || e.Location.City!.Contains(filterText!) || e.Location.ZipCode!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Location.Name!.Contains(name!)).WhereIf(!string.IsNullOrWhiteSpace(city), e => e.Location.City!.Contains(city!)).WhereIf(!string.IsNullOrWhiteSpace(zipCode), e => e.Location.ZipCode!.Contains(zipCode!)).WhereIf(parkingFeeMin.HasValue, e => e.Location.ParkingFee >= parkingFeeMin!.Value).WhereIf(parkingFeeMax.HasValue, e => e.Location.ParkingFee <= parkingFeeMax!.Value).WhereIf(isActive.HasValue, e => e.Location.IsActive == isActive).WhereIf(stateId != null && stateId != Guid.Empty, e => e.State != null && e.State.Id == stateId).WhereIf(appointmentTypeId != null && appointmentTypeId != Guid.Empty, e => e.AppointmentType != null && e.AppointmentType.Id == appointmentTypeId);
    }

    public virtual async Task<List<Location>> GetListAsync(string? filterText = null, string? name = null, string? city = null, string? zipCode = null, decimal? parkingFeeMin = null, decimal? parkingFeeMax = null, bool? isActive = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, name, city, zipCode, parkingFeeMin, parkingFeeMax, isActive);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? LocationConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? name = null, string? city = null, string? zipCode = null, decimal? parkingFeeMin = null, decimal? parkingFeeMax = null, bool? isActive = null, Guid? stateId = null, Guid? appointmentTypeId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, name, city, zipCode, parkingFeeMin, parkingFeeMax, isActive, stateId, appointmentTypeId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<Location> ApplyFilter(IQueryable<Location> query, string? filterText = null, string? name = null, string? city = null, string? zipCode = null, decimal? parkingFeeMin = null, decimal? parkingFeeMax = null, bool? isActive = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Name!.Contains(filterText!) || e.City!.Contains(filterText!) || e.ZipCode!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Name!.Contains(name!)).WhereIf(!string.IsNullOrWhiteSpace(city), e => e.City!.Contains(city!)).WhereIf(!string.IsNullOrWhiteSpace(zipCode), e => e.ZipCode!.Contains(zipCode!)).WhereIf(parkingFeeMin.HasValue, e => e.ParkingFee >= parkingFeeMin!.Value).WhereIf(parkingFeeMax.HasValue, e => e.ParkingFee <= parkingFeeMax!.Value).WhereIf(isActive.HasValue, e => e.IsActive == isActive);
    }
}