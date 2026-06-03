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

namespace HealthcareSupport.CaseEvaluation.AppointmentDocumentTypes;

public class EfCoreAppointmentDocumentTypeRepository : EfCoreRepository<CaseEvaluationDbContext, AppointmentDocumentType, Guid>, IAppointmentDocumentTypeRepository
{
    public EfCoreAppointmentDocumentTypeRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, Guid? appointmentTypeId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        query = ApplyFilter(query, filterText, appointmentTypeId);
        // Reserved system rows ("Generated Packet") are never bulk-deletable --
        // the per-row path is guarded in AppointmentDocumentTypeManager; the
        // "delete all" convenience must honor the same invariant.
        query = query.Where(x => !x.IsSystem);
        var ids = query.Select(x => x.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<AppointmentDocumentType>> GetListAsync(string? filterText = null, Guid? appointmentTypeId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, appointmentTypeId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? AppointmentDocumentTypeConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, Guid? appointmentTypeId = null, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetDbSetAsync()), filterText, appointmentTypeId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> NameExistsAsync(string name, Guid? appointmentTypeId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var loweredName = name.ToLower();
        var query = (await GetQueryableAsync())
            .Where(e => e.IsActive && e.Name.ToLower() == loweredName);

        // Scope the uniqueness check to the same appointment type. Null is its
        // own scope ("applies to all types"); compared explicitly so EF does not
        // translate a null parameter into a never-true `= NULL` predicate.
        query = appointmentTypeId.HasValue
            ? query.Where(e => e.AppointmentTypeId == appointmentTypeId.Value)
            : query.Where(e => e.AppointmentTypeId == null);

        if (excludeId.HasValue)
        {
            query = query.Where(e => e.Id != excludeId.Value);
        }

        return await query.AnyAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<AppointmentDocumentType> ApplyFilter(IQueryable<AppointmentDocumentType> query, string? filterText = null, Guid? appointmentTypeId = null)
    {
        return query
            .WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Name!.Contains(filterText!))
            .WhereIf(appointmentTypeId.HasValue, e => e.AppointmentTypeId == appointmentTypeId!.Value);
    }
}
