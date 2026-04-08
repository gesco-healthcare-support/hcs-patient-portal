using HealthcareSupport.CaseEvaluation.States;
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

namespace HealthcareSupport.CaseEvaluation.AppointmentEmployerDetails;

public class EfCoreAppointmentEmployerDetailRepository : EfCoreRepository<CaseEvaluationDbContext, AppointmentEmployerDetail, Guid>, IAppointmentEmployerDetailRepository
{
    public EfCoreAppointmentEmployerDetailRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<AppointmentEmployerDetailWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(appointmentEmployerDetail => new AppointmentEmployerDetailWithNavigationProperties { AppointmentEmployerDetail = appointmentEmployerDetail, Appointment = dbContext.Set<Appointment>().FirstOrDefault(c => c.Id == appointmentEmployerDetail.AppointmentId), State = dbContext.Set<State>().FirstOrDefault(c => c.Id == appointmentEmployerDetail.StateId) }).FirstOrDefault();
    }

    public virtual async Task<List<AppointmentEmployerDetailWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? employerName = null, string? phoneNumber = null, string? street = null, string? city = null, Guid? appointmentId = null, Guid? stateId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, employerName, phoneNumber, street, city, appointmentId, stateId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentEmployerDetailConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<AppointmentEmployerDetailWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from appointmentEmployerDetail in (await GetDbSetAsync())
               join appointment in (await GetDbContextAsync()).Set<Appointment>() on appointmentEmployerDetail.AppointmentId equals appointment.Id into appointments
               from appointment in appointments.DefaultIfEmpty()
               join state in (await GetDbContextAsync()).Set<State>() on appointmentEmployerDetail.StateId equals state.Id into states
               from state in states.DefaultIfEmpty()
               select new AppointmentEmployerDetailWithNavigationProperties
               {
                   AppointmentEmployerDetail = appointmentEmployerDetail,
                   Appointment = appointment,
                   State = state
               };
    }

    protected virtual IQueryable<AppointmentEmployerDetailWithNavigationProperties> ApplyFilter(IQueryable<AppointmentEmployerDetailWithNavigationProperties> query, string? filterText, string? employerName = null, string? phoneNumber = null, string? street = null, string? city = null, Guid? appointmentId = null, Guid? stateId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.AppointmentEmployerDetail.EmployerName!.Contains(filterText!) || e.AppointmentEmployerDetail.PhoneNumber!.Contains(filterText!) || e.AppointmentEmployerDetail.Street!.Contains(filterText!) || e.AppointmentEmployerDetail.City!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(employerName), e => e.AppointmentEmployerDetail.EmployerName.Contains(employerName)).WhereIf(!string.IsNullOrWhiteSpace(phoneNumber), e => e.AppointmentEmployerDetail.PhoneNumber.Contains(phoneNumber)).WhereIf(!string.IsNullOrWhiteSpace(street), e => e.AppointmentEmployerDetail.Street.Contains(street)).WhereIf(!string.IsNullOrWhiteSpace(city), e => e.AppointmentEmployerDetail.City.Contains(city)).WhereIf(appointmentId != null && appointmentId != Guid.Empty, e => e.Appointment != null && e.Appointment.Id == appointmentId).WhereIf(stateId != null && stateId != Guid.Empty, e => e.State != null && e.State.Id == stateId);
    }

    public virtual async Task<List<AppointmentEmployerDetail>> GetListAsync(string? filterText = null, string? employerName = null, string? phoneNumber = null, string? street = null, string? city = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, employerName, phoneNumber, street, city);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentEmployerDetailConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? employerName = null, string? phoneNumber = null, string? street = null, string? city = null, Guid? appointmentId = null, Guid? stateId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, employerName, phoneNumber, street, city, appointmentId, stateId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<AppointmentEmployerDetail> ApplyFilter(IQueryable<AppointmentEmployerDetail> query, string? filterText = null, string? employerName = null, string? phoneNumber = null, string? street = null, string? city = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.EmployerName!.Contains(filterText!) || e.PhoneNumber!.Contains(filterText!) || e.Street!.Contains(filterText!) || e.City!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(employerName), e => e.EmployerName.Contains(employerName)).WhereIf(!string.IsNullOrWhiteSpace(phoneNumber), e => e.PhoneNumber.Contains(phoneNumber)).WhereIf(!string.IsNullOrWhiteSpace(street), e => e.Street.Contains(street)).WhereIf(!string.IsNullOrWhiteSpace(city), e => e.City.Contains(city));
    }
}