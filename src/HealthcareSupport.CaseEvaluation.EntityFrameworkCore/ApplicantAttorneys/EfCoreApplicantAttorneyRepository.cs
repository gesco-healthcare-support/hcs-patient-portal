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

namespace HealthcareSupport.CaseEvaluation.ApplicantAttorneys;

public class EfCoreApplicantAttorneyRepository : EfCoreRepository<CaseEvaluationDbContext, ApplicantAttorney, Guid>, IApplicantAttorneyRepository
{
    public EfCoreApplicantAttorneyRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<ApplicantAttorneyWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(applicantAttorney => new ApplicantAttorneyWithNavigationProperties { ApplicantAttorney = applicantAttorney, State = dbContext.Set<State>().FirstOrDefault(c => c.Id == applicantAttorney.StateId), IdentityUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == applicantAttorney.IdentityUserId) }).FirstOrDefault();
    }

    public virtual async Task<List<ApplicantAttorneyWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, firmName, phoneNumber, city, stateId, identityUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? ApplicantAttorneyConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<ApplicantAttorneyWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from applicantAttorney in (await GetDbSetAsync())
               join state in (await GetDbContextAsync()).Set<State>() on applicantAttorney.StateId equals state.Id into states
               from state in states.DefaultIfEmpty()
               join identityUser in (await GetDbContextAsync()).Set<IdentityUser>() on applicantAttorney.IdentityUserId equals identityUser.Id into identityUsers
               from identityUser in identityUsers.DefaultIfEmpty()
               select new ApplicantAttorneyWithNavigationProperties
               {
                   ApplicantAttorney = applicantAttorney,
                   State = state,
                   IdentityUser = identityUser
               };
    }

    protected virtual IQueryable<ApplicantAttorneyWithNavigationProperties> ApplyFilter(IQueryable<ApplicantAttorneyWithNavigationProperties> query, string? filterText, string? firmName = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.ApplicantAttorney.FirmName!.Contains(filterText!) || e.ApplicantAttorney.PhoneNumber!.Contains(filterText!) || e.ApplicantAttorney.City!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(firmName), e => e.ApplicantAttorney.FirmName.Contains(firmName)).WhereIf(!string.IsNullOrWhiteSpace(phoneNumber), e => e.ApplicantAttorney.PhoneNumber.Contains(phoneNumber)).WhereIf(!string.IsNullOrWhiteSpace(city), e => e.ApplicantAttorney.City.Contains(city)).WhereIf(stateId != null && stateId != Guid.Empty, e => e.State != null && e.State.Id == stateId).WhereIf(identityUserId != null && identityUserId != Guid.Empty, e => e.IdentityUser != null && e.IdentityUser.Id == identityUserId);
    }

    public virtual async Task<List<ApplicantAttorney>> GetListAsync(string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, firmName, phoneNumber, city);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? ApplicantAttorneyConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, firmName, phoneNumber, city, stateId, identityUserId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<ApplicantAttorney> ApplyFilter(IQueryable<ApplicantAttorney> query, string? filterText = null, string? firmName = null, string? phoneNumber = null, string? city = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.FirmName!.Contains(filterText!) || e.PhoneNumber!.Contains(filterText!) || e.City!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(firmName), e => e.FirmName.Contains(firmName)).WhereIf(!string.IsNullOrWhiteSpace(phoneNumber), e => e.PhoneNumber.Contains(phoneNumber)).WhereIf(!string.IsNullOrWhiteSpace(city), e => e.City.Contains(city));
    }
}