using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Appointments;
using Volo.Abp.Identity;
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

namespace HealthcareSupport.CaseEvaluation.AppointmentAccessors;

public class EfCoreAppointmentAccessorRepository : EfCoreRepository<CaseEvaluationDbContext, AppointmentAccessor, Guid>, IAppointmentAccessorRepository
{
    public EfCoreAppointmentAccessorRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<AppointmentAccessorWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(appointmentAccessor => new AppointmentAccessorWithNavigationProperties { AppointmentAccessor = appointmentAccessor, IdentityUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == appointmentAccessor.IdentityUserId), Appointment = dbContext.Set<Appointment>().FirstOrDefault(c => c.Id == appointmentAccessor.AppointmentId) }).FirstOrDefault();
    }

    public virtual async Task<List<AppointmentAccessorWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, AccessType? accessTypeId = null, Guid? identityUserId = null, Guid? appointmentId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, accessTypeId, identityUserId, appointmentId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentAccessorConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<AppointmentAccessorWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from appointmentAccessor in (await GetDbSetAsync())
               join identityUser in (await GetDbContextAsync()).Set<IdentityUser>() on appointmentAccessor.IdentityUserId equals identityUser.Id into identityUsers
               from identityUser in identityUsers.DefaultIfEmpty()
               join appointment in (await GetDbContextAsync()).Set<Appointment>() on appointmentAccessor.AppointmentId equals appointment.Id into appointments
               from appointment in appointments.DefaultIfEmpty()
               select new AppointmentAccessorWithNavigationProperties
               {
                   AppointmentAccessor = appointmentAccessor,
                   IdentityUser = identityUser,
                   Appointment = appointment
               };
    }

    protected virtual IQueryable<AppointmentAccessorWithNavigationProperties> ApplyFilter(IQueryable<AppointmentAccessorWithNavigationProperties> query, string? filterText, AccessType? accessTypeId = null, Guid? identityUserId = null, Guid? appointmentId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true).WhereIf(accessTypeId.HasValue, e => e.AppointmentAccessor.AccessTypeId == accessTypeId).WhereIf(identityUserId != null && identityUserId != Guid.Empty, e => e.IdentityUser != null && e.IdentityUser.Id == identityUserId).WhereIf(appointmentId != null && appointmentId != Guid.Empty, e => e.Appointment != null && e.Appointment.Id == appointmentId);
    }

    public virtual async Task<List<AppointmentAccessor>> GetListAsync(string? filterText = null, AccessType? accessTypeId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, accessTypeId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentAccessorConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, AccessType? accessTypeId = null, Guid? identityUserId = null, Guid? appointmentId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, accessTypeId, identityUserId, appointmentId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<AppointmentAccessor> ApplyFilter(IQueryable<AppointmentAccessor> query, string? filterText = null, AccessType? accessTypeId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true).WhereIf(accessTypeId.HasValue, e => e.AccessTypeId == accessTypeId);
    }
}