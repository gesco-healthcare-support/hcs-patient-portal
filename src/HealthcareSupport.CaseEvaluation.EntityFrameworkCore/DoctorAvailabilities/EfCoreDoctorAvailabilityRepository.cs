using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
using HealthcareSupport.CaseEvaluation.Locations;
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

namespace HealthcareSupport.CaseEvaluation.DoctorAvailabilities;

public class EfCoreDoctorAvailabilityRepository : EfCoreRepository<CaseEvaluationDbContext, DoctorAvailability, Guid>, IDoctorAvailabilityRepository
{
    public EfCoreDoctorAvailabilityRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<DoctorAvailabilityWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(doctorAvailability => new DoctorAvailabilityWithNavigationProperties { DoctorAvailability = doctorAvailability, Location = dbContext.Set<Location>().FirstOrDefault(c => c.Id == doctorAvailability.LocationId), AppointmentType = dbContext.Set<AppointmentType>().FirstOrDefault(c => c.Id == doctorAvailability.AppointmentTypeId) }).FirstOrDefault();
    }

    public virtual async Task<List<DoctorAvailabilityWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, DateTime? availableDateMin = null, DateTime? availableDateMax = null, TimeOnly? fromTimeMin = null, TimeOnly? fromTimeMax = null, TimeOnly? toTimeMin = null, TimeOnly? toTimeMax = null, BookingStatus? bookingStatusId = null, Guid? locationId = null, Guid? appointmentTypeId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, availableDateMin, availableDateMax, fromTimeMin, fromTimeMax, toTimeMin, toTimeMax, bookingStatusId, locationId, appointmentTypeId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DoctorAvailabilityConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<DoctorAvailabilityWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from doctorAvailability in (await GetDbSetAsync())
               join location in (await GetDbContextAsync()).Set<Location>() on doctorAvailability.LocationId equals location.Id into locations
               from location in locations.DefaultIfEmpty()
               join appointmentType in (await GetDbContextAsync()).Set<AppointmentType>() on doctorAvailability.AppointmentTypeId equals appointmentType.Id into appointmentTypes
               from appointmentType in appointmentTypes.DefaultIfEmpty()
               select new DoctorAvailabilityWithNavigationProperties
               {
                   DoctorAvailability = doctorAvailability,
                   Location = location,
                   AppointmentType = appointmentType
               };
    }

    protected virtual IQueryable<DoctorAvailabilityWithNavigationProperties> ApplyFilter(IQueryable<DoctorAvailabilityWithNavigationProperties> query, string? filterText, DateTime? availableDateMin = null, DateTime? availableDateMax = null, TimeOnly? fromTimeMin = null, TimeOnly? fromTimeMax = null, TimeOnly? toTimeMin = null, TimeOnly? toTimeMax = null, BookingStatus? bookingStatusId = null, Guid? locationId = null, Guid? appointmentTypeId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true).WhereIf(availableDateMin.HasValue, e => e.DoctorAvailability.AvailableDate >= availableDateMin!.Value).WhereIf(availableDateMax.HasValue, e => e.DoctorAvailability.AvailableDate <= availableDateMax!.Value).WhereIf(fromTimeMin.HasValue, e => e.DoctorAvailability.FromTime >= fromTimeMin!.Value).WhereIf(fromTimeMax.HasValue, e => e.DoctorAvailability.FromTime <= fromTimeMax!.Value).WhereIf(toTimeMin.HasValue, e => e.DoctorAvailability.ToTime >= toTimeMin!.Value).WhereIf(toTimeMax.HasValue, e => e.DoctorAvailability.ToTime <= toTimeMax!.Value).WhereIf(bookingStatusId.HasValue, e => e.DoctorAvailability.BookingStatusId == bookingStatusId).WhereIf(locationId != null && locationId != Guid.Empty, e => e.Location != null && e.Location.Id == locationId).WhereIf(appointmentTypeId != null && appointmentTypeId != Guid.Empty, e => e.AppointmentType != null && e.AppointmentType.Id == appointmentTypeId);
    }

    public virtual async Task<List<DoctorAvailability>> GetListAsync(string? filterText = null, DateTime? availableDateMin = null, DateTime? availableDateMax = null, TimeOnly? fromTimeMin = null, TimeOnly? fromTimeMax = null, TimeOnly? toTimeMin = null, TimeOnly? toTimeMax = null, BookingStatus? bookingStatusId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, availableDateMin, availableDateMax, fromTimeMin, fromTimeMax, toTimeMin, toTimeMax, bookingStatusId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DoctorAvailabilityConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, DateTime? availableDateMin = null, DateTime? availableDateMax = null, TimeOnly? fromTimeMin = null, TimeOnly? fromTimeMax = null, TimeOnly? toTimeMin = null, TimeOnly? toTimeMax = null, BookingStatus? bookingStatusId = null, Guid? locationId = null, Guid? appointmentTypeId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, availableDateMin, availableDateMax, fromTimeMin, fromTimeMax, toTimeMin, toTimeMax, bookingStatusId, locationId, appointmentTypeId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<DoctorAvailability> ApplyFilter(IQueryable<DoctorAvailability> query, string? filterText = null, DateTime? availableDateMin = null, DateTime? availableDateMax = null, TimeOnly? fromTimeMin = null, TimeOnly? fromTimeMax = null, TimeOnly? toTimeMin = null, TimeOnly? toTimeMax = null, BookingStatus? bookingStatusId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true).WhereIf(availableDateMin.HasValue, e => e.AvailableDate >= availableDateMin!.Value).WhereIf(availableDateMax.HasValue, e => e.AvailableDate <= availableDateMax!.Value).WhereIf(fromTimeMin.HasValue, e => e.FromTime >= fromTimeMin!.Value).WhereIf(fromTimeMax.HasValue, e => e.FromTime <= fromTimeMax!.Value).WhereIf(toTimeMin.HasValue, e => e.ToTime >= toTimeMin!.Value).WhereIf(toTimeMax.HasValue, e => e.ToTime <= toTimeMax!.Value).WhereIf(bookingStatusId.HasValue, e => e.BookingStatusId == bookingStatusId);
    }
}