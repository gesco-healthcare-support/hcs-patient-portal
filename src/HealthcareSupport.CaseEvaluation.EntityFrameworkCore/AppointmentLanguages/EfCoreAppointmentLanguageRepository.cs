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

namespace HealthcareSupport.CaseEvaluation.AppointmentLanguages;

public class EfCoreAppointmentLanguageRepository : EfCoreRepository<CaseEvaluationDbContext, AppointmentLanguage, Guid>, IAppointmentLanguageRepository
{
    public EfCoreAppointmentLanguageRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<List<AppointmentLanguage>> GetListAsync(string? filterText = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentLanguageConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetDbSetAsync()), filterText);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<AppointmentLanguage> ApplyFilter(IQueryable<AppointmentLanguage> query, string? filterText = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Name!.Contains(filterText!));
    }
}