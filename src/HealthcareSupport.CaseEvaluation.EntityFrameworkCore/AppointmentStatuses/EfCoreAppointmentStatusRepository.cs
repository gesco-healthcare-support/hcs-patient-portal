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

namespace HealthcareSupport.CaseEvaluation.AppointmentStatuses;

public class EfCoreAppointmentStatusRepository : EfCoreRepository<CaseEvaluationDbContext, AppointmentStatus, Guid>, IAppointmentStatusRepository
{
    public EfCoreAppointmentStatusRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        query = ApplyFilter(query, filterText);
        var ids = query.Select(x => x.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<AppointmentStatus>> GetListAsync(string? filterText = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentStatusConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetDbSetAsync()), filterText);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<AppointmentStatus> ApplyFilter(IQueryable<AppointmentStatus> query, string? filterText = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Name!.Contains(filterText!));
    }
}