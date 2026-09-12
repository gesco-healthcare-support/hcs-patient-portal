using Volo.Abp.Identity;
using HealthcareSupport.CaseEvaluation.ApplicantAttorneys;
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

namespace HealthcareSupport.CaseEvaluation.AppointmentApplicantAttorneys;

public class EfCoreAppointmentApplicantAttorneyRepository : EfCoreRepository<CaseEvaluationDbContext, AppointmentApplicantAttorney, Guid>, IAppointmentApplicantAttorneyRepository
{
    public EfCoreAppointmentApplicantAttorneyRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<AppointmentApplicantAttorneyWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await (await GetDbSetAsync()).Where(b => b.Id == id).Select(appointmentApplicantAttorney => new AppointmentApplicantAttorneyWithNavigationProperties { AppointmentApplicantAttorney = appointmentApplicantAttorney, Appointment = dbContext.Set<Appointment>().FirstOrDefault(c => c.Id == appointmentApplicantAttorney.AppointmentId), ApplicantAttorney = dbContext.Set<ApplicantAttorney>().FirstOrDefault(c => c.Id == appointmentApplicantAttorney.ApplicantAttorneyId), IdentityUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == appointmentApplicantAttorney.IdentityUserId) }).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<List<AppointmentApplicantAttorneyWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, Guid? appointmentId = null, Guid? applicantAttorneyId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, appointmentId, applicantAttorneyId, identityUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentApplicantAttorneyConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<AppointmentApplicantAttorneyWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from appointmentApplicantAttorney in (await GetDbSetAsync())
               join appointment in (await GetDbContextAsync()).Set<Appointment>() on appointmentApplicantAttorney.AppointmentId equals appointment.Id into appointments
               from appointment in appointments.DefaultIfEmpty()
               join applicantAttorney in (await GetDbContextAsync()).Set<ApplicantAttorney>() on appointmentApplicantAttorney.ApplicantAttorneyId equals applicantAttorney.Id into applicantAttorneys
               from applicantAttorney in applicantAttorneys.DefaultIfEmpty()
               join identityUser in (await GetDbContextAsync()).Set<IdentityUser>() on appointmentApplicantAttorney.IdentityUserId equals (Guid?)identityUser.Id into identityUsers
               from identityUser in identityUsers.DefaultIfEmpty()
               select new AppointmentApplicantAttorneyWithNavigationProperties
               {
                   AppointmentApplicantAttorney = appointmentApplicantAttorney,
                   Appointment = appointment,
                   ApplicantAttorney = applicantAttorney,
                   IdentityUser = identityUser
               };
    }

    protected virtual IQueryable<AppointmentApplicantAttorneyWithNavigationProperties> ApplyFilter(IQueryable<AppointmentApplicantAttorneyWithNavigationProperties> query, string? filterText, Guid? appointmentId = null, Guid? applicantAttorneyId = null, Guid? identityUserId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true).WhereIf(appointmentId != null && appointmentId != Guid.Empty, e => e.Appointment != null && e.Appointment.Id == appointmentId).WhereIf(applicantAttorneyId != null && applicantAttorneyId != Guid.Empty, e => e.ApplicantAttorney != null && e.ApplicantAttorney.Id == applicantAttorneyId).WhereIf(identityUserId != null && identityUserId != Guid.Empty, e => e.IdentityUser != null && e.IdentityUser.Id == identityUserId);
    }

    protected virtual IQueryable<AppointmentApplicantAttorney> ApplyFilter(IQueryable<AppointmentApplicantAttorney> query, string? filterText = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true);
    }

    public virtual async Task<List<AppointmentApplicantAttorney>> GetListAsync(string? filterText = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentApplicantAttorneyConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, Guid? appointmentId = null, Guid? applicantAttorneyId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, appointmentId, applicantAttorneyId, identityUserId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }
}