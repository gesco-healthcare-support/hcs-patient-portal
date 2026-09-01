using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Locations;
using HealthcareSupport.CaseEvaluation.AppointmentTypes;
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

namespace HealthcareSupport.CaseEvaluation.Doctors;

public class EfCoreDoctorRepository : EfCoreRepository<CaseEvaluationDbContext, Doctor, Guid>, IDoctorRepository
{
    public EfCoreDoctorRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<DoctorWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await (await GetDbSetAsync()).Where(b => b.Id == id).Include(x => x.AppointmentTypes).Include(x => x.Locations).Select(doctor => new DoctorWithNavigationProperties
        {
            Doctor = doctor,
            AppointmentTypes = (
            from doctorAppointmentTypes in doctor.AppointmentTypes
            join _appointmentType in dbContext.Set<AppointmentType>() on doctorAppointmentTypes.AppointmentTypeId equals _appointmentType.Id
            select _appointmentType).ToList(),
            Locations = (
            from doctorLocations in doctor.Locations
            join _location in dbContext.Set<Location>() on doctorLocations.LocationId equals _location.Id
            select _location).ToList()
        }).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<List<DoctorWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? email = null, Guid? appointmentTypeId = null, Guid? locationId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, firstName, lastName, email, appointmentTypeId, locationId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DoctorConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<DoctorWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from doctor in (await GetDbSetAsync())
               select new DoctorWithNavigationProperties
               {
                   Doctor = doctor,
                   AppointmentTypes = new List<AppointmentType>(),
                   Locations = new List<Location>()
               };
    }

    protected virtual IQueryable<DoctorWithNavigationProperties> ApplyFilter(IQueryable<DoctorWithNavigationProperties> query, string? filterText, string? firstName = null, string? lastName = null, string? email = null, Guid? appointmentTypeId = null, Guid? locationId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Doctor.FirstName!.Contains(filterText!) || e.Doctor.LastName!.Contains(filterText!) || e.Doctor.Email!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(firstName), e => e.Doctor.FirstName!.Contains(firstName!)).WhereIf(!string.IsNullOrWhiteSpace(lastName), e => e.Doctor.LastName!.Contains(lastName!)).WhereIf(!string.IsNullOrWhiteSpace(email), e => e.Doctor.Email!.Contains(email!)).WhereIf(appointmentTypeId != null && appointmentTypeId != Guid.Empty, e => e.Doctor.AppointmentTypes.Any(x => x.AppointmentTypeId == appointmentTypeId)).WhereIf(locationId != null && locationId != Guid.Empty, e => e.Doctor.Locations.Any(x => x.LocationId == locationId));
    }

    protected virtual IQueryable<Doctor> ApplyFilter(IQueryable<Doctor> query, string? filterText = null, string? firstName = null, string? lastName = null, string? email = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.FirstName!.Contains(filterText!) || e.LastName!.Contains(filterText!) || e.Email!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(firstName), e => e.FirstName!.Contains(firstName!)).WhereIf(!string.IsNullOrWhiteSpace(lastName), e => e.LastName!.Contains(lastName!)).WhereIf(!string.IsNullOrWhiteSpace(email), e => e.Email!.Contains(email!));
    }

    public virtual async Task<List<Doctor>> GetListAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? email = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, firstName, lastName, email);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DoctorConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? firstName = null, string? lastName = null, string? email = null, Guid? appointmentTypeId = null, Guid? locationId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, firstName, lastName, email, appointmentTypeId, locationId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }
}