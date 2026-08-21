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

namespace HealthcareSupport.CaseEvaluation.ClaimExaminers;

public class EfCoreClaimExaminerRepository : EfCoreRepository<CaseEvaluationDbContext, ClaimExaminer, Guid>, IClaimExaminerRepository
{
    public EfCoreClaimExaminerRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<ClaimExaminerWithNavigationProperties?> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return await (await GetDbSetAsync()).Where(b => b.Id == id).Select(claimExaminer => new ClaimExaminerWithNavigationProperties { ClaimExaminer = claimExaminer, State = dbContext.Set<State>().FirstOrDefault(c => c.Id == claimExaminer.StateId), IdentityUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == claimExaminer.IdentityUserId) }).FirstOrDefaultAsync(cancellationToken);
    }

    public virtual async Task<List<ClaimExaminerWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? email = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, email, phoneNumber, city, stateId, identityUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? ClaimExaminerConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<ClaimExaminerWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from claimExaminer in (await GetDbSetAsync())
               join state in (await GetDbContextAsync()).Set<State>() on claimExaminer.StateId equals state.Id into states
               from state in states.DefaultIfEmpty()
               join identityUser in (await GetDbContextAsync()).Set<IdentityUser>() on claimExaminer.IdentityUserId equals (Guid?)identityUser.Id into identityUsers
               from identityUser in identityUsers.DefaultIfEmpty()
               select new ClaimExaminerWithNavigationProperties
               {
                   ClaimExaminer = claimExaminer,
                   State = state,
                   IdentityUser = identityUser
               };
    }

    protected virtual IQueryable<ClaimExaminerWithNavigationProperties> ApplyFilter(IQueryable<ClaimExaminerWithNavigationProperties> query, string? filterText, string? email = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null)
    {
        return query
            .WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.ClaimExaminer.Email!.Contains(filterText!) || e.ClaimExaminer.LastName!.Contains(filterText!) || e.ClaimExaminer.PhoneNumber!.Contains(filterText!) || e.ClaimExaminer.City!.Contains(filterText!))
            .WhereIf(!string.IsNullOrWhiteSpace(email), e => e.ClaimExaminer.Email!.Contains(email!))
            .WhereIf(!string.IsNullOrWhiteSpace(phoneNumber), e => e.ClaimExaminer.PhoneNumber!.Contains(phoneNumber!))
            .WhereIf(!string.IsNullOrWhiteSpace(city), e => e.ClaimExaminer.City!.Contains(city!))
            .WhereIf(stateId != null && stateId != Guid.Empty, e => e.State != null && e.State.Id == stateId)
            .WhereIf(identityUserId != null && identityUserId != Guid.Empty, e => e.IdentityUser != null && e.IdentityUser.Id == identityUserId);
    }

    protected virtual IQueryable<ClaimExaminer> ApplyFilter(IQueryable<ClaimExaminer> query, string? filterText = null, string? email = null, string? phoneNumber = null, string? city = null)
    {
        return query
            .WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Email!.Contains(filterText!) || e.LastName!.Contains(filterText!) || e.PhoneNumber!.Contains(filterText!) || e.City!.Contains(filterText!))
            .WhereIf(!string.IsNullOrWhiteSpace(email), e => e.Email!.Contains(email!))
            .WhereIf(!string.IsNullOrWhiteSpace(phoneNumber), e => e.PhoneNumber!.Contains(phoneNumber!))
            .WhereIf(!string.IsNullOrWhiteSpace(city), e => e.City!.Contains(city!));
    }

    public virtual async Task<List<ClaimExaminer>> GetListAsync(string? filterText = null, string? email = null, string? phoneNumber = null, string? city = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, email, phoneNumber, city);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? ClaimExaminerConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? email = null, string? phoneNumber = null, string? city = null, Guid? stateId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, email, phoneNumber, city, stateId, identityUserId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }
}
